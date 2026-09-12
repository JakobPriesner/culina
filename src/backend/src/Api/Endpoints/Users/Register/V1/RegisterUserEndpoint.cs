using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.Register;
using Request = Contracts.Users.Register.Request;
using Response = Contracts.Users.Register.Response;

namespace Api.Endpoints.Users.Register.V1;

/// <summary>Creates an account.</summary>
internal sealed class RegisterUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/users", async (
                Request request,
                // Injected per delegate parameter, not into the constructor:
                // the endpoint is a singleton and the handler is scoped.
                ICommandHandler<RegisterUserCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(request.ToCommand(), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    created => Results.Created($"{ApiPaths.V1}/users/{created.UserId}", created),
                    CustomResults.Problem);
            })
            .WithName("registerUserV1")
            .WithTags(Tags.Users)
            .WithSummary("Create an account")
            .WithDescription(
                "Creates an account. The first account on an instance always succeeds and becomes "
                + "the administrator, with a household of its own; afterwards the instance's "
                + "registration settings decide.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitExtensions.Register);
    }
}
