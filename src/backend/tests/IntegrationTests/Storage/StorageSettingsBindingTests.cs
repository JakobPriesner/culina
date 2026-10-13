using Application.Abstractions.Settings;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Storage;

/// <summary>A bucket is used only when one is configured, and a half-configured one stops the process rather than falling back to a disk nobody mounted.</summary>
public sealed class StorageSettingsBindingTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("culina-storage").FullName;

    [Fact]
    public void AddStorageSettings_ShouldKeepImagesOnDisk_WhenNoBucketIsConfigured()
    {
        var settings = Register([]);

        Assert.Null(settings.S3);
        Assert.True(Directory.Exists(settings.ImagePath));
    }

    [Fact]
    public void AddStorageSettings_ShouldUseTheBucket_AndLeaveTheImageDirectoryAlone()
    {
        var settings = Register(new()
        {
            ["Storage:S3:Endpoint"] = "https://fsn1.your-objectstorage.com",
            ["Storage:S3:Bucket"] = "culina",
            ["Storage:S3:AccessKey"] = "key",
            ["Storage:S3:SecretKey"] = "secret",
            ["Storage:S3:ForcePathStyle"] = "true"
        });

        Assert.Equal("culina", settings.S3?.Bucket);
        Assert.Equal(S3StorageSettings.DefaultRegion, settings.S3?.Region);
        Assert.True(settings.S3?.ForcePathStyle);
        // No volume is needed for images that live in a bucket.
        Assert.False(Directory.Exists(settings.ImagePath));
    }

    [Theory]
    [InlineData("Storage:S3:Bucket", "culina", "Storage__S3__Endpoint")]
    [InlineData("Storage:S3:Endpoint", "https://s3.example.com", "Storage__S3__Bucket")]
    // A mistyped key still says a bucket was meant.
    [InlineData("Storage:S3:Buckett", "culina", "Storage__S3__Endpoint")]
    public void AddStorageSettings_ShouldRefuseToStart_WhenTheBucketIsHalfConfigured(string key, string value, string missing)
    {
        void Act() => Register(new() { [key] = value });

        var refused = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains(missing, refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("s3.example.com")]
    [InlineData("ftp://s3.example.com")]
    public void AddStorageSettings_ShouldRefuseAnEndpointThatIsNotAnHttpAddress(string endpoint)
    {
        void Act() => Register(new()
        {
            ["Storage:S3:Endpoint"] = endpoint,
            ["Storage:S3:Bucket"] = "culina",
            ["Storage:S3:AccessKey"] = "key",
            ["Storage:S3:SecretKey"] = "secret"
        });

        var refused = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Storage__S3__Endpoint", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddStorageSettings_ShouldNameTheNestedVariable_WhenAValueIsMalformed()
    {
        void Act() => Register(new() { ["Storage:S3:ForcePathStyle"] = "yes" });

        var refused = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Storage__S3__ForcePathStyle", refused.Message, StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    private StorageSettings Register(Dictionary<string, string?> values)
    {
        values["Storage:ImagePath"] = Path.Combine(root, "images");
        values["Storage:DataProtectionKeyPath"] = Path.Combine(root, "keys");

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        using var provider = new ServiceCollection()
            .AddStorageSettings(configuration)
            .BuildServiceProvider();

        return provider.GetRequiredService<StorageSettings>();
    }
}
