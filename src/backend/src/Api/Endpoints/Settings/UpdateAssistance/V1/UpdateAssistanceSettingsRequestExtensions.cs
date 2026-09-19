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
            [.. request.Connections.Select(one =>
                new ConnectionEdit(one.Provider, one.ApiKey, one.BaseUrl))],
            [.. request.Uses.Select(one =>
                new UseEdit(one.Capability, one.Enabled, one.Provider, one.Model))],
            request.MonthlyBudget,
            request.PersonalBudget);
    }
}
