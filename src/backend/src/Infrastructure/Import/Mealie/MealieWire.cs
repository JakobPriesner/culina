using System.Text.Json.Serialization;

namespace Infrastructure.Import.Mealie;

/// <summary>Mealie's own shapes, as its v2 API actually sends them.</summary>
/// <remarks>
/// Foreign types, named after Mealie's fields (not ours) so mapping happens in one visible place.
/// Mealie writes camelCase, and <c>orgURL</c> is matched because property names are read case-insensitively.
/// Everything is nullable: Mealie fills its defaults inconsistently across versions and migrations.
/// </remarks>
internal sealed record MealiePage<TItem>
{
    /// <summary>How many there are altogether.</summary>
    public int? Total { get; init; }

    /// <summary>How many pages there are at this page size.</summary>
    public int? TotalPages { get; init; }

    /// <summary>What is on this page.</summary>
    public IReadOnlyList<TItem>? Items { get; init; }
}

/// <summary>A recipe as Mealie's list endpoint sends it; the detail endpoint adds its contents.</summary>
internal record MealieRecipeSummary
{
    /// <summary>Stable across renames, unlike the slug.</summary>
    public Guid? Id { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    /// <summary>A cache key, not an address: present only when the recipe has a picture.</summary>
    public string? Image { get; init; }

    /// <summary>Portions, or 0 when unset.</summary>
    public decimal? RecipeServings { get; init; }

    /// <summary>What it makes, as written: the older field, and the only one on recipes from before servings were a number.</summary>
    public string? RecipeYield { get; init; }

    /// <summary>Free text: "1 hour 30 minutes", "PT20M", "45".</summary>
    public string? PrepTime { get; init; }

    /// <summary>Free text. Mealie's UI shows <see cref="PerformTime"/> as cooking time; this one is the older name.</summary>
    public string? CookTime { get; init; }

    /// <summary>Free text: what Mealie's UI labels cooking time.</summary>
    public string? PerformTime { get; init; }

    public IReadOnlyList<MealieNamed>? Tags { get; init; }

    public IReadOnlyList<MealieNamed>? RecipeCategory { get; init; }

    /// <summary>Where the recipe was scraped from, back when it was.</summary>
    public string? OrgUrl { get; init; }
}

/// <summary>A recipe as Mealie's detail endpoint sends it.</summary>
internal sealed record MealieRecipe : MealieRecipeSummary
{
    public IReadOnlyList<MealieIngredient>? RecipeIngredient { get; init; }

    public IReadOnlyList<MealieStep>? RecipeInstructions { get; init; }
}

/// <summary>A tag, a category, a unit or a food: all a name as far as this app is concerned.</summary>
internal sealed record MealieNamed
{
    public string? Name { get; init; }
}

/// <summary>One ingredient line.</summary>
/// <remarks>
/// A line Mealie never parsed has no food: its whole text is in <see cref="Note"/>, and the quantity is
/// a default rather than something anybody wrote.
/// </remarks>
internal sealed record MealieIngredient
{
    /// <summary>The amount, or 0 when there is none.</summary>
    public decimal? Quantity { get; init; }

    public MealieNamed? Unit { get; init; }

    public MealieNamed? Food { get; init; }

    public string? Note { get; init; }

    /// <summary>Mealie's own rendering of the line.</summary>
    public string? Display { get; init; }

    /// <summary>A section heading that starts at this line, which is still an ingredient itself.</summary>
    public string? Title { get; init; }

    /// <summary>The line as it was originally written, when Mealie kept it.</summary>
    public string? OriginalText { get; init; }
}

/// <summary>One instruction.</summary>
/// <remarks>Mealie's ingredient references are a step's list, not places in its text, so they are not read.</remarks>
internal sealed record MealieStep
{
    /// <summary>A section heading over this step.</summary>
    public string? Title { get; init; }

    /// <summary>What to do, in Markdown.</summary>
    public string? Text { get; init; }
}

/// <summary>What Mealie answers when it signs somebody in: a session token that expires, so it is never kept.</summary>
internal sealed record MealieSession
{
    // Not a Mealie model, so not camelCase.
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }
}

/// <summary>A long-lived API token, as Mealie answers the one call that makes it.</summary>
/// <remarks>Shown once, carries the person's full rights, and Culina never writes with it.</remarks>
internal sealed record MealieApiToken
{
    public string? Token { get; init; }
}
