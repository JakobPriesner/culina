using Application.Abstractions;
using Application.Abstractions.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Mail;

/// <summary>Sends what the outbox holds, retrying with growing gaps until a mail goes out or has been tried enough.</summary>
internal sealed partial class MailOutboxWorker(
    IServiceScopeFactory scopes,
    MailSettings settings,
    IMailTransport transport,
    ILogger<MailOutboxWorker> logger) : BackgroundService
{
    /// <summary>
    /// How long the worker waits before looking again. A sign-in link is waited for, so it is short; one indexed
    /// query every few seconds costs nothing.
    /// </summary>
    internal static TimeSpan Poll { get; set; } = TimeSpan.FromSeconds(3);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A durable worker logs a failed pass and tries again; the outbox keeps every mail.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Off the host's start-up path.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Mail switched off keeps what is queued: switched back on soon, it still goes out.
                if (settings.IsOn)
                {
                    await DeliverAsync(stoppingToken).ConfigureAwait(false);
                }

                await Task.Delay(Poll, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                WorkerFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task DeliverAsync(CancellationToken stoppingToken)
    {
        var scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var outbox = scope.ServiceProvider.GetRequiredService<MailOutbox>();

            foreach (var queued in await outbox.ClaimAsync(stoppingToken).ConfigureAwait(false))
            {
                if (queued.Mail is null)
                {
                    Unreadable(logger, queued.Id);
                    await outbox.RemoveAsync(queued.Id, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var sent = await transport.SendAsync(settings, queued.Mail, stoppingToken).ConfigureAwait(false);

                await sent.Match(
                    () => outbox.RemoveAsync(queued.Id, stoppingToken),
                    async error =>
                    {
                        if (queued.Attempts >= MailOutbox.MaxAttempts)
                        {
                            GaveUp(logger, queued.Id, queued.Attempts, error.Description);
                            await outbox.RemoveAsync(queued.Id, stoppingToken).ConfigureAwait(false);
                        }
                        else
                        {
                            WillRetry(logger, queued.Id, queued.Attempts, error.Description);
                        }
                    }).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(EventId = 1801, Level = LogLevel.Warning, Message = "Mail {MailId} could not be sent on attempt {Attempt} and will be tried again: {Reason}")]
    private static partial void WillRetry(ILogger logger, Guid mailId, int attempt, string reason);

    [LoggerMessage(EventId = 1802, Level = LogLevel.Error, Message = "Mail {MailId} was given up on after {Attempt} attempts: {Reason}")]
    private static partial void GaveUp(ILogger logger, Guid mailId, int attempt, string reason);

    [LoggerMessage(EventId = 1803, Level = LogLevel.Error, Message = "Mail {MailId} could not be decrypted and was dropped; the data protection key ring has changed.")]
    private static partial void Unreadable(ILogger logger, Guid mailId);

    [LoggerMessage(EventId = 1804, Level = LogLevel.Error, Message = "Mail worker failed; retrying.")]
    private static partial void WorkerFailed(ILogger logger, Exception exception);
}
