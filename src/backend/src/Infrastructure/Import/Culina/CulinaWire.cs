namespace Infrastructure.Import.Culina;

/// <summary>Another Culina's shapes, as its v1 API sends them.</summary>
/// <remarks>
/// Its own copies rather than the Contracts types: the other instance may run an older or newer version, so only
/// what is read is declared, and everything is nullable so a field it does not send is a missing detail, not a
/// failed import.
/// </remarks>
internal sealed record CulinaSession
{
    /// <summary>The bearer token, sent only to a client that signed in as one (here <c>instance</c>).</summary>
    public string? SessionToken { get; init; }
}

/// <summary>The households the signed-in person belongs to.</summary>
internal sealed record CulinaHouseholds
{
    public IReadOnlyList<CulinaHousehold>? Items { get; init; }
}

/// <summary>One kitchen.</summary>
internal sealed record CulinaHousehold
{
    public Guid? HouseholdId { get; init; }

    /// <summary>Set while it is in the bin, waiting to be purged.</summary>
    public DateTimeOffset? DeletedAt { get; init; }
}

/// <summary>A page of a household's recipes.</summary>
internal sealed record CulinaRecipePage
{
    public IReadOnlyList<CulinaRecipeSummary>? Items { get; init; }

    /// <summary>Opaque: handed back as <c>cursor</c> for the next page.</summary>
    public string? NextCursor { get; init; }

    public int? Total { get; init; }
}

/// <summary>A recipe as the list sends it.</summary>
internal sealed record CulinaRecipeSummary
{
    public Guid? RecipeId { get; init; }

    /// <summary>Whose kitchen it belongs to; a recipe another kitchen lends shows up in the list too.</summary>
    public Guid? HouseholdId { get; init; }

    public string? Title { get; init; }

    public Guid? ImageId { get; init; }

    public int? TotalMinutes { get; init; }
}

/// <summary>A whole recipe.</summary>
internal sealed record CulinaRecipe
{
    public Guid? RecipeId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    /// <summary><c>en</c> or <c>de</c>.</summary>
    public string? Language { get; init; }

    public decimal? YieldAmount { get; init; }

    /// <summary><c>servings</c> or <c>pieces</c>.</summary>
    public string? YieldKind { get; init; }

    public string? YieldLabel { get; init; }

    public int? PrepMinutes { get; init; }

    public int? CookMinutes { get; init; }

    public Guid? ImageId { get; init; }

    public IReadOnlyList<CulinaGroup>? Groups { get; init; }

    public IReadOnlyList<CulinaStep>? Steps { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Where it came from before it was there, when it was imported.</summary>
    public CulinaOrigin? Origin { get; init; }
}

/// <summary>A named part of the ingredient list.</summary>
internal sealed record CulinaGroup
{
    public string? Name { get; init; }

    public IReadOnlyList<CulinaIngredient>? Ingredients { get; init; }
}

/// <summary>One ingredient line.</summary>
internal sealed record CulinaIngredient
{
    /// <summary>What a step's segments and uses refer to.</summary>
    public Guid? IngredientId { get; init; }

    public decimal? Quantity { get; init; }

    public string? Unit { get; init; }

    public string? Name { get; init; }

    public string? Note { get; init; }
}

/// <summary>One step.</summary>
internal sealed record CulinaStep
{
    public string? Title { get; init; }

    public IReadOnlyList<CulinaSegment>? Segments { get; init; }

    public int? DurationSeconds { get; init; }

    /// <summary>Ingredients the step uses, by <see cref="CulinaIngredient.IngredientId"/>.</summary>
    public IReadOnlyList<Guid>? Uses { get; init; }
}

/// <summary>Words, or a reference to one of the recipe's ingredients.</summary>
internal sealed record CulinaSegment
{
    /// <summary><c>text</c> or <c>ingredient</c>.</summary>
    public string? Type { get; init; }

    public string? Value { get; init; }

    public Guid? RecipeIngredientId { get; init; }
}

/// <summary>Where a recipe was imported from.</summary>
internal sealed record CulinaOrigin
{
    public string? SourceUrl { get; init; }
}
