using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes;
using Domain.Import;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Create;

/// <summary>Starts a recipe.</summary>
/// <param name="HouseholdId">Which household will own it.</param>
/// <param name="Title">What to call it.</param>
/// <param name="UserId">Who is writing it down.</param>
/// <param name="DraftId">The assistant draft it came from, when it came from one.</param>
public sealed record CreateRecipeCommand(
    Guid HouseholdId,
    string Title,
    Guid UserId,
    Guid? DraftId = null);

internal sealed class CreateRecipeCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IRecipeOriginRepository origins,
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
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
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
        var now = time.GetUtcNow();
        var recipe = Recipe.Create(command.HouseholdId, title, command.UserId, now);

        return await unitOfWork.InTransactionAsync(
            async token =>
            {
                var added = await recipes.AddAsync(recipe, token).ConfigureAwait(false);

                return await added.Match(
                    async () =>
                    {
                        await RememberDraftedAsync(command, recipe, now, token).ConfigureAwait(false);

                        return Result<RecipeDetail>.Success(recipe.Describe());
                    },
                    error => Task.FromResult(Result<RecipeDetail>.Failure(error)))
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records that this recipe started as something the assistant wrote.
    /// </summary>
    /// <remarks>
    /// The same table and the same shape an imported recipe uses, because it is
    /// the same fact: this recipe did not start here. The draft's own id is the
    /// external id, so every ask is its own — which keeps the "once per
    /// household" index meaningful rather than making a second drafted recipe
    /// collide with the first.
    /// </remarks>
    private async Task RememberDraftedAsync(
        CreateRecipeCommand command,
        Recipe recipe,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (command.DraftId is not { } draftId)
        {
            return;
        }

        await origins.AddAsync(
                new RecipeOrigin(
                    recipe.Id,
                    recipe.HouseholdId,
                    SourceKind.Assistant,
                    SourceId: null,
                    draftId.ToString(),
                    SourceUrl: null,
                    now),
                cancellationToken)
            .ConfigureAwait(false);
    }

}
