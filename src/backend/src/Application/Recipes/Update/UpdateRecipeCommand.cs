using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Update;

/// <summary>Replaces a recipe's entire contents.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="ExpectedVersion">The version the caller last saw.</param>
/// <param name="Details">Everything except the ingredients and steps.</param>
/// <param name="Groups">The new ingredient groups.</param>
/// <param name="Steps">The new steps.</param>
public sealed record UpdateRecipeCommand(
    Guid RecipeId,
    Guid UserId,
    long ExpectedVersion,
    RecipeDraft Details,
    IReadOnlyList<IngredientGroupContract> Groups,
    IReadOnlyList<StepContract> Steps);

/// <summary>The scalar half of a recipe, straight off the wire.</summary>
/// <param name="Title">What to call it.</param>
/// <param name="Description">A short introduction.</param>
/// <param name="Language">The language code.</param>
/// <param name="YieldAmount">How many it makes.</param>
/// <param name="YieldKind">Of what.</param>
/// <param name="PrepMinutes">Hands-on time.</param>
/// <param name="CookMinutes">Time in the oven or on the hob.</param>
/// <param name="Tags">Its tags.</param>
public sealed record RecipeDraft(
    string Title,
    string? Description,
    string Language,
    decimal YieldAmount,
    string YieldKind,
    int? PrepMinutes,
    int? CookMinutes,
    IReadOnlyList<string> Tags);

internal sealed class UpdateRecipeCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<UpdateRecipeCommand, RecipeDetail>
{
    public async Task<Result<RecipeDetail>> Handle(
        UpdateRecipeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.Update");

        var found = await RecipeAccess
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var prepared = found.Bind(recipe => Apply(recipe, command).Map(() => recipe));

        var result = await prepared.Match(
            recipe => SaveAsync(recipe, command.ExpectedVersion, cancellationToken),
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>
    /// Parses the whole request first, then hands it to the aggregate in two
    /// calls — details, then contents — because only the aggregate can tell
    /// whether a step's references resolve.
    /// </summary>
    private Result Apply(Recipe recipe, UpdateRecipeCommand command)
    {
        var now = time.GetUtcNow();

        return RecipeParsing.ToDetails(command.Details)
            .Bind(details => recipe.Describe(details, now))
            .Bind(() => RecipeParsing.ToGroups(command.Groups))
            .Bind(groups => RecipeParsing.ToSteps(command.Steps)
                .Bind(steps => recipe.SetContents(groups, steps, now)));
    }

    private async Task<Result<RecipeDetail>> SaveAsync(
        Recipe recipe,
        long expectedVersion,
        CancellationToken cancellationToken) =>
        await unitOfWork.InTransactionAsync(
            async token =>
            {
                var saved = await recipes
                    .UpdateAsync(recipe, expectedVersion, token)
                    .ConfigureAwait(false);

                return saved.Map(version =>
                {
                    recipe.AcceptVersion(version);

                    return recipe.Describe();
                });
            },
            cancellationToken).ConfigureAwait(false);
}
