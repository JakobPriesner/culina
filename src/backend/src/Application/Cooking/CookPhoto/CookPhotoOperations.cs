using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Recipes.GetCookLog;
using Application.Recipes.GetImage;
using Application.Telemetry;
using Contracts.Recipes.GetCookLog;
using Domain.Cooking;
using Domain.Shared;

namespace Application.Cooking.CookPhoto;

/// <summary>
/// A picture of one attempt: hung on it, taken off it, and served.
/// </summary>
/// <remarks>Personal like the note and the log: scoped to the caller's own entries in SQL, so another's attempt is not found rather than forbidden.</remarks>
public sealed record SetCookPhotoCommand(Guid EntryId, Guid UserId, Stream Content);

/// <summary>Takes the picture off an attempt.</summary>
public sealed record RemoveCookPhotoCommand(Guid EntryId, Guid UserId);

/// <summary>Finds an attempt's picture.</summary>
public sealed record GetCookPhotoQuery(Guid EntryId, Guid UserId, int Width);

internal sealed class SetCookPhotoCommandHandler(
    ICookLogRepository log,
    IImageStore images,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetCookPhotoCommand, Response>
{
    public async Task<Result<Response>> Handle(
        SetCookPhotoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Cooking.SetCookPhoto");

        var found = await log
            .FindAsync(command.EntryId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            entry => StoreAsync(entry, command, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> StoreAsync(
        CookLogEntry entry,
        SetCookPhotoCommand command,
        CancellationToken cancellationToken)
    {
        // Written to the volume before the row: a file with no row is sweepable, a row with no file is a broken image.
        var stored = await images
            .StoreAsync(command.Content, cancellationToken)
            .ConfigureAwait(false);

        return await stored.Match(
            image => unitOfWork.InTransactionAsync(
                async token =>
                {
                    var photo = new Domain.Cooking.CookPhoto(image.ContentHash, image.Width, image.Height);

                    var written = await log
                        .SetPhotoAsync(entry.Id, entry.UserId, photo, token)
                        .ConfigureAwait(false);

                    return await written.Match(
                        () => ReadBackAsync(log, entry, token),
                        error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
                },
                cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
    }

    // The whole log, since the screen shows a strip of every attempt.
    internal static async Task<Result<Response>> ReadBackAsync(
        ICookLogRepository log,
        CookLogEntry entry,
        CancellationToken cancellationToken) =>
        (await log.ForRecipeAsync(entry.RecipeId, entry.UserId, cancellationToken)
            .ConfigureAwait(false)).ToResponse();
}

internal sealed class RemoveCookPhotoCommandHandler(ICookLogRepository log, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveCookPhotoCommand, Response>
{
    public async Task<Result<Response>> Handle(
        RemoveCookPhotoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Cooking.RemoveCookPhoto");

        var found = await log
            .FindAsync(command.EntryId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            entry => unitOfWork.InTransactionAsync(
                async token =>
                {
                    // The file is left alone: it is content-addressed, may be another attempt's, and a sweep reclaims it.
                    var written = await log
                        .SetPhotoAsync(entry.Id, entry.UserId, photo: null, token)
                        .ConfigureAwait(false);

                    return await written.Match(
                        () => SetCookPhotoCommandHandler.ReadBackAsync(log, entry, token),
                        error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
                },
                cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class GetCookPhotoQueryHandler(ICookLogRepository log, IImageStore images)
    : IQueryHandler<GetCookPhotoQuery, ImageDelivery>
{
    public async Task<Result<ImageDelivery>> Handle(
        GetCookPhotoQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Cooking.GetCookPhoto");

        var found = await log
            .FindAsync(query.EntryId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = found.Match(
            entry => entry.Photo is { } photo
                ? ImageDelivery.Of(images, photo.ContentHash, query.Width)
                : Result<ImageDelivery>.Failure(CookingErrors.EntryNotFound),
            Result<ImageDelivery>.Failure);

        return tracked.Record(result);
    }
}
