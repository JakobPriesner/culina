using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Contracts.Settings.GetAssistance;
using Domain.Assistance;
using Domain.Shared;
using Response = Contracts.Settings.GetAssistance.Response;

namespace Application.Settings.GetAssistance;

/// <summary>Reads how the assistant is set up.</summary>
public sealed record GetAssistanceSettingsQuery;

internal sealed class GetAssistanceSettingsQueryHandler(
    AssistanceSettings settings,
    IAssistants assistants)
    : IQueryHandler<GetAssistanceSettingsQuery, Response>
{
    public Task<Result<Response>> Handle(
        GetAssistanceSettingsQuery query,
        CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Settings.GetAssistance");

        // Straight from the live singleton; the keys never come back, only whether each connection has one.
        return Task.FromResult(tracked.Record(Result<Response>.Success(new Response
        {
            Enabled = settings.Enabled,
            Connections = [.. Every()],
            Uses = [.. Jobs()],
            MonthlyBudget = settings.MonthlyBudget,
            PersonalBudget = settings.PersonalBudget
        })));
    }

    // Every provider this build knows, connected or not, so an unconnected one is an empty row to fill in.
    private IEnumerable<ConnectionContract> Every() =>
        AssistantKind.All.Select(kind =>
        {
            var connection = settings.ConnectionFor(kind);

            return new ConnectionContract
            {
                Provider = kind.Code,
                ApiKeyConfigured = connection?.HasApiKey ?? false,
                BaseUrl = connection?.BaseUrl ?? string.Empty,
                Usable = connection?.IsUsable ?? false
            };
        });

    // Every job, chosen or not, with the model it would fall back to: the server decides the default, not the form.
    private IEnumerable<UseContract> Jobs() =>
        Capability.All.Select(capability =>
        {
            var use = settings.UseFor(capability);
            var kind = AssistantKind.Parse(use?.Provider);

            return new UseContract
            {
                Capability = capability.Code,
                Enabled = use?.Enabled ?? false,
                Provider = use?.Provider ?? string.Empty,
                Model = use?.Model ?? string.Empty,
                DefaultModel = kind is null
                    ? string.Empty
                    : assistants.DefaultModelFor(kind, capability)
            };
        });
}
