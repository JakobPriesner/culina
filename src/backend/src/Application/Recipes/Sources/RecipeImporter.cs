using Application.Abstractions;
using Contracts.Recipes.Sources;
using Domain.Import;
using Domain.Recipes;
using Domain.Search;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>Where one recipe is being brought from, and to.</summary>
/// <remarks>What is the same for every recipe in one import, so it is read once per run.</remarks>
public sealed record ImportInto(
    RecipeSource Source,
    IRecipeLibrary Reader,
    Guid CookbookId,
    Guid UserId,
    Language Language,
    bool AllowLookalikes = false,
    IReadOnlyDictionary<string, Guid>? AlreadyHere = null);

/// <summary>One recipe: fetched, translated, written and remembered. Never returns a failure; every outcome is a line to show.</summary>
/// <remarks>
/// Its own service because an import runs recipes in parallel and each needs its own scope (a unit of work
/// is a database connection).
/// </remarks>
internal sealed class RecipeImporter(
    IRecipeOriginRepository origins,
    IRecipeRepository recipes,
    ILookalikeRecipes lookalikes,
    ICookbookRepository cookbooks,
    IImageStore images,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    private const string PictureContentType = "image/webp";

    internal const string Imported = "imported";
    private const string AlreadyHere = "already_here";
    private const string LooksLike = "looks_like";
    internal const string Failed = "failed";

    public async Task<ImportedRecipe> ImportAsync(
        ImportInto into,
        string externalId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(into);

        // The run answers for the whole selection in one query; alone, a recipe asks for itself. The unique
        // index on the origin is what really prevents duplicates.
        var here = into.AlreadyHere
            ?? await origins
                .AlreadyHereAsync(into.Source.HouseholdId, into.Source.Kind, [externalId], cancellationToken)
                .ConfigureAwait(false);

        if (here.TryGetValue(externalId, out var mine))
        {
            // Not a failure: re-running an import is the ordinary way to catch up.
            return new ImportedRecipe
            {
                ExternalId = externalId,
                Outcome = AlreadyHere,
                RecipeId = mine
            };
        }

        var fetched = await into.Reader
            .FetchAsync(into.Source, externalId, cancellationToken)
            .ConfigureAwait(false);

        return await fetched.Match(
            recipe => WriteAsync(into, recipe, cancellationToken),
            error => Task.FromResult(new ImportedRecipe
            {
                ExternalId = externalId,
                Outcome = Failed,
                Reason = error.Code
            })).ConfigureAwait(false);
    }

    private async Task<ImportedRecipe> WriteAsync(
        ImportInto into,
        SourceRecipe theirs,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        var built = SourceRecipeMapping.ToDetails(theirs, into.Language)
            .Bind(details => SourceRecipeMapping.ToGroups(theirs)
                .Bind(ingredients => SourceRecipeMapping.ToSteps(theirs, ingredients.Landed)
                    .Bind(steps =>
                    {
                        var recipe = Recipe.Create(
                            into.Source.HouseholdId,
                            details.Title,
                            into.UserId,
                            into.Language,
                            now);

                        return recipe.Describe(details, now)
                            .Bind(() => recipe.SetContents(ingredients.Groups, steps, now))
                            .Map(() => recipe);
                    })));

        return await built.Match(
            recipe => into.AllowLookalikes
                ? StoreAsync(into, theirs, recipe, cancellationToken)
                : UnlessAlikeAsync(into, theirs, recipe, cancellationToken),
            error => Task.FromResult(new ImportedRecipe
            {
                ExternalId = theirs.ExternalId,
                Title = theirs.Title,
                Outcome = Failed,
                Reason = error.Code
            })).ConfigureAwait(false);
    }

    // Held back, never dropped: nothing is stored, so asking again with "anyway" brings it over as it would have come.
    private async Task<ImportedRecipe> UnlessAlikeAsync(
        ImportInto into,
        SourceRecipe theirs,
        Recipe recipe,
        CancellationToken cancellationToken)
    {
        var names = recipe.Groups.SelectMany(group => group.Ingredients).Select(one => one.Name).ToList();

        var alike = await lookalikes
            .FindAsync(
                into.Source.HouseholdId,
                into.UserId,
                new LookalikeCandidate(
                    recipe.Title.Value,
                    names,
                    CulinaryLexicon.Describe(recipe.Title.Value, theirs.Tags, names)),
                cancellationToken)
            .ConfigureAwait(false);

        return alike is null
            ? await StoreAsync(into, theirs, recipe, cancellationToken).ConfigureAwait(false)
            : new ImportedRecipe
            {
                ExternalId = theirs.ExternalId,
                Title = recipe.Title.Value,
                Outcome = LooksLike,
                RecipeId = alike.RecipeId,
                LooksLike = new ImportLookalike
                {
                    Title = alike.Title,
                    SharedIngredients = alike.SharedIngredients,
                    CookCount = alike.CookCount
                }
            };
    }

    private async Task<ImportedRecipe> StoreAsync(
        ImportInto into,
        SourceRecipe theirs,
        Recipe recipe,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        // One transaction per recipe: one unreadable recipe must not undo the rest, and recipe, origin and
        // shelf are one fact (a recipe without its origin would be imported again).
        var written = await unitOfWork.InTransactionAsync(
                async token =>
                {
                    var stored = await recipes.AddAsync(recipe, token).ConfigureAwait(false);

                    return await stored.Match(
                        async () =>
                        {
                            var remembered = await origins.AddAsync(
                                    new RecipeOrigin(
                                        recipe.Id,
                                        into.Source.HouseholdId,
                                        into.Source.Kind,
                                        into.Source.Id,
                                        theirs.ExternalId,
                                        SourceUrl.From(theirs.SourceUrl),
                                        now),
                                    token)
                                .ConfigureAwait(false);

                            return await remembered.Match(
                                async () =>
                                {
                                    await cookbooks
                                        .AddRecipeAsync(
                                            into.CookbookId, recipe.Id, into.UserId, now, token)
                                        .ConfigureAwait(false);
                                    await cookbooks.TouchAsync(into.CookbookId, now, token)
                                        .ConfigureAwait(false);

                                    return Result.Success();
                                },
                                error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
                        },
                        error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);

        return await written.Match(
            async () =>
            {
                await PictureAsync(into, theirs, recipe.Id, cancellationToken).ConfigureAwait(false);

                return new ImportedRecipe
                {
                    ExternalId = theirs.ExternalId,
                    Title = recipe.Title.Value,
                    Outcome = Imported,
                    RecipeId = recipe.Id
                };
            },
            error => Task.FromResult(new ImportedRecipe
            {
                ExternalId = theirs.ExternalId,
                Title = theirs.Title,
                Outcome = Failed,
                Reason = error.Code
            })).ConfigureAwait(false);
    }

    // After the recipe and outside its transaction: a photo is optional, so nothing about it may cost the
    // recipe. Stored by the same code as uploads, which decodes and re-encodes; nothing from the other server is trusted.
    private async Task PictureAsync(
        ImportInto into,
        SourceRecipe theirs,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(theirs.ImageUrl))
        {
            return;
        }

        var fetched = await into.Reader
            .FetchPictureAsync(into.Source, theirs.ImageUrl, cancellationToken)
            .ConfigureAwait(false);

        await fetched.Match(
            content => AttachAsync(content, recipeId, cancellationToken),
            _ => Task.CompletedTask).ConfigureAwait(false);
    }

    private async Task AttachAsync(
        Stream content,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        await using (content.ConfigureAwait(false))
        {
            var stored = await images.StoreAsync(content, cancellationToken).ConfigureAwait(false);

            await stored.Match(
                image => unitOfWork.InTransactionAsync(
                    async token => await recipes
                        .SetImageAsync(recipeId, image, PictureContentType, time.GetUtcNow(), token)
                        .ConfigureAwait(false),
                    cancellationToken),
                // A photo this could not decode is one the recipe does without; nothing was written.
                _ => Task.FromResult(Result<ImageReplacement>.Failure(ImportErrors.NotAPicture)))
                .ConfigureAwait(false);
        }
    }
}
