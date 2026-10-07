namespace Infrastructure.Import.Tandoor;

/// <summary>Tandoor's own shapes, as its API actually sends them.</summary>
/// <remarks>
/// Foreign types, named after Tandoor's fields (not ours) so mapping happens in one visible place.
/// Everything is nullable: a years-old library carries rows from many Tandoor versions.
/// </remarks>
internal sealed record TandoorPage<TItem>
{
    /// <summary>How many there are altogether.</summary>
    public int? Count { get; init; }

    /// <summary>The whole URL of the next page, or null at the end.</summary>
    public string? Next { get; init; }

    /// <summary>What is on this page.</summary>
    public IReadOnlyList<TItem>? Results { get; init; }
}

/// <summary>A recipe as Tandoor's list endpoint sends it.</summary>
internal sealed record TandoorRecipeSummary
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An absolute URL, a path on the instance, or nothing.</summary>
    public string? Image { get; init; }

    /// <summary>Hands-on minutes.</summary>
    public int? WorkingTime { get; init; }

    /// <summary>Minutes spent waiting: proving, resting, in the oven.</summary>
    public int? WaitingTime { get; init; }

    public decimal? Servings { get; init; }
}

/// <summary>A recipe as Tandoor's detail endpoint sends it.</summary>
internal sealed record TandoorRecipe
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? Image { get; init; }

    public int? WorkingTime { get; init; }

    public int? WaitingTime { get; init; }

    public decimal? Servings { get; init; }

    /// <summary>Where the recipe was imported from, back when it was.</summary>
    public string? SourceUrl { get; init; }

    public IReadOnlyList<TandoorKeyword>? Keywords { get; init; }

    public IReadOnlyList<TandoorStep>? Steps { get; init; }
}

/// <summary>A tag.</summary>
internal sealed record TandoorKeyword
{
    /// <summary>The leaf name.</summary>
    public string? Name { get; init; }

    /// <summary>The name including its parents, on instances that nest keywords.</summary>
    public string? Label { get; init; }
}

/// <summary>One of Tandoor's steps, which is where its ingredients live.</summary>
/// <remarks>
/// Tandoor hangs ingredients off steps; this app keeps one list and lets steps point into it (see
/// <see cref="TandoorMapping"/>). A header step with no instruction is Tandoor's "For the sauce:",
/// i.e. our ingredient group.
/// </remarks>
internal sealed record TandoorStep
{
    /// <summary>The step's heading, when it has one.</summary>
    public string? Name { get; init; }

    /// <summary>What to do.</summary>
    public string? Instruction { get; init; }

    /// <summary>Minutes this step takes.</summary>
    public int? Time { get; init; }

    /// <summary>Whether this step is a heading rather than an instruction.</summary>
    public bool ShowAsHeader { get; init; }

    public IReadOnlyList<TandoorIngredient>? Ingredients { get; init; }
}

/// <summary>One ingredient line.</summary>
internal sealed record TandoorIngredient
{
    public decimal? Amount { get; init; }

    public TandoorNamed? Unit { get; init; }

    public TandoorNamed? Food { get; init; }

    public string? Note { get; init; }

    /// <summary>Whether this row is a heading inside the ingredient list.</summary>
    public bool IsHeader { get; init; }

    /// <summary>Whether Tandoor was told this one has no amount worth showing.</summary>
    public bool NoAmount { get; init; }

    /// <summary>The line as it was originally written, when Tandoor kept it.</summary>
    public string? OriginalText { get; init; }
}

/// <summary>A food or a unit: both are a name with a plural.</summary>
internal sealed record TandoorNamed
{
    public string? Name { get; init; }

    public string? PluralName { get; init; }
}

/// <summary>
/// What Tandoor answers when it trades a sign-in for a token; only the token is read and kept.
/// </summary>
/// <remarks>
/// The token carries Tandoor's full <c>read write app</c> scope and is not a throwaway; Culina
/// never writes with it.
/// </remarks>
internal sealed record TandoorToken
{
    public string? Token { get; init; }
}
