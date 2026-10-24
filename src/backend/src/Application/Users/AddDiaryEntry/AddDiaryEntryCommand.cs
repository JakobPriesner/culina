using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Diary;
using Domain.Shared;
using Response = Contracts.Users.AddDiaryEntry.Response;

namespace Application.Users.AddDiaryEntry;

/// <summary>Logs something a person ate, with figures the server looks up.</summary>
/// <param name="UserId">Who ate it.</param>
/// <param name="Date">The day.</param>
/// <param name="Meal">The meal's code.</param>
/// <param name="What">What it was.</param>
public sealed record AddDiaryEntryCommand(Guid UserId, DateOnly Date, string? Meal, DiaryWhat What);

internal sealed class AddDiaryEntryCommandHandler(IDiaryRepository diary, DiaryLookup lookup)
    : ICommandHandler<AddDiaryEntryCommand, Response>
{
    public async Task<Result<Response>> Handle(AddDiaryEntryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Users.AddDiaryEntry");

        if (DiaryCodes.ToMeal(command.Meal) is not { } meal)
        {
            return tracked.Record(Result<Response>.Failure(DiaryErrors.InvalidMeal));
        }

        var enabled = await DiaryAccess.EnabledAsync(diary, command.UserId, cancellationToken).ConfigureAwait(false);

        var result = await enabled.Match(
            async () =>
            {
                var logged = await lookup
                    .LogAsync(command.UserId, command.Date, meal, command.What, cancellationToken)
                    .ConfigureAwait(false);

                return await logged.Match(
                    async entry =>
                    {
                        await diary.AddAsync(entry, cancellationToken).ConfigureAwait(false);

                        return Result<Response>.Success(new Response { Entry = entry.ToDetail() });
                    },
                    error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
