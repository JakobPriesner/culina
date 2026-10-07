namespace Application.Abstractions.Settings;

/// <summary>Argon2id parameters. Existing hashes keep verifying (each stores its own parameters) and are upgraded on the owner's next login.</summary>
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
        // The lower bounds are the OWASP minimums for Argon2id; below them an operator would weaken the hash.
        SettingsGuard.InRange(MemoryKib, 19456, 4 * 1024 * 1024, SectionName, nameof(MemoryKib));
        SettingsGuard.InRange(Iterations, 2, 64, SectionName, nameof(Iterations));
        SettingsGuard.InRange(Parallelism, 1, 16, SectionName, nameof(Parallelism));
    }
}
