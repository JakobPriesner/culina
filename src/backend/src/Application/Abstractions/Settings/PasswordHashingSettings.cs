namespace Application.Abstractions.Settings;

/// <summary>
/// Argon2id parameters.
/// </summary>
/// <remarks>
/// Raise <see cref="MemoryKib"/> as far as the host tolerates: it is what makes
/// the hash expensive to attack in parallel. Existing hashes keep verifying
/// because each stores the parameters it was created with, and they are
/// transparently upgraded on the owner's next successful login.
/// </remarks>
public sealed record PasswordHashingSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "PasswordHashing";

    /// <summary>Memory cost in kibibytes.</summary>
    public int MemoryKib { get; init; } = 65536;

    /// <summary>Time cost: the number of passes over memory.</summary>
    public int Iterations { get; init; } = 3;

    /// <summary>How many lanes the computation uses.</summary>
    public int Parallelism { get; init; } = 2;

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate()
    {
        // The lower bounds are the OWASP minimums for Argon2id. Below them the
        // hash is fast enough to attack, so an operator must not be able to
        // weaken it by accident.
        SettingsGuard.InRange(MemoryKib, 19456, 4 * 1024 * 1024, SectionName, nameof(MemoryKib));
        SettingsGuard.InRange(Iterations, 2, 64, SectionName, nameof(Iterations));
        SettingsGuard.InRange(Parallelism, 1, 16, SectionName, nameof(Parallelism));
    }
}
