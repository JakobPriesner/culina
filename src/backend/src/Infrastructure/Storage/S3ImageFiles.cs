using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Application.Abstractions.Settings;

namespace Infrastructure.Storage;

/// <summary>Renditions in an S3-compatible bucket (AWS S3, MinIO, Hetzner, R2), under the same keys as on disk.</summary>
/// <param name="client">The client for the bucket's service.</param>
/// <param name="settings">Which bucket.</param>
internal sealed class S3ImageFiles(IAmazonS3 client, S3StorageSettings settings) : IImageFiles
{
    /// <summary>A client for any S3-compatible service, not only AWS.</summary>
    internal static AmazonS3Client Connect(S3StorageSettings settings) =>
        new(
            new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = settings.Endpoint,
                AuthenticationRegion = settings.Region,
                ForcePathStyle = settings.ForcePathStyle,
                // The SDK's default checksums on every request are refused or mishandled by several S3-compatible services.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            });

    /// <summary>An S3 PUT is atomic, so the key only ever holds a whole object, and putting the same bytes again changes nothing.</summary>
    public async Task WriteAsync(string key, byte[] bytes)
    {
        using var content = new MemoryStream(bytes, writable: false);

        await client.PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = settings.Bucket,
                    Key = key,
                    InputStream = content,
                    ContentType = "image/webp",
                    // Plain signed payload: streaming chunk signatures are not supported everywhere (R2).
                    UseChunkEncoding = false
                },
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<bool> CopyToAsync(string key, Stream destination, CancellationToken cancellationToken)
    {
        GetObjectResponse response;

        try
        {
            response = await client
                .GetObjectAsync(settings.Bucket, key, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AmazonS3Exception missing) when (missing.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        using (response)
        {
            await response.ResponseStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>One request per key: deleting a missing key succeeds, and multi-object delete needs checksums some services handle differently.</summary>
    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        client.DeleteObjectAsync(settings.Bucket, key, cancellationToken);
}
