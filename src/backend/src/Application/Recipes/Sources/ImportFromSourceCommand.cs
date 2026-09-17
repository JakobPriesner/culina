using System.Globalization;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Application.Telemetry;
using Contracts.Recipes.Sources;
using Domain.Cookbooks;
using Domain.Import;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>Brings some of another app's recipes over.</summary>
/// <param name="SourceId">Which connection.</param>
/// <param name="UserId">Who is importing.</param>
/// <param name="Draft">Which recipes, and the shelf they are landing on.</param>
public sealed record ImportFromSourceCommand(
    Guid SourceId,
    Guid UserId,
    ImportFromSourceRequest Draft);

/// <summary>
/// Brings a batch of recipes over, one at a time and independently.
/// </summary>
/// <remarks>
/// <para>
/// A batch rather than a library, and no job queue behind it. Moving eight
/// hundred recipes is a long operation with an uncertain outcome, which usually
/// argues for a background worker — a table of jobs, a poller, a status
/// endpoint, and a new way for the system to be half-finished. It is not needed
/// here, because the request is already idempotent: the database refuses a
/// second origin for the same recipe, so asking twice is asking once.
/// </para>
/// <para>
/// That one property is what lets the client walk its own selection a batch at
/// a time. Progress is real rather than estimated, because a batch that came
/// back is a batch that is done. A closed laptop loses nothing. And a person
/// who comes back next month sees only what is new, by exactly the same
/// mechanism.
/// </para>
/// <para>
/// Every recipe is written in a transaction of its own, on purpose. One recipe
/// that cannot be read must not undo the forty that could — a batch is a
/// convenience, not a unit of meaning.
/// </para>
/// </remarks>
internal sealed class ImportFromSourceCommandHandler(
    IRecipeSourceRepository sources,
    IRecipeOriginRepository origins,
    IRecipeRepository recipes,
    ICookbookRepository cookbooks,
    IHouseholdRepository households,
    IRecipeLibraries libraries,
    IImageStore images,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<ImportFromSourceCommand, ImportFromSourceResponse>
{
    /// <summary>
    /// How many recipes one request brings over.
    /// </summary>
    /// <remarks>
    /// Each one is a round trip to somebody else's server, so this is a
    /// ceiling on how long a request can hold a connection open as much as it
    /// is on how much work it does. Small enough that a batch finishes inside
    /// any proxy's timeout, large enough that eight hundred recipes is thirty
    /// requests rather than eight hundred.
    /// </remarks>
    internal const int MaxBatch = 25;

    /// <summary>What the image store writes, and so what a recipe row records.</summary>
    private const string PictureContentType = "image/webp";

    private const string Imported = "imported";
    private const string AlreadyHere = "already_here";
    private const string Failed = "failed";

    public async Task<Result<ImportFromSourceResponse>> Handle(
        ImportFromSourceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Sources.Import");

        if (command.Draft.ExternalIds.Count > MaxBatch)
        {
            return tracked.Record(
                Result<ImportFromSourceResponse>.Failure(ImportErrors.TooManyAtOnce));
        }

        var found = await SourceAccess
            .UsableAsync(sources, households, command.SourceId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            source => RunAsync(source, command, cancellationToken),
            error => Task.FromResult(Result<ImportFromSourceResponse>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<ImportFromSourceResponse>> RunAsync(
        RecipeSource source,
        ImportFromSourceCommand command,
        CancellationToken cancellationToken)
    {
        return await libraries.For(source.Kind).Match(
            reader => BringOverAsync(source, reader, command, cancellationToken),
            error => Task.FromResult(Result<ImportFromSourceResponse>.Failure(error)))
            .ConfigureAwait(false);
    }

    private async Task<Result<ImportFromSourceResponse>> BringOverAsync(
        RecipeSource source,
        IRecipeLibrary reader,
        ImportFromSourceCommand command,
        CancellationToken cancellationToken)
    {
        var shelf = await ShelfAsync(source, command, cancellationToken).ConfigureAwait(false);

        return await shelf.Match(
            async cookbook =>
            {
                List<ImportedRecipe> results = [];

                foreach (var externalId in command.Draft.ExternalIds)
                {
                    results.Add(await OneAsync(
                            source, reader, cookbook, externalId, command.UserId, cancellationToken)
                        .ConfigureAwait(false));
                }

                await MarkUsedAsync(source, cancellationToken).ConfigureAwait(false);

                return Result<ImportFromSourceResponse>.Success(new ImportFromSourceResponse
                {
                    CookbookId = cookbook.Id,
                    CookbookName = cookbook.Name.Value,
                    Results = results
                });
            },
            error => Task.FromResult(Result<ImportFromSourceResponse>.Failure(error)))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The shelf everything in this import lands on, made once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single design decision that makes this feature feel like part of the
    /// app rather than bolted to it. Four hundred recipes arriving into a
    /// library of six hundred is invisible, unverifiable and irreversible.
    /// Four hundred recipes on a cookbook called "From Tandoor, 17 September"
    /// is something you can open, look through, show somebody, and throw away.
    /// </para>
    /// <para>
    /// And it costs no new concept. Cookbooks already exist, already have a
    /// page, already carry a cover and a count. An import is simply a cookbook
    /// that filled itself.
    /// </para>
    /// </remarks>
    private async Task<Result<Cookbook>> ShelfAsync(
        RecipeSource source,
        ImportFromSourceCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Draft.CookbookId is { } existing)
        {
            // Every batch after the first passes the shelf back, so an import
            // of twenty requests lands on one shelf rather than twenty.
            var found = await CookbookAccess
                .VisibleAsync(cookbooks, households, existing, command.UserId, cancellationToken)
                .ConfigureAwait(false);

            return found.Map(shelf => shelf.Cookbook);
        }

        var now = time.GetUtcNow();

        return await CookbookName.Create(ShelfName(source, now)).Match(
            async name => await unitOfWork.InTransactionAsync(
                    async token => await Cookbook
                        .Create(source.HouseholdId, name, ShelfDescription(source), command.UserId, now)
                        .Match(
                            async cookbook =>
                            {
                                var stored = await cookbooks.AddAsync(cookbook, token)
                                    .ConfigureAwait(false);

                                return stored.Map(() => cookbook);
                            },
                            error => Task.FromResult(Result<Cookbook>.Failure(error)))
                        .ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false),
            error => Task.FromResult(Result<Cookbook>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// One recipe: fetched, translated, written, and remembered.
    /// </summary>
    /// <remarks>
    /// Never returns a failure. Every outcome is a line in the response,
    /// because the caller is importing four hundred of these and needs to know
    /// which twelve did not work — not to be told that the whole thing did not.
    /// </remarks>
    private async Task<ImportedRecipe> OneAsync(
        RecipeSource source,
        IRecipeLibrary reader,
        Cookbook shelf,
        string externalId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var here = await origins
            .AlreadyHereAsync(source.HouseholdId, source.Kind, [externalId], cancellationToken)
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

        var fetched = await reader.FetchAsync(source, externalId, cancellationToken)
            .ConfigureAwait(false);

        return await fetched.Match(
            recipe => WriteAsync(source, reader, shelf, recipe, userId, cancellationToken),
            error => Task.FromResult(new ImportedRecipe
            {
                ExternalId = externalId,
                Outcome = Failed,
                Reason = error.Code
            })).ConfigureAwait(false);
    }

    private async Task<ImportedRecipe> WriteAsync(
        RecipeSource source,
        IRecipeLibrary reader,
        Cookbook shelf,
        SourceRecipe theirs,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        var built = SourceRecipeMapping.ToDetails(theirs)
            .Bind(details => SourceRecipeMapping.ToGroups(theirs)
                .Bind(groups => SourceRecipeMapping.ToSteps(theirs)
                    .Bind(steps =>
                    {
                        var recipe = Recipe.Create(source.HouseholdId, details.Title, userId, now);

                        return recipe.Describe(details, now)
                            .Bind(() => recipe.SetContents(groups, steps, now))
                            .Map(() => recipe);
                    })));

        return await built.Match(
            recipe => StoreAsync(source, reader, shelf, theirs, recipe, userId, cancellationToken),
            error => Task.FromResult(new ImportedRecipe
            {
                ExternalId = theirs.ExternalId,
                Title = theirs.Title,
                Outcome = Failed,
                Reason = error.Code
            })).ConfigureAwait(false);
    }

    private async Task<ImportedRecipe> StoreAsync(
        RecipeSource source,
        IRecipeLibrary reader,
        Cookbook shelf,
        SourceRecipe theirs,
        Recipe recipe,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        // One transaction per recipe: the recipe, where it came from, and the
        // shelf it is on are one fact, and a recipe that exists without its
        // origin would be imported again on the next run.
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
                                        source.HouseholdId,
                                        source.Kind,
                                        source.Id,
                                        theirs.ExternalId,
                                        theirs.SourceUrl,
                                        now),
                                    token)
                                .ConfigureAwait(false);

                            return await remembered.Match(
                                async () =>
                                {
                                    await cookbooks
                                        .AddRecipeAsync(shelf.Id, recipe.Id, userId, now, token)
                                        .ConfigureAwait(false);
                                    await cookbooks.TouchAsync(shelf.Id, now, token)
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
                await PictureAsync(source, reader, theirs, recipe.Id, cancellationToken)
                    .ConfigureAwait(false);

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
        RecipeSource source,
        IRecipeLibrary reader,
        SourceRecipe theirs,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(theirs.ImageUrl))
        {
            return;
        }

        var fetched = await reader
            .FetchPictureAsync(source, theirs.ImageUrl, cancellationToken)
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

    private async Task MarkUsedAsync(RecipeSource source, CancellationToken cancellationToken)
    {
        source.Used(time.GetUtcNow());

        await unitOfWork.InTransactionAsync(
                async token =>
                {
                    await sources.SaveAsync(source, token).ConfigureAwait(false);

                    return true;
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// What the shelf is called: where from, and when.
    /// </summary>
    /// <remarks>
    /// The date is what tells two imports from the same instance apart, which
    /// is the whole reason somebody imports twice. An invariant date rather
    /// than a localised one: this is a name stored in a row, read by everybody
    /// in the household whatever language they read the app in, and a name that
    /// rendered differently per reader would be a different cookbook to each
    /// of them.
    /// </remarks>
    private static string ShelfName(RecipeSource source, DateTimeOffset now) =>
        Truncate(
            $"{source.Label} · {now.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}",
            CookbookName.MaxLength);

    private static string ShelfDescription(RecipeSource source) =>
        $"Brought over from {source.Address.Value}.";

    private static string Truncate(string value, int limit) =>
        value.Length <= limit ? value : value[..limit].TrimEnd();
}
