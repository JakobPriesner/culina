using System.Text.Json;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Recipes;
using Application.Telemetry;
using Domain.Cooking;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Archive;

/// <summary>Writes a household's recipes out as a file.</summary>
public sealed record ExportArchiveQuery(Guid HouseholdId, Guid UserId, Stream Destination);

/// <summary>What was written, so the response can name the file.</summary>
public sealed record ArchiveWritten(int Recipes);

internal sealed class ExportArchiveQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IPersonalNoteRepository notes,
    ICookLogRepository log,
    IImageStore images,
    TimeProvider time)
    : IQueryHandler<ExportArchiveQuery, ArchiveWritten>
{
    // A page at a time, so a large household does not put every aggregate in memory.
    private const int Page = 50;

    // The largest rendition: an archive is what somebody is left with.
    private const int ImageWidth = 1600;

    public async Task<Result<ArchiveWritten>> Handle(
        ExportArchiveQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Archive.Export");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () => Result<ArchiveWritten>.Success(
                await WriteAsync(query, cancellationToken).ConfigureAwait(false)),
            error => Task.FromResult(Result<ArchiveWritten>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    // Streamed, not serialised: with photographs inline an archive is tens of megabytes.
    private async Task<ArchiveWritten> WriteAsync(
        ExportArchiveQuery query,
        CancellationToken cancellationToken)
    {
        var writer = new Utf8JsonWriter(
            query.Destination,
            new JsonWriterOptions { Indented = true });

        await using (writer.ConfigureAwait(false))
        {
            writer.WriteStartObject();
            writer.WriteNumber("culina", RecipeArchive.Version);
            writer.WriteString("exportedAt", time.GetUtcNow());
            writer.WritePropertyName("recipes");
            writer.WriteStartArray();

            var written = 0;
            string? cursor = null;

            do
            {
                var page = await recipes
                    .SearchAsync(
                        new RecipeSearch(
                            query.HouseholdId,
                            query.UserId,
                            Query: null,
                            Tags: [],
                            Ingredients: [],
                            MaxMinutes: null,
                            CookbookId: null,
                            Rules: null,
                            RecipeSort.Title,
                            cursor,
                            Page),
                        cancellationToken)
                    .ConfigureAwait(false);

                // One query per kind for the whole page, not four per recipe.
                var ids = page.Items.Select(summary => summary.RecipeId).ToArray();
                var found = await recipes.FindManyAsync(ids, cancellationToken).ConfigureAwait(false);
                var personalNotes = await notes.ForRecipesAsync(ids, query.UserId, cancellationToken).ConfigureAwait(false);
                var cooked = await log.ForRecipesAsync(ids, query.UserId, cancellationToken).ConfigureAwait(false);
                var hashes = await recipes.ImageHashesAsync(ids, cancellationToken).ConfigureAwait(false);

                foreach (var summary in page.Items)
                {
                    var archived = found.TryGetValue(summary.RecipeId, out var recipe)
                        ? await ToArchivedAsync(
                                recipe,
                                [.. personalNotes[recipe.Id]],
                                [.. cooked[recipe.Id]],
                                hashes.GetValueOrDefault(recipe.Id),
                                cancellationToken)
                            .ConfigureAwait(false)
                        : null;

                    if (archived is null)
                    {
                        // Deleted between the page and the read: one missing recipe beats a failed export.
                        continue;
                    }

                    // Written raw because JsonSerializer.Serialize flushes synchronously, which Kestrel refuses;
                    // flushing is this loop's job, asynchronously, once per recipe.
                    writer.WriteRawValue(
                        JsonSerializer.SerializeToUtf8Bytes(archived, RecipeArchive.Format),
                        skipInputValidation: true);

                    written += 1;

                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                cursor = page.NextCursor;
            }
            while (cursor is not null);

            writer.WriteEndArray();
            writer.WriteEndObject();

            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            return new ArchiveWritten(written);
        }
    }

    private async Task<ArchivedRecipe?> ToArchivedAsync(
        Recipe recipe,
        IReadOnlyList<PersonalNote> written,
        IReadOnlyList<CookLogEntry> cooked,
        string? imageHash,
        CancellationToken cancellationToken)
    {
        var positions = PositionsOf(recipe);

        return new ArchivedRecipe
        {
            Title = recipe.Title.Value,
            Description = recipe.Description,
            Language = RecipeWords.Of(recipe.Language),
            YieldAmount = recipe.Yield.Amount,
            YieldKind = RecipeWords.Of(recipe.Yield.Kind),
            YieldLabel = recipe.Yield.Label,
            PrepMinutes = recipe.PrepMinutes,
            CookMinutes = recipe.CookMinutes,
            Tags = [.. recipe.Tags],
            Groups = [.. recipe.Groups.Select(ToArchived)],
            Steps = [.. recipe.Steps.Select(step => ToArchived(step, positions))],
            Image = await ToArchivedImageAsync(imageHash, cancellationToken).ConfigureAwait(false),
            // Only the recipe-level note: a step note could land on the wrong step after a restore.
            Note = written.FirstOrDefault(note => note.StepId is null)?.Body,
            Cooked = [.. cooked.Select(entry => entry.MadeAt)]
        };
    }

    private static ArchivedGroup ToArchived(IngredientGroup group) => new(
        group.Name,
        [
            .. group.Ingredients.Select(one => new ArchivedIngredient(
                one.Quantity.Amount,
                one.Quantity.Unit?.Code,
                one.Name,
                one.Note))
        ]);

    /// <summary>A step, with ingredient references turned into positions (see <see cref="PositionsOf"/>).</summary>
    private static ArchivedStep ToArchived(Step step, Dictionary<Guid, int> order) =>
        new(
            [
                .. step.Segments.Select(segment => segment switch
                {
                    TextSegment text => new ArchivedSegment(text.Value, null),
                    IngredientSegment reference when order.TryGetValue(
                        reference.RecipeIngredientId, out var at) =>
                        new ArchivedSegment(null, at),
                    // Reference to a vanished ingredient: dropped, not written as a hole.
                    _ => new ArchivedSegment(string.Empty, null)
                })
            ],
            step.DurationSeconds,
            [.. step.Uses.Where(order.ContainsKey).Select(id => order[id]).Order()],
            step.Title);

    /// <summary>Each ingredient's position, groups read in order: the order a restore writes them back.</summary>
    private static Dictionary<Guid, int> PositionsOf(Recipe recipe) => recipe.Groups
        .SelectMany(group => group.Ingredients)
        .Select((ingredient, index) => (ingredient.Id, index))
        .ToDictionary(one => one.Id, one => one.index);

    private async Task<ArchivedImage?> ToArchivedImageAsync(
        string? contentHash,
        CancellationToken cancellationToken)
    {
        if (contentHash is null)
        {
            return null;
        }

        var buffer = new MemoryStream();

        await using (buffer.ConfigureAwait(false))
        {
            var copied = await images
                .CopyToAsync(contentHash, ImageWidth, buffer, cancellationToken)
                .ConfigureAwait(false);

            return copied.Match<ArchivedImage?>(
                () => new ArchivedImage("image/webp", Convert.ToBase64String(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))),
                // A row whose file is gone exports without a picture.
                _ => null);
        }
    }
}
