using Application.Abstractions;
using Contracts.Recipes.Sources;
using Domain.Import;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>Where one recipe is being brought from, and to.</summary>
/// <param name="Source">The connection, with the credential to read it.</param>
/// <param name="Reader">The app-specific client that knows how to ask.</param>
/// <param name="CookbookId">The shelf this import lands on.</param>
/// <param name="UserId">Who asked for it.</param>
/// <param name="Language">The language they read, which every recipe lands in.</param>
/// <remarks>
/// Everything that is the same for every recipe in one import, passed once
/// rather than threaded through six parameters per call. The language is here
/// for exactly that reason: it is read once for the run rather than once per
/// recipe, which for four hundred recipes is the difference between one query
/// and four hundred.
/// </remarks>
public sealed record ImportInto(
    RecipeSource Source,
    IRecipeLibrary Reader,
    Guid CookbookId,
    Guid UserId,
    Language Language);

/// <summary>
/// One recipe: fetched, translated, written, and remembered.
/// </summary>
/// <remarks>
/// <para>
/// Its own service rather than a method on the import handler, because an
/// import runs its recipes in parallel and each one needs a scope of its own —
/// a unit of work is a database connection, and a connection is not something
/// two recipes may share. The runner opens a scope per recipe and resolves this.
/// </para>
/// <para>
/// Never returns a failure. Every outcome is a line the caller can show,
/// because somebody importing four hundred recipes needs to know which twelve
/// did not work — not to be told that the whole thing did not.
/// </para>
/// </remarks>
internal sealed class RecipeImporter(
    IRecipeOriginRepository origins,
    IRecipeRepository recipes,
    ICookbookRepository cookbooks,
    IImageStore images,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    /// <summary>What the image store writes, and so what a recipe row records.</summary>
    private const string PictureContentType = "image/webp";

    private const string Imported = "imported";
    private const string AlreadyHere = "already_here";
    private const string Failed = "failed";

    /// <summary>Brings one of their recipes over.</summary>
    /// <param name="into">Which connection, which shelf, whose import.</param>
    /// <param name="externalId">Which of their recipes.</param>
    /// <param name="cancellationToken">Cancels the fetch and the write.</param>
    public async Task<ImportedRecipe> ImportAsync(
        ImportInto into,
        string externalId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(into);

        var here = await origins
            .AlreadyHereAsync(into.Source.HouseholdId, into.Source.Kind, [externalId], cancellationToken)
            .ConfigureAwait(false);

        if (here.TryGetValue(externalId, out var mine))
        {
            // Not a failure, and it must not be counted as one. Re-running an
            // import is the ordinary way to catch up on what is new.
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
            recipe => StoreAsync(into, theirs, recipe, cancellationToken),
            error => Task.FromResult(new ImportedRecipe
            {
                ExternalId = theirs.ExternalId,
                Title = theirs.Title,
                Outcome = Failed,
                Reason = error.Code
            })).ConfigureAwait(false);
    }

    private async Task<ImportedRecipe> StoreAsync(
        ImportInto into,
        SourceRecipe theirs,
        Recipe recipe,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        // One transaction per recipe, on purpose. One recipe that cannot be
        // read must not undo the forty that could — and the recipe, where it
        // came from, and the shelf it is on are one fact: a recipe that existed
        // without its origin would be imported again on the next run.
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
                                        theirs.SourceUrl,
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

    /// <summary>
    /// Brings the recipe's photo over, if it can be had.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After the recipe, outside its transaction, and returning nothing. A
    /// photo is the one part of a recipe that is genuinely optional — a recipe
    /// without one is the ordinary state of most recipes somebody typed — so
    /// nothing about it may cost the recipe. A picture that is missing, too
    /// large, slow, behind a sign-in this cannot pass, or simply not a picture
    /// leaves a recipe that is complete in every other way.
    /// </para>
    /// <para>
    /// Stored by exactly the same code an upload goes through, which decides
    /// what the file is by decoding it and keeps its own re-encoding. Nothing
    /// from another server is trusted about what it sent.
    /// </para>
    /// </remarks>
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
                // A photo this could not decode is a photo the recipe does
                // without. The file was never written, so nothing is orphaned.
                _ => Task.FromResult(Result<ImageReplacement>.Failure(ImportErrors.NotAPicture)))
                .ConfigureAwait(false);
        }
    }
}
