using Domain.Cooking;
using Response = Contracts.CookSessions.Response;

namespace Application.CookSessions;

/// <summary>Maps a session onto the shape the API returns.</summary>
internal static class CookSessionMappings
{
    internal static Response ToResponse(this CookSession session, string recipeTitle)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new Response
        {
            SessionId = session.Id,
            RecipeId = session.RecipeId,
            RecipeTitle = recipeTitle,
            Servings = session.Servings,
            CurrentStepIndex = session.CurrentStepIndex,
            StartedAt = session.StartedAt,
            LastActiveAt = session.LastActiveAt,
            Version = session.Version
        };
    }
}
