namespace Contracts.Recipes.Share;

/// <summary>
/// The link that publishes one recipe.
/// </summary>
/// <remarks>
/// The token rather than a whole address: where Culina is reachable from is the
/// browser's business, not the server's, and an instance behind a proxy or on a
/// second hostname would otherwise hand out links to the wrong origin.
/// </remarks>
public sealed record Response
{
    /// <summary>The secret that goes in the link.</summary>
    public required string Token { get; init; }

    /// <summary>When the recipe was first published.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
