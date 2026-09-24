using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Search;
using Application.Telemetry;
using Contracts.Recipes.GetCompletions;
using Domain.Search;
using Domain.Shared;

namespace Application.Recipes.GetCompletions;

/// <summary>Completes a half-typed search.</summary>
/// <param name="HouseholdId">Whose kitchen.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Query">What is in the field so far.</param>
public sealed record GetCompletionsQuery(Guid HouseholdId, Guid UserId, string? Query);

internal sealed class GetCompletionsQueryHandler(
    ISearchVocabulary vocabulary,
    IHouseholdRepository households)
    : IQueryHandler<GetCompletionsQuery, Response>
{
    /// <summary>
    /// How many of each kind: three recipes, three ingredients, three tags and
    /// a refinement is a list read at a glance, which is the whole point of
    /// offering it before the results arrive.
    /// </summary>
    private const int PerKind = 3;

    /// <summary>The ceiling a refinement offers: the one people type most.</summary>
    private const int QuickMinutes = 30;

    public async Task<Result<Response>> Handle(GetCompletionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetCompletions");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () => Result<Response>.Success(await CompleteAsync(query, cancellationToken).ConfigureAwait(false)),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Response> CompleteAsync(GetCompletionsQuery query, CancellationToken cancellationToken)
    {
        // Only the words still to be found are completed: "vegetarisch häh"
        // is a diet that has been understood and a word that has not.
        var typed = QueryUnderstanding.Parse(query.Query).FreeText;

        // One letter is a prefix of half the library, and completing it is
        // noise rather than help.
        if (SearchText.FoldAe(typed).Length < 2)
        {
            return new Response { Items = [] };
        }

        var found = await vocabulary
            .CompletionsAsync(query.HouseholdId, typed, PerKind, cancellationToken)
            .ConfigureAwait(false);

        return new Response
        {
            Items =
            [
                .. found.Recipes.Select(recipe => new Completion
                {
                    Kind = "recipe",
                    Label = recipe.Title,
                    RecipeId = recipe.RecipeId,
                    ImageId = recipe.ImageId,
                    TotalMinutes = recipe.TotalMinutes
                }),
                .. found.Ingredients.Select(ingredient => new Completion
                {
                    Kind = "ingredient",
                    Label = ingredient.Name,
                    RecipeCount = ingredient.RecipeCount
                }),
                .. found.Tags.Select(tag => new Completion
                {
                    Kind = "tag",
                    Label = tag.Name,
                    Slug = tag.Slug,
                    RecipeCount = tag.RecipeCount
                }),
                .. Refinement(found.Ingredients)
            ]
        };
    }

    /// <summary>
    /// "Hähnchen · unter 30 Minuten", offered only when it would split what
    /// the ingredient finds: a refinement that keeps every recipe, or none,
    /// is a tap wasted.
    /// </summary>
    private static IEnumerable<Completion> Refinement(IReadOnlyList<IngredientCompletion> ingredients) =>
        ingredients
            .Where(ingredient => ingredient.QuickCount > 0 && ingredient.QuickCount < ingredient.RecipeCount)
            .Take(1)
            .Select(ingredient => new Completion
            {
                Kind = "refinement",
                Label = ingredient.Name,
                RecipeCount = ingredient.QuickCount,
                MaxMinutes = QuickMinutes
            });
}
