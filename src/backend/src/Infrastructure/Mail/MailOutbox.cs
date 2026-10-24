using System.Text.Json;
using Application.Abstractions;
using Domain.Shared;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Mail;

/// <summary>Stores mail to send, encrypted, and hands it to the worker that sends it.</summary>
internal sealed class MailOutbox(
    DbExecutor executor,
    [FromKeyedServices(MailOutbox.Protection)] ISecretProtector protector,
    TimeProvider time) : IMailOutbox
{
    /// <summary>What a queued mail is protected for; a mail cannot be read back as any other secret.</summary>
    internal const string Protection = "Culina.Mail.Outbox.v1";

    /// <summary>How many times a mail is tried before it is given up on; the retries span about a day.</summary>
    internal const int MaxAttempts = 8;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task EnqueueAsync(OutgoingMail mail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);

        var now = time.GetUtcNow();

        await executor.ExecuteAsync(
            "insert into mail_outbox (id, message, created_at, retry_at) values (@id, @message, @now, @now);",
            new { id = CulinaId.New(), message = protector.Protect(JsonSerializer.Serialize(mail, Json)), now },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes up to a few mails that are due, pushing each one's next try back first: a worker that dies mid-send
    /// leaves the mail to be tried again later, never twice at once.
    /// </summary>
    internal async Task<IReadOnlyList<QueuedMail>> ClaimAsync(CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<QueuedRow>(
            """
            update mail_outbox
            set attempts = attempts + 1,
                retry_at = now() + least(power(2, attempts), 240) * interval '1 minute'
            where id in (
                select id from mail_outbox
                where retry_at <= now()
                order by retry_at
                limit 10
                for update skip locked)
            returning id, message, attempts;
            """,
            null,
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => new QueuedMail(row.Id, Read(row.Message), row.Attempts))];
    }

    /// <summary>Forgets a mail that was sent, or given up on.</summary>
    internal Task RemoveAsync(Guid id, CancellationToken cancellationToken) =>
        executor.ExecuteAsync("delete from mail_outbox where id = @id;", new { id }, cancellationToken);

    /// <summary>
    /// A message whose key is gone (a key ring lost and restored empty) cannot be sent; null, so the worker drops it.
    /// </summary>
    private OutgoingMail? Read(string message) =>
        protector.Unprotect(message) is { } json ? JsonSerializer.Deserialize<OutgoingMail>(json, Json) : null;

    private sealed record QueuedRow
    {
        public Guid Id { get; init; }

        public string Message { get; init; } = string.Empty;

        public int Attempts { get; init; }
    }
}

/// <summary>A mail taken from the outbox to send.</summary>
/// <param name="Id">Its row.</param>
/// <param name="Mail">What to send, or null when it can no longer be read.</param>
/// <param name="Attempts">How many tries this one is, counting this one.</param>
internal sealed record QueuedMail(Guid Id, OutgoingMail? Mail, int Attempts);
