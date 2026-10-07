namespace Application.Recipes.Drafts;

/// <summary>How much a recipe draft will be asked to work from.</summary>
/// <remarks>Limits on cost, not capability; shared so the endpoint, the intake and the draft refuse the same amount.</remarks>
public static class DraftLimits
{
    /// <summary>Text and transcript together, in characters.</summary>
    public const int MaxMaterialCharacters = 20_000;

    /// <summary>How many photographs one draft is read from.</summary>
    public const int MaxPhotos = 8;

    /// <summary>All the photographs together, in bytes.</summary>
    public const long MaxPhotoBytes = 40L * 1024 * 1024;
}
