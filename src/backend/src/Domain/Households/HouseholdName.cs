using Domain.Shared;

namespace Domain.Households;

/// <summary>What a household is called.</summary>
public sealed record HouseholdName
{
    /// <summary>The longest name the database column accepts.</summary>
    public const int MaxLength = 80;

    private HouseholdName(string value) => Value = value;

    /// <summary>The trimmed name.</summary>
    public string Value { get; }

    /// <summary>Parses a name, returning a failure rather than throwing.</summary>
    /// <param name="value">What the caller supplied.</param>
    public static Result<HouseholdName> Create(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength
            ? HouseholdErrors.InvalidName
            : new HouseholdName(trimmed);
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
