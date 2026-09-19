using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Settings.GetAssistance.Response;

namespace Application.Settings.GetAssistance;

/// <summary>Reads how the assistant is set up.</summary>
public sealed record GetAssistanceSettingsQuery;

internal sealed class GetAssistanceSettingsQueryHandler(AssistanceSettings settings)
    : IQueryHandler<GetAssistanceSettingsQuery, Response>
{
    public Task<Result<Response>> Handle(
        GetAssistanceSettingsQuery query,
        CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Settings.GetAssistance");

        // Straight from the singleton, like the registration policy: it is the
        // live value every other handler sees. The key is the one field that
        // does not come back — only whether there is one.
        return Task.FromResult(tracked.Record(Result<Response>.Success(new Response
        {
            Enabled = settings.Enabled,
            Provider = settings.Provider,
            ApiKeyConfigured = settings.HasApiKey,
            Connected = settings.IsConnected,
            BaseUrl = settings.BaseUrl,
            ComposeModel = settings.ComposeModel,
            DrawModel = settings.DrawModel,
            ImproveEnabled = settings.ImproveEnabled,
            DraftEnabled = settings.DraftEnabled,
            ReadEnabled = settings.ReadEnabled,
            DrawEnabled = settings.DrawEnabled,
            MonthlyBudget = settings.MonthlyBudget,
            PersonalBudget = settings.PersonalBudget
        })));
    }
}
