using Application.Recipes.SaveNotes;
using Request = Contracts.Recipes.SaveNotes.Request;

namespace Api.Endpoints.Recipes.SaveNotes.V1;

/// <summary>Turns the request body into the command the handler accepts.</summary>
internal static class SaveNotesRequestExtensions
{
    internal static SaveNotesCommand ToCommand(this Request request, Guid recipeId, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SaveNotesCommand(
            recipeId,
            userId,
            request.Overall,
            [.. request.Steps.Select(note => (note.StepId, note.Body))]);
    }
}
