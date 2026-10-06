using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Konscious.Security.Cryptography;

namespace Infrastructure.Identity;

/// <summary>
/// Argon2id, with the parameters the deployment configured.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safe, so it is registered as a singleton. Memory-hard by design: the
/// cost that matters is <c>MemoryKib</c>, because it is what stops an attacker
/// running thousands of guesses in parallel on a GPU.
/// </para>
/// <para>
/// That same cost is paid by the server, and every sign-in pays it — an
/// unknown address included, against the decoy. Unbounded, a burst of
/// anonymous sign-ins would claim memory and CPU without limit, so at most one
/// hash per core runs at a time and the rest wait their turn. Waiting rather
/// than failing keeps a busy moment from looking like a wrong password.
/// </para>
/// </remarks>
/// <param name="settings">The configured cost parameters.</param>
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

        // Constant time: a byte-by-byte comparison leaks how much of the hash
        // matched, which is enough to reconstruct it one byte at a time.
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
