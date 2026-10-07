using System.Text.RegularExpressions;
using Domain.Shared;

namespace Domain.Users;

/// <summary>
/// A validated email address, stored lowercase so comparison is unambiguous.
/// </summary>
public sealed partial record Email
{
    /// <summary>The largest address the database column accepts.</summary>
    public const int MaxLength = 254;

    private Email(string value) => Value = value;

    /// <summary>The normalised address.</summary>
    public string Value { get; }

    /// <summary>Parses an address, returning a failure rather than throwing; the pattern is deliberately permissive and rejects only obvious mistakes.</summary>
    public static Result<Email> Create(string? value)
    {
        var normalised = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalised)
            || normalised.Length > MaxLength
            || !Pattern().IsMatch(normalised))
        {
            return UserErrors.InvalidEmail;
        }

        return new Email(normalised);
    }

    /// <summary>The domain alone, for logging: a full address is personal data and never appears in a log line.</summary>
    public string Domain => Value[(Value.IndexOf('@', StringComparison.Ordinal) + 1)..];

    /// <inheritdoc/>
    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex Pattern();
}
