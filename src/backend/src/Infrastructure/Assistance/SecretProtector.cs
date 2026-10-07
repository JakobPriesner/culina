using Application.Abstractions;
using Application.Abstractions.Settings;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Assistance;

/// <summary>Encrypts stored secrets with the key ring on the persisted volume.</summary>
/// <remarks>
/// Each secret has a purpose string, so a value encrypted for one thing cannot be decrypted as
/// another: the assistant key and a <see cref="SourceTokens"/> token are not interchangeable.
/// </remarks>
internal sealed class SecretProtector : ISecretProtector
{
    /// <summary>
    /// What a connected recipe source's API token is protected for, and the key its protector is
    /// registered under; changing it invalidates every stored token.
    /// </summary>
    internal const string SourceTokens = "Culina.RecipeSources.Token.v1";

    /// <summary>What the assistant's key is protected for. Changing it invalidates it.</summary>
    private const string ApiKey = "Culina.Assistance.ApiKey.v1";

    private readonly IDataProtector protector;

    public SecretProtector(StorageSettings storage)
        : this(storage, ApiKey)
    {
    }

    /// <summary>A protector for secrets kept for <paramref name="purpose"/>.</summary>
    internal SecretProtector(StorageSettings storage, string purpose)
    {
        ArgumentNullException.ThrowIfNull(storage);

        protector = DataProtectionProvider
            .Create(new DirectoryInfo(storage.DataProtectionKeyPath))
            .CreateProtector(purpose);
    }

    public string Protect(string secret) => protector.Protect(secret);

    public string? Unprotect(string protectedSecret)
    {
        try
        {
            return protector.Unprotect(protectedSecret);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Usually a key ring that was lost and came back empty: the ciphertext is intact but
            // unreadable. That means "no assistant configured" and the key entered again, not a
            // crash.
            return null;
        }
    }
}
