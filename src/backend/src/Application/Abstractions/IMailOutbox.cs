namespace Application.Abstractions;

/// <summary>One mail, ready to send.</summary>
/// <param name="To">The recipient's address.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="Text">The plain-text body, which every client can show.</param>
/// <param name="Html">The same words as HTML.</param>
public sealed record OutgoingMail(string To, string Subject, string Text, string Html);

/// <summary>Queues mail to be sent after the request, by a worker that retries.</summary>
/// <remarks>
/// Written in the caller's transaction, so a mail goes out only when what it talks about was committed (a token that
/// was rolled back is never mailed), and a slow or failing mail server never holds up or fails the request. That
/// also keeps the time a request takes the same whether or not it queued a mail.
/// </remarks>
public interface IMailOutbox
{
    /// <summary>Queues a mail.</summary>
    /// <param name="mail">What to send.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task EnqueueAsync(OutgoingMail mail, CancellationToken cancellationToken);
}
