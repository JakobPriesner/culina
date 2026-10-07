using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Konscious.Security.Cryptography;

namespace Infrastructure.Identity;

/// <summary>Argon2id, with the parameters the deployment configured. Thread-safe, so a singleton.</summary>
/// <remarks>
/// Memory-hard, and every sign-in pays that cost (an unknown address too, against the decoy), so at most one
/// hash per core runs at a time and the rest wait: a burst of anonymous sign-ins must not claim unbounded resources.
/// </remarks>
internal sealed class Argon2PasswordHasher(PasswordHashingSettings settings) : IPasswordHasher, IDisposable
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly SemaphoreSlim turns = new(Environment.ProcessorCount);

    private readonly Lazy<string> decoy = new(
        () => HashWith(Guid.NewGuid().ToString("n"), RandomNumberGenerator.GetBytes(SaltBytes), settings),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string DecoyHash => decoy.Value;

    public Task<string> HashAsync(string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        return InTurnAsync(
            () => HashWith(password, RandomNumberGenerator.GetBytes(SaltBytes), settings),
            cancellationToken);
    }

    public async Task<PasswordVerification> VerifyAsync(
        string password,
        string encodedHash,
        CancellationToken cancellationToken)
    {
        var stored = Argon2Hash.Decode(encodedHash);

        if (stored is null || string.IsNullOrEmpty(password))
        {
            return PasswordVerification.Failed;
        }

        var candidate = await InTurnAsync(
            () => Derive(password, stored.Salt, stored.MemoryKib, stored.Iterations, stored.Parallelism),
            cancellationToken).ConfigureAwait(false);

        // Constant time: byte-by-byte comparison leaks how much of the hash matched.
        if (!CryptographicOperations.FixedTimeEquals(candidate, stored.Hash))
        {
            return PasswordVerification.Failed;
        }

        return stored.IsWeakerThan(settings.MemoryKib, settings.Iterations, settings.Parallelism)
            ? PasswordVerification.ValidButNeedsRehash
            : PasswordVerification.Valid;
    }

    public void Dispose() => turns.Dispose();

    private async Task<TResult> InTurnAsync<TResult>(Func<TResult> work, CancellationToken cancellationToken)
    {
        await turns.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return work();
        }
        finally
        {
            turns.Release();
        }
    }

    private static string HashWith(string password, byte[] salt, PasswordHashingSettings settings)
    {
        var hash = Derive(password, salt, settings.MemoryKib, settings.Iterations, settings.Parallelism);

        return new Argon2Hash(settings.MemoryKib, settings.Iterations, settings.Parallelism, salt, hash)
            .Encode();
    }

    private static byte[] Derive(string password, byte[] salt, int memoryKib, int iterations, int parallelism)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        return argon.GetBytes(HashBytes);
    }
}
