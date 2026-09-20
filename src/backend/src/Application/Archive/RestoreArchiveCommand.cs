using System.Text.Json;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
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
    IHouseholdRepository households,
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

        var allowed = await RecipeAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            () => RestoreAsync(command, cancellationToken),
            error => Task.FromResult(Result<ArchiveRestored>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

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

        // Refused rather than half-read. A version this does not know may spell
        // a step differently, and a half-restored recipe is worse than a failed
        // restore — you cannot tell which half is wrong.
        if (archive.Culina != RecipeArchive.Version)
        {
            return ArchiveErrors.UnknownVersion;
        }

        var restored = 0;
        var skipped = 0;

        foreach (var entry in archive.Recipes)
        {
            var written = await WriteOneAsync(command, entry, cancellationToken)
                .ConfigureAwait(false);

            if (written)
            {
                restored += 1;
            }
            else
            {
                skipped += 1;
            }
        }

        return new ArchiveRestored(restored, skipped);
    }

    /// <summary>
    /// Writes one recipe, in its own transaction.
    /// </summary>
    /// <remarks>
    /// One at a time rather than all or nothing. An archive is usually restored
    /// because something went wrong, and refusing four hundred recipes over one
    /// that a newer version wrote strangely is the least helpful thing this
    /// could do. What could not be written is counted and reported.
    /// </remarks>
    private async Task<bool> WriteOneAsync(
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

                return added.Match(() => true, _ => false);
            },
            _ => Task.FromResult(false)).ConfigureAwait(false);
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
            title.Bind(named => language.Map(spoken =>
            {
                var recipe = Recipe.Create(householdId, named, userId, spoken, now);

                recipe.Describe(
                    new RecipeDetails(
                        named,
                        entry.Description,
                        spoken,
                        yields,
                        entry.PrepMinutes,
                        entry.CookMinutes,
                        entry.Tags ?? []),
                    now);

                recipe.SetContents(built, BuildSteps(entry, built), now);

                return recipe;
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

    /// <summary>
    /// The steps, with their ingredient positions turned back into references.
    /// </summary>
    /// <remarks>
    /// The position is into the recipe's ingredients read in order, which is
    /// how they were written out. A position that points past the end is
    /// dropped rather than guessed at: a step referring to the wrong ingredient
    /// is the one failure this app exists to prevent.
    /// </remarks>
    private static List<Step> BuildSteps(
        ArchivedRecipe entry,
        IReadOnlyList<IngredientGroup> groups)
    {
        var order = groups.SelectMany(group => group.Ingredients).ToList();
        var steps = new List<Step>();

        foreach (var (step, at) in (entry.Steps ?? []).Select((one, index) => (one, index)))
        {
            var segments = (step.Segments ?? [])
                .Select(segment => segment.Ingredient is { } position && position < order.Count
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

    /// <summary>The file was written by a version this does not know.</summary>
    public static readonly Error UnknownVersion = new(
        "archive.unknown_version",
        "That archive was written by a newer version of Culina.",
        ErrorType.Validation);
}
