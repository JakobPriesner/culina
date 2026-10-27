using System.Security.Cryptography;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Recipes;
using Domain.Shared;
using SkiaSharp;

namespace Infrastructure.Storage;

/// <summary>Stores recipe images content-addressed, so duplicates cost nothing and renditions cache forever.</summary>
/// <param name="settings">How big an image may be.</param>
/// <param name="files">Where the renditions live: a volume or a bucket.</param>
internal sealed class ImageStore(StorageSettings settings, IImageFiles files) : IImageStore
{
    /// <summary>
    /// A ceiling on decoded pixels, checked before decoding: the decompression-bomb guard. Every image is decoded at
    /// four bytes a pixel whatever its source depth, so this also bounds the decoder's memory (256 MB).
    /// </summary>
    private const int MaxPixels = 8000 * 8000;

    /// <summary>How many images may be decoded at once, bounding the decoder's memory across concurrent uploads.</summary>
    private static readonly SemaphoreSlim Decoding = new(2, 2);

    /// <summary>The longest side of a picture sent to an assistant, about what providers scale down to anyway.</summary>
    private const int LongestReadingSide = 2048;

    /// <summary>
    /// Only the formats Culina accepts, checked from the bytes before anything is decoded: a TIFF, BMP, ICO or HEIF is
    /// refused as unreadable, so no other decoder ever sees an upload.
    /// </summary>
    private static readonly SKEncodedImageFormat[] Accepted =
    [
        SKEncodedImageFormat.Jpeg,
        SKEncodedImageFormat.Png,
        SKEncodedImageFormat.Webp,
        SKEncodedImageFormat.Gif
    ];

    /// <summary>Trilinear: a cubic filter in Skia does not prefilter, so a phone photo shrunk tenfold would alias.</summary>
    private static readonly SKSamplingOptions Downscaling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

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

            return await DecodeAsync(
                    buffered.GetBuffer().AsMemory(0, (int)buffered.Length),
                    StoreRenditionsAsync,
                    cancellationToken)
                .ConfigureAwait(false);
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

        return await DecodeAsync(content, picture => Task.FromResult(ForReading(picture)), cancellationToken)
            .ConfigureAwait(false);
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

    private async Task<StoredImage> StoreRenditionsAsync(Decoded picture)
    {
        List<Rendition> renditions = [];

        foreach (var width in ImageWidths.All)
        {
            // Bounded by width alone, and never upscaled: an enlarged small photo only costs bytes.
            using var rendition = picture.Draw(picture.Fit(width, int.MaxValue), SKColors.Transparent);
            using var encoded = rendition.Encode(SKEncodedImageFormat.Webp, 82);

            renditions.Add(new Rendition(width, encoded.ToArray()));
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(renditions[^1].Bytes));

        foreach (var rendition in renditions)
        {
            // The request's token is deliberately not passed: a client hanging up is no reason to discard the renditions.
            await files.WriteAsync(KeyFor(hash, rendition.Width), rendition.Bytes).ConfigureAwait(false);
        }

        return new StoredImage(hash, picture.Upright.Width, picture.Upright.Height, renditions[^1].Bytes.Length);
    }

    /// <summary>A JPEG a model can read, flattened onto white so transparent screenshots stay legible.</summary>
    private static RecipePicture ForReading(Decoded picture)
    {
        using var flattened = picture.Draw(picture.Fit(LongestReadingSide, LongestReadingSide), SKColors.White);
        using var encoded = flattened.Encode(SKEncodedImageFormat.Jpeg, 90);

        return new RecipePicture(encoded.ToArray(), "image/jpeg");
    }

