using Application.Users.UpdateDiaryEntry;
using Request = Contracts.Users.UpdateDiaryEntry.Request;

namespace Api.Endpoints.Users.UpdateDiaryEntry.V1;

/// <summary>Turns the request body into the command the handler accepts.</summary>
internal static class UpdateDiaryEntryRequestExtensions
{
    internal static UpdateDiaryEntryCommand ToCommand(this Request request, Guid userId, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateDiaryEntryCommand(
            userId,
            entryId,
            request.Date,
            request.Meal,
            request.Portions,
            request.Amount,
            request.Unit,
            request.Label,
            request.Kcal,
            request.Protein,
            request.Carbohydrate,
            request.Fat);
    }
}
