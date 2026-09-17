namespace Domain.Import;

/// <summary>
/// Where one recipe came from.
/// </summary>
/// <remarks>
/// <para>
/// A fact rather than a relationship, which is why it is a record with no
/// behaviour and no version: nothing about where a recipe came from ever
/// changes. The recipe itself may be rewritten until nothing of the original is
/// left, and it still came from there.
/// </para>
/// <para>
/// <see cref="SourceId"/> is the one part that can go away, when the connection
/// it names is disconnected. Everything else survives that, because the reason
/// to keep provenance at all is to be able to answer "where did this come
/// from?" years later, long after the instance it came from was switched off.
/// </para>
/// </remarks>
/// <param name="RecipeId">The recipe this is about.</param>
/// <param name="HouseholdId">
/// Whose kitchen it landed in. Carried here as well as on the recipe so the
/// database can enforce "once per household" rather than trusting a check.
/// </param>
/// <param name="Kind">Which sort of place it came from.</param>
/// <param name="SourceId">Which connection brought it, when one did.</param>
/// <param name="ExternalId">What that place called it.</param>
/// <param name="SourceUrl">Where to go and look at the original.</param>
/// <param name="ImportedAt">When it arrived.</param>
public sealed record RecipeOrigin(
    Guid RecipeId,
    Guid HouseholdId,
    SourceKind Kind,
    Guid? SourceId,
    string ExternalId,
    string? SourceUrl,
    DateTimeOffset ImportedAt);
