using Amazon.S3;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Infrastructure.Storage;
using IntegrationTests.Fixtures;
using TestSupport;

namespace IntegrationTests.Storage;

/// <summary>Renditions in an S3-compatible bucket, on a real MinIO server.</summary>
public sealed class ImageStoreOnS3Tests(MinioFixture minio) : ImageStoreContract, IClassFixture<MinioFixture>, IAsyncLifetime
{
    private S3StorageSettings bucket = null!;
    private AmazonS3Client client = null!;
    private IImageStore store = null!;

    protected override IImageStore Store => store;

    protected override async Task<int> CountAsync() => (await MinioFixture.KeysAsync(bucket, Token)).Count;

    public async ValueTask InitializeAsync()
    {
        bucket = await minio.NewBucketAsync(Token);
        client = S3ImageFiles.Connect(bucket);
        store = new ImageStore(
            new StorageSettings { ImagePath = "unused", DataProtectionKeyPath = "unused", S3 = bucket },
            new S3ImageFiles(client, bucket));
    }

    [Fact]
    public async Task StoreAsync_ShouldKeepRenditionsUnderTheSameKeysAsOnDisk()
    {
        var stored = (await Store.StoreAsync(new MemoryStream(TestImages.Png(1800, 1200)), Token))
            .ShouldBeSuccess();
        var hash = stored.ContentHash;

        // The volume's relative paths, so an existing image directory can be copied into a bucket as it is.
        Assert.Equal(
            ImageWidths.All.Select(width => $"{hash[..2]}/{hash[2..4]}/{hash}-{width}.webp").Order(),
            (await MinioFixture.KeysAsync(bucket, Token)).Order());
    }

    public ValueTask DisposeAsync()
    {
        client.Dispose();

        return ValueTask.CompletedTask;
    }
}
