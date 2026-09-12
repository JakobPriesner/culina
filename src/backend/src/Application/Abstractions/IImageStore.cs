using Domain.Shared;

namespace Application.Abstractions;

/// <summary>
/// The widths Culina keeps of every image.
/// </summary>
/// <remarks>
/// Three fixed sizes, not a resize-on-demand parameter: an arbitrary width is a
/// denial-of-service lever and a cache that never warms. A card, a detail
/// header and a retina detail header are the sizes the app actually asks for.
/// Constants rather than an enum, because the values are the widths.
/// </remarks>
public static class ImageWidths
{
    /// <summary>Grid cards.</summary>
    public const int Card = 400;

    /// <summary>The detail page's header.</summary>
    public const int Detail = 800;

    /// <summary>The detail header on a dense display.</summary>
    public const int Retina = 1600;

    /// <summary>Every rendition that is stored, smallest first.</summary>
    public static IReadOnlyList<int> All { get; } = [Card, Detail, Retina];

    /// <summary>Whether a requested width is one that exists.</summary>
    /// <param name="width">The width the caller asked for.</param>
    public static bool Exists(int width) => All.Contains(width);
}

/// <summary>Stores and serves recipe images.</summary>
public interface IImageStore
{
    /// <summary>
    /// Validates, re-encodes and stores an uploaded image.
    /// </summary>
    /// <param name="content">The uploaded bytes.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <remarks>
    /// The implementation decides what the file is by decoding it, never by
    /// trusting a content type or an extension, and the bytes it stores are its
    /// own re-encoding — which is what strips EXIF and neutralises a file that
    /// is valid in two formats at once.
    /// </remarks>
    Task<Result<StoredImage>> StoreAsync(Stream content, CancellationToken cancellationToken);

    /// <summary>Writes one rendition to a destination.</summary>
    /// <param name="contentHash">Which image.</param>
    /// <param name="width">Which rendition, from <see cref="ImageWidths"/>.</param>
    /// <param name="destination">Where to write it, usually the response body.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <remarks>
    /// The store writes rather than handing back an open stream, so it keeps
    /// ownership of the file handle and closes it. A returned stream would
    /// transfer that ownership to a caller who has no reason to know it exists.
    /// </remarks>
    Task<Result> CopyToAsync(
        string contentHash,
        int width,
        Stream destination,
        CancellationToken cancellationToken);

    /// <summary>Deletes every rendition of an image.</summary>
    /// <param name="contentHash">Which image.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    Task<Result> DeleteAsync(string contentHash, CancellationToken cancellationToken);
}

/// <summary>
/// What an image write displaced.
/// </summary>
/// <param name="PreviousContentHash">
/// The image that was replaced, or null when there was none.
/// </param>
/// <remarks>
/// A named record rather than a nullable string inside a result, because a
/// result carries a value or an error and "there was nothing to replace" is
/// neither — it is an ordinary, expected part of the answer.
/// </remarks>
public sealed record ImageReplacement(string? PreviousContentHash);

/// <summary>What was stored.</summary>
/// <param name="ContentHash">The address the renditions live at.</param>
/// <param name="Width">The original's width, after re-encoding.</param>
/// <param name="Height">The original's height, after re-encoding.</param>
/// <param name="ByteSize">The largest rendition's size.</param>
public sealed record StoredImage(string ContentHash, int Width, int Height, int ByteSize);
