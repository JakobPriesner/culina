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

    /// <summary>
    /// Parses an address, returning a failure rather than throwing.
    /// </summary>
    /// <param name="value">What the caller supplied.</param>
    /// <remarks>
    /// The pattern is deliberately permissive. Fully validating an address is
    /// impossible in a regular expression and pointless in practice: the only
    /// proof an address works is a message arriving at it. This rejects the
    /// obvious mistakes and nothing else.
    /// </remarks>
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

    /// <summary>The address, for logging in a form that reveals nothing.</summary>
    /// <remarks>
    /// A full address is personal data and never appears in a log line. The
    /// domain alone is enough to tell "our users" from "a bot with a
    /// disposable address".
    /// </remarks>
    public string Domain => Value[(Value.IndexOf('@', StringComparison.Ordinal) + 1)..];

    /// <inheritdoc/>
    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex Pattern();
}
