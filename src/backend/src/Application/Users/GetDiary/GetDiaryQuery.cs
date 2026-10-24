using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Diary;
using Domain.Shared;
using Response = Contracts.Users.GetDiary.Response;

namespace Application.Users.GetDiary;

/// <summary>Reads a range of a person's diary, every day in it, logged or not.</summary>
/// <param name="UserId">Whose.</param>
/// <param name="From">The first day.</param>
/// <param name="To">The last day, inclusive; the first when left out.</param>
public sealed record GetDiaryQuery(Guid UserId, DateOnly From, DateOnly? To);

internal sealed class GetDiaryQueryHandler(IDiaryRepository diary) : IQueryHandler<GetDiaryQuery, Response>
{
    public async Task<Result<Response>> Handle(GetDiaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Users.GetDiary");

        var to = query.To ?? query.From;

        if (!DiaryDates.IsWithin(query.From) || !DiaryDates.IsWithin(to) || to < query.From
            || to.DayNumber - query.From.DayNumber >= DiaryDates.MaxRangeDays)
        {
            return tracked.Record(Result<Response>.Failure(DiaryErrors.InvalidDates));
        }

        var enabled = await DiaryAccess.EnabledAsync(diary, query.UserId, cancellationToken).ConfigureAwait(false);

        var result = await enabled.Match(
            async () =>
            {
                var entries = await diary
                    .EntriesAsync(query.UserId, query.From, to, cancellationToken)
                    .ConfigureAwait(false);
                var byDay = entries.ToLookup(entry => entry.Date);

                return Result<Response>.Success(new Response
                {
                    Days =
                    [
                        .. Enumerable
                            .Range(query.From.DayNumber, to.DayNumber - query.From.DayNumber + 1)
                            .Select(DateOnly.FromDayNumber)
                            .Select(day => DiaryMappings.ToDay(day, [.. byDay[day]]))
                    ]
                });
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
