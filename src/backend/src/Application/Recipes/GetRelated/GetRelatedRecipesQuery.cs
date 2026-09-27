using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Recipes.GetRelated;
using Domain.Search;
using Domain.Shared;
using Found = Application.Abstractions.RelatedRecipe;
using RelatedRecipe = Contracts.Recipes.GetRelated.RelatedRecipe;

namespace Application.Recipes.GetRelated;

/// <summary>Asks which recipes of the same household are most like this one.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Cursor">Where the previous page ended, or null for the first.</param>
/// <param name="Limit">How many at most.</param>
public sealed record GetRelatedRecipesQuery(Guid RecipeId, Guid UserId, string? Cursor, int Limit);

internal sealed class GetRelatedRecipesQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IRelatedRecipes related)
    : IQueryHandler<GetRelatedRecipesQuery, Response>
{
    /// <summary>How many shared things a reason names: enough to be a reason, few enough to read.</summary>
    private const int Named = 3;

    public async Task<Result<Response>> Handle(GetRelatedRecipesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetRelated");

        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, query.RecipeId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await visible.Match(
            async recipe =>
            {
                // The recipe's own library, which anybody who can see it can
                // see all of: an heir sees everything its parent does.
                var library = await HouseholdAccess
                    .LibraryAsync(households, recipe.HouseholdId, cancellationToken)
                    .ConfigureAwait(false);

                // Read on until the page is full or the kitchen runs out. A
                // related recipe with nothing to say for itself is left out, and
                // a page left short by that would read as the end of the shelf
                // when it is not.
                List<RelatedRecipe> items = [];
                var cursor = query.Cursor;

                do
                {
                    var page = await related
                        .FindAsync(recipe.Id, library, query.UserId, cursor, query.Limit - items.Count, cancellationToken)
                        .ConfigureAwait(false);

                    items.AddRange(page.Items.Select(one => Describe(one, recipe.Language)).OfType<RelatedRecipe>());
                    cursor = page.NextCursor;
                }
                while (items.Count < query.Limit && cursor is not null);

                return Result<Response>.Success(new Response { Items = items, NextCursor = cursor });
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>
    /// A related recipe with the reason it is related, or null when there is
    /// nothing telling to say — a suggestion with no reason is not offered.
    /// </summary>
    private static RelatedRecipe? Describe(Found found, Language language)
    {
        var kinds = Words(found.Kinds, language);
        var stuff = Words(found.Stuff, language);

        // Named for whichever of the two explains more of the score: two
        // pasta bakes are related because of what they are, a Chili and a
        // Bolognese because of what is in them.
        var byKind = kinds.Count > 0 && (stuff.Count == 0 || 0.6 * found.KindScore >= 0.4 * found.StuffScore);
        var shared = byKind ? kinds : stuff;

        return shared.Count == 0
            ? null
            : new RelatedRecipe
            {
                RecipeId = found.Recipe.RecipeId,
                HouseholdId = found.Recipe.HouseholdId,
                Title = found.Recipe.Title,
                ImageId = found.Recipe.ImageId,
                TotalMinutes = found.Recipe.TotalMinutes,
                YieldAmount = found.Recipe.YieldAmount,
                YieldKind = found.Recipe.YieldKind,
                YieldLabel = found.Recipe.YieldLabel,
                Tags = found.Recipe.Tags,
                CookCount = found.Recipe.CookCount,
                LastCookedAt = found.Recipe.LastCookedAt,
                UpdatedAt = found.Recipe.UpdatedAt,
                Reason = new RelatedReason { Kind = byKind ? "kinds" : "ingredients", Shared = shared }
            };
    }

    /// <summary>
    /// The shared concepts worth saying, in the recipe's own language.
    /// </summary>
    /// <remarks>
    /// A concept is left out when it is only there as what another one is a
    /// kind of: two chicken recipes share chicken, and saying "chicken,
    /// poultry, meat" is saying one thing three times.
    /// </remarks>
    internal static IReadOnlyList<string> Words(IReadOnlyList<string> shared, Language language) =>
    [
        .. shared
            .Where(key => !shared.Any(other => other != key && CulinaryLexicon.Lineage(other).Skip(1).Contains(key)))
            .Select(CulinaryLexicon.Find)
            .OfType<Concept>()
            .Select(concept => (language == Language.De ? concept.De : concept.En)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Named)
    ];
}
