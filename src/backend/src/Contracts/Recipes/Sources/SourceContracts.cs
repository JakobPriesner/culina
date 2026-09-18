namespace Contracts.Recipes.Sources;

/// <summary>Asks for another app's recipe library to be connected.</summary>
public sealed record ConnectSourceRequest
{
    /// <summary>Which kitchen is connecting it.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>Which app. Currently only <c>tandoor</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Where it is: <c>https://recipes.example.com</c>.
    /// </summary>
    /// <remarks>
    /// Anything after the host is dropped, because people paste what is in the
    /// address bar and a path kept here would be prefixed onto every request
    /// this connection ever makes.
    /// </remarks>
    public required string Address { get; init; }

    /// <summary>
    /// The API token from that app.
    /// </summary>
    /// <remarks>
    /// Goes in, and never comes back out. No response on this API carries it.
    /// Send this <em>or</em> a username and password, never both.
    /// </remarks>
    public string? Token { get; init; }

    /// <summary>
    /// The name that account signs in with over there.
    /// </summary>
    /// <remarks>
    /// The alternative to <see cref="Token"/>, and the one most people can
    /// actually supply: "make an API token first" is a thing somebody has to go
    /// and learn before they can start, and it is where most attempts to move
    /// recipes stop.
    /// </remarks>
    public string? Username { get; init; }

    /// <summary>
    /// The password for that account.
    /// </summary>
    /// <remarks>
    /// Used once, to ask that app for a token, and then dropped. It is never
    /// stored, never logged, and never returned. What is kept is the token that
    /// came back — the same token the person would have made by hand.
    /// </remarks>
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
    /// A picture of it, over there.
    /// </summary>
    /// <remarks>
    /// Reported, but deliberately not drawn in the picker. A browser loading it
    /// would be asking that server directly, with no token — and media behind a
    /// sign-in is common enough that a grid of broken pictures is the likelier
    /// outcome than a grid of photos. The picture is fetched by this server,
    /// with the token, when the recipe is actually brought over.
    /// </remarks>
    public string? ImageUrl { get; init; }

    /// <summary>How long it takes, when that app says.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>
    /// The recipe this one already is here, when it has been brought over
    /// before.
    /// </summary>
    /// <remarks>
    /// The single most important field on this contract. Importing is not a
    /// one-off — people come back for what is new — and a picker that cannot
    /// say "you already have this" makes the second visit as much work as the
    /// first.
    /// </remarks>
    public Guid? AlreadyHere { get; init; }
}

/// <summary>Asks for some of their recipes to be brought over.</summary>
public sealed record ImportFromSourceRequest
{
    /// <summary>
    /// Which of their recipes, by the id the browse gave back.
    /// </summary>
    /// <remarks>
    /// The whole selection, in one request. It names the work rather than doing
    /// it: the answer comes back as soon as the import has a name and a shelf,
    /// and the recipes arrive afterwards, over the stream.
    /// </remarks>
    public required IReadOnlyList<string> ExternalIds { get; init; }
}

/// <summary>An import that has been accepted and is now running.</summary>
/// <remarks>
/// Everything a caller needs to follow it and to leave: the id of the stream to
/// listen on, and the shelf the recipes are landing on whether or not anybody
/// is still watching.
/// </remarks>
public sealed record ImportStartedResponse
{
    /// <summary>Which import. Stream it at <c>imports/{importId}/events</c>.</summary>
    public required Guid ImportId { get; init; }

    /// <summary>
    /// The cookbook everything from this import is going onto.
    /// </summary>
    /// <remarks>
    /// Known before the first recipe is fetched, which is what lets the screen
    /// offer the way out of it from the beginning. Four hundred recipes
    /// arriving into a library is invisible; four hundred recipes on a shelf
    /// called "From Tandoor, 17 September" is a thing you can look at, share,
    /// and delete.
    /// </remarks>
    public required Guid CookbookId { get; init; }

    /// <summary>What it is called.</summary>
    public required string CookbookName { get; init; }

    /// <summary>How many recipes were asked for.</summary>
    public required int Total { get; init; }
}

/// <summary>One line of an import's progress, as the stream sends it.</summary>
/// <remarks>
/// One event per recipe finished, in the order they finished, and one last
/// event with no recipe on it saying the run is over. Every event carries the
/// running count, so a client that joined late or missed one is still right.
/// </remarks>
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
    /// <c>imported</c>, <c>already_here</c>, or <c>failed</c>.
    /// </summary>
    /// <remarks>
    /// Three outcomes and not two, because "we already had it" is not a failure
    /// and must not be counted as one. Told per recipe rather than as a total,
    /// so twelve that could not be read can be shown by name instead of as a
    /// number.
    /// </remarks>
    public required string Outcome { get; init; }

    /// <summary>The recipe here, when there is one.</summary>
    public Guid? RecipeId { get; init; }

    /// <summary>What it is called, for showing the failures by name.</summary>
    public string? Title { get; init; }

    /// <summary>Why it failed, as an error code.</summary>
    public string? Reason { get; init; }
}
