using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Recipes;
using Application.Telemetry;
using Contracts.Searches;
using Domain.Searches;
using Domain.Shared;

namespace Application.Searches;

/// <summary>Every search a household has saved.</summary>
public sealed record GetSavedSearchesQuery(Guid HouseholdId, Guid UserId);

/// <summary>Saves a search.</summary>
public sealed record CreateSavedSearchCommand(Guid UserId, CreateSavedSearchRequest Draft);

/// <summary>Renames a saved search, and rewrites what it asks for.</summary>
public sealed record UpdateSavedSearchCommand(
    Guid SearchId,
    Guid UserId,
    UpdateSavedSearchRequest Draft);

/// <summary>Forgets a saved search.</summary>
public sealed record DeleteSavedSearchCommand(Guid SearchId, Guid UserId);

internal sealed class GetSavedSearchesQueryHandler(
    ISavedSearchRepository searches,
    IHouseholdRepository households)
    : IQueryHandler<GetSavedSearchesQuery, SavedSearchesResponse>
{
    public async Task<Result<SavedSearchesResponse>> Handle(
        GetSavedSearchesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Searches.GetAll");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () =>
            {
                var saved = await searches
                    .ListAsync(query.HouseholdId, cancellationToken)
                    .ConfigureAwait(false);

                return Result<SavedSearchesResponse>.Success(saved.ToResponse());
            },
            error => Task.FromResult(Result<SavedSearchesResponse>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class CreateSavedSearchCommandHandler(
    ISavedSearchRepository searches,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CreateSavedSearchCommand, SavedSearchDetail>
{
    public async Task<Result<SavedSearchDetail>> Handle(
        CreateSavedSearchCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Searches.Create");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.Draft.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var prepared = allowed
            .Bind(() => SavedSearchName.Create(command.Draft.Name))
            .Bind(name => SavedSearchMappings
                .ToCriteria(command.Draft.Criteria)
                .Map(criteria => SavedSearch.Create(
                    command.Draft.HouseholdId,
                    name,
                    criteria,
                    command.UserId,
                    time.GetUtcNow())));

        var result = await prepared.Match(
            search => unitOfWork.InTransactionAsync(
                async token =>
                {
                    var written = await searches.AddAsync(search, token).ConfigureAwait(false);

                    return written.Map(() => search.ToDetail());
                },
                cancellationToken),
            error => Task.FromResult(Result<SavedSearchDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class UpdateSavedSearchCommandHandler(
    ISavedSearchRepository searches,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<UpdateSavedSearchCommand, SavedSearchDetail>
{
    public async Task<Result<SavedSearchDetail>> Handle(
        UpdateSavedSearchCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Searches.Update");

        var found = await SavedSearchAccess
            .VisibleAsync(searches, households, command.SearchId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            search => unitOfWork.InTransactionAsync(
                async token =>
                {
                    var revised = SavedSearchName
                        .Create(command.Draft.Name)
                        .Bind(name => SavedSearchMappings
                            .ToCriteria(command.Draft.Criteria)
                            .Map(criteria =>
                            {
                                search.Revise(name, criteria, time.GetUtcNow());

                                return search;
                            }));

                    return await revised.Match(
                        async one =>
                        {
                            var saved = await searches.SaveAsync(one, token).ConfigureAwait(false);

                            return saved.Map(() => one.ToDetail());
                        },
                        error => Task.FromResult(Result<SavedSearchDetail>.Failure(error)))
                        .ConfigureAwait(false);
                },
                cancellationToken),
            error => Task.FromResult(Result<SavedSearchDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class DeleteSavedSearchCommandHandler(
    ISavedSearchRepository searches,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteSavedSearchCommand>
{
    public async Task<Result> Handle(
        DeleteSavedSearchCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Searches.Delete");

        var found = await SavedSearchAccess
            .VisibleAsync(searches, households, command.SearchId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            search => unitOfWork.InTransactionAsync(
                async token =>
                {
                    await searches.DeleteAsync(search.Id, token).ConfigureAwait(false);

                    return Result.Success();
                },
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

/// <summary>Whether the caller may see or change a saved search: household membership only, and a non-member is told it does not exist.</summary>
internal static class SavedSearchAccess
{
    internal static async Task<Result<SavedSearch>> VisibleAsync(
        ISavedSearchRepository searches,
        IHouseholdRepository households,
        Guid searchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var found = await searches.FindAsync(searchId, cancellationToken).ConfigureAwait(false);

        return await found.Match(
            async search => await households
                .IsMemberAsync(search.HouseholdId, userId, cancellationToken)
                .ConfigureAwait(false)
                    ? Result<SavedSearch>.Success(search)
                    : SavedSearchErrors.NotFound(searchId),
            error => Task.FromResult(Result<SavedSearch>.Failure(error))).ConfigureAwait(false);
    }
}
