using System.Globalization;

namespace Infrastructure.Identity;

/// <summary>The PHC string form of an Argon2id hash. Self-describing, so parameters can be raised and old hashes still verify.</summary>
/// <param name="MemoryKib">Memory cost in kibibytes.</param>
/// <param name="Iterations">Time cost.</param>
/// <param name="Parallelism">Lanes.</param>
/// <param name="Salt">The per-hash salt.</param>
/// <param name="Hash">The derived key.</param>
internal sealed record Argon2Hash(
    int MemoryKib,
    int Iterations,
    int Parallelism,
    byte[] Salt,
    byte[] Hash)
{
    private const string Prefix = "$argon2id$v=19$";

    internal string Encode() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Prefix}m={MemoryKib},t={Iterations},p={Parallelism}${Base64Url.Encode(Salt)}${Base64Url.Encode(Hash)}");

    internal static Argon2Hash? Decode(string? encoded)
    {
        if (encoded is null || !encoded.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = encoded[Prefix.Length..].Split('$');

        if (parts.Length != 3)
        {
            return null;
        }

        var costs = parts[0].Split(',');

        if (costs.Length != 3
            || !TryRead(costs[0], "m=", out var memory)
            || !TryRead(costs[1], "t=", out var iterations)
            || !TryRead(costs[2], "p=", out var parallelism)
            || !Base64Url.TryDecode(parts[1], out var salt)
            || !Base64Url.TryDecode(parts[2], out var hash))
        {
            return null;
        }

        return new Argon2Hash(memory, iterations, parallelism, salt, hash);
    }

    // Whether this hash used weaker settings than are configured now, so it is replaced on the next successful sign-in.
    internal bool IsWeakerThan(int memoryKib, int iterations, int parallelism) =>
        MemoryKib < memoryKib || Iterations < iterations || Parallelism < parallelism;

    private static bool TryRead(string part, string prefix, out int value)
    {
        value = 0;

        return part.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(part[prefix.Length..], CultureInfo.InvariantCulture, out value);
    }
}

/// <summary>Unpadded base64, as the PHC string format specifies.</summary>
internal static class Base64Url
{
    internal static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=');

    internal static bool TryDecode(string value, out byte[] decoded)
    {
        var padded = value.PadRight(value.Length + ((4 - (value.Length % 4)) % 4), '=');

        try
        {
            decoded = Convert.FromBase64String(padded);

            return true;
        }
        catch (FormatException)
        {
            // A corrupt stored hash is a failed verification, not a crash.
            decoded = [];

            return false;
        }
    }
}
