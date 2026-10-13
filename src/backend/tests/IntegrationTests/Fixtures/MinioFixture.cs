using Amazon.S3.Model;
using Application.Abstractions.Settings;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Infrastructure.Storage;

namespace IntegrationTests.Fixtures;

/// <summary>A real MinIO server, so the S3 adapter is proven against a service that checks signatures.</summary>
/// <remarks>
/// Bitnami's frozen build, pinned: MinIO no longer publishes images of its own. Each test takes a
/// bucket of its own, so none sees another's renditions.
/// </remarks>
public sealed class MinioFixture : IAsyncLifetime
{
    private const string AccessKey = "culina";
    private const string SecretKey = "culina-test-secret";
    private const int Port = 9000;

    private readonly IContainer container = new ContainerBuilder("bitnamilegacy/minio:2025.5.24-debian-12-r5")
        .WithEnvironment("MINIO_ROOT_USER", AccessKey)
        .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
        .WithPortBinding(Port, assignRandomHostPort: true)
        // Logged by the server that stays up, not the one Bitnami's setup starts and stops first.
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("API: http"))
        .Build();

    /// <summary>Creates an empty bucket and the settings that point Culina at it.</summary>
    public async Task<S3StorageSettings> NewBucketAsync(CancellationToken cancellationToken)
    {
        var settings = new S3StorageSettings
        {
            Endpoint = $"http://{container.Hostname}:{container.GetMappedPublicPort(Port)}",
            Bucket = $"culina-{Guid.CreateVersion7():n}",
            AccessKey = AccessKey,
            SecretKey = SecretKey,
            ForcePathStyle = true
        };

        using var client = S3ImageFiles.Connect(settings);

        await client.PutBucketAsync(settings.Bucket, cancellationToken);

        return settings;
    }

    /// <summary>Every key in a bucket.</summary>
    public static async Task<IReadOnlyList<string>> KeysAsync(S3StorageSettings bucket, CancellationToken cancellationToken)
    {
        using var client = S3ImageFiles.Connect(bucket);

        var listed = await client.ListObjectsV2Async(
            new ListObjectsV2Request { BucketName = bucket.Bucket },
            cancellationToken);

        return [.. (listed.S3Objects ?? []).Select(stored => stored.Key)];
    }

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}
