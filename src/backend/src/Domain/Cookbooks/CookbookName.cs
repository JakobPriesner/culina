using Domain.Shared;

namespace Domain.Cookbooks;

/// <summary>
/// What a cookbook is called.
/// </summary>
/// <remarks>
/// The only thing a cookbook requires, for the same reason a recipe requires
/// only a title: "Christmas" is a complete thought, and a form that asks for
/// more before it will save is a form people abandon halfway through having
/// the idea.
/// </remarks>
public sealed record CookbookName
{
    /// <summary>The longest name the database column is meant to hold.</summary>
    public const int MaxLength = 80;

    private CookbookName(string value) => Value = value;

    /// <summary>The trimmed name.</summary>
    public string Value { get; }

    /// <summary>Parses a name, returning a failure rather than throwing.</summary>
    /// <param name="value">What the caller supplied.</param>
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
