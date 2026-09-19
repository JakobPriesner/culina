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
/// another. There is one caller today; the purpose is what keeps the second one
/// from being able to read the first one's secrets by accident.
/// </para>
/// </remarks>
internal sealed class SecretProtector : ISecretProtector
{
    /// <summary>What these secrets are for. Changing it invalidates every one.</summary>
    private const string Purpose = "Culina.Assistance.ApiKey.v1";

    private readonly IDataProtector protector;

    public SecretProtector(StorageSettings storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        protector = DataProtectionProvider
            .Create(new DirectoryInfo(storage.DataProtectionKeyPath))
            .CreateProtector(Purpose);
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
