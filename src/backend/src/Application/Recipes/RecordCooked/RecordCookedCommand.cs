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
public sealed record RecordCookedCommand(
    Guid RecipeId,
    Guid UserId,
    DateTimeOffset? MadeAt,
    decimal? Servings,
    string? Note);

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
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var prepared = visible.Bind(recipe => CookLogEntry.Record(
            recipe.Id,
            command.UserId,
            recipe.HouseholdId,
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
