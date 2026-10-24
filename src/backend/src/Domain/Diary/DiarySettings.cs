using Domain.Shared;

namespace Domain.Diary;

/// <summary>Whether a person keeps a diary, what they agreed to, and their own targets.</summary>
/// <remarks>
/// Off until the person switches it on, which records their agreement to the wording of
/// <see cref="CurrentConsentVersion"/>: a kcal diary is treated as health data. The targets are their own numbers,
/// never computed; a low one is answered by the client with a calm sentence, not refused here.
/// </remarks>
public sealed class DiarySettings
{
    /// <summary>The version of the wording a person agrees to by switching the diary on; raise it when the wording changes.</summary>
    public const int CurrentConsentVersion = 1;

    /// <summary>The highest daily kcal target accepted.</summary>
    public const int MaxKcalTarget = 10000;

    /// <summary>The highest daily protein target accepted, in grams.</summary>
    public const int MaxProteinTarget = 1000;

    private DiarySettings(
        Guid userId,
        bool enabled,
        DateTimeOffset? consentedAt,
        int? consentVersion,
        int? kcalTarget,
        int? proteinTarget,
        DiaryHeadline headline,
        long version)
    {
        UserId = userId;
        Enabled = enabled;
        ConsentedAt = consentedAt;
        ConsentVersion = consentVersion;
        KcalTarget = kcalTarget;
        ProteinTarget = proteinTarget;
        Headline = headline;
        Version = version;
    }

    /// <summary>Whose settings.</summary>
    public Guid UserId { get; }

    /// <summary>Whether the diary is switched on.</summary>
    public bool Enabled { get; }

    /// <summary>When the person last agreed, by switching it on.</summary>
    public DateTimeOffset? ConsentedAt { get; }

    /// <summary>Which wording they agreed to.</summary>
    public int? ConsentVersion { get; }

    /// <summary>Their daily kcal target (Richtwert), if they set one.</summary>
    public int? KcalTarget { get; }

    /// <summary>Their daily protein target in grams, if they set one.</summary>
    public int? ProteinTarget { get; }

    /// <summary>What the day's headline shows.</summary>
    public DiaryHeadline Headline { get; }

    /// <summary>The settings' version.</summary>
    public long Version { get; }

    /// <summary>A person who never opened the diary: off, nothing set.</summary>
    /// <param name="userId">Whose.</param>
    public static DiarySettings Default(Guid userId) =>
        new(userId, false, null, null, null, null, DiaryHeadline.Eaten, version: 0);

    /// <summary>
    /// The settings after a change. Switching on needs agreement to the current wording and records it; switching off
    /// keeps the record of the last agreement.
    /// </summary>
    /// <param name="enabled">On or off.</param>
    /// <param name="consentVersion">The wording the person agrees to when switching on.</param>
    /// <param name="kcalTarget">Their kcal target, or null.</param>
    /// <param name="proteinTarget">Their protein target, or null.</param>
    /// <param name="headline">Eaten or remaining.</param>
    /// <param name="now">When.</param>
    public Result<DiarySettings> Change(
        bool enabled,
        int? consentVersion,
        int? kcalTarget,
        int? proteinTarget,
        DiaryHeadline headline,
        DateTimeOffset now)
    {
        if (kcalTarget is <= 0 or > MaxKcalTarget || proteinTarget is <= 0 or > MaxProteinTarget)
        {
            return DiaryErrors.InvalidTarget;
        }

        var switchingOn = enabled && !Enabled;

        if (switchingOn && consentVersion != CurrentConsentVersion)
        {
            return DiaryErrors.ConsentRequired;
        }

        return new DiarySettings(
            UserId,
            enabled,
            switchingOn ? now : ConsentedAt,
            switchingOn ? CurrentConsentVersion : ConsentVersion,
            kcalTarget,
            proteinTarget,
            headline,
            Version + 1);
    }

    /// <summary>Rebuilds settings from storage.</summary>
    /// <param name="userId">Whose.</param>
    /// <param name="enabled">On or off.</param>
    /// <param name="consentedAt">When they agreed.</param>
    /// <param name="consentVersion">To which wording.</param>
    /// <param name="kcalTarget">Their kcal target.</param>
    /// <param name="proteinTarget">Their protein target.</param>
    /// <param name="headline">Eaten or remaining.</param>
    /// <param name="version">The version.</param>
    public static DiarySettings Restore(
        Guid userId,
        bool enabled,
        DateTimeOffset? consentedAt,
        int? consentVersion,
        int? kcalTarget,
        int? proteinTarget,
        DiaryHeadline headline,
        long version) =>
        new(userId, enabled, consentedAt, consentVersion, kcalTarget, proteinTarget, headline, version);
}

/// <summary>What the day's headline number is.</summary>
public enum DiaryHeadline
{
    /// <summary>What was eaten; the default.</summary>
    Eaten,

    /// <summary>What is left of the kcal target, never below zero.</summary>
    Remaining
}
