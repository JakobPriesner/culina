using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Cooking;
using Domain.Shared;
using Response = Contracts.CookSessions.Response;

namespace Application.CookSessions;

/// <summary>Moves a cook along, or rescales what they are cooking.</summary>
/// <param name="SessionId">Which session.</param>
/// <param name="UserId">Whose it must be.</param>
/// <param name="CurrentStepIndex">The step they are on now, if it moved.</param>
/// <param name="Servings">A new scaling, if it changed.</param>
public sealed record UpdateCookSessionCommand(
    Guid SessionId,
    Guid UserId,
    int? CurrentStepIndex,
    decimal? Servings);

internal sealed class UpdateCookSessionCommandHandler(
    ICookSessionRepository sessions,
    IRecipeRepository recipes,
    TimeProvider time)
    : ICommandHandler<UpdateCookSessionCommand, Response>
{
    public async Task<Result<Response>> Handle(
        UpdateCookSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("CookSessions.Update");

        var found = await sessions
            .FindAsync(command.SessionId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            session => ApplyAsync(session, command, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> ApplyAsync(
        CookSession session,
        UpdateCookSessionCommand command,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var before = session.Version;

        // Rescaling is worth a version; a step advance is not. Doing both in
        // one call therefore takes the guarded path.
        var rescaled = command.Servings is { } servings
            ? session.Rescale(servings, now)
            : Result.Success();

        var moved = rescaled.Bind(() => command.CurrentStepIndex is { } index
            ? session.MoveTo(index, now)
            : Result.Success());

        var saved = await moved.Match(
            () => session.Version == before
                ? sessions.TouchAsync(session, cancellationToken)
                : sessions.UpdateAsync(session, before, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        var recipe = await recipes.FindAsync(session.RecipeId, cancellationToken)
            .ConfigureAwait(false);

        return saved.Bind(() => recipe).Map(found => session.ToResponse(found.Title.Value));
    }
}
