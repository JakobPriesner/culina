using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Shopping;
using Application.Telemetry;
using Contracts.Recipes.GetIngredients;
using Domain.Ingredients;
using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Recipes.GetIngredients;

/// <summary>
/// Suggests what an ingredient line could be about; <c>Language</c> names the seeded ones.
/// </summary>
public sealed record GetIngredientsQuery(
    Guid HouseholdId,
    Guid UserId,
    string? Query,
    string? Language);

internal sealed class GetIngredientsQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IIngredientNameRepository names)
    : IQueryHandler<GetIngredientsQuery, Response>
{
    /// <summary>
    /// How many to offer: a list you scroll is a list you stop reading, and ten is about as many as
    /// anyone scans.
    /// </summary>
    private const int Limit = 10;

    public async Task<Result<Response>> Handle(
        GetIngredientsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetIngredients");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () => Result<Response>.Success(
                await SuggestAsync(query, cancellationToken).ConfigureAwait(false)),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Response> SuggestAsync(
        GetIngredientsQuery query,
        CancellationToken cancellationToken)
    {
        // A bad language code is not worth a 400 here: the worst it does is name the seeded ones in
        // English.
        var language = RecipeWords.ToLanguage(query.Language).Match(one => one, _ => Language.En);

        var library = await HouseholdAccess
            .LibraryAsync(households, query.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var equivalent = await EquivalentAsync(query, cancellationToken).ConfigureAwait(false);

        var written = await recipes
            .OwnIngredientNamesAsync(library, query.Query, Limit, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<string> own = equivalent is null
            ? written
            : [equivalent, .. written.Where(name => name != equivalent).Take(Limit - 1)];

        var taken = own.Select(ItemName.Fold).ToHashSet(StringComparer.Ordinal);

        var seeded = CommonIngredients
            .Matching(query.Query, language, Limit)
            .Where(entry => !taken.Contains(ItemName.Fold(entry.In(language))));

        return new Response
        {
            Items =
            [
                .. own.Select(name => new IngredientSuggestion(
                    name,
                    ShoppingWords.Of(SectionFor(name)),
                    Own: true,
                    Equivalent: name == equivalent)),
                .. seeded
                    .Take(Limit - own.Count)
                    .Select(entry => new IngredientSuggestion(
                        entry.In(language),
                        ShoppingWords.Of(entry.Section),
                        Own: false,
                        Equivalent: false))
            ]
        };
    }

    /// <summary>
    /// The household's own name for what was typed, when it is spelt otherwise: the one the typed
    /// name was merged into, else its most used lookalike. So the editor can say "you already have
    /// this" before a near-duplicate is saved.
    /// </summary>
    private async Task<string?> EquivalentAsync(GetIngredientsQuery query, CancellationToken cancellationToken)
    {
        var typed = query.Query?.Trim() ?? string.Empty;

        if (typed.Length == 0)
        {
            return null;
        }

        var merges = await names.MergesAsync(query.HouseholdId, cancellationToken).ConfigureAwait(false);
        var kept = new IngredientMerges(merges).Resolve(typed);

        if (kept != typed)
        {
            return kept;
        }

        var uses = await names.UsesAsync(query.HouseholdId, cancellationToken).ConfigureAwait(false);

        return IngredientLookalikes.Existing(typed, uses)?.Name;
    }

    private static ShoppingSection SectionFor(string name) =>
        ItemName.Create(name).Match(
            SectionKeywords.SectionFor,
            _ => ShoppingSection.Other);
}
