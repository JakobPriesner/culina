using Domain.Shared;

namespace Domain.Searches;

/// <summary>
/// What a saved search is called.
/// </summary>
/// <remarks>
/// Short on purpose: the name is read as a chip beside the search field, and a
/// chip whose label wraps is one that pushes the results off the screen.
/// </remarks>
public sealed record SavedSearchName
{
    /// <summary>The longest name that still fits on a chip.</summary>
    public const int MaxLength = 40;

    private SavedSearchName(string value) => Value = value;

    /// <summary>The trimmed name.</summary>
    public string Value { get; }

    /// <summary>Parses a name, returning a failure rather than throwing.</summary>
    /// <param name="value">What the caller supplied.</param>
    public static Result<SavedSearchName> Create(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength
            ? SavedSearchErrors.InvalidName
            : new SavedSearchName(trimmed);
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
