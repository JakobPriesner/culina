using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="StorageSettings"/>.</summary>
public static class StorageSettingsExtensions
{
    /// <summary>Binds the section, validates it, and registers it as a singleton.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read from.</param>
    public static IServiceCollection AddStorageSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = new SettingsSection(configuration, StorageSettings.SectionName);

        var settings = new StorageSettings
        {
            ImagePath = section.String(nameof(StorageSettings.ImagePath), "/data/images"),
            DataProtectionKeyPath = section.String(
                nameof(StorageSettings.DataProtectionKeyPath),
                "/data/keys"),
            ConfigPath = section.String(nameof(StorageSettings.ConfigPath), StorageSettings.DefaultConfigPath),
            MaxImageBytes = section.Int(nameof(StorageSettings.MaxImageBytes), 10 * 1024 * 1024),
            S3 = Bucket(configuration)
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }

    /// <summary>Null when no S3 key is set; any key set, even a mistyped one, means a bucket was meant, so validation then demands the rest.</summary>
    private static S3StorageSettings? Bucket(IConfiguration configuration)
    {
        var path = $"{StorageSettings.SectionName}:S3";

        if (configuration.GetSection(path).GetChildren().All(key => string.IsNullOrEmpty(key.Value)))
        {
            return null;
        }

        var section = new SettingsSection(configuration, path);

        return new S3StorageSettings
        {
            Endpoint = section.String(nameof(S3StorageSettings.Endpoint), ""),
            Bucket = section.String(nameof(S3StorageSettings.Bucket), ""),
            AccessKey = section.String(nameof(S3StorageSettings.AccessKey), ""),
            SecretKey = section.String(nameof(S3StorageSettings.SecretKey), ""),
            Region = section.String(nameof(S3StorageSettings.Region), S3StorageSettings.DefaultRegion),
            ForcePathStyle = section.Bool(nameof(S3StorageSettings.ForcePathStyle), false)
        };
    }
}
