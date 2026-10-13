using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Domain.Suggestions;

namespace Application.Suggestions.RecordImpressions;

/// <summary>Notes what a suggestion set showed somebody.</summary>
/// <param name="HouseholdId">Whose kitchen the set was for.</param>
/// <param name="UserId">Who saw it.</param>
/// <param name="RecipeIds">What was on screen.</param>
public sealed record RecordImpressionsCommand(Guid HouseholdId, Guid UserId, IReadOnlyList<Guid> RecipeIds);

/// <summary>Remembers the shortlist, so the next days' lists lean away from it.</summary>
/// <remarks>
/// A write of its own, sent by the client once the set has painted, so <c>GET /suggestions</c> stays a read:
/// a response that never rendered was never shown.
/// </remarks>
internal sealed class RecordImpressionsCommandHandler(
    ISuggestionImpressions impressions,
    IHouseholdRepository households,
    TimeProvider time)
    : ICommandHandler<RecordImpressionsCommand>
{
    public async Task<Result> Handle(
        RecordImpressionsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Suggestions.RecordImpressions");

        if (command.RecipeIds.Count > SuggestionContext.MaxCount)
        {
            return tracked.Record(Result.Failure(SuggestionErrors.TooManyImpressions));
        }

        var member = await households
            .IsMemberAsync(command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!member)
        {
            // Not-found, not forbidden, as every household read gives a non-member.
            return tracked.Record(Result.Failure(HouseholdErrors.NotFound(command.HouseholdId)));
        }

        var library = await HouseholdAccess
            .LibraryAsync(households, command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        // The server's day, as the GET ranks by it: a client's clock would let a set be filed under a day it was not shown.
        await impressions
            .RecordAsync(
                command.UserId,
                command.RecipeIds,
                library,
                DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime),
                cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(Result.Success());
    }
}
