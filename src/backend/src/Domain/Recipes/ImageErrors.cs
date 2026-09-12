using Domain.Shared;

namespace Domain.Recipes;

/// <summary>Failures when handling a recipe image.</summary>
public static class ImageErrors
{
    /// <summary>The upload is not an image this app can read.</summary>
    /// <remarks>
    /// The same error whether the bytes are a text file, a format Culina does
    /// not support, or a deliberately malformed image: the caller's fix is the
    /// same in every case, and saying which would only help someone probing the
    /// decoder.
    /// </remarks>
    public static readonly Error Unreadable = new(
        "recipes.image_unreadable",
        "That file is not an image Culina can read. Try a JPEG, PNG or WebP.",
        ErrorType.Validation);

    /// <summary>The upload is larger than the instance accepts.</summary>
    public static Error TooLarge(int maxBytes) => new(
        "recipes.image_too_large",
        $"That image is larger than the {maxBytes / (1024 * 1024)} MB this instance accepts.",
        ErrorType.Validation);

    /// <summary>The image has more pixels than is plausible for a photo.</summary>
    public static readonly Error TooManyPixels = new(
        "recipes.image_too_many_pixels",
        "That image has more pixels than a photograph needs. Scale it down first.",
        ErrorType.Validation);

    /// <summary>There is no image at that address.</summary>
    public static readonly Error NotFound = new(
        "recipes.image_not_found",
        "That image is no longer stored.",
        ErrorType.NotFound);

    /// <summary>The requested rendition is not one that exists.</summary>
    public static readonly Error UnknownWidth = new(
        "recipes.image_unknown_width",
        "Images are available at widths 400, 800 and 1600.",
        ErrorType.Validation);
}
