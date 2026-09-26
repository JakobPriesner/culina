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
/// <param name="CookbookId">Which shelf.</param>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record AddRecipeToCookbookCommand(Guid CookbookId, Guid RecipeId, Guid UserId);

/// <summary>Takes a recipe off a shelf.</summary>
/// <param name="CookbookId">Which shelf.</param>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record RemoveRecipeFromCookbookCommand(Guid CookbookId, Guid RecipeId, Guid UserId);

/// <summary>Which cookbooks a recipe is on.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="HouseholdId">Whose shelves to look on, or null for the recipe's own household.</param>
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
                // Its rules are its whole membership. Accepting a row here
                // would create a recipe on the shelf that nothing could
                // explain and the next read would not return.
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
        // Through the recipe, the way a planned meal is checked: one step
        // proves both that the caller can see it and that the shelf's
        // household holds it, as its own or by inheritance.
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

        // Only when something actually went on. A recipe already on the shelf
        // is a success that changed nothing, and bumping the version for it
        // would throw away every cached copy of the cookbook to say so.
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

                    // Taking off something that was never on is the outcome the
                    // caller wanted, so it succeeds rather than reporting a
                    // recipe nobody asked about.
                    return Result.Success();
                },
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
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
                // The shelves of the household asking: an inherited recipe
                // is on this kitchen's shelves, not on the ones it came from.
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
