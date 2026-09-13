using System.Text.Json;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Archive;

/// <summary>Writes a household's recipes out as a file.</summary>
/// <param name="HouseholdId">Whose kitchen.</param>
/// <param name="UserId">Who is asking, and whose notes go in it.</param>
/// <param name="Destination">Where to write it.</param>
public sealed record ExportArchiveQuery(Guid HouseholdId, Guid UserId, Stream Destination);

/// <summary>What was written, so the response can name the file.</summary>
/// <param name="Recipes">How many went in.</param>
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
    /// <summary>
    /// How many recipes are read at a time.
    /// </summary>
    /// <remarks>
    /// A page rather than all of them. A household with four hundred recipes
    /// should not put four hundred aggregates in memory to write a file that is
    /// streamed out anyway.
    /// </remarks>
    private const int Page = 50;

    /// <summary>The rendition an archive carries.</summary>
    /// <remarks>
    /// The largest one. An archive is what somebody is left with, and a
    /// thumbnail is not a photograph of dinner.
    /// </remarks>
    private const int ImageWidth = 1600;

    public async Task<Result<ArchiveWritten>> Handle(
        ExportArchiveQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Archive.Export");

        var allowed = await RecipeAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () => Result<ArchiveWritten>.Success(
                await WriteAsync(query, cancellationToken).ConfigureAwait(false)),
            error => Task.FromResult(Result<ArchiveWritten>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>
    /// Writes the file straight to the response, a recipe at a time.
    /// </summary>
    /// <remarks>
    /// Streamed rather than serialised: with its photographs inline, an archive
    /// of a well-used household is tens of megabytes, and building it in memory
    /// to hand to a serialiser would make the export the largest allocation in
    /// the process.
    /// </remarks>
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
                            RecipeSort.Title,
                            cursor,
                            Page),
                        cancellationToken)
                    .ConfigureAwait(false);

                foreach (var summary in page.Items)
                {
                    var found = await recipes
                        .FindAsync(summary.RecipeId, cancellationToken)
                        .ConfigureAwait(false);

                    var archived = await found.Match(
                        recipe => ToArchivedAsync(recipe, query.UserId, cancellationToken),
                        _ => Task.FromResult<ArchivedRecipe?>(null)).ConfigureAwait(false);

                    if (archived is null)
                    {
                        // Deleted between the page and the read. One recipe
                        // missing beats a failed export of four hundred.
                        continue;
                    }

                    // Serialised to bytes and written raw, rather than handed
                    // to the serialiser: `JsonSerializer.Serialize` flushes the
                    // writer when it finishes, and a synchronous flush into a
                    // response body is refused outright by Kestrel. Flushing is
                    // this loop's job, once per recipe, asynchronously.
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
        Guid userId,
        CancellationToken cancellationToken)
    {
        var written = await notes
            .ForRecipeAsync(recipe.Id, userId, cancellationToken)
            .ConfigureAwait(false);

        var cooked = await log
            .ForRecipeAsync(recipe.Id, userId, cancellationToken)
            .ConfigureAwait(false);

        return new ArchivedRecipe
        {
            Title = recipe.Title.Value,
            Description = recipe.Description,
            Language = RecipeWords.Of(recipe.Language),
            YieldAmount = recipe.Yield.Amount,
            YieldKind = RecipeWords.Of(recipe.Yield.Kind),
            PrepMinutes = recipe.PrepMinutes,
            CookMinutes = recipe.CookMinutes,
            Tags = [.. recipe.Tags],
            Groups = [.. recipe.Groups.Select(ToArchived)],
            Steps = [.. recipe.Steps.Select(step => ToArchived(step, recipe))],
            Image = await ToArchivedImageAsync(recipe.Id, cancellationToken).ConfigureAwait(false),
            // The overall note, which is the one on the recipe rather than on a
            // step: a step note restored against a step that moved is a note
            // attached to the wrong instruction.
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

    /// <summary>
    /// A step, with its ingredient references turned into positions.
    /// </summary>
    /// <remarks>
    /// The position is in the recipe's ingredients read in order, group by
    /// group — the same order they are written back in — so a step that said
    /// "melt the butter" still says it after a restore.
    /// </remarks>
    private static ArchivedStep ToArchived(Step step, Recipe recipe)
    {
        var order = recipe.Groups
            .SelectMany(group => group.Ingredients)
            .Select((ingredient, index) => (ingredient.Id, index))
            .ToDictionary(one => one.Id, one => one.index);

        return new ArchivedStep(
            [
                .. step.Segments.Select(segment => segment switch
                {
                    TextSegment text => new ArchivedSegment(text.Value, null),
                    IngredientSegment reference when order.TryGetValue(
                        reference.RecipeIngredientId, out var at) =>
                        new ArchivedSegment(null, at),
                    // A reference to an ingredient the recipe no longer has.
                    // Dropped rather than written as a hole.
                    _ => new ArchivedSegment(string.Empty, null)
                })
            ],
            step.DurationSeconds);
    }

    private async Task<ArchivedImage?> ToArchivedImageAsync(
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        var hash = await recipes.ImageHashAsync(recipeId, cancellationToken).ConfigureAwait(false);

        return await hash.Match<Task<ArchivedImage?>>(
            async contentHash =>
            {
                var buffer = new MemoryStream();

                await using (buffer.ConfigureAwait(false))
                {
                    var copied = await images
                        .CopyToAsync(contentHash, ImageWidth, buffer, cancellationToken)
                        .ConfigureAwait(false);

                    return copied.Match<ArchivedImage?>(
                        () => new ArchivedImage("image/webp", Convert.ToBase64String(buffer.ToArray())),
                        // A row whose file is gone exports without a picture
                        // rather than failing the whole archive.
                        _ => null);
                }
            },
            _ => Task.FromResult<ArchivedImage?>(null)).ConfigureAwait(false);
    }
}
