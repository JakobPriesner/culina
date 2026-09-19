using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.UpdateAssistance;
using Request = Contracts.Settings.UpdateAssistance.Request;
using Response = Contracts.Settings.UpdateAssistance.Response;

namespace Api.Endpoints.Settings.UpdateAssistance.V1;

/// <summary>Changes how the assistant is set up.</summary>
internal sealed class UpdateAssistanceSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/settings/assistance", async (
                Request request,
                ICommandHandler<UpdateAssistanceSettingsCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(request.ToCommand(), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("updateAssistanceSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Change how the assistant is set up")
            .WithDescription(
                "Instance administrator only. Takes effect on the next request, with no "
                + "restart.\n\n"
                + "`apiKey` has three states, and the difference matters: omit it to keep the "
                + "key already stored, send an empty string to remove it, and send a value to "
                + "replace it. It is never returned by any endpoint, so a form has nothing to "
                + "put in the box and omitting it is what saving an unrelated change looks "
                + "like.\n\n"
                + "Turning the assistant on without a key stores it as off — there is nothing "
                + "for it to be on with.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
