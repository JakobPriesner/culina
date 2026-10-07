using Api.Endpoints.RecipeDrafts.FromMedia.V1;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Application.Recipes.Drafts;
using Application.Recipes.Intake;
using Contracts.Recipes.Intake;
using Domain.Assistance;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.RecipeDrafts.Intake.V1;

internal sealed class RecipeIntakeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost($"{ApiPaths.V1}/recipe-intakes", async (
            [FromForm] IFormFileCollection photos, [FromForm] string? material, [FromForm] string? transcript, [FromForm] string? sourceUrl, [FromForm] bool? fetchSource,
            Guid id, Guid householdId, string? language, HttpContext context, StorageSettings storage, RecipeIntake intake, CancellationToken token) =>
        {
            if (photos.Count > DraftLimits.MaxPhotos || photos.Sum(p => p.Length) > DraftLimits.MaxPhotoBytes || photos.Any(p => p.Length == 0 || p.Length > storage.MaxImageBytes))
            {
                return CustomResults.Problem(AssistanceErrors.TooMuchToWorkFrom);
            }

            List<IntakePhoto> images = [];
            foreach (var photo in photos)
            {
                using var stream = photo.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, token).ConfigureAwait(false);
                var bytes = buffer.ToArray();
                var type = ReadMediaRecipeDraftEndpoint.MediaType(bytes);
                if (type is null)
                {
                    return CustomResults.Problem(AssistanceErrors.NothingToWorkFrom);
                }

                images.Add(new IntakePhoto(bytes, type));
            }
            var result = await intake.StartAsync(id, context.CurrentUser().UserId, householdId,
                new IntakeMaterial(material ?? "", transcript ?? "", sourceUrl, language ?? "en", images, fetchSource ?? false), token).ConfigureAwait(false);
            return result.Match(job => Results.Accepted($"{ApiPaths.V1}/recipe-intakes/{job.Id}", job), CustomResults.Problem);
        }).WithQueryParameters("id", "householdId", "language").WithName("startRecipeIntakeV1").WithTags(Tags.Recipes).Produces<IntakeJob>(202)
            .WithMetadata(new RequestSizeLimitAttribute(42 * 1024 * 1024)).DisableAntiforgery()
            .RequireAuthorization().RequireRateLimiting(RateLimitExtensions.Assistance);

        app.MapPost($"{ApiPaths.V1}/recipe-intakes/{{id:guid}}/retry", async (Guid id, Guid nextId, HttpContext context, IRecipeIntakeJobs jobs, RecipeIntake intake, CancellationToken token) =>
        {
            var userId = context.CurrentUser().UserId;
            var already = await jobs.GetAsync(nextId, userId, token).ConfigureAwait(false);
            if (already is not null)
            {
                return Results.Accepted($"{ApiPaths.V1}/recipe-intakes/{already.Id}", already);
            }

            var job = await jobs.GetAsync(id, userId, token).ConfigureAwait(false);
            if (job is null || job.Stage != "failed")
            {
                return Results.NotFound();
            }

            var original = await jobs.MaterialAsync(id, userId, token).ConfigureAwait(false);
            if (original is null)
            {
                return Results.NotFound();
            }

            var result = await intake.StartAsync(nextId, userId, job.HouseholdId, original, token).ConfigureAwait(false);
            return await result.Match(async next =>
            {
                await jobs.ReviewAsync(id, userId, token).ConfigureAwait(false);
                return (IResult)Results.Accepted($"{ApiPaths.V1}/recipe-intakes/{next.Id}", next);
            }, error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
        }).WithQueryParameters("nextId").WithName("retryRecipeIntakeV1").WithTags(Tags.Recipes).Produces<IntakeJob>(202).RequireAuthorization().RequireRateLimiting(RateLimitExtensions.Assistance);

        app.MapGet($"{ApiPaths.V1}/recipe-intakes", async (HttpContext context, IRecipeIntakeJobs jobs, CancellationToken token) =>
            Results.Ok(await jobs.ListAsync(context.CurrentUser().UserId, token).ConfigureAwait(false)))
            .WithName("listRecipeIntakesV1").WithTags(Tags.Recipes).Produces<IReadOnlyList<IntakeJob>>().RequireAuthorization();
        app.MapGet($"{ApiPaths.V1}/recipe-intakes/{{id:guid}}", async (Guid id, HttpContext context, IRecipeIntakeJobs jobs, CancellationToken token) =>
        {
            var job = await jobs.GetAsync(id, context.CurrentUser().UserId, token).ConfigureAwait(false);
            return job is null ? Results.NotFound() : Results.Ok(job);
        }).WithName("getRecipeIntakeV1").WithTags(Tags.Recipes).Produces<IntakeJob>().RequireAuthorization();
        app.MapGet($"{ApiPaths.V1}/recipe-intakes/{{id:guid}}/photos/{{index:int}}", async (Guid id, int index, HttpContext context, IRecipeIntakeJobs jobs, CancellationToken token) =>
        {
            var material = await jobs.MaterialAsync(id, context.CurrentUser().UserId, token).ConfigureAwait(false);
            if (material is null || index < 0 || index >= material.Photos.Count)
            {
                return Results.NotFound();
            }

            var photo = material.Photos[index];
            context.Response.Headers.CacheControl = "no-store";
            return Results.Bytes(photo.Bytes, photo.MediaType);
        }).WithName("getRecipeIntakePhotoV1").WithTags(Tags.Recipes).RequireAuthorization();
        app.MapPost($"{ApiPaths.V1}/recipe-intakes/{{id:guid}}/reviewed", async (Guid id, HttpContext context, IRecipeIntakeJobs jobs, CancellationToken token) =>
        {
            await jobs.ReviewAsync(id, context.CurrentUser().UserId, token).ConfigureAwait(false);
            return Results.NoContent();
        }).WithName("reviewRecipeIntakeV1").WithTags(Tags.Recipes).RequireAuthorization();
        app.MapGet($"{ApiPaths.V1}/push/key", async (IIntakeNotifications push, CancellationToken token) =>
            Results.Ok(new PushKey(await push.PublicKeyAsync(token).ConfigureAwait(false))))
            .WithName("getPushKeyV1").WithTags(Tags.Recipes).Produces<PushKey>().RequireAuthorization();
        app.MapPut($"{ApiPaths.V1}/push/subscription", async (PushRegistration subscription, HttpContext context, IIntakeNotifications push, CancellationToken token) =>
        {
            var result = await push.RegisterAsync(context.CurrentUser().UserId, subscription, token).ConfigureAwait(false);
            return result.Match(Results.NoContent, CustomResults.Problem);
        }).WithName("registerPushV1").WithTags(Tags.Recipes).RequireAuthorization();
        app.MapDelete($"{ApiPaths.V1}/push/subscription", async (string endpoint, HttpContext context, IIntakeNotifications push, CancellationToken token) =>
        {
            await push.RemoveAsync(context.CurrentUser().UserId, endpoint, token).ConfigureAwait(false);
            return Results.NoContent();
        }).WithQueryParameters("endpoint").WithName("removePushV1").WithTags(Tags.Recipes).RequireAuthorization();
    }
}
