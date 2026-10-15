using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Recipes.Drafts;
using Contracts.Recipes.Intake;
using Domain.Assistance;
using Domain.Shared;

namespace Application.Recipes.Intake;

/// <summary>One photograph of a request, not yet read: its size is known before a byte is.</summary>
/// <param name="Length">The size in bytes, as the upload says.</param>
/// <param name="Open">Opens the bytes.</param>
public sealed record PhotoUpload(long Length, Func<Stream> Open);

/// <summary>Asks for a durable import of text, a link or photographs.</summary>
public sealed record StartRecipeIntakeCommand(
    Guid Id,
    Guid UserId,
    Guid HouseholdId,
    string? Material,
    string? Transcript,
    string? SourceUrl,
    string? Language,
    bool FetchSource,
    IReadOnlyList<PhotoUpload> Photos);

/// <summary>Checks the photographs by size and by their bytes, then queues the import.</summary>
internal sealed class StartRecipeIntakeCommandHandler(StorageSettings storage, IntakeSubmission submission)
    : ICommandHandler<StartRecipeIntakeCommand, IntakeJob>
{
    public async Task<Result<IntakeJob>> Handle(StartRecipeIntakeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var photos = command.Photos;

        if (photos.Count > DraftLimits.MaxPhotos
            || photos.Sum(photo => photo.Length) > DraftLimits.MaxPhotoBytes
            || photos.Any(photo => photo.Length == 0 || photo.Length > storage.MaxImageBytes))
        {
            return AssistanceErrors.TooMuchToWorkFrom;
        }

        List<IntakePhoto> images = [];

        foreach (var photo in photos)
        {
            var bytes = await ReadAsync(photo, cancellationToken).ConfigureAwait(false);

            // The bytes, not a caller's Content-Type, say what it is.
            if (PictureFormat.MediaTypeOf(bytes) is not { } mediaType)
            {
                return AssistanceErrors.NothingToWorkFrom;
            }

            images.Add(new IntakePhoto(bytes, mediaType));
        }

        var material = new IntakeMaterial(
            command.Material ?? "",
            command.Transcript ?? "",
            command.SourceUrl,
            command.Language ?? "en",
            images,
            command.FetchSource);

        return await submission
            .StartAsync(command.Id, command.UserId, command.HouseholdId, material, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadAsync(PhotoUpload photo, CancellationToken cancellationToken)
    {
        var stream = photo.Open();

        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();

            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            return buffer.ToArray();
        }
    }
}
