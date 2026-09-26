using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Cooking;
using Domain.Shared;
using Response = Contracts.Recipes.RecordCooked.Response;

namespace Application.Recipes.RecordCooked;

/// <summary>Records that you cooked a recipe.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who cooked it.</param>
/// <param name="MadeAt">When, or null for now.</param>
/// <param name="Servings">How much was made.</param>
/// <param name="Note">Anything worth remembering.</param>
/// <param name="HouseholdId">
/// The household it was cooked in, or null for the recipe's own. An inherited
/// recipe cooked in the heir is the heir's history, not the parent's.
/// </param>
public sealed record RecordCookedCommand(
    Guid RecipeId,
    Guid UserId,
    DateTimeOffset? MadeAt,
    decimal? Servings,
    string? Note,
    Guid? HouseholdId);

internal sealed class RecordCookedCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    ICookLogRepository log,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<RecordCookedCommand, Response>
{
    public async Task<Result<Response>> Handle(
        RecordCookedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.RecordCooked");

        var visible = await RecipeAccess
            .VisibleInAsync(recipes, households, command.RecipeId, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Stamped with the kitchen it was cooked in. Stamping the recipe's own
        // household instead would put somebody who only inherits it into the
        // parent's suggestions by name, in a household they are not part of.
        var prepared = visible.Bind(recipe => CookLogEntry.Record(
            recipe.Id,
            command.UserId,
            command.HouseholdId ?? recipe.HouseholdId,
            command.MadeAt ?? time.GetUtcNow(),
            command.Servings,
            command.Note));

        var result = await prepared.Match(
            entry => StoreAsync(entry, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> StoreAsync(
        CookLogEntry entry,
        CancellationToken cancellationToken) =>
        await unitOfWork.InTransactionAsync(
            async token =>
            {
                var added = await log.AddAsync(entry, token).ConfigureAwait(false);

                return await added.Match(
                    async () =>
                    {
                        // The new count comes back with the entry, so the "made
                        // it" toast can say "that's the 8th time" without the
                        // client asking again.
                        var entries = await log
                            .ForRecipeAsync(entry.RecipeId, entry.UserId, token)
                            .ConfigureAwait(false);

                        return Result<Response>.Success(new Response
                        {
                            EntryId = entry.Id,
                            MadeAt = entry.MadeAt,
                            Count = entries.Count
                        });
                    },
                    error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
}
