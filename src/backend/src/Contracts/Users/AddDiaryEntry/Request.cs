namespace Contracts.Users.AddDiaryEntry;

/// <summary>
/// Something a person ate. Say exactly one of: a recipe (<c>recipeId</c>, with <c>portions</c>), a food (<c>foodCode</c>,
/// with <c>amount</c> and <c>unit</c>), or typed figures (<c>label</c> and <c>kcal</c>). The server looks figures up;
/// for a recipe or a food the client never sends them.
/// </summary>
public sealed record Request
{
    /// <summary>The day, <c>yyyy-MM-dd</c>.</summary>
    public required DateOnly Date { get; init; }

    /// <summary><c>breakfast</c>, <c>lunch</c>, <c>dinner</c> or <c>snack</c>.</summary>
    public required string Meal { get; init; }

    /// <summary>A recipe, eaten in portions (or pieces, for a recipe that makes pieces).</summary>
    public Guid? RecipeId { get; init; }

    /// <summary>How many portions of the recipe; defaults to one.</summary>
    public decimal? Portions { get; init; }

    /// <summary>
    /// The household whose recipe or own food it is: for an inherited recipe, the kitchen it was eaten in; for a
    /// household's own food (<c>h:</c> code), required.
    /// </summary>
    public Guid? HouseholdId { get; init; }

    /// <summary>The "made it" this portion comes from, so undoing it can offer to take the portion too.</summary>
    public Guid? CookLogEntryId { get; init; }

    /// <summary>A food: a BLS code, or <c>h:</c> and a household food's id.</summary>
    public string? FoodCode { get; init; }

    /// <summary>How much of the food, in <c>unit</c>.</summary>
    public decimal? Amount { get; init; }

    /// <summary><c>g</c> (the default), or a typical unit the food has (<c>piece</c>, <c>slice</c>…, see <c>GET /foods</c>).</summary>
    public string? Unit { get; init; }

    /// <summary>For typed figures: what it was.</summary>
    public string? Label { get; init; }

    /// <summary>For typed figures: kilocalories, required.</summary>
    public decimal? Kcal { get; init; }

    /// <summary>For typed figures: protein in grams, if known.</summary>
    public decimal? Protein { get; init; }

    /// <summary>For typed figures: carbohydrate in grams, if known.</summary>
    public decimal? Carbohydrate { get; init; }

    /// <summary>For typed figures: fat in grams, if known.</summary>
    public decimal? Fat { get; init; }
}
