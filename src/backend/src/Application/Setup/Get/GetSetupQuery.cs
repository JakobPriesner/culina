using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Setup.Get.Response;

namespace Application.Setup.Get;

/// <summary>Reads how far this instance has got in being set up.</summary>
public sealed record GetSetupQuery;

internal sealed class GetSetupQueryHandler(ISetupProgress progress, IHostRestart host)
    : IQueryHandler<GetSetupQuery, Response>
{
    public async Task<Result<Response>> Handle(GetSetupQuery query, CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Setup.Get");

        var stage = await progress.CurrentAsync(cancellationToken).ConfigureAwait(false);

        return tracked.Record(Result<Response>.Success(new Response
        {
            Stage = stage switch
            {
                SetupStage.Database => "database",
                SetupStage.Account => "account",
                _ => "complete"
            },
            StartedAt = host.StartedAt
        }));
    }
}
