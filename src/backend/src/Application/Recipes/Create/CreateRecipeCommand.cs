using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Create;

/// <summary>Starts a recipe.</summary>
/// <param name="HouseholdId">Which household will own it.</param>
/// <param name="Title">What to call it.</param>
/// <param name="UserId">Who is writing it down.</param>
public sealed record CreateRecipeCommand(Guid HouseholdId, string Title, Guid UserId);

internal sealed class CreateRecipeCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CreateRecipeCommand, RecipeDetail>
{
    public async Task<Result<RecipeDetail>> Handle(
        CreateRecipeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.Create");

        var permitted = await RecipeAccess
            .MayWriteToHouseholdAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var prepared = permitted.Bind(() => RecipeTitle.Create(command.Title));

        var result = await prepared.Match(
            title => StoreAsync(command, title, cancellationToken),
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<RecipeDetail>> StoreAsync(
        CreateRecipeCommand command,
        RecipeTitle title,
        CancellationToken cancellationToken)
    {
        // Nothing but a title. Everything else is optional and addable later,
        // which is what makes the create form something people finish.
        var recipe = Recipe.Create(command.HouseholdId, title, command.UserId, time.GetUtcNow());

        return await unitOfWork.InTransactionAsync(
            async token =>
            {
                var added = await recipes.AddAsync(recipe, token).ConfigureAwait(false);

                return added.Bind(() => Result<RecipeDetail>.Success(recipe.Describe()));
            },
            cancellationToken).ConfigureAwait(false);
    }

}
