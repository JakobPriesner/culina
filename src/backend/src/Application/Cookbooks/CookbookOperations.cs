using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Contracts.Cookbooks;
using Domain.Cookbooks;
using Domain.Shared;

namespace Application.Cookbooks;

/// <summary>A page of a household's cookbooks.</summary>
/// <param name="HouseholdId">Whose shelves.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Cursor">Where to resume, or null for the first page.</param>
/// <param name="Limit">How many at most.</param>
public sealed record GetCookbooksQuery(Guid HouseholdId, Guid UserId, string? Cursor, int Limit);

/// <summary>One cookbook.</summary>
/// <param name="CookbookId">Which one.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetCookbookQuery(Guid CookbookId, Guid UserId);

/// <summary>Starts a cookbook.</summary>
/// <param name="UserId">Whose idea it is.</param>
/// <param name="Draft">What to call it, and what it is for.</param>
public sealed record CreateCookbookCommand(Guid UserId, CreateCookbookRequest Draft);

/// <summary>Renames a cookbook.</summary>
/// <param name="CookbookId">Which one.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="ExpectedVersion">The version the caller was holding.</param>
/// <param name="Draft">What it should now say.</param>
public sealed record UpdateCookbookCommand(
    Guid CookbookId,
    Guid UserId,
    long ExpectedVersion,
    UpdateCookbookRequest Draft);

/// <summary>Removes a cookbook, leaving every recipe that was on it.</summary>
/// <param name="CookbookId">Which one.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record DeleteCookbookCommand(Guid CookbookId, Guid UserId);

internal sealed class GetCookbooksQueryHandler(
    ICookbookRepository cookbooks,
    IHouseholdRepository households)
    : IQueryHandler<GetCookbooksQuery, CookbooksResponse>
{
    public async Task<Result<CookbooksResponse>> Handle(
        GetCookbooksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Cookbooks.GetAll");

        var allowed = await RecipeAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () =>
            {
                var page = await cookbooks
                    .ListAsync(query.HouseholdId, query.Cursor, query.Limit, cancellationToken)
                    .ConfigureAwait(false);

                return Result<CookbooksResponse>.Success(page.ToResponse());
            },
            error => Task.FromResult(Result<CookbooksResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class GetCookbookQueryHandler(
    ICookbookRepository cookbooks,
    IHouseholdRepository households)
    : IQueryHandler<GetCookbookQuery, CookbookDetail>
{
    public async Task<Result<CookbookDetail>> Handle(
        GetCookbookQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Cookbooks.GetById");

        var found = await CookbookAccess
            .VisibleAsync(cookbooks, households, query.CookbookId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(found.Map(shelf => shelf.ToDetail()));
    }
}

internal sealed class CreateCookbookCommandHandler(
    ICookbookRepository cookbooks,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CreateCookbookCommand, CookbookDetail>
{
    public async Task<Result<CookbookDetail>> Handle(
        CreateCookbookCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Cookbooks.Create");

        var allowed = await RecipeAccess
            .MemberOfAsync(households, command.Draft.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Rules in the request are what makes a shelf that fills itself. There
        // is no separate flag to disagree with them.
        var prepared = allowed
            .Bind(() => CookbookName.Create(command.Draft.Name))
            .Bind(name => CookbookWords
                .ToRules(command.Draft.Rules)
                .Bind(rules => command.Draft.Rules is null
                    ? Cookbook.Create(
                        command.Draft.HouseholdId,
                        name,
                        command.Draft.Description,
                        command.UserId,
                        time.GetUtcNow())
                    : Cookbook.CreateSmart(
                        command.Draft.HouseholdId,
                        name,
                        command.Draft.Description,
                        rules,
                        command.UserId,
                        time.GetUtcNow())));

        var result = await prepared.Match(
            cookbook => unitOfWork.InTransactionAsync(
                async token =>
                {
                    var written = await cookbooks.AddAsync(cookbook, token).ConfigureAwait(false);

                    return await written.Match(
                        // A shelf somebody fills starts empty, and saying so
                        // costs nothing. One that fills itself is already full
                        // the moment it exists — that is the entire point of it
                        // — so it has to be read back to find out how full.
                        async () => cookbook.Kind == CookbookKind.Manual
                            ? Result<CookbookDetail>.Success(
                                new CookbookOnAShelf(cookbook, 0, []).ToDetail())
                            : (await cookbooks
                                .DescribeAsync(cookbook.Id, token)
                                .ConfigureAwait(false))
                                .Map(shelf => shelf.ToDetail()),
                        error => Task.FromResult(Result<CookbookDetail>.Failure(error)))
                        .ConfigureAwait(false);
                },
                cancellationToken),
            error => Task.FromResult(Result<CookbookDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class UpdateCookbookCommandHandler(
    ICookbookRepository cookbooks,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<UpdateCookbookCommand, CookbookDetail>
{
    public async Task<Result<CookbookDetail>> Handle(
        UpdateCookbookCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Cookbooks.Update");

        var found = await CookbookAccess
            .VisibleAsync(cookbooks, households, command.CookbookId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            shelf => unitOfWork.InTransactionAsync(
                token => RenameAsync(command, shelf, token),
                cancellationToken),
            error => Task.FromResult(Result<CookbookDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<CookbookDetail>> RenameAsync(
        UpdateCookbookCommand command,
        CookbookOnAShelf shelf,
        CancellationToken cancellationToken)
    {
        var renamed = CookbookName
            .Create(command.Draft.Name)
            .Bind(name => CookbookWords
                .ToRules(command.Draft.Rules)
                .Bind(rules => shelf.Cookbook.Revise(
                    name,
                    command.Draft.Description,
                    command.Draft.Rules is null ? null : rules,
                    time.GetUtcNow())));

        return await renamed.Match(
            async () =>
            {
                var saved = await cookbooks
                    .SaveAsync(shelf.Cookbook, command.ExpectedVersion, cancellationToken)
                    .ConfigureAwait(false);

                return saved.Map(version =>
                {
                    shelf.Cookbook.AcceptVersion(version);

                    return shelf.ToDetail();
                });
            },
            error => Task.FromResult(Result<CookbookDetail>.Failure(error))).ConfigureAwait(false);
    }
}

internal sealed class DeleteCookbookCommandHandler(
    ICookbookRepository cookbooks,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteCookbookCommand>
{
    public async Task<Result> Handle(
        DeleteCookbookCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Cookbooks.Delete");

        var found = await CookbookAccess
            .VisibleAsync(cookbooks, households, command.CookbookId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            shelf => unitOfWork.InTransactionAsync(
                async token =>
                {
                    // Only the shelf. Every recipe that was on it stays exactly
                    // where it was — a cookbook is a pointer, and deleting one
                    // deletes no food.
                    await cookbooks.DeleteAsync(shelf.Cookbook.Id, token).ConfigureAwait(false);

                    return Result.Success();
                },
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
