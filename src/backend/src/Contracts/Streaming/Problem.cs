namespace Contracts.Streaming;

/// <summary>
/// A failure that happened after the answer had already started.
/// </summary>
/// <remarks>
/// <para>
/// Not a problem document, and it cannot be one. RFC 9457 describes a response,
/// and by the time a model has written half a recipe the response is a 200 that
/// has been arriving for several seconds — the status line was sent before
/// anybody knew this would go wrong. So the failure travels as an ordinary
/// event on the stream instead.
/// </para>
/// <para>
/// The same two fields a problem document leads with, and for the same reason:
/// <see cref="Code"/> is what a client branches on, and
/// <see cref="Detail"/> is a sentence that will be reworded. A client that
/// compares against the sentence has a bug waiting for the next copy edit.
/// </para>
/// </remarks>
public sealed record Problem
{
    /// <summary>The machine-readable code, formatted <c>module.reason</c>.</summary>
    public required string Code { get; init; }

    /// <summary>A sentence for a person, where the client has nothing better.</summary>
    public required string Detail { get; init; }
}
