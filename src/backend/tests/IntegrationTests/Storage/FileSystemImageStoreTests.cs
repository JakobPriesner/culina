using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Infrastructure.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using TestSupport;

namespace IntegrationTests.Storage;

/// <summary>Renditions on a real disk, written while something else is going on.</summary>
public sealed class FileSystemImageStoreTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string root = Directory.CreateTempSubdirectory("culina-images").FullName;

    [Fact]
    public async Task StoreAsync_ShouldLeaveCompleteRenditions_WhenAnEarlierStoreWasCutOffMidWrite()
    {
        var photo = NoisyPng();
        var store = NewStore();
        using var cutOff = new CancellationTokenSource();

        // The request goes away as soon as the first file reaches the disk, like a client hanging up.
        var hangUp = Task.Run(
            async () =>
            {
                while (!Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any())
                {
                    Token.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                await cutOff.CancelAsync().ConfigureAwait(false);
            },
            Token);

        try
        {
            await store.StoreAsync(new MemoryStream(photo), cutOff.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The hang-up, if it landed before the store finished.
        }

        await hangUp.ConfigureAwait(true);

        var stored = (await store.StoreAsync(new MemoryStream(photo), Token).ConfigureAwait(true)).ShouldBeSuccess();

        foreach (var width in ImageWidths.All)
        {
            await AssertDecodesAsync(store, stored.ContentHash, width).ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task StoreAsync_ShouldServeOnlyCompleteRenditions_WhenTheSamePhotoIsStoredConcurrently()
    {
        var photo = NoisyPng();
        var store = NewStore();

        // Each store reads its renditions back as soon as it returns, as an import does while more are written.
        var stores = Enumerable.Range(0, 8).Select(
            _ => Task.Run(
                async () =>
                {
                    var stored = (await store.StoreAsync(new MemoryStream(photo), Token).ConfigureAwait(false))
                        .ShouldBeSuccess();

                    foreach (var width in ImageWidths.All)
                    {
                        await AssertDecodesAsync(store, stored.ContentHash, width).ConfigureAwait(false);
                    }
                },
                Token));

        await Task.WhenAll(stores).ConfigureAwait(true);

        // Three renditions, and nothing half-written beside them.
        Assert.Equal(
            ImageWidths.All.Count,
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count());
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    private FileSystemImageStore NewStore() =>
        new(new StorageSettings { ImagePath = root, DataProtectionKeyPath = root });

    private static async Task AssertDecodesAsync(FileSystemImageStore store, string hash, int width)
    {
        using var served = new MemoryStream();

        (await store.CopyToAsync(hash, width, served, Token).ConfigureAwait(false)).ShouldBeSuccess();
        served.Position = 0;

        using var image = await Image.LoadAsync(served, Token).ConfigureAwait(false);

        Assert.True(image.Width <= width);
    }

    // Noise, so its renditions are large enough that writing one takes a while.
    private static byte[] NoisyPng()
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
