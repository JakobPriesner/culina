using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Diary;
using Domain.Shared;
using Response = Contracts.Users.UpdateDiaryEntry.Response;

namespace Application.Users.UpdateDiaryEntry;

/// <summary>Moves an entry to another day or meal, logs another amount of the same thing, or corrects own figures.</summary>
/// <param name="UserId">Whose.</param>
/// <param name="EntryId">Which entry.</param>
/// <param name="Date">Another day, or null.</param>
/// <param name="Meal">Another meal's code, or null.</param>
/// <param name="Portions">Another number of portions of the recipe, or null.</param>
/// <param name="Amount">Another amount of the food, or null.</param>
/// <param name="Unit">Another unit for the food, or null.</param>
/// <param name="Label">Own figures: another name, or null.</param>
/// <param name="Kcal">Own figures: other kilocalories, or null.</param>
/// <param name="Protein">Own figures: protein, or null for unknown.</param>
/// <param name="Carbohydrate">Own figures: carbohydrate, or null for unknown.</param>
/// <param name="Fat">Own figures: fat, or null for unknown.</param>
public sealed record UpdateDiaryEntryCommand(
    Guid UserId,
    Guid EntryId,
    DateOnly? Date,
    string? Meal,
    decimal? Portions,
    decimal? Amount,
    string? Unit,
    string? Label = null,
    decimal? Kcal = null,
    decimal? Protein = null,
    decimal? Carbohydrate = null,
    decimal? Fat = null)
{
    /// <summary>Whether the change carries own figures, which replace an own entry's figures as a whole.</summary>
    public bool Retypes => Label is not null || Kcal is not null || Protein is not null || Carbohydrate is not null || Fat is not null;
}

internal sealed class UpdateDiaryEntryCommandHandler(IDiaryRepository diary, DiaryLookup lookup)
    : ICommandHandler<UpdateDiaryEntryCommand, Response>
{
    public async Task<Result<Response>> Handle(UpdateDiaryEntryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Users.UpdateDiaryEntry");

        var meal = command.Meal is null ? (DiaryMeal?)null : DiaryCodes.ToMeal(command.Meal);

        if (command.Meal is not null && meal is null)
        {
            return tracked.Record(Result<Response>.Failure(DiaryErrors.InvalidMeal));
        }

        var enabled = await DiaryAccess.EnabledAsync(diary, command.UserId, cancellationToken).ConfigureAwait(false);
        var found = await enabled.Match(
            () => diary.FindAsync(command.UserId, command.EntryId, cancellationToken),
            error => Task.FromResult(Result<DiaryEntry>.Failure(error))).ConfigureAwait(false);

        var result = await found.Match(
            async entry =>
            {
                var changed = await ChangeAsync(entry, command, meal ?? entry.Meal, cancellationToken).ConfigureAwait(false);

                return await changed.Match(
                    async updated =>
                    {
                        var stored = await diary.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);

                        return stored.Map(() => new Response { Entry = updated.ToDetail() });
                    },
                    error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    // A new amount is looked up afresh, exactly as a new entry would be; own figures are checked as new ones would be;
    // a new day or meal keeps the figures as logged.
    private async Task<Result<DiaryEntry>> ChangeAsync(
        DiaryEntry entry,
        UpdateDiaryEntryCommand command,
        DiaryMeal meal,
        CancellationToken cancellationToken)
    {
        var date = command.Date ?? entry.Date;
        var amountChanged = command.Portions is not null || command.Amount is not null || command.Unit is not null;

        if (command.Retypes)
        {
            // Figures are the server's to look up for anything but the person's own.
            if (entry.Source != DiarySource.Typed || amountChanged)
            {
                return DiaryErrors.UnclearSource;
            }

            var retyped = new DiaryWhat(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                command.Label ?? entry.Label,
                command.Kcal ?? entry.Figures.Kcal,
                command.Protein,
                command.Carbohydrate,
                command.Fat);

            var typed = await lookup.LogAsync(entry.UserId, date, meal, retyped, cancellationToken).ConfigureAwait(false);

            return typed.Map(fresh => entry.Replacing(fresh).MovedTo(date, meal));
        }

        if (!amountChanged)
        {
            return DiaryDates.IsWithin(date) ? entry.MovedTo(date, meal) : DiaryErrors.InvalidDates;
        }

        if (entry.Source == DiarySource.Typed)
        {
            return DiaryErrors.InvalidAmount;
        }

        var what = new DiaryWhat(
            entry.Link.RecipeId,
            command.Portions ?? (entry.Source == DiarySource.Recipe ? entry.Amount.Value : null),
            entry.Link.HouseholdId,
            entry.Link.CookLogEntryId,
            entry.Link.FoodCode,
            command.Amount ?? (entry.Source == DiarySource.Food ? entry.Amount.Value : null),
            command.Unit ?? (entry.Source == DiarySource.Food ? entry.Amount.Unit : null),
            null,
            null,
            null,
            null,
            null);

        var relogged = await lookup.LogAsync(entry.UserId, date, meal, what, cancellationToken).ConfigureAwait(false);

        return relogged.Map(fresh => entry.Replacing(fresh).MovedTo(date, meal));
    }
}
