using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes.GetNotes;
using Application.Telemetry;
using Domain.Cooking;
using Domain.Recipes;
using Domain.Shared;
using Response = Contracts.Recipes.GetNotes.Response;

namespace Application.Recipes.SaveNotes;

/// <summary>Replaces your notes on one recipe.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Whose notes.</param>
/// <param name="Overall">A note about the recipe as a whole, or null.</param>
/// <param name="Steps">Notes attached to steps.</param>
public sealed record SaveNotesCommand(
    Guid RecipeId,
    Guid UserId,
    string? Overall,
    IReadOnlyList<(Guid StepId, string Body)> Steps);

internal sealed class SaveNotesCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IPersonalNoteRepository notes,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<SaveNotesCommand, Response>
{
    public async Task<Result<Response>> Handle(
        SaveNotesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.SaveNotes");

        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var prepared = visible.Bind(recipe => Build(recipe, command));

        var result = await prepared.Match(
            written => SaveAsync(command, written, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>
    /// Builds the notes to keep. A blank body is a deletion, not a stored empty
    /// string, so the panel does not show a box nobody asked for.
    /// </summary>
    private Result<IReadOnlyList<PersonalNote>> Build(Recipe recipe, SaveNotesCommand command)
    {
        var now = time.GetUtcNow();
        var stepIds = recipe.Steps.Select(step => step.Id).ToHashSet();

        if (command.Steps.Any(note => !stepIds.Contains(note.StepId)))
        {
            return CookingErrors.UnknownStep;
        }

        var written = command.Steps
            .Where(note => !string.IsNullOrWhiteSpace(note.Body))
            .Select(note => PersonalNote.Write(command.RecipeId, command.UserId, note.StepId, note.Body, now))
            .ToList();

        if (!string.IsNullOrWhiteSpace(command.Overall))
        {
            written.Insert(
                0,
                PersonalNote.Write(command.RecipeId, command.UserId, null, command.Overall, now));
        }

        return written.Collect();
    }

    private async Task<Result<Response>> SaveAsync(
        SaveNotesCommand command,
        IReadOnlyList<PersonalNote> written,
        CancellationToken cancellationToken) =>
        await unitOfWork.InTransactionAsync(
            async token =>
            {
                var saved = await notes
                    .ReplaceAsync(command.RecipeId, command.UserId, written, token)
                    .ConfigureAwait(false);

                return saved.Map(() => written.ToResponse());
            },
            cancellationToken).ConfigureAwait(false);
}
