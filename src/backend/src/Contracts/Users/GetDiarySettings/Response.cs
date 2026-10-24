namespace Contracts.Users.GetDiarySettings;

/// <summary>Whether you keep a food diary, what you agreed to, and your own targets.</summary>
public sealed record Response
{
    /// <summary>Whether the diary is switched on.</summary>
    public required bool Enabled { get; init; }

    /// <summary>When you last agreed, by switching it on.</summary>
    public DateTimeOffset? ConsentedAt { get; init; }

    /// <summary>Which wording you agreed to.</summary>
    public int? ConsentVersion { get; init; }

    /// <summary>The wording switching on agrees to now; send it as <c>consentVersion</c> when switching on.</summary>
    public required int CurrentConsentVersion { get; init; }

    /// <summary>Your daily kcal target, if you set one.</summary>
    public int? KcalTarget { get; init; }

    /// <summary>Your daily protein target in grams, if you set one.</summary>
    public int? ProteinTarget { get; init; }

    /// <summary><c>eaten</c> or <c>remaining</c>: what the day's headline shows.</summary>
    public required string Headline { get; init; }

    /// <summary>The settings' version.</summary>
    public required long Version { get; init; }
}
