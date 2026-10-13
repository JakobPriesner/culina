using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Contracts.Cookbooks;
using Domain.Cookbooks;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Cookbooks;

/// <summary>Puts a recipe on a shelf.</summary>
public sealed record AddRecipeToCookbookCommand(Guid CookbookId, Guid RecipeId, Guid UserId);

/// <summary>Takes a recipe off a shelf.</summary>
public sealed record RemoveRecipeFromCookbookCommand(Guid CookbookId, Guid RecipeId, Guid UserId);

/// <summary>Arranges a chosen shelf: every recipe on it, in the order it should stand.</summary>
public sealed record ReorderCookbookRecipesCommand(
    Guid CookbookId,
    Guid UserId,
    long ExpectedVersion,
    IReadOnlyList<Guid> RecipeIds);

/// <summary>Which cookbooks a recipe is on.</summary>
public sealed record GetRecipeCookbooksQuery(Guid RecipeId, Guid UserId, Guid? HouseholdId);

internal sealed class AddRecipeToCookbookCommandHandler(
    ICookbookRepository cookbooks,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<AddRecipeToCookbookCommand>
{
    public async Task<Result> Handle(
        AddRecipeToCookbookCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Cookbooks.AddRecipe");

        var shelf = await CookbookAccess
            .VisibleAsync(cookbooks, households, command.CookbookId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await shelf.Match(
            found => found.Cookbook.Kind == CookbookKind.Smart
                // Its rules are its whole membership; accepting a row would put a recipe on it that nothing explains.
                ? Task.FromResult(Result.Failure(CookbookErrors.RulesDecideMembership))
                : AddAsync(command, found, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result> AddAsync(
        AddRecipeToCookbookCommand command,
        CookbookOnAShelf shelf,
        CancellationToken cancellationToken)
    {
        // Through the recipe: one step proves the caller can see it and the shelf's household holds it (own or inherited).
        var recipe = await RecipeAccess
            .VisibleInAsync(
                recipes, households, command.RecipeId, shelf.Cookbook.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        return await recipe.Match(
            _ => unitOfWork.InTransactionAsync(
                token => WriteAsync(command, token),
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result> WriteAsync(
        AddRecipeToCookbookCommand command,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        var written = await cookbooks
            .AddRecipeAsync(command.CookbookId, command.RecipeId, command.UserId, now, cancellationToken)
            .ConfigureAwait(false);

        // Only when something went on: a recipe already there changed nothing, and a bump would drop cached copies.
        if (written)
        {
            await cookbooks.TouchAsync(command.CookbookId, now, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}

internal sealed class RemoveRecipeFromCookbookCommandHandler(
    ICookbookRepository cookbooks,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<RemoveRecipeFromCookbookCommand>
{
    public async Task<Result> Handle(
        RemoveRecipeFromCookbookCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Cookbooks.RemoveRecipe");

        var shelf = await CookbookAccess
            .VisibleAsync(cookbooks, households, command.CookbookId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await shelf.Match(
            found => found.Cookbook.Kind == CookbookKind.Smart
                ? Task.FromResult(Result.Failure(CookbookErrors.RulesDecideMembership))
                : unitOfWork.InTransactionAsync(
                async token =>
                {
                    var now = time.GetUtcNow();

                    var removed = await cookbooks
                        .RemoveRecipeAsync(command.CookbookId, command.RecipeId, token)
                        .ConfigureAwait(false);

                    if (removed)
                    {
                        await cookbooks.TouchAsync(command.CookbookId, now, token).ConfigureAwait(false);
                    }

                    // Taking off something never on is the outcome wanted, so it succeeds.
                    return Result.Success();
                },
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class ReorderCookbookRecipesCommandHandler(
    ICookbookRepository cookbooks,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<ReorderCookbookRecipesCommand, CookbookDetail>
{
    public async Task<Result<CookbookDetail>> Handle(
        ReorderCookbookRecipesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Cookbooks.Reorder");

        var shelf = await CookbookAccess
            .VisibleAsync(cookbooks, households, command.CookbookId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await shelf.Match(
            found => found.Cookbook.Kind == CookbookKind.Smart
                // Its rules decide what is on it, and it reads newest first: there is no order to choose.
                ? Task.FromResult(Result<CookbookDetail>.Failure(CookbookErrors.RulesDecideMembership))
                : unitOfWork.InTransactionAsync(
                    token => ReorderAsync(command, found.Cookbook, token),
                    cancellationToken),
            error => Task.FromResult(Result<CookbookDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<CookbookDetail>> ReorderAsync(
        ReorderCookbookRecipesCommand command,
        Cookbook cookbook,
        CancellationToken cancellationToken)
    {
        cookbook.Touch(time.GetUtcNow());

        // The version first: a shelf changed since it was read is 412, so the client reads it again, rather than
        // 400 for an order that was right when it was made. A failure below rolls the bump back.
        var saved = await cookbooks
            .SaveAsync(cookbook, command.ExpectedVersion, cancellationToken)
            .ConfigureAwait(false);

        return await saved.Match(
            async _ =>
            {
                var onShelf = await cookbooks.RecipeIdsAsync(cookbook.Id, cancellationToken).ConfigureAwait(false);

                // The same recipes, each once: a client that read the shelf right and sent something else.
                if (command.RecipeIds.Count != onShelf.Count || !command.RecipeIds.ToHashSet().SetEquals(onShelf))
                {
                    return CookbookErrors.OrderMismatch;
                }

                await cookbooks.ReorderAsync(cookbook.Id, command.RecipeIds, cancellationToken).ConfigureAwait(false);

                // Read again: the cover follows the shelf's order.
                var arranged = await cookbooks.DescribeAsync(cookbook.Id, cancellationToken).ConfigureAwait(false);

                return arranged.Map(one => one.ToDetail());
            },
            error => Task.FromResult(Result<CookbookDetail>.Failure(error))).ConfigureAwait(false);
    }
}

internal sealed class GetRecipeCookbooksQueryHandler(
    ICookbookRepository cookbooks,
    IRecipeRepository recipes,
    IHouseholdRepository households)
    : IQueryHandler<GetRecipeCookbooksQuery, RecipeCookbooksResponse>
{
    public async Task<Result<RecipeCookbooksResponse>> Handle(
        GetRecipeCookbooksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Cookbooks.GetForRecipe");

        var recipe = await RecipeAccess
            .VisibleInAsync(recipes, households, query.RecipeId, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await recipe.Match(
            async found =>
            {
                // The asking household's shelves: an inherited recipe is on this kitchen's shelves.
                var shelves = await cookbooks
                    .ContainingAsync(found.Id, query.HouseholdId ?? found.HouseholdId, cancellationToken)
                    .ConfigureAwait(false);

                return Result<RecipeCookbooksResponse>.Success(shelves.ToResponse());
            },
            error => Task.FromResult(Result<RecipeCookbooksResponse>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }
}
