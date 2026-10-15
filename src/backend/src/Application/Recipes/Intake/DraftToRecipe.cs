using Application.Abstractions;
using Application.Households;
using Application.Recipes.Import;
using Application.Recipes.Update;
using Contracts.Recipes;
using Domain.Import;
using Domain.Recipes;
using Domain.Shared;
using Draft = Contracts.Recipes.Drafts.Response;

namespace Application.Recipes.Intake;

/// <summary>Turns a finished draft into a saved recipe, its origin and the completed job, or fails the job.</summary>
public sealed class DraftToRecipe(
    IRecipeIntakeJobs jobs,
    IHouseholdRepository households,
    IRecipeRepository recipes,
    IRecipeOriginRepository origins,
    IUnitOfWork transactions,
    IntakeChanges changes,
    TimeProvider time)
{
    /// <summary>Saves the finished draft as a recipe, or fails the job.</summary>
    internal async Task SaveAsync(IntakeWork work, Draft draft, CancellationToken token)
    {
        var permitted = await HouseholdAccess.MemberOfAsync(households, work.HouseholdId, work.UserId, token).ConfigureAwait(false);
        var prepared = permitted.Bind(() => Build(work, draft));
        var saved = await prepared.Match(
            recipe => transactions.InTransactionAsync(t => StoreAsync(work, recipe, t), token),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        // A failed origin fails the whole transaction, so the recipe is not left behind with a job that says it is done.
        await saved.Tap(() => changes.Changed(work.UserId, work.Id)).Match(
            () => Task.CompletedTask,
            error => jobs.FailAsync(work.Id, error.Code, token)).ConfigureAwait(false);
    }

    private async Task<Result> StoreAsync(IntakeWork work, Recipe recipe, CancellationToken token)
    {
        var added = await recipes.AddAsync(recipe, token).ConfigureAwait(false);

        return await added.Match(
            async () =>
            {
                var original = SourceUrl.From(work.Material.SourceUrl);
                var kind = original is null ? SourceKind.Assistant : SourceKind.Web;
                var origin = await origins.AddAsync(
                    new RecipeOrigin(recipe.Id, recipe.HouseholdId, kind, null, work.Id.ToString(), original, time.GetUtcNow()),
                    token).ConfigureAwait(false);

                return await origin.Match(
                    async () =>
                    {
                        await jobs.CompleteAsync(work.Id, recipe.Id, token).ConfigureAwait(false);

                        return Result.Success();
                    },
                    error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
            },
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
    }

    private Result<Recipe> Build(IntakeWork work, Draft draft)
    {
        var language = RecipeWords.ToLanguage(work.Material.Language);
        return RecipeTitle.Create(draft.Title).Bind(title => language.Bind(code =>
        {
            var recipe = Recipe.Create(work.HouseholdId, title, work.UserId, code, time.GetUtcNow());
            // The model gives a number and a word: "12" and "Muffins" is pieces, "4" and "Portionen" servings.
            var details = new RecipeDraft(
                title.ToString(),
                RecipeFit.Description(draft.Description),
                work.Material.Language,
                draft.YieldAmount ?? 1,
                RecipeWords.Of(YieldText.KindOf(draft.YieldAmount, draft.YieldLabel)),
                draft.YieldLabel,
                draft.PrepMinutes,
                draft.CookMinutes,
                RecipeFit.Tags(draft.Tags));
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
