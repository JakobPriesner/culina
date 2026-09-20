namespace Contracts.Recipes;

/// <summary>
/// One moment of a picture being drawn.
/// </summary>
/// <remarks>
/// <para>
/// A picture has no halfway state worth sending — a provider hands over a
/// finished image or nothing — so unlike a recipe being written this stream
/// carries no content until the end. What it carries instead is the fact that
/// the work is still going, and how long it has been going for.
/// </para>
/// <para>
/// That is not decoration. Drawing is the slowest thing this app asks of
/// anybody: sixteen seconds is a fast one and two minutes is the ceiling. A
/// request that says nothing for that long is one a proxy closes, a browser
/// gives up on, and a person assumes has broken — and the server, knowing none
/// of that, finishes the drawing, pays for it and stores it against a screen
/// that has already said it failed. A tick every few seconds settles all three.
/// </para>
/// </remarks>
public sealed record DrawingEvent
{
    /// <summary>
    /// How long the assistant has been drawing, in seconds.
    /// </summary>
    /// <remarks>
    /// Counted by the server rather than the client, so a tab that was
    /// backgrounded and throttled still shows the real elapsed time when it
    /// comes back.
    /// </remarks>
    public required int Seconds { get; init; }

    /// <summary>Whether this is the last one.</summary>
    public bool Finished { get; init; }

    /// <summary>The recipe with its new picture on it, on a successful last event.</summary>
    /// <remarks>
    /// The whole recipe rather than an image id, because the drawn picture has
    /// gone through the same path an upload does and the caller needs the
    /// version that came back as much as the image.
    /// </remarks>
    public RecipeDetail? Recipe { get; init; }

    /// <summary>Why it stopped, when the last event is a failure.</summary>
    public Streaming.Problem? Problem { get; init; }
}
