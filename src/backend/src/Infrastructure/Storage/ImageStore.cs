using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Recipes;
using Domain.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Processing;

namespace Infrastructure.Storage;

/// <summary>Stores recipe images content-addressed, so duplicates cost nothing and renditions cache forever.</summary>
/// <param name="settings">How big an image may be.</param>
/// <param name="files">Where the renditions live: a volume or a bucket.</param>
internal sealed class ImageStore(StorageSettings settings, IImageFiles files) : IImageStore
{
    /// <summary>A ceiling on decoded pixels, checked before resizing: the decompression-bomb guard.</summary>
    private const int MaxPixels = 8000 * 8000;

    /// <summary>First frame only: every frame of an animated or multi-page image is a full-canvas allocation.</summary>
    private static readonly DecoderOptions FirstFrameOnly = new()
    {
        MaxFrames = 1,
        Configuration = Bounded()
    };

    /// <summary>The largest single decoder buffer in megabytes; pixel count alone does not bound memory (16-bit PNGs).</summary>
    private const int MostBufferMegabytes = 256;

    /// <summary>How many images may be decoded at once, bounding the decoder's memory across concurrent uploads.</summary>
    private static readonly SemaphoreSlim Decoding = new(2, 2);

    /// <summary>The longest side of a picture sent to an assistant, about what providers scale down to anyway.</summary>
    private const int LongestReadingSide = 2048;

    public async Task<Result<StoredImage>> StoreAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var buffered = new MemoryStream();

        await using (buffered.ConfigureAwait(false))
        {
            var copied = await BoundedRead.CopyAsync(content, buffered, settings.MaxImageBytes, cancellationToken)
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

        var buffered = MemoryMarshal.TryGetArray(content, out var segment)
            ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(content.ToArray(), writable: false);

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

        return await files.CopyToAsync(KeyFor(contentHash, width), destination, cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : ImageErrors.NotFound;
    }

    public async Task<Result> DeleteAsync(string contentHash, CancellationToken cancellationToken)
    {
        foreach (var width in ImageWidths.All)
        {
            await files.DeleteAsync(KeyFor(contentHash, width), cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
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
            // The request's token is deliberately not passed: a client hanging up is no reason to discard the renditions.
            await files.WriteAsync(KeyFor(hash, rendition.Width), rendition.Bytes).ConfigureAwait(false);
        }

        return new StoredImage(hash, image.Width, image.Height, renditions[^1].Bytes.Length);
    }

    /// <summary>A JPEG a model can read, flattened onto white so transparent screenshots stay legible.</summary>
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
            await image
                .SaveAsync(encoded, new JpegEncoder { Quality = 90, SkipMetadata = true }, cancellationToken)
                .ConfigureAwait(false);

            return new RecipePicture(encoded.ToArray(), "image/jpeg");
        }
    }

    /// <summary>Opens an untrusted image, the one way into the decoder: header first, first frame only, bounded buffers, limited concurrency.</summary>
    private static async Task<Result<TOut>> DecodeAsync<TOut>(
        Stream buffered,
        Func<Image, Task<TOut>> use,
        CancellationToken cancellationToken)
        where TOut : notnull
    {
        ImageInfo header;

        try
        {
            // Header only: a few KB of PNG can declare 50000x50000 pixels, and decoding it would be the attack.
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
            // Decoding is the format check: content type and extension are attacker-supplied.
            using var image = await Image.LoadAsync(FirstFrameOnly, buffered, cancellationToken).ConfigureAwait(false);

            // Phones store orientation as an EXIF tag, which the re-encoding drops, so turn it into pixels first.
            image.Mutate(context => context.AutoOrient());

            // Then only pixels: ImageSharp's JPEG encoder writes EXIF (with coordinates) even with SkipMetadata,
            // so the profiles are dropped explicitly. The ICC profile goes too: its entries are parsed lazily, on
            // first read (GHSA-gwg2-r3hj-4w44), and dropping it means nothing after decoding ever can read them.
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
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

    /// <summary>A buffer the allocator refused; decoders wrap it as unreadable content, resizing throws it as is.</summary>
    private static bool OverTheCeiling(Exception failure) =>
        failure is InvalidMemoryOperationException
        || failure.InnerException is InvalidMemoryOperationException;

    /// <summary>
    /// Only the formats Culina accepts, not Configuration.Default's every format: a TIFF, BMP or TGA is refused as
    /// unreadable, and the TIFF decoder with its advisories is never registered. JPEG and WebP also carry the encoders
    /// the renditions use.
    /// </summary>
    private static Configuration Bounded()
    {
        var configuration = new Configuration(
            new JpegConfigurationModule(),
            new PngConfigurationModule(),
            new WebpConfigurationModule(),
            new GifConfigurationModule());

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
                // Max never upscales: an enlarged small photo only costs bytes.
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
                            // Drops metadata: EXIF can carry where the owner's kitchen is.
                            SkipMetadata = true
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                renditions.Add(new Rendition(width, encoded.ToArray()));
            }
        }

        return renditions;
    }

    /// <summary>Two levels of hash prefixes, so no folder grows huge; the same key on disk and in a bucket, so one can be copied into the other.</summary>
    private static string KeyFor(string hash, int width) => $"{hash[..2]}/{hash[2..4]}/{hash}-{width}.webp";

    private sealed record Rendition(int Width, byte[] Bytes);
}
