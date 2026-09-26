namespace Application.Abstractions;

/// <summary>Reads the vocabulary a household's recipes have built up.</summary>
/// <remarks>
/// There is no tag management: a tag exists because a recipe carries it and
/// stops existing when the last one lets it go. So the only question worth
/// asking is which ones are in use, and how much.
/// </remarks>
public interface ITagRepository
{
    /// <summary>The tags a household's recipes carry, most used first, one per slug.</summary>
    /// <param name="library">Whose recipes: a household, then every household it inherits from.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<TagUsage>> InUseAsync(IReadOnlyList<Guid> library, CancellationToken cancellationToken);
}

/// <summary>One tag, and how many recipes carry it.</summary>
/// <param name="Slug">The normalised form filters and rules name.</param>
/// <param name="Name">The words somebody typed.</param>
/// <param name="RecipeCount">How many recipes carry it.</param>
public sealed record TagUsage(string Slug, string Name, int RecipeCount);
