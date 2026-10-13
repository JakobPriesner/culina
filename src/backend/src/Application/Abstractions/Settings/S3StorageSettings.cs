namespace Application.Abstractions.Settings;

/// <summary>An S3-compatible bucket for recipe images (AWS S3, MinIO, Hetzner, R2), used instead of the image directory when set.</summary>
public sealed record S3StorageSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "Storage__S3";

    /// <summary>The service's address, such as <c>https://fsn1.your-objectstorage.com</c>.</summary>
    public required string Endpoint { get; init; }

    /// <summary>The bucket the renditions are written to.</summary>
    public required string Bucket { get; init; }

    /// <summary>The access key id.</summary>
    public required string AccessKey { get; init; }

    /// <summary>The secret access key.</summary>
    public required string SecretKey { get; init; }

    /// <summary>Where <see cref="Region"/> points when nothing says otherwise: MinIO's and AWS's default.</summary>
    public const string DefaultRegion = "us-east-1";

    /// <summary>The region requests are signed for, when the provider names its own (Hetzner: <c>fsn1</c>).</summary>
    public string Region { get; init; } = DefaultRegion;

    /// <summary>Addresses the bucket in the path rather than the host name, which MinIO usually needs.</summary>
    public bool ForcePathStyle { get; init; }

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate()
    {
        SettingsGuard.NotBlank(Endpoint, SectionName, nameof(Endpoint));

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Configuration {SectionName}__{nameof(Endpoint)} must be an http:// or https:// address, but was '{Endpoint}'.");
        }

        SettingsGuard.NotBlank(Bucket, SectionName, nameof(Bucket));
        SettingsGuard.NotBlank(AccessKey, SectionName, nameof(AccessKey));
        SettingsGuard.NotBlank(SecretKey, SectionName, nameof(SecretKey));
        SettingsGuard.NotBlank(Region, SectionName, nameof(Region));
    }
}
