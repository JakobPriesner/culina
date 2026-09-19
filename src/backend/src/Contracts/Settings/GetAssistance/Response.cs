namespace Contracts.Settings.GetAssistance;

/// <summary>
/// The model this instance talks to, and what it is allowed to do.
/// </summary>
/// <remarks>
/// A contract type rather than the settings record serialised directly, and
/// this is the case that was anticipated when the registration settings said
/// so: the API key goes in and <see cref="ApiKeyConfigured"/> comes back out,
/// never the value. There is no field on this type that could carry it.
/// </remarks>
public sealed record Response
{
    /// <summary>Whether the assistant is on.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Which provider: <c>gemini</c> or <c>openai</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>Whether a key has been entered. Never the key.</summary>
    public required bool ApiKeyConfigured { get; init; }

    /// <summary>
    /// Whether the connection has everything this provider needs.
    /// </summary>
    /// <remarks>
    /// Not the same question as <see cref="ApiKeyConfigured"/>, and the
    /// difference is the whole reason both are here: a model running on your
    /// own hardware needs an address and no key, so it can be perfectly well
    /// connected with no key at all.
    /// </remarks>
    public required bool Connected { get; init; }

    /// <summary>Where the provider is, when it is not where it usually is.</summary>
    public required string BaseUrl { get; init; }

    /// <summary>The model that writes recipes.</summary>
    public required string ComposeModel { get; init; }

    /// <summary>The model that draws pictures.</summary>
    public required string DrawModel { get; init; }

    /// <summary>Whether it may rewrite a recipe somebody already has.</summary>
    public required bool ImproveEnabled { get; init; }

    /// <summary>Whether it may write one from an idea.</summary>
    public required bool DraftEnabled { get; init; }

    /// <summary>Whether it may read one out of a photograph or a block of text.</summary>
    public required bool ReadEnabled { get; init; }

    /// <summary>Whether it may draw a picture.</summary>
    public required bool DrawEnabled { get; init; }

    /// <summary>What the instance may spend in a month, or null for no ceiling.</summary>
    public decimal? MonthlyBudget { get; init; }

    /// <summary>What one person may spend of it, or null for no share.</summary>
    public decimal? PersonalBudget { get; init; }
}
