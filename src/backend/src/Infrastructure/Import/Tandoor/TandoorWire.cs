namespace Infrastructure.Import.Tandoor;

/// <summary>
/// Tandoor's own shapes, as its API actually sends them.
/// </summary>
/// <remarks>
/// <para>
/// Foreign types, and they stay foreign: nothing outside this folder sees one.
/// They are named after Tandoor's fields rather than after ours on purpose —
/// a DTO that quietly renamed <c>working_time</c> to <c>PrepMinutes</c> would
/// be doing the mapping in the place nobody looks for it, and the day Tandoor
/// changes what <c>working_time</c> means, nobody would find it either.
/// </para>
/// <para>
/// Everything is nullable, including things Tandoor's documentation says are
/// required. A library somebody has been keeping for five years has been
/// through five years of Tandoor versions, and the import has to survive the
/// rows those left behind.
/// </para>
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

    /// <summary>An absolute URL, or a path on the instance, or nothing.</summary>
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

/// <summary>
/// One of Tandoor's steps, which is where its ingredients live.
/// </summary>
/// <remarks>
/// The structural difference between the two apps, and the reason
/// <see cref="TandoorMapping"/> exists. Tandoor hangs ingredients off steps;
/// this app keeps one ingredient list for the recipe and lets a step point into
/// it. A step whose <see cref="ShowAsHeader"/> is set and whose instruction is
/// empty is Tandoor's way of writing "For the sauce:", which is exactly this
/// app's ingredient group.
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
