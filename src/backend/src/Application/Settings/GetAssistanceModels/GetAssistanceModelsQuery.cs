using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Assistance;
using Application.Telemetry;
using Contracts.Settings.GetAssistanceModels;
using Domain.Assistance;
using Domain.Shared;
using Response = Contracts.Settings.GetAssistanceModels.Response;

namespace Application.Settings.GetAssistanceModels;

/// <summary>Reads what each connected provider currently offers.</summary>
public sealed record GetAssistanceModelsQuery;

internal sealed class GetAssistanceModelsQueryHandler(
    AssistanceSettings settings,
    IAssistants assistants,
    ISecretProtector protector)
    : IQueryHandler<GetAssistanceModelsQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetAssistanceModelsQuery query,
        CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Settings.GetAssistanceModels");

        // Sequential rather than in parallel. Three calls on a screen somebody
        // opened deliberately is not worth the concurrency, and doing them one
        // at a time means a slow provider delays the answer rather than three
        // simultaneous connections to three companies.
        List<ProviderModelsContract> listed = [];

        foreach (var kind in AssistantKind.All.Where(kind =>
            settings.ConnectionFor(kind) is { IsUsable: true }))
        {
            listed.Add(await AskAsync(kind, cancellationToken).ConfigureAwait(false));
        }

        return tracked.Record(Result<Response>.Success(new Response { Providers = listed }));
    }

    private async Task<ProviderModelsContract> AskAsync(
        AssistantKind kind,
        CancellationToken cancellationToken)
    {
        var resolved = Resolve(kind);

        var listed = await resolved.Match(
            connected => assistants.For(kind).Match(
                assistant => assistant.ListModelsAsync(connected, cancellationToken),
                error => Task.FromResult(Result<IReadOnlyList<ModelInfo>>.Failure(error))),
            error => Task.FromResult(Result<IReadOnlyList<ModelInfo>>.Failure(error)))
            .ConfigureAwait(false);

        return listed.Match(
            models => new ProviderModelsContract
            {
                Provider = kind.Code,
                Reachable = true,
                Models = [.. models.Select(model => new ModelContract
                {
                    Id = model.Id,
                    Label = model.Label,
                    CanDraw = model.CanDraw
                })]
            },
            error => new ProviderModelsContract
            {
                Provider = kind.Code,
                Reachable = false,
                // The first place an administrator finds out that the key they
                // pasted does not work, so the reason is worth carrying.
                Problem = error.Code,
                Models = []
            });
    }

    /// <summary>
    /// Enough of a connection to ask what it offers.
    /// </summary>
    /// <remarks>
    /// No model on it: listing is the one call that does not need one, and
    /// putting a resolved default here would mean the screen could not list
    /// models until a model had been chosen.
    /// </remarks>
    private Result<Connected> Resolve(AssistantKind kind)
    {
        if (settings.ConnectionFor(kind) is not { IsUsable: true } connection)
        {
            return AssistanceErrors.NotConfigured;
        }

        var key = kind.NeedsApiKey ? protector.Unprotect(connection.ProtectedApiKey) : string.Empty;

        return key is null
            ? AssistanceErrors.NotConfigured
            : new Connected(
                key,
                connection.BaseUrl.Length > 0 ? connection.BaseUrl : assistants.HomeOf(kind),
                Model: string.Empty);
    }
}
