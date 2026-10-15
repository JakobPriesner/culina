namespace Application.Recipes.Drafts;

/// <summary>Which image format a file really is, from its bytes: a caller's Content-Type is a claim.</summary>
/// <remarks>Only the formats the provider adapters understand are sent to a vision model.</remarks>
public static class PictureFormat
{
    /// <summary>The media type of JPEG, PNG or WebP bytes; null for anything else.</summary>
    public static string? MediaTypeOf(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0xff, 0xd8, 0xff, ..] => "image/jpeg",
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, ..] => "image/png",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => null
    };
}
