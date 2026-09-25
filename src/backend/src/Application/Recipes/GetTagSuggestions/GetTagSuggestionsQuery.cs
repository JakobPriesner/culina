using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes.GetTagSuggestions;
using Domain.Search;
using Domain.Shared;

namespace Application.Recipes.GetTagSuggestions;

/// <summary>Asks which tags a recipe could carry and does not.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetTagSuggestionsQuery(Guid RecipeId, Guid UserId);

/// <summary>
/// Offers the tags the lexicon reads a recipe as, where the recipe does not
/// already carry them.
/// </summary>
/// <remarks>
/// <para>
/// Offered, never applied. The household's tags stay the household's: if the
/// lexicon wrote them, "household vocabulary beats the lexicon" would be the
/// lexicon beating itself. And where the household already has a word for a
/// thing — its own "italienisch", its own "Ofengericht" — that word is the one
/// offered, so a suggestion adds to the vocabulary it has rather than starting
/// a second one beside it.
/// </para>
/// <para>
/// Only what a recipe <em>is</em>: its dish, cuisine, meal, method and diet.
/// Ingredients are already found by the search without a tag, and "warm" or
/// "süß" are true of too much to sort anything by. And only what the recipe
/// names and one step above it — a Lasagne is offered "Lasagne", "Auflauf" and
/// "Italienisch", not everything a Lasagne is ultimately a kind of.
/// </para>
/// </remarks>
internal sealed class GetTagSuggestionsQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    ITagRepository tags)
    : IQueryHandler<GetTagSuggestionsQuery, Response>
{
    /// <summary>A row of a few, read at a glance beside the tags themselves.</summary>
    private const int Limit = 5;

    /// <summary>The kinds offered, in the order they are offered.</summary>
    private static readonly ConceptKind[] Offered =
    [
        ConceptKind.Cuisine,
        ConceptKind.Meal,
        ConceptKind.Method,
        ConceptKind.Diet,
        ConceptKind.Dish
    ];

    public async Task<Result<Response>> Handle(GetTagSuggestionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetTagSuggestions");

        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, query.RecipeId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await visible.Match(
            async recipe =>
            {
                var used = await tags.InUseAsync(recipe.HouseholdId, cancellationToken).ConfigureAwait(false);

                return Result<Response>.Success(new Response
                {
                    Items = Suggest(
                        new Suggesting(
                            recipe.Title.Value,
                            recipe.Tags,
                            [.. recipe.Groups.SelectMany(group => group.Ingredients).Select(one => one.Name)],
                            recipe.Language),
                        used)
                });
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>The tags worth offering one recipe, given the household's vocabulary.</summary>
    internal static IReadOnlyList<TagSuggestion> Suggest(Suggesting recipe, IReadOnlyList<TagUsage> household)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(household);

        // Its tags by the words somebody typed, which is what the lexicon reads
        // — and what its search document was built from.
        var names = household.ToDictionary(tag => tag.Slug, tag => tag.Name, StringComparer.Ordinal);
        var carried = recipe.Tags.Select(slug => names.GetValueOrDefault(slug, slug)).ToList();

        var described = CulinaryLexicon.Describe(recipe.Title, carried, recipe.Ingredients);

        // What it names, rather than what that is a kind of, and one step up.
        var named = described
            .Where(key => !described.Any(other => other != key && CulinaryLexicon.Lineage(other).Skip(1).Contains(key)))
            .ToList();
        var near = named
            .Concat(named.SelectMany(key => CulinaryLexicon.Find(key)?.Parents ?? []))
            .Distinct(StringComparer.Ordinal);

        var covered = carried.SelectMany(CulinaryLexicon.Recognise).ToHashSet(StringComparer.Ordinal);

        // The household's own word for a concept: the most used tag that is a
        // name of it, and one this recipe does not carry already.
        var theirs = household
            .Where(tag => !recipe.Tags.Contains(tag.Slug, StringComparer.Ordinal))
            .Select(tag => (Concept: CulinaryLexicon.Name(tag.Name)?.Key, Tag: tag))
            .Where(one => one.Concept is not null)
            .GroupBy(one => one.Concept!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.MaxBy(one => one.Tag.RecipeCount).Tag, StringComparer.Ordinal);

        return
        [
            .. near
                .Select(CulinaryLexicon.Find)
                .OfType<Concept>()
                .Where(concept => Offered.Contains(concept.Kind) && !covered.Contains(concept.Key))
                .Select(concept => (Concept: concept, Tag: theirs.GetValueOrDefault(concept.Key)))
                .OrderByDescending(one => one.Tag?.RecipeCount ?? -1)
                .ThenBy(one => Array.IndexOf(Offered, one.Concept.Kind))
                .ThenByDescending(one => CulinaryLexicon.Lineage(one.Concept.Key).Count)
                .Select(one => one.Tag is { } tag
                    ? new TagSuggestion { Name = tag.Name, Slug = tag.Slug }
                    : new TagSuggestion { Name = Word(one.Concept, recipe.Language) })
                .DistinctBy(one => one.Name, StringComparer.OrdinalIgnoreCase)
                .Where(one => !carried.Contains(one.Name, StringComparer.OrdinalIgnoreCase))
                .Take(Limit)
        ];
    }

    private static string Word(Concept concept, Language language) =>
        (language == Language.De ? concept.De : concept.En)[0];
}

/// <summary>What is read about a recipe to suggest its tags.</summary>
/// <param name="Title">Its title.</param>
/// <param name="Tags">The slugs of the tags it carries.</param>
/// <param name="Ingredients">Its ingredient names.</param>
/// <param name="Language">The language it is written in, which a new tag is worded in.</param>
internal sealed record Suggesting(
    string Title,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Ingredients,
    Language Language);
