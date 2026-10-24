namespace Contracts.Users.UpdateDiarySettings;

/// <summary>Your diary settings, all of them.</summary>
public sealed record Request
{
    /// <summary>On or off. Switching on needs <c>consentVersion</c>.</summary>
    public required bool Enabled { get; init; }

    /// <summary>The wording you agree to by switching on: the <c>currentConsentVersion</c> you were shown.</summary>
    public int? ConsentVersion { get; init; }

    /// <summary>Your daily kcal target, or null for none.</summary>
    public int? KcalTarget { get; init; }

    /// <summary>Your daily protein target in grams, or null for none.</summary>
    public int? ProteinTarget { get; init; }

    /// <summary><c>eaten</c> (the default) or <c>remaining</c>.</summary>
    public string? Headline { get; init; }

    /// <summary>When switching off: also delete every entry. Left out, they are kept and hidden until you switch on again.</summary>
    public bool? EraseEntries { get; init; }
}
