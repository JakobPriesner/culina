namespace Contracts.Settings.GetAssistanceModels;

/// <summary>What each connected provider currently offers.</summary>
/// <remarks>
/// One request for all providers; an unreachable one is a row saying so, not a failure losing the
/// other two.
/// </remarks>
public sealed record Response
{
    /// <summary>One entry per provider that is connected.</summary>
    public required IReadOnlyList<ProviderModelsContract> Providers { get; init; }
}

/// <summary>One provider's models, or the reason there are none.</summary>
public sealed record ProviderModelsContract
{
    /// <summary><c>gemini</c>, <c>openai</c> or <c>ollama</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>Whether the provider answered.</summary>
    public required bool Reachable { get; init; }

    /// <summary>
    /// Why the provider did not answer, as an error code (the client has the words); a wrong key
    /// and an unreachable address both land here.
    /// </summary>
    public string? Problem { get; init; }

    /// <summary>What it offers, newest naming and all.</summary>
    public required IReadOnlyList<ModelContract> Models { get; init; }
}

/// <summary>One model.</summary>
public sealed record ModelContract
{
    /// <summary>What to store as the model name.</summary>
    public required string Id { get; init; }

    /// <summary>What to show, where the provider says something nicer.</summary>
    public required string Label { get; init; }

    /// <summary>
    /// Whether it makes pictures; read from the model's name by the adapter, as no provider states
    /// it.
    /// </summary>
    public required bool CanDraw { get; init; }
}
