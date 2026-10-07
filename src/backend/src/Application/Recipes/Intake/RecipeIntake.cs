using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Recipes.Drafts;
using Application.Recipes.Import;
using Application.Recipes.Update;
using Contracts.Recipes;
using Contracts.Recipes.Intake;
using Domain.Import;
using Domain.Recipes;
using Domain.Shared;
using Draft = Contracts.Recipes.Drafts.Response;

namespace Application.Recipes.Intake;

/// <summary>Accepts durable recipe imports and saves completed drafts atomically.</summary>
public sealed class RecipeIntake(IRecipeIntakeJobs jobs, IHouseholdRepository households,
    IRecipeRepository recipes, IRecipeOriginRepository origins, IUnitOfWork transactions,
    ICommandHandler<ComposeRecipeDraftCommand, DraftProgress> composer,
    IQueryHandler<ImportRecipeQuery, Contracts.Recipes.Import.Response> reader, TimeProvider time)
{
    /// <summary>Checks access and input before retaining work.</summary>
    public async Task<Result<IntakeJob>> StartAsync(Guid id, Guid userId, Guid householdId, IntakeMaterial material, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(material);
        var access = await HouseholdAccess.MemberOfAsync(households, householdId, userId, token).ConfigureAwait(false);
        var valid = access.Bind(() => RecipeWords.ToLanguage(material.Language).Map(_ => material));
        if (id == Guid.Empty || material.Text.Length + material.Transcript.Length > DraftLimits.MaxMaterialCharacters
            || material.Photos.Count > DraftLimits.MaxPhotos
            || material.Photos.Sum(p => p.Bytes.Length) > DraftLimits.MaxPhotoBytes)
        {
            return Domain.Assistance.AssistanceErrors.TooMuchToWorkFrom;
        }

        if (string.IsNullOrWhiteSpace(material.Text) && string.IsNullOrWhiteSpace(material.Transcript) && material.Photos.Count == 0 && !material.FetchSource)
        {
            return Domain.Assistance.AssistanceErrors.NothingToWorkFrom;
        }

        if ((material.FetchSource && material.SourceUrl is null) || (material.SourceUrl is not null && SourceUrl.From(material.SourceUrl) is null))
        {
            return ImportErrors.UnreachableAddress;
        }

        return await valid.Match(_ => transactions.InTransactionAsync(
            t => jobs.EnqueueAsync(id, userId, householdId, material, t), token),
            error => Task.FromResult(Result<IntakeJob>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// How often a draft that is still being written is stored. Every part of a
    /// recipe is an update that rewrites the whole draft, and a recipe is
    /// hundreds of parts; the person watching is shown a few a second at most.
    /// </summary>
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

        await SaveAsync(work, draft, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the page the person shared and adds what it says to what they
    /// wrote. Null once the job has been failed.
    /// </summary>
    private async Task<IntakeWork?> FetchSourceAsync(IntakeWork work, string sourceUrl, CancellationToken token)
    {
        await jobs.ProgressAsync(work.Id, "reading", null, token).ConfigureAwait(false);

        var fetched = await reader.Handle(new ImportRecipeQuery(sourceUrl, work.UserId), token).ConfigureAwait(false);
        var source = work.Material;
        string? sourceError = null;

        fetched.Match(page =>
        {
            var pageText = page.Text ?? string.Join("\n", new[] { page.Title }.Concat(page.IngredientLines).Concat(page.Steps).Where(s => !string.IsNullOrWhiteSpace(s)));
            var material = string.Join("\n\n", new[] { source.Text, pageText }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal));
            var transcript = source.Transcript.Length > 0 ? source.Transcript : page.Transcript ?? "";

            // The same combined limit as every provider ask. Original shared
            // words win; fetched captions use the remaining space.
            var limit = DraftLimits.MaxMaterialCharacters;
            var textBudget = limit - Math.Min(source.Transcript.Length, limit);
            material = material[..Math.Min(material.Length, textBudget)];
            transcript = transcript[..Math.Min(transcript.Length, limit - material.Length)];
            source = source with { Text = material, Transcript = transcript, SourceUrl = page.SourceUrl, FetchSource = false };
        }, error => sourceError = error.Code);

        if (sourceError is not null && string.IsNullOrWhiteSpace(source.Text) && string.IsNullOrWhiteSpace(source.Transcript) && source.Photos.Count == 0)
        {
            await jobs.FailAsync(work.Id, sourceError, token).ConfigureAwait(false);

            return null;
        }

        source = source with { FetchSource = false };
        await jobs.SourceAsync(work.Id, source, token).ConfigureAwait(false);

        return work with { Material = source };
    }

    /// <summary>
    /// Has the model write the recipe, storing it as it grows. Null once the job
    /// has been failed.
    /// </summary>
    private async Task<Draft?> ComposeAsync(IntakeWork work, CancellationToken token)
    {
        await jobs.ProgressAsync(work.Id, "reading", null, token).ConfigureAwait(false);

        var opened = await composer.Handle(new ComposeRecipeDraftCommand("social", work.HouseholdId, work.Material.Text, null, work.Material.Language, work.UserId)
        {
            Transcript = work.Material.Transcript,
            Pictures = work.Material.Photos.Select(p => new RecipePicture(p.Bytes, p.MediaType)).ToArray()
        }, token).ConfigureAwait(false);

        DraftProgress? progress = null;
        string? failure = null;
        opened.Match(value => progress = value, error => failure = error.Code);

        if (progress is null)
        {
            await jobs.FailAsync(work.Id, failure!, token).ConfigureAwait(false);

            return null;
        }

        await jobs.ProgressAsync(work.Id, "thinking", null, token).ConfigureAwait(false);

        Draft? draft = null;
        var finished = false;
        var lastStored = time.GetTimestamp();

        await foreach (var part in progress.Events.WithCancellation(token).ConfigureAwait(false))
        {
            draft = part.Draft;

            if (part.Problem is { } problem)
            {
                await jobs.ProgressAsync(work.Id, "writing", draft, token).ConfigureAwait(false);
                await jobs.FailAsync(work.Id, problem.Code, token).ConfigureAwait(false);

                return null;
            }

            finished = part.Finished;

            // The last part is always stored: it is the one that is saved.
            if (finished || time.GetElapsedTime(lastStored) >= ProgressEvery)
            {
                await jobs.ProgressAsync(work.Id, finished ? "saving" : "writing", draft, token).ConfigureAwait(false);
                lastStored = time.GetTimestamp();
            }
        }

        if (!finished || draft is null)
        {
            await jobs.FailAsync(work.Id, "RecipeIntake.Interrupted", token).ConfigureAwait(false);

            return null;
        }

        return draft;
    }

    /// <summary>Saves the finished draft as a recipe, or fails the job.</summary>
    private async Task SaveAsync(IntakeWork work, Draft draft, CancellationToken token)
    {
        var permitted = await HouseholdAccess.MemberOfAsync(households, work.HouseholdId, work.UserId, token).ConfigureAwait(false);
        var prepared = permitted.Bind(() => Build(work, draft));
        var saved = await prepared.Match(recipe => transactions.InTransactionAsync(async t =>
        {
            var added = await recipes.AddAsync(recipe, t).ConfigureAwait(false);
            return await added.Match(async () =>
            {
                var original = SourceUrl.From(work.Material.SourceUrl);
                await origins.AddAsync(new RecipeOrigin(recipe.Id, recipe.HouseholdId,
                    original is null ? SourceKind.Assistant : SourceKind.Web, null, work.Id.ToString(), original, time.GetUtcNow()), t).ConfigureAwait(false);
                await jobs.CompleteAsync(work.Id, recipe.Id, t).ConfigureAwait(false);
                return Result.Success();
            }, e => Task.FromResult(Result.Failure(e))).ConfigureAwait(false);
        }, token), e => Task.FromResult(Result.Failure(e))).ConfigureAwait(false);

        string? errorCode = null;
        saved.Match(() => { }, e => errorCode = e.Code);

        if (errorCode is not null)
        {
            await jobs.FailAsync(work.Id, errorCode, token).ConfigureAwait(false);
        }
    }

    private Result<Recipe> Build(IntakeWork work, Draft draft)
    {
        var language = RecipeWords.ToLanguage(work.Material.Language);
        return RecipeTitle.Create(draft.Title).Bind(title => language.Bind(code =>
        {
            var recipe = Recipe.Create(work.HouseholdId, title, work.UserId, code, time.GetUtcNow());
            var details = new RecipeDraft(title.ToString(), draft.Description, work.Material.Language, draft.YieldAmount ?? 1, "servings", draft.YieldLabel, draft.PrepMinutes, draft.CookMinutes, draft.Tags);
            var groups = draft.Groups.Select(g => new IngredientGroupContract
            {
                Name = g.Name,
                Ingredients = g.Ingredients.Select(i => new IngredientContract
                { Name = i.Name, Note = i.Note, Quantity = i.Quantity, Unit = i.Unit }).ToArray()
            }).ToArray();
            var steps = draft.Steps.Select(s => new StepContract
            {
                Title = s.Title,
                DurationSeconds = s.DurationSeconds,
                Segments = [new StepSegmentContract { Type = "text", Value = s.Text }],
                Uses = []
            }).ToArray();
            return RecipeParsing.ToDetails(details).Bind(d => recipe.Describe(d, time.GetUtcNow()))
                .Bind(() => RecipeParsing.ToGroups(groups)).Bind(g => RecipeParsing.ToSteps(steps).Bind(s => recipe.SetContents(g, s, time.GetUtcNow())))
                .Map(() => recipe);
        }));
    }
}
