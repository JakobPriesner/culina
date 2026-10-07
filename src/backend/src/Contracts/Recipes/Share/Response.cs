namespace Contracts.Recipes.Share;

/// <summary>
/// The link that publishes one recipe.
/// </summary>
/// <remarks>The token, not a whole address: where Culina is reachable from is the browser's business.</remarks>
public sealed record Response
{
    /// <summary>The secret that goes in the link.</summary>
    public required string Token { get; init; }

    /// <summary>When the recipe was first published.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
