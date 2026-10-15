using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Intake;
using Contracts.Recipes.Intake;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.RecipeDrafts.StartIntake.V1;

/// <summary>Accepts text, a link or photographs as a durable import.</summary>
internal sealed class StartRecipeIntakeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-intakes", async (
                [FromForm] IFormFileCollection photos,
                [FromForm] string? material,
                [FromForm] string? transcript,
                [FromForm] string? sourceUrl,
                [FromForm] bool? fetchSource,
                Guid id,
                Guid householdId,
                string? language,
                HttpContext context,
                ICommandHandler<StartRecipeIntakeCommand, IntakeJob> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new StartRecipeIntakeCommand(
                            id,
                            context.CurrentUser().UserId,
                            householdId,
                            material,
                            transcript,
                            sourceUrl,
                            language,
                            fetchSource ?? false,
                            [.. photos.Select(photo => new PhotoUpload(photo.Length, photo.OpenReadStream))]),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(job => Results.Accepted($"{ApiPaths.V1}/recipe-intakes/{job.Id}", job), CustomResults.Problem);
            })
            .WithQueryParameters("id", "householdId", "language")
            .WithName("startRecipeIntakeV1")
            .WithTags(Tags.Recipes)
            .Produces<IntakeJob>(202)
            .WithMetadata(new RequestSizeLimitAttribute(42 * 1024 * 1024))
            .DisableAntiforgery()
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitExtensions.Assistance);
    }
}
