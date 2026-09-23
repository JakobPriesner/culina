using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Recipes.Sources;
using Domain.Import;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>Connects another app's recipe library to a household.</summary>
/// <param name="UserId">Who is connecting it.</param>
/// <param name="Draft">Where it is, and the token to read it with.</param>
public sealed record ConnectSourceCommand(Guid UserId, ConnectSourceRequest Draft);

/// <summary>What a household has connected.</summary>
/// <param name="HouseholdId">Whose kitchen.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetSourcesQuery(Guid HouseholdId, Guid UserId);

/// <summary>Forgets a connection. Every recipe it brought over stays.</summary>
/// <param name="SourceId">Which connection.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record DisconnectSourceCommand(Guid SourceId, Guid UserId);

/// <summary>Reads a page of somebody else's library.</summary>
/// <param name="SourceId">Which connection.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Page">The token from the previous page, or null to start.</param>
/// <param name="Query">What to search for over there.</param>
public sealed record BrowseSourceQuery(Guid SourceId, Guid UserId, string? Page, string? Query);

internal sealed class ConnectSourceCommandHandler(
    IRecipeSourceRepository sources,
    IHouseholdRepository households,
    IRecipeLibraries libraries,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<ConnectSourceCommand, SourceSummary>
{
    public async Task<Result<SourceSummary>> Handle(
        ConnectSourceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Sources.Connect");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.Draft.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            () => BuildThenStoreAsync(command, cancellationToken),
            error => Task.FromResult(Result<SourceSummary>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>
    /// Works out what to connect with, then connects.
    /// </summary>
    /// <remarks>
    /// Two ways in, and the split is not a convenience. A token is the thing
    /// this actually stores, but "go and make an API token first" is a task
    /// somebody has to go and learn before they can begin, and it is where most
    /// attempts to move a recipe library stop. So a name and password are
    /// traded for a token here, and only the token is kept.
    /// </remarks>
    private async Task<Result<SourceSummary>> BuildThenStoreAsync(
        ConnectSourceCommand command,
        CancellationToken cancellationToken)
    {
        if (SourceKind.Parse(command.Draft.Kind) is not { } kind)
        {
            return ImportErrors.UnknownSourceKind;
        }

        var parsed = SourceAddress.Create(command.Draft.Address)
            .Bind(address => libraries.For(kind).Map(reader => (address, reader)));

        return await parsed.Match(
            found => WithATokenAsync(command, kind, found.address, found.reader, cancellationToken),
            error => Task.FromResult(Result<SourceSummary>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<SourceSummary>> WithATokenAsync(
        ConnectSourceCommand command,
        SourceKind kind,
        SourceAddress address,
        IRecipeLibrary reader,
        CancellationToken cancellationToken)
    {
        var token = await TokenAsync(command, address, reader, cancellationToken)
            .ConfigureAwait(false);

        var built = token.Bind(secret => RecipeSource.Create(
            command.Draft.HouseholdId,
            kind,
            command.Draft.Label,
            address,
            secret,
            command.UserId,
            time.GetUtcNow()));

        return await built.Match(
            source => ProveThenStoreAsync(source, reader, cancellationToken),
            error => Task.FromResult(Result<SourceSummary>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// The token to store: the one that was given, or one signed in for.
    /// </summary>
    /// <remarks>
    /// Exactly one of the two, never both. Accepting both and preferring one
    /// would mean a request that says two different things gets a silent answer
    /// about which was believed.
    /// </remarks>
    private static async Task<Result<string>> TokenAsync(
        ConnectSourceCommand command,
        SourceAddress address,
        IRecipeLibrary reader,
        CancellationToken cancellationToken)
    {
        var hasToken = !string.IsNullOrWhiteSpace(command.Draft.Token);
        var hasSignIn = !string.IsNullOrWhiteSpace(command.Draft.Username)
            && !string.IsNullOrWhiteSpace(command.Draft.Password);

        if (hasToken && hasSignIn)
        {
            return ImportErrors.AmbiguousCredentials;
        }

        if (hasToken)
        {
            return command.Draft.Token!;
        }

        if (!hasSignIn)
        {
            return ImportErrors.InvalidSourceToken;
        }

        // The password goes no further than this call. What comes back is the
        // token, and the token is the only thing that is ever written down.
        return await reader
            .SignInAsync(address, command.Draft.Username!, command.Draft.Password!, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Talks to the other app before writing anything down.
    /// </summary>
    /// <remarks>
    /// The order is the feature. A connection that is stored first and tested
    /// later looks fine on the settings screen and fails the first time
    /// somebody tries to use it — by which point they have forgotten which of
    /// the address and the token they got wrong. Tested here, the answer
    /// arrives while the form is still open.
    /// </remarks>
    private async Task<Result<SourceSummary>> ProveThenStoreAsync(
        RecipeSource source,
        IRecipeLibrary reader,
        CancellationToken cancellationToken)
    {
        // Still tested even when a sign-in just succeeded: signing in proves the
        // account, and this proves the token can actually read recipes, which is
        // the thing the connection is for.
        var reachable = await reader.TestAsync(source, cancellationToken).ConfigureAwait(false);

        return await reachable.Match(
            async () => await unitOfWork.InTransactionAsync(
                    async token =>
                    {
                        var stored = await sources.AddAsync(source, token).ConfigureAwait(false);

                        return stored.Map(() => source.ToSummary());
                    },
                    cancellationToken)
                .ConfigureAwait(false),
            error => Task.FromResult(Result<SourceSummary>.Failure(error))).ConfigureAwait(false);
    }
}

internal sealed class GetSourcesQueryHandler(
    IRecipeSourceRepository sources,
    IHouseholdRepository households)
    : IQueryHandler<GetSourcesQuery, SourcesResponse>
{
    public async Task<Result<SourcesResponse>> Handle(
        GetSourcesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Sources.GetAll");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () =>
            {
                var connected = await sources
                    .ListAsync(query.HouseholdId, cancellationToken)
                    .ConfigureAwait(false);

                return Result<SourcesResponse>.Success(
                    new SourcesResponse { Items = [.. connected.Select(one => one.ToSummary())] });
            },
            error => Task.FromResult(Result<SourcesResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class DisconnectSourceCommandHandler(
    IRecipeSourceRepository sources,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DisconnectSourceCommand>
{
    public async Task<Result> Handle(
        DisconnectSourceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Sources.Disconnect");

        var found = await SourceAccess
            .UsableAsync(sources, households, command.SourceId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            async source =>
            {
                // The recipes it brought over are not touched, and neither is
                // the fact that they came from it. Disconnecting is putting the
                // token away, not undoing the move.
                await unitOfWork.InTransactionAsync(
                        async token =>
                        {
                            await sources.DeleteAsync(source.Id, token).ConfigureAwait(false);

                            return true;
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                return Result.Success();
            },
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class BrowseSourceQueryHandler(
    IRecipeSourceRepository sources,
    IRecipeOriginRepository origins,
    IHouseholdRepository households,
    IRecipeLibraries libraries)
    : IQueryHandler<BrowseSourceQuery, SourceRecipesResponse>
{
    public async Task<Result<SourceRecipesResponse>> Handle(
        BrowseSourceQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Sources.Browse");

        var found = await SourceAccess
            .UsableAsync(sources, households, query.SourceId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            source => ReadAsync(source, query, cancellationToken),
            error => Task.FromResult(Result<SourceRecipesResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<SourceRecipesResponse>> ReadAsync(
        RecipeSource source,
        BrowseSourceQuery query,
        CancellationToken cancellationToken)
    {
        var library = libraries.For(source.Kind);

        var page = await library.Match(
            reader => reader.BrowseAsync(source, query.Page, query.Query, cancellationToken),
            error => Task.FromResult(Result<SourcePage>.Failure(error))).ConfigureAwait(false);

        return await page.Match(
            async read =>
            {
                // Asked once for the whole page rather than once per row. This
                // runs on every scroll of somebody's two-thousand-recipe
                // library, and fifty queries per screen would be felt.
                var here = await origins
                    .AlreadyHereAsync(
                        source.HouseholdId,
                        source.Kind,
                        [.. read.Recipes.Select(recipe => recipe.ExternalId)],
                        cancellationToken)
                    .ConfigureAwait(false);

                return Result<SourceRecipesResponse>.Success(read.ToResponse(here));
            },
            error => Task.FromResult(Result<SourceRecipesResponse>.Failure(error))).ConfigureAwait(false);
    }
}
