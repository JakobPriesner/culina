using Application.Users.UpdatePreferences;
using Request = Contracts.Users.UpdatePreferences.Request;

namespace Api.Endpoints.Users.UpdatePreferences.V1;

/// <summary>Turns the request body into the command the handler accepts.</summary>
internal static class UpdatePreferencesRequestExtensions
{
    internal static UpdatePreferencesCommand ToCommand(this Request request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdatePreferencesCommand(
            userId,
            request.Locale,
            request.Theme,
            request.Mode,
            request.MeasurementSystem);
    }
}
