using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Domain.Cooking;
using Domain.Shared;
using Response = Contracts.CookSessions.Response;

namespace Application.CookSessions;

/// <summary>Starts cooking a recipe.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is cooking.</param>
/// <param name="Servings">The scaling to cook at.</param>
public sealed record StartCookSessionCommand(Guid RecipeId, Guid UserId, decimal Servings);

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

        // Reading the recipe is not only for the title: it is the membership
        // check. A recipe the caller cannot see is a recipe they cannot cook.
        var recipe = await RecipeAccess
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var started = recipe.Bind(found => CookSession
            .Start(found.Id, command.UserId, found.HouseholdId, command.Servings, time.GetUtcNow())
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
