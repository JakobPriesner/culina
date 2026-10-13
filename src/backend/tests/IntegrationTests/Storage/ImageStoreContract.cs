using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Application.Abstractions;
using Domain.Recipes;
using IntegrationTests.Fixtures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using TestSupport;

namespace IntegrationTests.Storage;

/// <summary>What the image store must do the same wherever it keeps renditions, on a volume or in a bucket.</summary>
public abstract class ImageStoreContract
{
    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The store under test, over a place of its own that starts empty.</summary>
    protected abstract IImageStore Store { get; }

    /// <summary>How many files the place now holds, half-written ones included.</summary>
    protected abstract Task<int> CountAsync();

    [Fact]
    public async Task StoreAsync_ShouldKeepEveryWidth_EachNoWiderThanItself()
    {
        var stored = (await Store.StoreAsync(new MemoryStream(TestImages.Png(1800, 1200)), Token))
            .ShouldBeSuccess();

        foreach (var width in ImageWidths.All)
        {
            await AssertDecodesAsync(stored.ContentHash, width);
        }

        Assert.Equal(ImageWidths.All.Count, await CountAsync());
    }

    [Fact]
    public async Task StoreAsync_ShouldKeepOneCopy_WhenTheSamePhotoIsStoredTwice()
    {
        var photo = TestImages.Png(900, 600);

        var first = (await Store.StoreAsync(new MemoryStream(photo), Token)).ShouldBeSuccess();
        var second = (await Store.StoreAsync(new MemoryStream(photo), Token)).ShouldBeSuccess();

        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Equal(ImageWidths.All.Count, await CountAsync());
    }

    [Fact]
    public async Task StoreAsync_ShouldServeOnlyCompleteRenditions_WhenTheSamePhotoIsStoredConcurrently()
    {
        var photo = NoisyPng();

        // Each store reads its renditions back as soon as it returns, as an import does while more are written.
        var stores = Enumerable.Range(0, 8).Select(
            _ => Task.Run(
                async () =>
                {
                    var stored = (await Store.StoreAsync(new MemoryStream(photo), Token).ConfigureAwait(false))
                        .ShouldBeSuccess();

                    foreach (var width in ImageWidths.All)
                    {
                        await AssertDecodesAsync(stored.ContentHash, width).ConfigureAwait(false);
                    }
                },
                Token));

        await Task.WhenAll(stores);

        // Three renditions, and nothing half-written beside them.
        Assert.Equal(ImageWidths.All.Count, await CountAsync());
    }

    [Fact]
    public async Task CopyToAsync_ShouldBeNotFound_ForAPhotoNeverStored()
    {
        using var served = new MemoryStream();

        var result = await Store.CopyToAsync(new string('a', 64), ImageWidths.Card, served, Token);

        result.ShouldBeFailure(ImageErrors.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEveryWidth()
    {
        var stored = (await Store.StoreAsync(new MemoryStream(TestImages.Png(1800, 1200)), Token))
            .ShouldBeSuccess();

        (await Store.DeleteAsync(stored.ContentHash, Token)).ShouldBeSuccess();

        foreach (var width in ImageWidths.All)
        {
            using var served = new MemoryStream();

            (await Store.CopyToAsync(stored.ContentHash, width, served, Token)).ShouldBeFailure(ImageErrors.NotFound);
        }

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ShouldSucceed_ForAPhotoNeverStored()
    {
        (await Store.DeleteAsync(new string('a', 64), Token)).ShouldBeSuccess();
    }

    protected async Task AssertDecodesAsync(string hash, int width)
    {
        using var served = new MemoryStream();

        (await Store.CopyToAsync(hash, width, served, Token).ConfigureAwait(false)).ShouldBeSuccess();
        served.Position = 0;

        using var image = await Image.LoadAsync(served, Token).ConfigureAwait(false);

        Assert.True(image.Width <= width);
    }

    /// <summary>Noise, so its renditions are large enough that writing one takes a while.</summary>
    protected static byte[] NoisyPng()
    {
        using var image = new Image<Rgb24>(2000, 1500);

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(rows.GetRowSpan(y)));
            }
        });

        using var buffer = new MemoryStream();

        image.Save(buffer, new PngEncoder());

        return buffer.ToArray();
    }
}
