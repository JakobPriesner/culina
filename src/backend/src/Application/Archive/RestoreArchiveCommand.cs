using System.Text.Json;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Recipes;
using Application.Telemetry;
using Domain.Cookbooks;
using Domain.Cooking;
using Domain.Products;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Archive;

/// <summary>Writes an archive's recipes into a household.</summary>
/// <param name="HouseholdId">Which kitchen they land in.</param>
/// <param name="UserId">Who is restoring them.</param>
/// <param name="Content">The archive file.</param>
public sealed record RestoreArchiveCommand(Guid HouseholdId, Guid UserId, Stream Content);

/// <summary>What a restore did.</summary>
/// <param name="Restored">How many recipes were written.</param>
/// <param name="Skipped">How many could not be, and were passed over.</param>
public sealed record ArchiveRestored(int Restored, int Skipped);

internal sealed class RestoreArchiveCommandHandler(
    IRecipeRepository recipes,
    ICookbookRepository cookbooks,
    IHouseholdRepository households,
    IPersonalNoteRepository notes,
    ICookLogRepository log,
    RecipeImageWriter images,
    IProductRepository products,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<RestoreArchiveCommand, ArchiveRestored>
{
    public async Task<Result<ArchiveRestored>> Handle(
        RestoreArchiveCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Archive.Restore");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            () => RestoreAsync(command, cancellationToken),
            error => Task.FromResult(Result<ArchiveRestored>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>The most recipes one restore writes; larger households restore in several archives.</summary>
    internal const int MaxRecipes = 2_000;

    private async Task<Result<ArchiveRestored>> RestoreAsync(
        RestoreArchiveCommand command,
        CancellationToken cancellationToken)
    {
        Archive? archive;

        try
        {
            archive = await JsonSerializer
                .DeserializeAsync<Archive>(command.Content, RecipeArchive.Format, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return ArchiveErrors.NotAnArchive;
        }

        if (archive is null || archive.Recipes is null)
        {
            return ArchiveErrors.NotAnArchive;
        }

        // Checked up front: each recipe is its own transaction.
        if (archive.Recipes.Count > MaxRecipes)
        {
            return ArchiveErrors.TooManyRecipes;
        }

        // Refused rather than half-read: a half-restored recipe is worse than a failed restore.
        if (archive.Culina != RecipeArchive.Version)
        {
            return ArchiveErrors.UnknownVersion;
        }

        var restored = 0;
        var skipped = 0;

        // The new id at each archive position, or null where that recipe was skipped.
        var written = new List<Guid?>(archive.Recipes.Count);

        foreach (var entry in archive.Recipes)
        {
            var id = await WriteOneAsync(command, entry, cancellationToken)
                .ConfigureAwait(false);

            written.Add(id);

            if (id is not null)
            {
                restored += 1;
            }
            else
            {
                skipped += 1;
            }
        }

        foreach (var shelf in archive.Cookbooks ?? [])
        {
            await WriteCookbookAsync(command, shelf, written, cancellationToken).ConfigureAwait(false);
        }

        foreach (var product in archive.Products ?? [])
        {
            await WriteProductAsync(command, product, cancellationToken).ConfigureAwait(false);
        }

        return new ArchiveRestored(restored, skipped);
    }

    // A barcode the household already named keeps its name, and one that does not hold together is passed over: a restore adds.
    private async Task WriteProductAsync(
        RestoreArchiveCommand command,
        ArchivedProduct entry,
        CancellationToken cancellationToken) =>
        await HouseholdProduct
            .Create(entry.Gtin, entry.Name, entry.Ingredient, entry.Quantity, entry.Unit, entry.CookMinutes, entry.Note)
            .Match(
                product => products.AddAsync(command.HouseholdId, command.UserId, product, cancellationToken),
                error => Task.FromResult(Result<long>.Failure(error))).ConfigureAwait(false);

    // One recipe per transaction, not all or nothing: an archive is restored because something went wrong, so one odd recipe must not block the rest. Failures are counted.
    private async Task<Guid?> WriteOneAsync(
        RestoreArchiveCommand command,
        ArchivedRecipe entry,
        CancellationToken cancellationToken)
    {
        var built = Build(command.HouseholdId, command.UserId, entry);

        return await built.Match(
            async recipe =>
            {
                var added = await unitOfWork.InTransactionAsync(
                    token => recipes.AddAsync(recipe, token),
                    cancellationToken).ConfigureAwait(false);

                // The recipe is kept even when a picture, note or cook entry is not: it is the part that matters.
                return await added.Match(
                    async () =>
                    {
                        await WriteExtrasAsync(command, recipe.Id, entry, cancellationToken).ConfigureAwait(false);

                        return (Guid?)recipe.Id;
                    },
                    _ => Task.FromResult<Guid?>(null)).ConfigureAwait(false);
            },
            _ => Task.FromResult<Guid?>(null)).ConfigureAwait(false);
    }

    // What export writes beside the recipe: its photograph and the restoring person's note and cooking history.
    // Each is passed over when it does not hold together, like a cookbook, rather than failing the recipe.
    private async Task WriteExtrasAsync(
        RestoreArchiveCommand command,
        Guid recipeId,
        ArchivedRecipe entry,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        if (entry.Image is { } image)
        {
            await WriteImageAsync(recipeId, image, cancellationToken).ConfigureAwait(false);
        }

        var note = PersonalNote.Write(recipeId, command.UserId, null, entry.Note, now);

        await note.Match(
            written => notes.ReplaceAsync(recipeId, command.UserId, [written], cancellationToken),
            _ => Task.FromResult(Result.Success())).ConfigureAwait(false);

        foreach (var madeAt in entry.Cooked ?? [])
        {
            var cooked = CookLogEntry.Record(recipeId, command.UserId, command.HouseholdId, madeAt, null, null);

            await cooked.Match(
                one => log.AddAsync(one, cancellationToken),
                _ => Task.FromResult(Result.Success())).ConfigureAwait(false);
        }
    }

    private async Task WriteImageAsync(Guid recipeId, ArchivedImage image, CancellationToken cancellationToken)
    {
        var bytes = new byte[image.Data.Length];

        if (!Convert.TryFromBase64String(image.Data, bytes, out var length))
        {
            return;
        }

        var content = new MemoryStream(bytes, 0, length);

        await using (content.ConfigureAwait(false))
        {
            // Stored again through the same path as an upload, so it is decoded and re-encoded, never trusted.
            await images.AttachAsync(recipeId, content, cancellationToken).ConfigureAwait(false);
        }
    }

    // A shelf in its own transaction, like a recipe, and passed over when it does not hold together. Its recipes are
    // the copies this restore just wrote, never the household's originals: a restore adds.
    private async Task WriteCookbookAsync(
        RestoreArchiveCommand command,
        ArchivedCookbook entry,
        List<Guid?> written,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var name = CookbookName.Create(entry.Name);
        var built = entry.Rules is { } rules
            ? CookbookRules.Create(rules.Tags, rules.Ingredients, rules.MaxMinutes).Bind(asked => name.Bind(named =>
                Cookbook.CreateSmart(command.HouseholdId, named, entry.Description, asked, command.UserId, now)))
            : name.Bind(named => Cookbook.Create(command.HouseholdId, named, entry.Description, command.UserId, now));

        var onShelf = entry.Rules is null
            ? (entry.Recipes ?? [])
                .Where(position => position >= 0 && position < written.Count)
                .Select(position => written[position])
                .OfType<Guid>()
                .Distinct()
                .ToList()
            : [];

        var cover = entry.Cover is { } position && position >= 0 && position < written.Count ? written[position] : null;

        await built.Match(
            cookbook =>
            {
                cookbook.UseAsCover(cover, now);

                return unitOfWork.InTransactionAsync(
                async token =>
                {
                    var added = await cookbooks.AddAsync(cookbook, token).ConfigureAwait(false);

                    return await added.Match(
                        async () =>
                        {
                            // A microsecond apart, so the shelf reads back in the archive's order.
                            foreach (var (recipeId, at) in onShelf.Select((id, index) => (id, index)))
                            {
                                await cookbooks
                                    .AddRecipeAsync(cookbook.Id, recipeId, command.UserId, now.AddTicks(at * 10), token)
                                    .ConfigureAwait(false);
                            }

                            return Result.Success();
                        },
                        error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
                },
                cancellationToken);
            },
            _ => Task.FromResult(Result.Success())).ConfigureAwait(false);
    }

    private Result<Recipe> Build(Guid householdId, Guid userId, ArchivedRecipe entry)
    {
        var now = time.GetUtcNow();

        var title = RecipeTitle.Create(entry.Title);
        var language = RecipeWords.ToLanguage(entry.Language);
        var kind = RecipeWords.ToYieldKind(entry.YieldKind);

        if (title.Match(_ => false, _ => true)
            || language.Match(_ => false, _ => true)
            || kind.Match(_ => false, _ => true))
        {
            return ArchiveErrors.NotAnArchive;
        }

        var measure = kind.Bind(one => Yield.Create(entry.YieldAmount, one, entry.YieldLabel));
        var groups = BuildGroups(entry);

        return measure.Bind(yields => groups.Bind(built =>
            title.Bind(named => language.Bind(spoken =>
            {
                var recipe = Recipe.Create(householdId, named, userId, spoken, now);

                // Chained, not discarded: a recipe that does not hold together (an impossible duration, too many
                // lines, a dangling reference) is skipped, not stored as a title with nothing in it.
                return recipe
                    .Describe(
                        new RecipeDetails(
                            named,
                            RecipeFit.Description(entry.Description),
                            spoken,
                            yields,
                            entry.PrepMinutes,
                            entry.CookMinutes,
                            RecipeFit.Tags(entry.Tags)),
                        now)
                    .Bind(() => recipe.SetContents(built, BuildSteps(entry, built), now))
                    .Map(() => recipe);
            }))));
    }

    private static Result<IReadOnlyList<IngredientGroup>> BuildGroups(ArchivedRecipe entry) =>
        (entry.Groups ?? [])
            .Select((group, order) => (group.Ingredients ?? [])
                .Select((line, at) => RecipeWords
                    .ToQuantity(line.Quantity, line.Unit)
                    .Bind(amount => RecipeIngredient.Create(null, at, amount, line.Name, line.Note)))
                .Collect()
                .Bind(lines => IngredientGroup.Create(null, group.Name, order, lines)))
            .Collect();

    // Steps with ingredient positions turned back into references. A position past the end is dropped, not guessed:
    // a step referring to the wrong ingredient is the failure this app exists to prevent.
    private static List<Step> BuildSteps(
        ArchivedRecipe entry,
        IReadOnlyList<IngredientGroup> groups)
    {
        var order = groups.SelectMany(group => group.Ingredients).ToList();
        var steps = new List<Step>();

        foreach (var (step, at) in (entry.Steps ?? []).Select((one, index) => (one, index)))
        {
            var segments = (step.Segments ?? [])
                .Select(segment => segment.Ingredient is { } position && position >= 0 && position < order.Count
                    ? new IngredientSegment(order[position].Id)
                    : (StepSegment)new TextSegment(segment.Text ?? string.Empty))
                .ToList();

            var uses = (step.Uses ?? [])
                .Where(position => position >= 0 && position < order.Count)
                .Select(position => order[position].Id)
                .ToList();

            Step.Create(null, at, segments, uses, step.DurationSeconds, step.Title)
                .Match(built => steps.Add(built), _ => { });
        }

        return steps;
    }
}

/// <summary>What can go wrong restoring an archive.</summary>
public static class ArchiveErrors
{
    /// <summary>The file is not one of these.</summary>
    public static readonly Error NotAnArchive = new(
        "archive.not_an_archive",
        "That file is not a Culina archive.",
        ErrorType.Validation);

    /// <summary>The archive holds more recipes than one restore writes.</summary>
    public static readonly Error TooManyRecipes = new(
        "archive.too_many_recipes",
        "That archive holds too many recipes to restore at once.",
        ErrorType.Validation);

    /// <summary>The file was written by a version this does not know.</summary>
    public static readonly Error UnknownVersion = new(
        "archive.unknown_version",
        "That archive was written by a newer version of Culina.",
        ErrorType.Validation);
}
