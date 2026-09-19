namespace Contracts.Settings.UpdateAssistance;

/// <summary>The assistant configuration to apply.</summary>
public sealed record Request
{
    /// <summary>Whether the assistant is on.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Which provider: <c>gemini</c> or <c>openai</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>
    /// A new API key, or null to keep the one already stored.
    /// </summary>
    /// <remarks>
    /// Three states rather than two, because a settings form that is saved for
    /// an unrelated reason must not wipe the key. Null — the field left out —
    /// means leave it alone, which is what a form sends when its key box is
    /// empty because there was nothing to show in it. An empty string means
    /// take it away. Anything else replaces it.
    /// </remarks>
    public string? ApiKey { get; init; }

    /// <summary>Where the provider is, or empty for its usual address.</summary>
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
