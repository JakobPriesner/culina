namespace Domain.Shared;

/// <summary>
/// The languages Culina supports.
/// </summary>
/// <remarks>
/// One enum for two uses that are genuinely the same question — which language
/// is this text in. A person reads the interface in one; a recipe's title and
/// steps are written in one. Two enums with the same members would be two
/// places to add the third language to.
/// </remarks>
public enum Language
{
    /// <summary>English.</summary>
    En = 0,

    /// <summary>German.</summary>
    De = 1
}
