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
public sealed record ImportFromSourceCommand(
    Guid SourceId,
    Guid UserId,
    ImportFromSourceRequest Draft,
    Language DeviceLanguage);

/// <summary>Accepts an import and hands it to the worker.</summary>
/// <remarks>
/// Does only what can fail quickly (connection check, reader check, creating the shelf), then queues the
/// run. Asking again for the same selection is safe: arrived recipes come back as <c>already_here</c>.
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
    // A ceiling on what one person can queue at once; a bigger library imports twice.
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
            .ReadableAsync(sources, households, command.SourceId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            source => StartAsync(source, command, cancellationToken),
            error => Task.FromResult(Result<ImportStartedResponse>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    // The reader is resolved here as well as in the worker, so an unreadable app is a refusal the caller sees.
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
                AllowLookalikes = command.Draft.AllowLookalikes,
                DeviceLanguage = command.DeviceLanguage
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

    // The shelf is made in the request so the accepting answer already says where to find the import.
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

    // Only a manual shelf of this household: another kitchen's does not exist, and a smart one has no room for it.
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

    // The date tells two imports apart. Invariant rather than localised: the name is stored and read by the
    // whole household in any language.
    private static string ShelfName(RecipeSource source, DateTimeOffset now) =>
        Truncate(
            $"{source.Label} · {now.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}",
            CookbookName.MaxLength);

    private static string ShelfDescription(RecipeSource source) =>
        $"Brought over from {source.Address.Value}.";

    private static string Truncate(string value, int limit) =>
        value.Length <= limit ? value : value[..limit].TrimEnd();
}
