using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Assistance;
using Application.Telemetry;
using Contracts.Settings.GetAssistanceModels;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.Logging;
using Response = Contracts.Settings.GetAssistanceModels.Response;

namespace Application.Settings.GetAssistanceModels;

/// <summary>Reads what each connected provider currently offers.</summary>
public sealed record GetAssistanceModelsQuery;

internal sealed partial class GetAssistanceModelsQueryHandler(
    AssistanceSettings settings,
    IAssistants assistants,
    ISecretProtector protector,
    ILogger<GetAssistanceModelsQueryHandler> logger)
    : IQueryHandler<GetAssistanceModelsQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetAssistanceModelsQuery query,
        CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Settings.GetAssistanceModels");

        // Sequential: three calls on a deliberate screen, and a slow provider delays the answer instead of three simultaneous connections.
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
            models =>
            {
                // Counts, since an empty catalogue and one never asked look identical on screen.
                var drawing = models.Count(model => model.CanDraw);

                Listed(logger, kind.Code, models.Count, drawing);

                return new ProviderModelsContract
                {
                    Provider = kind.Code,
                    Reachable = true,
                    Models = [.. models.Select(model => new ModelContract
                    {
                        Id = model.Id,
                        Label = model.Label,
                        CanDraw = model.CanDraw
                    })]
                };
            },
            error =>
            {
                // Said twice: the screen tells the administrator now, the log is the only record that one provider stopped answering.
                CouldNotList(logger, kind.Code, error.Code);

                return new ProviderModelsContract
                {
                    Provider = kind.Code,
                    Reachable = false,
                    // The first place an administrator learns the pasted key does not work.
                    Problem = error.Code,
                    Models = []
                };
            });
    }

    [LoggerMessage(
        EventId = 1511,
        Level = LogLevel.Information,
        Message = "Provider {Provider} offers {Count} models, {Drawing} of which draw")]
    private static partial void Listed(ILogger logger, string provider, int count, int drawing);

    [LoggerMessage(
        EventId = 1510,
        Level = LogLevel.Warning,
        Message = "Provider {Provider} could not be asked what it offers: {Reason}")]
    private static partial void CouldNotList(ILogger logger, string provider, string reason);

    // Enough of a connection to ask what it offers. No model: listing needs none, and a default would block listing until one is chosen.
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
