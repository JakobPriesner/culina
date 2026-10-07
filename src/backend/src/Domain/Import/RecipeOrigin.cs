namespace Domain.Import;

/// <summary>Where one recipe came from: a fact, so a record with no behaviour or version. Only <see cref="SourceId"/> can go away (on disconnect); the rest answers "where did this come from?" years later.</summary>
/// <param name="RecipeId">The recipe this is about.</param>
/// <param name="HouseholdId">Whose kitchen it landed in; also here so the database can enforce "once per household".</param>
/// <param name="Kind">Which sort of place it came from.</param>
/// <param name="SourceId">Which connection brought it, when one did.</param>
/// <param name="ExternalId">What that place called it.</param>
/// <param name="SourceUrl">Where to look at the original, when there is an address worth linking to.</param>
/// <param name="ImportedAt">When it arrived.</param>
public sealed record RecipeOrigin(
    Guid RecipeId,
    Guid HouseholdId,
    SourceKind Kind,
    Guid? SourceId,
    string ExternalId,
    SourceUrl? SourceUrl,
    DateTimeOffset ImportedAt);
