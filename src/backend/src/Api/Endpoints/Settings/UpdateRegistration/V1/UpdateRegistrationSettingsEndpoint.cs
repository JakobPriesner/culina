using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.UpdateRegistration;
using Request = Contracts.Settings.UpdateRegistration.Request;
using Response = Contracts.Settings.UpdateRegistration.Response;

namespace Api.Endpoints.Settings.UpdateRegistration.V1;

/// <summary>Changes the registration policy.</summary>
internal sealed class UpdateRegistrationSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/settings/registration", async (
                Request request,
                ICommandHandler<UpdateRegistrationSettingsCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new UpdateRegistrationSettingsCommand(
                            request.OpenRegistration,
                            request.RequireInvitation,
                            request.MaxUsers),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("updateRegistrationSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Change the registration policy")
            .WithDescription(
                "Instance administrator only. Takes effect on the next registration, with no "
                + "restart.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
