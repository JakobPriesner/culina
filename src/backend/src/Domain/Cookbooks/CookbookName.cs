using Domain.Shared;

namespace Domain.Cookbooks;

/// <summary>
/// What a cookbook is called: its only requirement, since a form demanding more is abandoned
/// mid-idea.
/// </summary>
public sealed record CookbookName
{
    /// <summary>The longest name the database column is meant to hold.</summary>
    public const int MaxLength = 80;

    private CookbookName(string value) => Value = value;

    /// <summary>The trimmed name.</summary>
    public string Value { get; }

    /// <summary>Parses a name, returning a failure rather than throwing.</summary>
    public static Result<CookbookName> Create(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength
            ? CookbookErrors.InvalidName
            : new CookbookName(trimmed);
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
