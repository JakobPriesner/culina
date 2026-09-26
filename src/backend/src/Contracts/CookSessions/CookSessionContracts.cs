namespace Contracts.CookSessions;

/// <summary>What to start cooking.</summary>
public sealed record StartRequest
{
    /// <summary>Which recipe.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>The scaling to cook at, so resuming reopens at the same numbers.</summary>
    public required decimal Servings { get; init; }

    /// <summary>
    /// The household it is being cooked in, for a recipe that household
    /// inherits. Left out, the recipe's own household.
    /// </summary>
    public Guid? HouseholdId { get; init; }
}

/// <summary>A change to a session in progress.</summary>
/// <remarks>
/// Both fields are optional: a step advance sends only the step, which is by
/// far the most frequent call and stays cheap.
/// </remarks>
public sealed record UpdateRequest
{
    /// <summary>Which step the cook is on, from zero.</summary>
    public int? CurrentStepIndex { get; init; }

    /// <summary>A new scaling, because one more person arrived.</summary>
    public decimal? Servings { get; init; }
}

/// <summary>A cooking session.</summary>
public sealed record Response
{
    /// <summary>The session's id.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>What is being cooked.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>What that recipe is called, so a resume bar needs no second request.</summary>
    public required string RecipeTitle { get; init; }

    /// <summary>The scaling in force.</summary>
    public required decimal Servings { get; init; }

    /// <summary>Which step the cook is on.</summary>
    public required int CurrentStepIndex { get; init; }

    /// <summary>When they started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>When they last did anything.</summary>
    public required DateTimeOffset LastActiveAt { get; init; }

    /// <summary>The entity version, for If-Match on a change worth guarding.</summary>
    public required long Version { get; init; }
}
