using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Recipes;
using Domain.Shared;
using Response = Contracts.Recipes.Share.Response;

namespace Application.Recipes.CreateShare;

/// <summary>Publishes a recipe behind a link, or returns the link it already has.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is publishing it.</param>
public sealed record CreateShareCommand(Guid RecipeId, Guid UserId);

internal sealed class CreateShareCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IRecipeShareRepository shares,
    ISecretTokens tokens,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CreateShareCommand, Response>
{
    public async Task<Result<Response>> Handle(
        CreateShareCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.CreateShare");

        var editable = await RecipeAccess
            .EditableHouseholdAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await editable.Match(
            _ => PublishAsync(command.RecipeId, command.UserId, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> PublishAsync(
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Minted up front and discarded if already published: read-then-write would race into two links.
        var share = new RecipeShare(recipeId, tokens.NewToken(), userId, time.GetUtcNow());

        var stored = await unitOfWork.InTransactionAsync(
            token => shares.AddOrKeepAsync(share, token),
            cancellationToken).ConfigureAwait(false);

        return stored.Map(kept => new Response
        {
            Token = kept.Token,
            CreatedAt = kept.CreatedAt
        });
    }
}
