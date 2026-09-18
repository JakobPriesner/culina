using System.Security.Cryptography;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Recipes;
using Domain.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Infrastructure.Storage;

/// <summary>
/// Stores recipe images on a volume, content-addressed.
/// </summary>
/// <remarks>
/// <para>
/// On the filesystem rather than in the database: an image is large, immutable
/// and served straight through, and putting megabytes of it through the
/// connection pool would make every other query wait behind a photograph.
/// </para>
/// <para>
/// Content-addressed, so uploading the same photo twice costs nothing and a
/// rendition can be cached forever by its address.
/// </para>
/// </remarks>
/// <param name="settings">Where images live and how big one may be.</param>
internal sealed class FileSystemImageStore(StorageSettings settings) : IImageStore
{
    /// <summary>
    /// A ceiling on decoded pixels, checked before any resizing work happens.
    /// </summary>
    /// <remarks>
    /// This is the decompression-bomb guard: a 1 KB PNG can declare 50000 by
    /// 50000 pixels, and a decoder that believes it allocates ten gigabytes.
    /// </remarks>
    private const int MaxPixels = 8000 * 8000;

    public async Task<Result<StoredImage>> StoreAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var buffered = new MemoryStream();

        await using (buffered.ConfigureAwait(false))
        {
            var copied = await CopyBoundedAsync(content, buffered, cancellationToken)
                .ConfigureAwait(false);

            if (!copied)
            {
                return ImageErrors.TooLarge(settings.MaxImageBytes);
            }

            buffered.Position = 0;

            return await ReEncodeAsync(buffered, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<Result> CopyToAsync(
        string contentHash,
        int width,
        Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!ImageWidths.Exists(width))
        {
            return ImageErrors.UnknownWidth;
        }

        var path = PathFor(contentHash, width);

        if (!File.Exists(path))
        {
            return ImageErrors.NotFound;
        }

        var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        await using (file.ConfigureAwait(false))
        {
            await file.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public Task<Result> DeleteAsync(string contentHash, CancellationToken cancellationToken)
    {
        foreach (var width in ImageWidths.All)
        {
            var path = PathFor(contentHash, width);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        _ = cancellationToken;

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Copies at most one byte more than the limit, so an oversized upload is
    /// refused without ever being held in full.
    /// </summary>
    private async Task<bool> CopyBoundedAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        var total = 0L;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                return true;
            }

            total += read;

            if (total > settings.MaxImageBytes)
            {
                return false;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<Result<StoredImage>> ReEncodeAsync(
        Stream buffered,
        CancellationToken cancellationToken)
    {
        ImageInfo header;

        try
        {
            // The header first, and only the header. A byte limit does not bound
            // a pixel count: a few kilobytes of PNG can describe fifty thousand
            // pixels square, and decoding it to find that out is the whole of
            // the attack. Reading the dimensions costs nothing.
            header = await Image.IdentifyAsync(buffered, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is UnknownImageFormatException or InvalidImageContentException)
        {
            return ImageErrors.Unreadable;
        }

        if ((long)header.Width * header.Height > MaxPixels)
        {
            return ImageErrors.TooManyPixels;
        }

        buffered.Position = 0;

        Image image;

        try
        {
            // Decoding is the format check. A content type and an extension are
            // both attacker-supplied, and neither says what the bytes are.
            image = await Image.LoadAsync(buffered, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is UnknownImageFormatException or InvalidImageContentException)
        {
            return ImageErrors.Unreadable;
        }

        using (image)
        {
            // A phone writes a photograph taken upright as landscape pixels
            // plus an orientation tag, and that tag is the only thing saying
            // which way up it goes. The re-encoding drops every tag — which is
            // the entire point of it, for the one that says where the kitchen
            // is — so the rotation has to be turned into pixels first. Without
            // this, a photograph held upright is served on its side, and no
            // amount of centring it in the frame puts the dish back in the
            // middle.
            image.Mutate(context => context.AutoOrient());

            var renditions = await RenderAsync(image, cancellationToken).ConfigureAwait(false);
            var hash = Convert.ToHexStringLower(SHA256.HashData(renditions[^1].Bytes));

            foreach (var rendition in renditions)
            {
                await WriteAsync(hash, rendition, cancellationToken).ConfigureAwait(false);
            }

            return new StoredImage(hash, image.Width, image.Height, renditions[^1].Bytes.Length);
        }
    }

    private static async Task<List<Rendition>> RenderAsync(
        Image image,
        CancellationToken cancellationToken)
    {
        List<Rendition> renditions = [];

        foreach (var width in ImageWidths.All)
        {
            using var resized = image.Clone(context => context.Resize(new ResizeOptions
            {
                Size = new Size(width, 0),
                Mode = ResizeMode.Max,
                // Never upscale: a small photo enlarged is worse than a small
                // photo, and it costs bytes to be worse.
                Sampler = KnownResamplers.Lanczos3
            }));

            var encoded = new MemoryStream();

            await using (encoded.ConfigureAwait(false))
            {
                await resized
                    .SaveAsync(
                        encoded,
                        new WebpEncoder
                        {
                            Quality = 82,
                            // The re-encoding is the only place metadata can be
                            // dropped, and dropping it is the point. A photograph
                            // taken in somebody's kitchen carries where that
                            // kitchen is; an instance that served it back would be
                            // handing out its owner's address with a picture of
                            // dinner. Nothing in EXIF is worth keeping here.
                            SkipMetadata = true
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                renditions.Add(new Rendition(width, encoded.ToArray()));
            }
        }

        return renditions;
    }

    private async Task WriteAsync(
        string hash,
        Rendition rendition,
        CancellationToken cancellationToken)
    {
        var path = PathFor(hash, rendition.Width);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (File.Exists(path))
        {
            // Content-addressed, so identical bytes are already there.
            return;
        }

        await File.WriteAllBytesAsync(path, rendition.Bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Two levels of hash-prefix directories, so a large instance does not end
    /// up with a hundred thousand files in one folder.
    /// </summary>
    private string PathFor(string hash, int width) =>
        Path.Combine(settings.ImagePath, hash[..2], hash[2..4], $"{hash}-{width}.webp");

    private sealed record Rendition(int Width, byte[] Bytes);
}
