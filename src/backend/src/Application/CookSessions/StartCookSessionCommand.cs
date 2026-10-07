using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Domain.Cooking;
using Domain.Shared;
using Response = Contracts.CookSessions.Response;

namespace Application.CookSessions;

/// <summary>
/// Starts cooking a recipe at <c>Servings</c>, in the household given or the recipe's own.
/// </summary>
public sealed record StartCookSessionCommand(Guid RecipeId, Guid UserId, decimal Servings, Guid? HouseholdId);

internal sealed class StartCookSessionCommandHandler(
    ICookSessionRepository sessions,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<StartCookSessionCommand, Response>
{
    public async Task<Result<Response>> Handle(
        StartCookSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("CookSessions.Start");

        // Reading the recipe is also the membership check: a recipe the caller cannot see is one
        // they cannot cook.
        var recipe = await RecipeAccess
            .VisibleInAsync(recipes, households, command.RecipeId, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var started = recipe.Bind(found => CookSession
            .Start(
                found.Id,
                command.UserId,
                command.HouseholdId ?? found.HouseholdId,
                command.Servings,
                time.GetUtcNow())
            .Map(session => (Session: session, Title: found.Title.Value)));

        var result = await started.Match(
            pair => SaveAsync(pair.Session, pair.Title, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> SaveAsync(
        CookSession session,
        string title,
        CancellationToken cancellationToken) =>
        await unitOfWork.InTransactionAsync(
            async token =>
            {
                var saved = await sessions
                    .StartAsync(session, time.GetUtcNow(), token)
                    .ConfigureAwait(false);

                return saved.Map(() => session.ToResponse(title));
            },
            cancellationToken).ConfigureAwait(false);
}
