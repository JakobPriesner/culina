namespace Contracts.Households.GetTrash;

/// <summary>What is in a household's bin, newest first.</summary>
public sealed record Response
{
    /// <summary>Recipes and cookbooks that were deleted and can still be restored.</summary>
    public required IReadOnlyList<TrashItem> Items { get; init; }
}

/// <summary>One deleted recipe or cookbook.</summary>
public sealed record TrashItem
{
    /// <summary><c>recipe</c> or <c>cookbook</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Its id, for <c>POST /recipes/{id}/restorations</c> or <c>/cookbooks/{id}/restorations</c>.</summary>
    public required Guid Id { get; init; }

    /// <summary>The recipe's title or the cookbook's name.</summary>
    public required string Name { get; init; }

    /// <summary>When it was deleted.</summary>
    public required DateTimeOffset DeletedAt { get; init; }

    /// <summary>When it will be removed for good, unless it is restored first.</summary>
    public required DateTimeOffset PurgeAfter { get; init; }

    /// <summary>Who deleted it, by name, or null when that account no longer exists.</summary>
    public string? DeletedBy { get; init; }
}
