using System.Globalization;

namespace Infrastructure.Identity;

/// <summary>
/// The PHC string form of an Argon2id hash.
/// </summary>
/// <remarks>
/// Self-describing on purpose: the parameters travel with the hash, so they can
/// be raised later and every existing hash still verifies with the parameters
/// it was created under.
/// </remarks>
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

    /// <summary>
    /// Whether this hash was produced with weaker settings than are configured
    /// now, so it should be replaced on the owner's next successful sign-in.
    /// </summary>
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
            // A stored hash that is not base64 is corrupt, which is a failed
            // verification rather than a crash: the account simply cannot be
            // signed into until the password is reset.
            decoded = [];

            return false;
        }
    }
}
