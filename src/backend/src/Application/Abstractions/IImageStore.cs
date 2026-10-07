using Domain.Shared;

namespace Application.Abstractions;

/// <summary>The widths Culina keeps of every image.</summary>
/// <remarks>
/// Three fixed sizes, not resize-on-demand: an arbitrary width is a denial-of-service lever and a
/// cold cache.
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
    public static bool Exists(int width) => All.Contains(width);
}

/// <summary>Stores and serves recipe images.</summary>
public interface IImageStore
{
    /// <summary>Validates, re-encodes and stores an uploaded image.</summary>
    /// <remarks>
    /// The file is identified by decoding, never by content type or extension; stored bytes are the
    /// re-encoding, which strips EXIF and defuses polyglot files.
    /// </remarks>
    Task<Result<StoredImage>> StoreAsync(Stream content, CancellationToken cancellationToken);

    /// <summary>Re-encodes a picture for an assistant to read, and keeps nothing.</summary>
    /// <remarks>
    /// Ends in a JPEG of the pixels only: the picture leaves the instance, and a cookbook photo can
    /// reveal where the kitchen is.
    /// </remarks>
    Task<Result<RecipePicture>> ReEncodeForReadingAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    /// <summary>Writes one rendition to a destination.</summary>
    /// <remarks>
    /// The store writes rather than returning a stream, so it keeps ownership of the file handle.
    /// </remarks>
    Task<Result> CopyToAsync(
        string contentHash,
        int width,
        Stream destination,
        CancellationToken cancellationToken);

    /// <summary>Deletes every rendition of an image.</summary>
    Task<Result> DeleteAsync(string contentHash, CancellationToken cancellationToken);
}

/// <summary>What an image write displaced.</summary>
/// <param name="PreviousContentHash">
/// The image that was replaced, or null when there was none.
/// </param>
public sealed record ImageReplacement(string? PreviousContentHash);

/// <summary>What was stored.</summary>
/// <param name="ContentHash">The address the renditions live at.</param>
/// <param name="Width">The original's width, after re-encoding.</param>
/// <param name="Height">The original's height, after re-encoding.</param>
/// <param name="ByteSize">The largest rendition's size.</param>
public sealed record StoredImage(string ContentHash, int Width, int Height, int ByteSize);
