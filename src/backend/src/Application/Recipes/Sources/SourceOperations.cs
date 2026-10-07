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

    // Works out what to connect with. A name and password are traded for a token, because "make an API token first" is where most attempts stop; only the token is kept.
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

    // The token to store: the one given or one signed in for. Exactly one, so a request cannot say two things and get a silent answer.
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

        // The password goes no further than this call; only the token is ever written down.
        return await reader
            .SignInAsync(address, command.Draft.Username!, command.Draft.Password!, cancellationToken)
            .ConfigureAwait(false);
    }

    // Talks to the other app before writing anything down, so a wrong address or token shows while the form is open.
    private async Task<Result<SourceSummary>> ProveThenStoreAsync(
        RecipeSource source,
        IRecipeLibrary reader,
        CancellationToken cancellationToken)
    {
        // Tested even after a sign-in: that proves the account, this proves the token can read recipes.
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
                // The recipes it brought over, and their origin, stay: disconnecting puts the token away, not undoes the move.
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
            .ReadableAsync(sources, households, query.SourceId, query.UserId, cancellationToken)
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
                // Once for the whole page, not per row: this runs on every scroll of a large library.
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
