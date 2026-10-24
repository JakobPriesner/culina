using Application.Users.UpdateDiarySettings;
using Request = Contracts.Users.UpdateDiarySettings.Request;

namespace Api.Endpoints.Users.UpdateDiarySettings.V1;

/// <summary>Turns the request body into the command the handler accepts.</summary>
internal static class UpdateDiarySettingsRequestExtensions
{
    internal static UpdateDiarySettingsCommand ToCommand(this Request request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateDiarySettingsCommand(
            userId,
            request.Enabled,
            request.ConsentVersion,
            request.KcalTarget,
            request.ProteinTarget,
            request.Headline,
            request.EraseEntries ?? false);
    }
}
