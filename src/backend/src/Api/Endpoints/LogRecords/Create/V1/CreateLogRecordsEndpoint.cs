using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.LogRecords.Create;
using Request = Contracts.LogRecords.Create.Request;

namespace Api.Endpoints.LogRecords.Create.V1;

/// <summary>Takes what the web app noticed about itself into the server's log.</summary>
internal sealed class CreateLogRecordsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/log-records", async (
                Request request,
                HttpContext context,
                ICommandHandler<CreateLogRecordsCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(request.ToCommand(context), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(() => Results.Accepted(), CustomResults.Problem);
            })
            .WithName("createLogRecordsV1")
            .WithTags(Tags.LogRecords)
            .WithSummary("Report from the web app")
            .WithDescription(
                "Writes errors and policy violations the web app noticed into the server's log, which "
                + "exports them with everything else when a collector is configured. Up to ten records "
                + "per request. Signed in or not.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitExtensions.LogRecords);
    }
}