    /// <summary>Opens an untrusted image, the one way into the decoder: format and header first, first frame only, limited concurrency.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "SkiaSharp annotates SKCodec.Create as never null, but it returns null for any format it cannot read.")]
    private static async Task<Result<TOut>> DecodeAsync<TOut>(
        ReadOnlyMemory<byte> content,
        Func<Decoded, Task<TOut>> use,
        CancellationToken cancellationToken)
        where TOut : notnull
    {
        using var data = SKData.CreateCopy(content.Span);

        // Decoding is the format check: content type and extension are attacker-supplied.
        using var codec = SKCodec.Create(data);

        if (codec is null || !Accepted.Contains(codec.EncodedFormat))
        {
            return ImageErrors.Unreadable;
        }

        // Header only: a few KB of PNG can declare 50000x50000 pixels, and decoding it would be the attack.
        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            return ImageErrors.TooManyPixels;
        }

        await Decoding.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // No colour space: the pixels are taken as they are and the ICC profile is never parsed or written back.
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var pixels = new SKBitmap();

            if (!pixels.TryAllocPixels(info))
            {
                return ImageErrors.TooManyPixels;
            }

            // The first frame only: every frame of an animated GIF or WebP is a full-canvas decode.
            if (codec.GetPixels(info, pixels.GetPixels()) != SKCodecResult.Success)
            {
                return ImageErrors.Unreadable;
            }

            pixels.SetImmutable();
            using var image = SKImage.FromBitmap(pixels);

            // Only pixels go on from here: EXIF (with coordinates), XMP and IPTC stay in the upload, and the encoders
            // write none. Phones store orientation as an EXIF tag, so it is turned into pixels when drawing.
            return Result<TOut>.Success(await use(new Decoded(image, codec.EncodedOrigin)).ConfigureAwait(false));
        }
        finally
        {
            Decoding.Release();
        }
    }

    /// <summary>Two levels of hash prefixes, so no folder grows huge; the same key on disk and in a bucket, so one can be copied into the other.</summary>
    private static string KeyFor(string hash, int width) => $"{hash[..2]}/{hash[2..4]}/{hash}-{width}.webp";

    private sealed record Rendition(int Width, byte[] Bytes);

    /// <summary>A decoded first frame and the way the camera said it should be turned.</summary>
    private sealed record Decoded(SKImage Image, SKEncodedOrigin Origin)
    {
        /// <summary>The size once turned upright: EXIF orientations 5 to 8 swap width and height.</summary>
        public SKSizeI Upright { get; } = Origin >= SKEncodedOrigin.LeftTop
            ? new SKSizeI(Image.Height, Image.Width)
            : new SKSizeI(Image.Width, Image.Height);

        /// <summary>The upright size scaled down to fit the box, keeping the aspect ratio and never enlarging.</summary>
        public SKSizeI Fit(int maxWidth, int maxHeight)
        {
            var scale = Math.Min(1d, Math.Min(maxWidth / (double)Upright.Width, maxHeight / (double)Upright.Height));

            return new SKSizeI(
                Math.Max(1, (int)Math.Round(Upright.Width * scale)),
                Math.Max(1, (int)Math.Round(Upright.Height * scale)));
        }

        /// <summary>The picture upright, at the given size, over the given background.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "SkiaSharp annotates SKSurface.Create as never null, but it returns null when the pixels cannot be allocated.")]
        public SKImage Draw(SKSizeI size, SKColor background)
        {
            using var surface = SKSurface.Create(
                    new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul))
                ?? throw new InvalidOperationException($"No {size.Width}x{size.Height} surface could be allocated.");

            var canvas = surface.Canvas;

            canvas.Clear(background);
            canvas.Scale(size.Width / (float)Upright.Width, size.Height / (float)Upright.Height);
            canvas.Concat(Orientation(Origin, Image.Width, Image.Height));
            canvas.DrawImage(Image, 0, 0, Downscaling);

            return surface.Snapshot();
        }

        /// <summary>Maps the stored pixels onto the upright picture; Skia's own SkEncodedOriginToMatrix, which SkiaSharp does not expose.</summary>
        private static SKMatrix Orientation(SKEncodedOrigin origin, int width, int height) => origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => SKMatrix.Identity
        };
    }
}
