using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Recipes;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Copy;

/// <summary>Makes a household its own copy of a recipe it can read.</summary>
/// <remarks>
/// For an inherited recipe somebody wants to change: they change their own copy and the original
/// stays what its household wrote. Reading is already all it takes to write it out by hand, so any
/// readable recipe may be copied into any household they are in.
/// </remarks>
public sealed record CopyRecipeCommand(Guid RecipeId, Guid HouseholdId, Guid UserId);

internal sealed class CopyRecipeCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CopyRecipeCommand, RecipeDetail>
{
    public async Task<Result<RecipeDetail>> Handle(
        CopyRecipeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.Copy");

        var source = await RecipeAccess
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var into = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var now = time.GetUtcNow();
        var copied = source.Bind(recipe => into
            .Bind(() => recipe.CopyInto(command.HouseholdId, command.UserId, now))
            .Map(copy => (Source: recipe, Copy: copy)));

        var result = await copied.Match(
            pair => StoreAsync(pair.Source, pair.Copy, now, cancellationToken),
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private Task<Result<RecipeDetail>> StoreAsync(
        Recipe source,
        Recipe copy,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        unitOfWork.InTransactionAsync(
            async token =>
            {
                var added = await recipes.AddAsync(copy, token).ConfigureAwait(false);

                return await added.Match(
                    async () =>
                    {
                        await recipes.CopyImageAsync(source.Id, copy.Id, now, token).ConfigureAwait(false);

                        var stored = await recipes.FindAsync(copy.Id, token).ConfigureAwait(false);

                        return stored.Map(recipe => recipe.Describe());
                    },
                    error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);
            },
            cancellationToken);
}
