using Application.Settings.UpdateAssistance;
using Request = Contracts.Settings.UpdateAssistance.Request;

namespace Api.Endpoints.Settings.UpdateAssistance.V1;

/// <summary>Turns the request into the command it stands for.</summary>
internal static class UpdateAssistanceSettingsRequestExtensions
{
    internal static UpdateAssistanceSettingsCommand ToCommand(this Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateAssistanceSettingsCommand(
            request.Enabled,
            request.Provider,
            request.ApiKey,
            request.BaseUrl,
            request.ComposeModel,
            request.DrawModel,
            request.ImproveEnabled,
            request.DraftEnabled,
            request.ReadEnabled,
            request.DrawEnabled,
            request.MonthlyBudget,
            request.PersonalBudget);
    }
}
