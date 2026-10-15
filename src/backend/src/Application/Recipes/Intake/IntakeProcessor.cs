using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes.Drafts;
using Application.Recipes.Import;
using Domain.Shared;
using Draft = Contracts.Recipes.Drafts.Response;

namespace Application.Recipes.Intake;

/// <summary>Runs one claimed job: reads the shared page, has the model write the draft, then saves it as a recipe or leaves it for correction.</summary>
public sealed class IntakeProcessor(
    IRecipeIntakeJobs jobs,
    ICommandHandler<ComposeRecipeDraftCommand, DraftProgress> composer,
    IQueryHandler<ImportRecipeQuery, Contracts.Recipes.Import.Response> reader,
    DraftToRecipe recipes,
    TimeProvider time)
{
    /// <summary>How often a growing draft is stored: every part rewrites the whole draft.</summary>
    private static readonly TimeSpan ProgressEvery = TimeSpan.FromMilliseconds(500);

    /// <summary>Runs on server lifetime, never a request cancellation token.</summary>
    public async Task ProcessAsync(IntakeWork work, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(work);

        var draft = work.Draft;

        if (draft is null && work.Material.FetchSource && work.Material.SourceUrl is { } sourceUrl)
        {
            if (await FetchSourceAsync(work, sourceUrl, token).ConfigureAwait(false) is not { } fetched)
            {
                return;
            }

            work = fetched;
        }

        if (draft is null)
        {
            draft = await ComposeAsync(work, token).ConfigureAwait(false);

            if (draft is null)
            {
                return;
            }
        }

        // A composer draft stops here: the person corrects it and saves it themselves.
        if (work.Material.IsDraft)
        {
            await jobs.DraftedAsync(work.Id, token).ConfigureAwait(false);

            return;
        }

        await recipes.SaveAsync(work, draft, token).ConfigureAwait(false);
    }

    /// <summary>Adds the shared page's text to what the person wrote; null once the job has failed.</summary>
    private async Task<IntakeWork?> FetchSourceAsync(IntakeWork work, string sourceUrl, CancellationToken token)
    {
        await jobs.ProgressAsync(work.Id, JobStage.Reading, null, token).ConfigureAwait(false);

        var fetched = await reader.Handle(new ImportRecipeQuery(sourceUrl, work.UserId), token).ConfigureAwait(false);
        var (source, sourceError) = fetched.Match(
            page => (Material: WithPage(work.Material, page), Error: (string?)null),
            error => (Material: work.Material, Error: error.Code));

        if (sourceError is not null && string.IsNullOrWhiteSpace(source.Text) && string.IsNullOrWhiteSpace(source.Transcript) && source.Photos.Count == 0)
        {
            await jobs.FailAsync(work.Id, sourceError, token).ConfigureAwait(false);

            return null;
        }

        source = source with { FetchSource = false };
        await jobs.SourceAsync(work.Id, source, token).ConfigureAwait(false);

        return work with { Material = source };
    }

    private static IntakeMaterial WithPage(IntakeMaterial source, Contracts.Recipes.Import.Response page)
    {
        var pageText = page.Text ?? string.Join("\n", new[] { page.Title }.Concat(page.IngredientLines).Concat(page.Steps).Where(line => !string.IsNullOrWhiteSpace(line)));
        var material = string.Join("\n\n", new[] { source.Text, pageText }.Where(text => !string.IsNullOrWhiteSpace(text)).Distinct(StringComparer.Ordinal));
        var transcript = source.Transcript.Length > 0 ? source.Transcript : page.Transcript ?? "";

        // The person's own words win; fetched captions use the remaining space.
        var limit = DraftLimits.MaxMaterialCharacters;
        var textBudget = limit - Math.Min(source.Transcript.Length, limit);
        material = material[..Math.Min(material.Length, textBudget)];
        transcript = transcript[..Math.Min(transcript.Length, limit - material.Length)];

        return source with { Text = material, Transcript = transcript, SourceUrl = page.SourceUrl, FetchSource = false };
    }

    /// <summary>Has the model write the recipe, storing it as it grows; null once the job has failed.</summary>
    private async Task<Draft?> ComposeAsync(IntakeWork work, CancellationToken token)
    {
        await jobs.ProgressAsync(work.Id, JobStage.Reading, null, token).ConfigureAwait(false);

        var material = work.Material;
        var opened = await composer.Handle(new ComposeRecipeDraftCommand(material.DraftKind ?? IntakeKinds.Social, work.HouseholdId,
            material.Text, material.RecipeId, material.Language, work.UserId)
        {
            Transcript = material.Transcript,
            Pictures = material.Photos.Select(p => new RecipePicture(p.Bytes, p.MediaType)).ToArray()
        }, token).ConfigureAwait(false);

        var (progress, failure) = opened.Match(
            value => (Progress: (DraftProgress?)value, Failure: (string?)null),
            error => (Progress: null, Failure: error.Code));

        if (progress is null)
        {
            await jobs.FailAsync(work.Id, failure ?? IntakeErrors.Interrupted, token).ConfigureAwait(false);

            return null;
        }

        // Each write also asks whether it is still wanted: a draft dismissed meanwhile stops, and its stream with it.
        if (!await jobs.ProgressAsync(work.Id, JobStage.Thinking, null, token).ConfigureAwait(false))
        {
            return null;
        }

        Draft? draft = null;
        var finished = false;
        var lastStored = time.GetTimestamp();

        await foreach (var part in progress.Events.WithCancellation(token).ConfigureAwait(false))
        {
            draft = part.Draft;

            if (part.Problem is { } problem)
            {
                await jobs.ProgressAsync(work.Id, JobStage.Writing, draft, token).ConfigureAwait(false);
                await jobs.FailAsync(work.Id, problem.Code, token).ConfigureAwait(false);

                return null;
            }

            finished = part.Finished;

            // The last part is always stored.
            if (finished || time.GetElapsedTime(lastStored) >= ProgressEvery)
            {
                if (!await jobs.ProgressAsync(work.Id, finished ? JobStage.Saving : JobStage.Writing, draft, token).ConfigureAwait(false))
                {
                    return null;
                }

                lastStored = time.GetTimestamp();
            }
        }

        if (!finished || draft is null)
        {
            await jobs.FailAsync(work.Id, IntakeErrors.Interrupted, token).ConfigureAwait(false);

            return null;
        }

        return draft;
    }
}
