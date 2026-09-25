using System.Globalization;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes.Sources;
using Domain.Cookbooks;
using Domain.Import;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>Asks for some of another app's recipes to be brought over.</summary>
/// <param name="SourceId">Which connection.</param>
/// <param name="UserId">Who is importing.</param>
/// <param name="Draft">Which recipes.</param>
public sealed record ImportFromSourceCommand(
    Guid SourceId,
    Guid UserId,
    ImportFromSourceRequest Draft);

/// <summary>
/// Accepts an import, and hands it to the worker.
/// </summary>
/// <remarks>
/// <para>
/// This does everything that can fail quickly and nothing that takes time: it
/// checks the connection is this household's, that the app is one this can
/// read, and makes the shelf the recipes will land on. Then it queues the run
/// and answers. Fetching four hundred recipes from somebody else's server is
/// not work a request should be holding a connection open for — that is what
/// made the old client-driven batching necessary, and what the worker replaces.
/// </para>
/// <para>
/// Nothing here is a promise that every recipe arrives. The promise is that the
/// import exists, has a name, and can be watched — and that asking for the same
/// selection again is safe, because a recipe that arrived has an origin row and
/// comes back as <c>already_here</c> rather than a second copy.
/// </para>
/// </remarks>
internal sealed class ImportFromSourceCommandHandler(
    IRecipeSourceRepository sources,
    IHouseholdRepository households,
    ICookbookRepository cookbooks,
    IRecipeLibraries libraries,
    IUnitOfWork unitOfWork,
    ImportRuns runs,
    TimeProvider time)
    : ICommandHandler<ImportFromSourceCommand, ImportStartedResponse>
{
    /// <summary>
    /// How many recipes one import may carry.
    /// </summary>
    /// <remarks>
    /// Not a batch size any more — the whole selection arrives in one request
    /// and the work happens afterwards — so this is only a ceiling on how much
    /// one person can queue at a time. A library of a thousand recipes is
    /// already a big library; somebody with more imports twice.
    /// </remarks>
    internal const int MostInOneImport = 1000;

    public async Task<Result<ImportStartedResponse>> Handle(
        ImportFromSourceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Sources.Import");

        var asked = command.Draft.ExternalIds;

        if (asked.Count == 0)
        {
            return tracked.Record(
                Result<ImportStartedResponse>.Failure(ImportErrors.NothingToImport));
        }

        if (asked.Count > MostInOneImport)
        {
            return tracked.Record(
                Result<ImportStartedResponse>.Failure(ImportErrors.TooManyAtOnce));
        }

        var found = await SourceAccess
            .UsableAsync(sources, households, command.SourceId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            source => StartAsync(source, command, cancellationToken),
            error => Task.FromResult(Result<ImportStartedResponse>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>
    /// Refuses what cannot be read, then makes the shelf and queues the run.
    /// </summary>
    /// <remarks>
    /// The reader is resolved here as well as in the worker, so an app this
    /// cannot read is a refusal the caller sees rather than four hundred failed
    /// lines on a stream.
    /// </remarks>
    private Task<Result<ImportStartedResponse>> StartAsync(
        RecipeSource source,
        ImportFromSourceCommand command,
        CancellationToken cancellationToken) =>
        libraries.For(source.Kind).Match(
            _ => QueueAsync(source, command, cancellationToken),
            error => Task.FromResult(Result<ImportStartedResponse>.Failure(error)));

    private async Task<Result<ImportStartedResponse>> QueueAsync(
        RecipeSource source,
        ImportFromSourceCommand command,
        CancellationToken cancellationToken)
    {
        var shelf = command.Draft.CookbookId is { } earlier
            ? await EarlierShelfAsync(source, earlier, cancellationToken).ConfigureAwait(false)
            : await ShelfAsync(source, command.UserId, cancellationToken).ConfigureAwait(false);

        return shelf.Map(cookbook =>
        {
            var run = new ImportRun(
                source.Id,
                command.UserId,
                cookbook.Id,
                cookbook.Name.Value,
                command.Draft.ExternalIds,
                time.GetUtcNow())
            {
                AllowLookalikes = command.Draft.AllowLookalikes
            };

            runs.Start(run);

            return new ImportStartedResponse
            {
                ImportId = run.Id,
                CookbookId = cookbook.Id,
                CookbookName = cookbook.Name.Value,
                Total = run.Total
            };
        });
    }

    /// <summary>
    /// The shelf everything in this import lands on, made before it starts.
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
    /// Made in the request rather than by the worker, so the answer that
    /// accepts an import already says where to find it. That is what lets
    /// somebody walk away from the screen thirty seconds in.
    /// </para>
    /// </remarks>
    private async Task<Result<Cookbook>> ShelfAsync(
        RecipeSource source,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        return await CookbookName.Create(ShelfName(source, now)).Match(
            async name => await unitOfWork.InTransactionAsync(
                    async token => await Cookbook
                        .Create(source.HouseholdId, name, ShelfDescription(source), userId, now)
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
    /// The shelf of an earlier import, for the recipes it held back.
    /// </summary>
    /// <remarks>
    /// Only a shelf somebody fills by hand, and only this household's: a
    /// cookbook of another kitchen is one that does not exist, and a smart one
    /// has no room for a recipe its rules did not choose.
    /// </remarks>
    private async Task<Result<Cookbook>> EarlierShelfAsync(
        RecipeSource source,
        Guid cookbookId,
        CancellationToken cancellationToken)
    {
        var found = await cookbooks.FindAsync(cookbookId, cancellationToken).ConfigureAwait(false);

        return found.Bind(cookbook =>
            cookbook.HouseholdId == source.HouseholdId && cookbook.Kind == CookbookKind.Manual
                ? Result<Cookbook>.Success(cookbook)
                : CookbookErrors.NotFound(cookbookId));
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
