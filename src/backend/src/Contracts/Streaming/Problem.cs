namespace Contracts.Streaming;

/// <summary>
/// A failure after the response had already started (a 200 in flight), so it travels as an event on the stream.
/// Clients branch on <see cref="Code"/>, never on <see cref="Detail"/>, which is reworded.
/// </summary>
public sealed record Problem
{
    /// <summary>The machine-readable code, formatted <c>module.reason</c>.</summary>
    public required string Code { get; init; }

    /// <summary>A sentence for a person, where the client has nothing better.</summary>
    public required string Detail { get; init; }
}
