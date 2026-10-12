using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Ingredients;
using Domain.Ingredients;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Ingredients;

/// <summary>The ingredient names a household's own recipes use.</summary>
public sealed record GetIngredientNamesQuery(Guid HouseholdId, Guid UserId);

/// <summary>Every merge a household has made.</summary>
public sealed record GetIngredientMergesQuery(Guid HouseholdId, Guid UserId);

/// <summary>Merges one ingredient name into another, for the household's recipes now and later.</summary>
public sealed record CreateIngredientMergeCommand(Guid HouseholdId, Guid UserId, string From, string To);

/// <summary>Forgets a merge; <c>From</c> is the name that went away, spelled any way that folds the same.</summary>
public sealed record DeleteIngredientMergeCommand(Guid HouseholdId, Guid UserId, string From);

/// <summary>Names in a household's recipes that are probably one ingredient.</summary>
public sealed record GetMergeSuggestionsQuery(Guid HouseholdId, Guid UserId);

/// <summary>Stops suggesting a name for merging: it is its own ingredient.</summary>
public sealed record DeleteMergeSuggestionCommand(Guid HouseholdId, Guid UserId, string Name);

internal sealed class GetIngredientNamesQueryHandler(
    IIngredientNameRepository names,
    IHouseholdRepository households)
    : IQueryHandler<GetIngredientNamesQuery, IngredientNamesResponse>
{
    public async Task<Result<IngredientNamesResponse>> Handle(
        GetIngredientNamesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Ingredients.GetNames");

        var result = await IngredientAccess.ReadAsync(
            households,
            query.HouseholdId,
            query.UserId,
            async token =>
            {
                var uses = await names.UsesAsync(query.HouseholdId, token).ConfigureAwait(false);

                return new IngredientNamesResponse { Items = [.. uses.Select(IngredientMappings.ToContract)] };
            },
            cancellationToken).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class GetIngredientMergesQueryHandler(
    IIngredientNameRepository names,
    IHouseholdRepository households)
    : IQueryHandler<GetIngredientMergesQuery, IngredientMergesResponse>
{
    public async Task<Result<IngredientMergesResponse>> Handle(
        GetIngredientMergesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Ingredients.GetMerges");

        var result = await IngredientAccess.ReadAsync(
            households,
            query.HouseholdId,
            query.UserId,
            async token =>
            {
                var merges = await names.MergesAsync(query.HouseholdId, token).ConfigureAwait(false);

                return new IngredientMergesResponse { Items = [.. merges.Select(IngredientMappings.ToContract)] };
            },
            cancellationToken).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class CreateIngredientMergeCommandHandler(
    IIngredientNameRepository names,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CreateIngredientMergeCommand, CreatedIngredientMerge>
{
    public async Task<Result<CreatedIngredientMerge>> Handle(
        CreateIngredientMergeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Ingredients.CreateMerge");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Bind(() => IngredientMerge.Create(command.From, command.To)).Match(
            _ => unitOfWork.InTransactionAsync(token => MergeAsync(command, token), cancellationToken),
            error => Task.FromResult(Result<CreatedIngredientMerge>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    // Merging into a name that is itself merged away follows it, so rules stay one step deep.
    private async Task<Result<CreatedIngredientMerge>> MergeAsync(
        CreateIngredientMergeCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await names.MergesAsync(command.HouseholdId, cancellationToken).ConfigureAwait(false);
        var kept = new IngredientMerges(existing).Resolve(command.To.Trim());

        return await IngredientMerge.Create(command.From, kept).Match(
            async merge =>
            {
                var rewritten = await names
                    .MergeAsync(command.HouseholdId, merge, time.GetUtcNow(), cancellationToken)
                    .ConfigureAwait(false);

                return Result<CreatedIngredientMerge>.Success(new CreatedIngredientMerge
                {
                    From = merge.From.Value,
                    To = merge.To.Value,
                    RecipeCount = rewritten
                });
            },
            error => Task.FromResult(Result<CreatedIngredientMerge>.Failure(error))).ConfigureAwait(false);
    }
}

internal sealed class DeleteIngredientMergeCommandHandler(
    IIngredientNameRepository names,
    IHouseholdRepository households)
    : ICommandHandler<DeleteIngredientMergeCommand>
{
    public async Task<Result> Handle(DeleteIngredientMergeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Ingredients.DeleteMerge");

        var result = await IngredientAccess.ChangeAsync(
            households,
            command.HouseholdId,
            command.UserId,
            command.From,
            (key, token) => names.RemoveMergeAsync(command.HouseholdId, key, token),
            cancellationToken).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class GetMergeSuggestionsQueryHandler(
    IIngredientNameRepository names,
    IHouseholdRepository households)
    : IQueryHandler<GetMergeSuggestionsQuery, MergeSuggestionsResponse>
{
    public async Task<Result<MergeSuggestionsResponse>> Handle(
        GetMergeSuggestionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Ingredients.GetMergeSuggestions");

        var result = await IngredientAccess.ReadAsync(
            households,
            query.HouseholdId,
            query.UserId,
            async token =>
            {
                var uses = await names.UsesAsync(query.HouseholdId, token).ConfigureAwait(false);
                var keptApart = await names.KeptApartAsync(query.HouseholdId, token).ConfigureAwait(false);

                return new MergeSuggestionsResponse
                {
                    Items = [.. IngredientLookalikes.Find(uses, keptApart).Select(IngredientMappings.ToContract)]
                };
            },
            cancellationToken).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class DeleteMergeSuggestionCommandHandler(
    IIngredientNameRepository names,
    IHouseholdRepository households,
    TimeProvider time)
    : ICommandHandler<DeleteMergeSuggestionCommand>
{
    public async Task<Result> Handle(DeleteMergeSuggestionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Ingredients.DeleteMergeSuggestion");

        var result = await IngredientAccess.ChangeAsync(
            households,
            command.HouseholdId,
            command.UserId,
            command.Name,
            (key, token) => names.KeepApartAsync(command.HouseholdId, key, time.GetUtcNow(), token),
            cancellationToken).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

/// <summary>Membership first, then the work: every ingredient operation is open to any member.</summary>
internal static class IngredientAccess
{
    internal static async Task<Result<T>> ReadAsync<T>(
        IHouseholdRepository households,
        Guid householdId,
        Guid userId,
        Func<CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
        where T : notnull
    {
        var allowed = await HouseholdAccess
            .MemberOfAsync(households, householdId, userId, cancellationToken)
            .ConfigureAwait(false);

        return await allowed.Match(
            async () => Result<T>.Success(await read(cancellationToken).ConfigureAwait(false)),
            error => Task.FromResult(Result<T>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>Folds <paramref name="name"/> and hands the key to <paramref name="change"/>.</summary>
    internal static async Task<Result> ChangeAsync(
        IHouseholdRepository households,
        Guid householdId,
        Guid userId,
        string name,
        Func<string, CancellationToken, Task> change,
        CancellationToken cancellationToken)
    {
        var allowed = await HouseholdAccess
            .MemberOfAsync(households, householdId, userId, cancellationToken)
            .ConfigureAwait(false);

        return await allowed
            .Bind(() => ItemName.Create(name).Match(
                Result<ItemName>.Success,
                _ => IngredientErrors.InvalidName))
            .Match(
                async valid =>
                {
                    await change(valid.ComparisonKey, cancellationToken).ConfigureAwait(false);

                    return Result.Success();
                },
                error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
    }
}

/// <summary>Domain ingredient types to their wire shapes.</summary>
internal static class IngredientMappings
{
    internal static IngredientNameUse ToContract(IngredientUse use) =>
        new() { Name = use.Name, RecipeCount = use.RecipeCount };

    internal static IngredientMergeDetail ToContract(IngredientMerge merge) =>
        new() { From = merge.From.Value, To = merge.To.Value };

    internal static MergeSuggestionDetail ToContract(MergeSuggestion suggestion) =>
        new()
        {
            From = ToContract(suggestion.From),
            To = ToContract(suggestion.To),
            Reason = suggestion.Reason switch
            {
                LookalikeReason.Spelling => "spelling",
                LookalikeReason.SameFood => "same_food",
                _ => "typo"
            }
        };
}
