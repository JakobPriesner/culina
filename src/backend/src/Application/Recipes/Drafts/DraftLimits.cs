namespace Application.Recipes.Drafts;

/// <summary>
/// How much a recipe draft will be asked to work from, wherever the material
/// comes from: a pasted text, photographs, or a page that was fetched.
/// </summary>
/// <remarks>
/// The limits are about cost rather than capability: a model would happily read
/// ten times as much, at ten times the price, on a request anybody with an
/// account can make. One place, because the endpoint that takes the upload, the
/// intake that retains it and the draft that sends it all have to refuse the
/// same amount, and three copies of a number are three chances to disagree.
/// </remarks>
public static class DraftLimits
{
    /// <summary>Text and transcript together, in characters.</summary>
    public const int MaxMaterialCharacters = 20_000;

    /// <summary>How many photographs one draft is read from.</summary>
    public const int MaxPhotos = 8;

    /// <summary>All the photographs together, in bytes.</summary>
    public const long MaxPhotoBytes = 40L * 1024 * 1024;
}
