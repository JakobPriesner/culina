using Domain.Shared;

namespace Domain.Users;

/// <summary>
/// What a person is called in the app.
/// </summary>
public sealed record DisplayName
{
    /// <summary>The longest name the database column accepts.</summary>
    public const int MaxLength = 80;

    private DisplayName(string value) => Value = value;

    /// <summary>The trimmed name.</summary>
    public string Value { get; }

    /// <summary>Parses a name, returning a failure rather than throwing.</summary>
    /// <param name="value">What the caller supplied.</param>
    public static Result<DisplayName> Create(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength)
        {
            return UserErrors.InvalidDisplayName;
        }

        return new DisplayName(trimmed);
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
