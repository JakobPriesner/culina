namespace Contracts.Recipes;

/// <summary>
/// One moment of a picture being drawn.
/// </summary>
/// <remarks>
/// A picture has no halfway state, so the stream carries only elapsed time until the end; the periodic tick
/// keeps proxies, browsers and the person from giving up on a drawing that can take two minutes.
/// </remarks>
public sealed record DrawingEvent
{
    /// <summary>
    /// How long the assistant has been drawing, in seconds.
    /// </summary>
    public required int Seconds { get; init; }

    /// <summary>Whether this is the last one.</summary>
    public bool Finished { get; init; }

    /// <summary>The recipe with its new picture on it, on a successful last event.</summary>
    public RecipeDetail? Recipe { get; init; }

    /// <summary>Why it stopped, when the last event is a failure.</summary>
    public Streaming.Problem? Problem { get; init; }
}
