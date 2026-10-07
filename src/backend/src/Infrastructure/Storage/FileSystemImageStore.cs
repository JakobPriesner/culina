using System.Security.Cryptography;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Recipes;
using Domain.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Memory;
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

    /// <summary>
    /// The first frame only, of an animated GIF, WebP or PNG or a multi-page
    /// TIFF.
    /// </summary>
    /// <remarks>
    /// The pixel ceiling is one frame's, and every frame decodes onto a canvas
    /// the size of the whole image: a GIF under a kilobyte can declare a large
    /// screen and thousands of frames, and each one is a full allocation. A
    /// recipe photo is a still, so nothing past the first is worth decoding.
    /// </remarks>
    private static readonly DecoderOptions FirstFrameOnly = new()
    {
        MaxFrames = 1,
        Configuration = Bounded()
    };

    /// <summary>
    /// The largest single buffer the decoder may ask for, in megabytes.
    /// </summary>
    /// <remarks>
    /// A little over one frame at the pixel ceiling, four bytes a pixel (244
    /// MB). The pixel count alone does not bound memory, because a pixel is not
    /// always four bytes: a sixteen-bit PNG decodes to eight, so an image well
    /// inside the pixel ceiling could still ask for half a gigabyte. Past this
    /// the allocator refuses instead of allocating, and the upload is told it
    /// has too many pixels.
    /// </remarks>
    private const int MostBufferMegabytes = 256;

    /// <summary>
    /// How many images may be decoded at once, across every upload.
    /// </summary>
    /// <remarks>
    /// The two ceilings above bound one image; nothing bounded how many. A
    /// handful of the largest permitted photos arriving together — a recipe
    /// photo, a cook photo and a library import at once — was a handful of
    /// quarter gigabytes, plus a rotated copy of each. Two at a time keeps the
    /// decoder's share of memory fixed however many arrive, and an upload is
    /// rare enough that the next one waiting a second costs nothing.
    /// </remarks>
    private static readonly SemaphoreSlim Decoding = new(2, 2);

    /// <summary>
    /// The longest side of a picture sent to an assistant to be read.
    /// </summary>
    /// <remarks>
    /// About what the providers scale a picture down to anyway — OpenAI fits
    /// it inside 2048 square — so anything larger is bytes sent abroad to be
    /// thrown away. A phone screenshot or a cookbook page is still sharp here.
    /// </remarks>
    private const int LongestReadingSide = 2048;

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

    public async Task<Result<RecipePicture>> ReEncodeForReadingAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (content.Length > settings.MaxImageBytes)
        {
            return ImageErrors.TooLarge(settings.MaxImageBytes);
        }

        var buffered = new MemoryStream(content.ToArray(), writable: false);

        await using (buffered.ConfigureAwait(false))
        {
            return await DecodeAsync(buffered, image => ForReadingAsync(image, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
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

    private Task<Result<StoredImage>> ReEncodeAsync(
        Stream buffered,
        CancellationToken cancellationToken) =>
        DecodeAsync(buffered, image => StoreRenditionsAsync(image, cancellationToken), cancellationToken);

    private async Task<StoredImage> StoreRenditionsAsync(Image image, CancellationToken cancellationToken)
    {
        var renditions = await RenderAsync(image, cancellationToken).ConfigureAwait(false);
        var hash = Convert.ToHexStringLower(SHA256.HashData(renditions[^1].Bytes));

        foreach (var rendition in renditions)
        {
            await WriteAsync(hash, rendition).ConfigureAwait(false);
        }

        return new StoredImage(hash, image.Width, image.Height, renditions[^1].Bytes.Length);
    }

    /// <summary>
    /// A JPEG a model can read, and nothing else.
    /// </summary>
    /// <remarks>
    /// Flattened onto white first: a JPEG has no transparency, and black text
    /// on a transparent screenshot would otherwise come out black on black.
    /// </remarks>
    private static async Task<RecipePicture> ForReadingAsync(Image image, CancellationToken cancellationToken)
    {
        image.Mutate(context =>
        {
            context.BackgroundColor(Color.White);

            if (image.Width > LongestReadingSide || image.Height > LongestReadingSide)
            {
                context.Resize(new ResizeOptions
                {
                    Size = new Size(LongestReadingSide, LongestReadingSide),
                    Mode = ResizeMode.Max
                });
            }
        });

        var encoded = new MemoryStream();

        await using (encoded.ConfigureAwait(false))
        {
            // Without metadata, for the same reason as a stored rendition.
            await image
                .SaveAsync(encoded, new JpegEncoder { Quality = 90, SkipMetadata = true }, cancellationToken)
                .ConfigureAwait(false);

            return new RecipePicture(encoded.ToArray(), "image/jpeg");
        }
    }

    /// <summary>
    /// Opens an untrusted image and hands it over the right way up.
    /// </summary>
    /// <remarks>
    /// The one way into the decoder, so that every image decoded here passes
    /// the same checks: the header before any pixel, the first frame only, no
    /// buffer past the ceiling, and only so many at once.
    /// </remarks>
    private static async Task<Result<TOut>> DecodeAsync<TOut>(
        Stream buffered,
        Func<Image, Task<TOut>> use,
        CancellationToken cancellationToken)
        where TOut : notnull
    {
        ImageInfo header;

        try
        {
            // The header first, and only the header. A byte limit does not bound
            // a pixel count: a few kilobytes of PNG can describe fifty thousand
            // pixels square, and decoding it to find that out is the whole of
            // the attack. Reading the dimensions costs nothing.
            header = await Image.IdentifyAsync(FirstFrameOnly, buffered, cancellationToken).ConfigureAwait(false);
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

        await Decoding.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Decoding is the format check. A content type and an extension are
            // both attacker-supplied, and neither says what the bytes are.
            using var image = await Image.LoadAsync(FirstFrameOnly, buffered, cancellationToken).ConfigureAwait(false);

            // A phone writes a photograph taken upright as landscape pixels
            // plus an orientation tag, and that tag is the only thing saying
            // which way up it goes. The re-encoding drops every tag — which is
            // the entire point of it, for the one that says where the kitchen
            // is — so the rotation has to be turned into pixels first. Without
            // this, a photograph held upright is served on its side, and no
            // amount of centring it in the frame puts the dish back in the
            // middle.
            image.Mutate(context => context.AutoOrient());

            // Then nothing but pixels. Every encoder here is also told to skip
            // metadata, but not every one listens: ImageSharp's JPEG encoder
            // writes the EXIF profile, coordinates and all, with SkipMetadata
            // set. Dropping the profiles is what does not depend on that.
            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IptcProfile = null;

            return Result<TOut>.Success(await use(image).ConfigureAwait(false));
        }
        catch (Exception failure) when (OverTheCeiling(failure))
        {
            return ImageErrors.TooManyPixels;
        }
        catch (Exception failure) when (failure is UnknownImageFormatException or InvalidImageContentException)
        {
            return ImageErrors.Unreadable;
        }
        finally
        {
            Decoding.Release();
        }
    }

    /// <summary>
    /// A buffer the allocator refused, however it surfaced.
    /// </summary>
    /// <remarks>
    /// A decoder wraps the refusal as content it could not read; resizing and
    /// rotating throw it as it is. Either way the image was too big, not broken.
    /// </remarks>
    private static bool OverTheCeiling(Exception failure) =>
        failure is InvalidMemoryOperationException
        || failure.InnerException is InvalidMemoryOperationException;

    private static Configuration Bounded()
    {
        var configuration = Configuration.Default.Clone();

        configuration.MemoryAllocator = MemoryAllocator.Create(
            new MemoryAllocatorOptions { AllocationLimitMegabytes = MostBufferMegabytes });

        return configuration;
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

    /// <summary>
    /// Writes beside the final path and moves into it, so the address only
    /// ever holds a whole file.
    /// </summary>
    /// <remarks>
    /// A file at its address is never written again, so one cut off halfway
    /// would be served, broken, under a URL cached forever. Nor is the
    /// request's token passed: a client hanging up after the photo is
    /// rendered is no reason to throw the renditions away.
    /// </remarks>
    private async Task WriteAsync(string hash, Rendition rendition)
    {
        var path = PathFor(hash, rendition.Width);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (File.Exists(path))
        {
            // Content-addressed, so identical bytes are already there.
            return;
        }

        var temporary = $"{path}.{Guid.NewGuid():n}.tmp";

        await File.WriteAllBytesAsync(temporary, rendition.Bytes, CancellationToken.None).ConfigureAwait(false);

        try
        {
            File.Move(temporary, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            // A concurrent store of the same photo moved in first.
            File.Delete(temporary);
        }
    }

    /// <summary>
    /// Two levels of hash-prefix directories, so a large instance does not end
    /// up with a hundred thousand files in one folder.
    /// </summary>
    private string PathFor(string hash, int width) =>
        Path.Combine(settings.ImagePath, hash[..2], hash[2..4], $"{hash}-{width}.webp");

    private sealed record Rendition(int Width, byte[] Bytes);
}
