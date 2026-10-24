using System.Collections.Concurrent;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Shared;

namespace TestSupport;

/// <summary>Keeps every mail it is handed instead of sending it; can be told to fail, as a refusing server would.</summary>
public sealed class RecordingMailTransport : IMailTransport
{
    private readonly ConcurrentQueue<OutgoingMail> sent = new();

    /// <summary>What a server would answer, or null to accept every mail.</summary>
    public string? Refusal { get; set; }

    /// <summary>Every mail accepted so far, oldest first.</summary>
    public IReadOnlyList<OutgoingMail> Sent => [.. sent];

    public Task<Result> SendAsync(MailSettings settings, OutgoingMail mail, CancellationToken cancellationToken)
    {
        if (Refusal is { } reason)
        {
            return Task.FromResult(Result.Failure(MailErrors.SendFailed(reason)));
        }

        sent.Enqueue(mail);

        return Task.FromResult(Result.Success());
    }

    /// <summary>The mails to one address, waiting up to a few seconds for the outbox worker to hand over <paramref name="count"/>.</summary>
    public async Task<IReadOnlyList<OutgoingMail>> To(string address, int count, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

        while (true)
        {
            var mails = Sent.Where(one => one.To == address).ToList();

            if (mails.Count >= count || DateTimeOffset.UtcNow > deadline)
            {
                return mails;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    /// <summary>Forgets what was sent, between tests.</summary>
    public void Clear() => sent.Clear();
}
