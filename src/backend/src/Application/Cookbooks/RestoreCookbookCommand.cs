using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Cookbooks;
using Domain.Shared;

namespace Application.Cookbooks;

/// <summary>Takes a cookbook out of its household's bin, with the recipes it held.</summary>
/// <param name="CookbookId">Which cookbook.</param>
/// <param name="UserId">Who is asking; any member of the household may.</param>
public sealed record RestoreCookbookCommand(Guid CookbookId, Guid UserId);

internal sealed class RestoreCookbookCommandHandler(
    ITrashRepository trash,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RestoreCookbookCommand>
{
    public async Task<Result> Handle(RestoreCookbookCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Cookbooks.Restore");

        var householdId = await trash
            .HouseholdOfDeletedCookbookAsync(command.CookbookId, cancellationToken)
            .ConfigureAwait(false);

        var permitted = householdId is { } id
            && await households.IsMemberAsync(id, command.UserId, cancellationToken).ConfigureAwait(false);

        Result result = permitted
            ? await unitOfWork.InTransactionAsync(
                async token => await trash.RestoreCookbookAsync(command.CookbookId, token).ConfigureAwait(false)
                    ? Result.Success()
                    : Result.Failure(CookbookErrors.NotFound(command.CookbookId)),
                cancellationToken).ConfigureAwait(false)
            : CookbookErrors.NotFound(command.CookbookId);

        return tracked.Record(result);
    }
}
