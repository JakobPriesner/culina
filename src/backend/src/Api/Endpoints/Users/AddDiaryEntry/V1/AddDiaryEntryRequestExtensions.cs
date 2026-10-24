using Application.Users;
using Application.Users.AddDiaryEntry;
using Request = Contracts.Users.AddDiaryEntry.Request;

namespace Api.Endpoints.Users.AddDiaryEntry.V1;

/// <summary>Turns the request body into the command the handler accepts.</summary>
internal static class AddDiaryEntryRequestExtensions
{
    internal static AddDiaryEntryCommand ToCommand(this Request request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AddDiaryEntryCommand(
            userId,
            request.Date,
            request.Meal,
            new DiaryWhat(
                request.RecipeId,
                request.Portions,
                request.HouseholdId,
                request.CookLogEntryId,
                request.FoodCode,
                request.Amount,
                request.Unit,
                request.Label,
                request.Kcal,
                request.Protein,
                request.Carbohydrate,
                request.Fat));
    }
}
