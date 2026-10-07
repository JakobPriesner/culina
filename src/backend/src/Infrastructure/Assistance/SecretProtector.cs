using Application.Abstractions;
using Application.Abstractions.Settings;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Assistance;

/// <summary>
/// Encrypts stored secrets with the key ring on the persisted volume.
/// </summary>
/// <remarks>
/// <para>
/// The first real use of <c>Storage__DataProtectionKeyPath</c>, which has been
/// configured, documented and volume-backed since the beginning and protected
/// nothing. <c>docs/configuration.md</c> already says why it was provisioned
/// anyway: "anything the framework protects later would otherwise change key on
/// every restart".
/// </para>
/// <para>
/// A purpose string, so a value encrypted for one thing cannot be decrypted as
/// another. The assistant's key has one, and connected-source tokens have
/// their own, <see cref="SourceTokens"/>: a token cannot be read back as an
/// API key, nor the other way round.
/// </para>
/// </remarks>
internal sealed class SecretProtector : ISecretProtector
{
    /// <summary>
    /// What a connected recipe source's API token is protected for, and the
    /// key the protector for them is registered under. Changing it invalidates
    /// every stored token.
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
            // The ordinary way to get here is a key ring that was lost and came
            // back empty: the ciphertext is intact and no longer readable. That
            // is "no assistant is configured" and an administrator entering the
            // key again — not a crash, and not a reason to refuse to start.
            return null;
        }
    }
}
