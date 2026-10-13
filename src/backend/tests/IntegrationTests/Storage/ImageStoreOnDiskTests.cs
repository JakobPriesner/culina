using Application.Abstractions;
using Application.Abstractions.Settings;
using Infrastructure.Storage;
using TestSupport;

namespace IntegrationTests.Storage;

/// <summary>Renditions on a real disk, written while something else is going on.</summary>
public sealed class ImageStoreOnDiskTests : ImageStoreContract, IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("culina-images").FullName;

    public ImageStoreOnDiskTests()
    {
        var settings = new StorageSettings { ImagePath = root, DataProtectionKeyPath = root };

        Store = new ImageStore(settings, new FileSystemImageFiles(settings));
    }

    protected override IImageStore Store { get; }

    protected override Task<int> CountAsync() =>
        Task.FromResult(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count());

    [Fact]
    public async Task StoreAsync_ShouldLeaveCompleteRenditions_WhenAnEarlierStoreWasCutOffMidWrite()
    {
        var photo = NoisyPng();
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
            await Store.StoreAsync(new MemoryStream(photo), cutOff.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The hang-up, if it landed before the store finished.
        }

        await hangUp.ConfigureAwait(true);

        var stored = (await Store.StoreAsync(new MemoryStream(photo), Token).ConfigureAwait(true)).ShouldBeSuccess();

        foreach (var width in ImageWidths.All)
        {
            await AssertDecodesAsync(stored.ContentHash, width).ConfigureAwait(true);
        }
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
