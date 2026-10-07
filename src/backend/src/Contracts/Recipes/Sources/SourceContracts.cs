namespace Contracts.Recipes.Sources;

/// <summary>Asks for another app's recipe library to be connected.</summary>
public sealed record ConnectSourceRequest
{
    /// <summary>Which kitchen is connecting it.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>Which app. Currently only <c>tandoor</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Where it is, such as <c>https://recipes.example.com</c>. Anything after the host is dropped.</summary>
    public required string Address { get; init; }

    /// <summary>The app's API token. Never returned. Send this or a username and password, not both.</summary>
    public string? Token { get; init; }

    /// <summary>The account name over there; the alternative to <see cref="Token"/>.</summary>
    public string? Username { get; init; }

    /// <summary>Used once to obtain a token, then dropped. Never stored, logged or returned.</summary>
    public string? Password { get; init; }

    /// <summary>What to call it here. Its host name, when this is left out.</summary>
    public string? Label { get; init; }
}

/// <summary>The libraries a household has connected.</summary>
public sealed record SourcesResponse
{
    /// <summary>The connections, oldest first.</summary>
    public required IReadOnlyList<SourceSummary> Items { get; init; }
}

/// <summary>A connected library, as its row on screen shows it.</summary>
public sealed record SourceSummary
{
    /// <summary>The connection's id.</summary>
    public required Guid SourceId { get; init; }

    /// <summary>Which app it is.</summary>
    public required string Kind { get; init; }

    /// <summary>What it is called here.</summary>
    public required string Label { get; init; }

    /// <summary>Where it is. Deliberately shown, so a wrong one is visible.</summary>
    public required string Address { get; init; }

    /// <summary>When it was connected.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When recipes were last brought over, or null if never.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }
}

/// <summary>A page of somebody's library over there, ready to be chosen from.</summary>
public sealed record SourceRecipesResponse
{
    /// <summary>What is on this page.</summary>
    public required IReadOnlyList<SourceRecipeSummary> Items { get; init; }

    /// <summary>Pass this back as <c>page</c> for the next one, or null at the end.</summary>
    public string? NextPage { get; init; }

    /// <summary>How many there are over there altogether, when that app says.</summary>
    public int? Total { get; init; }
}

/// <summary>One of their recipes, as the card in the picker draws it.</summary>
public sealed record SourceRecipeSummary
{
    /// <summary>What the other app calls it. Pass this back to import it.</summary>
    public required string ExternalId { get; init; }

    /// <summary>Its name.</summary>
    public required string Title { get; init; }

    /// <summary>Its introduction, when it has one.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// A picture of it, over there. Not drawn in the picker (a browser has no token for that server);
    /// this server fetches it on import.
    /// </summary>
    public string? ImageUrl { get; init; }

    /// <summary>How long it takes, when that app says.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>The recipe here that this one was already imported as, if any.</summary>
    public Guid? AlreadyHere { get; init; }
}

/// <summary>Asks for some of their recipes to be brought over.</summary>
public sealed record ImportFromSourceRequest
{
    /// <summary>The whole selection, by the ids the browse gave back. Answered once the import is named; recipes follow over the stream.</summary>
    public required IReadOnlyList<string> ExternalIds { get; init; }

    /// <summary>Bring them over even where one looks like a recipe already here. Only set after the person confirmed it.</summary>
    public bool AllowLookalikes { get; init; }

    /// <summary>The shelf of an earlier import to land on, instead of a new one.</summary>
    public Guid? CookbookId { get; init; }
}

/// <summary>An import that has been accepted and is now running.</summary>
public sealed record ImportStartedResponse
{
    /// <summary>Which import. Stream it at <c>imports/{importId}/events</c>.</summary>
    public required Guid ImportId { get; init; }

    /// <summary>The cookbook everything from this import is going onto; known before the first recipe is fetched.</summary>
    public required Guid CookbookId { get; init; }

    /// <summary>What it is called.</summary>
    public required string CookbookName { get; init; }

    /// <summary>How many recipes were asked for.</summary>
    public required int Total { get; init; }
}

/// <summary>
/// One line of an import's progress: one per finished recipe, then a last one with no recipe.
/// Each carries the running count, so a late joiner is still right.
/// </summary>
public sealed record ImportEvent
{
    /// <summary>What happened to one recipe, or null on the last event.</summary>
    public ImportedRecipe? Recipe { get; init; }

    /// <summary>How many of the selection have been tried, including this one.</summary>
    public required int Done { get; init; }

    /// <summary>How many were asked for.</summary>
    public required int Total { get; init; }

    /// <summary>True on the last event, and only then.</summary>
    public required bool Finished { get; init; }
}


/// <summary>What happened to one recipe.</summary>
public sealed record ImportedRecipe
{
    /// <summary>Which of theirs this is about.</summary>
    public required string ExternalId { get; init; }

    /// <summary>
    /// <c>imported</c>, <c>already_here</c>, <c>looks_like</c> or <c>failed</c>.
    /// Only <c>failed</c> is an error; <c>looks_like</c> is held back for a person to decide.
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>The recipe here, when there is one: the one it became, the one it already was, or the one it looks like.</summary>
    public Guid? RecipeId { get; init; }

    /// <summary>What it looks like, for <c>looks_like</c>.</summary>
    public ImportLookalike? LooksLike { get; init; }

    /// <summary>What it is called, for showing the failures by name.</summary>
    public string? Title { get; init; }

    /// <summary>Why it failed, as an error code.</summary>
    public string? Reason { get; init; }
}

/// <summary>A recipe already here that an imported one looks like.</summary>
public sealed record ImportLookalike
{
    /// <summary>Its title.</summary>
    public required string Title { get; init; }

    /// <summary>How many ingredients the two have in common.</summary>
    public required int SharedIngredients { get; init; }

    /// <summary>How often the person importing has made it.</summary>
    public required int CookCount { get; init; }
}
