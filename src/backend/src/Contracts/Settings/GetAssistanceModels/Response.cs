namespace Contracts.Settings.GetAssistanceModels;

/// <summary>What each connected provider currently offers.</summary>
/// <remarks>
/// One request for every provider rather than one each, because the settings
/// screen wants them all at once — and a provider that cannot be reached is a
/// row that says so rather than a failure that loses the other two.
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
    /// Why it did not, when it did not.
    /// </summary>
    /// <remarks>
    /// An error code rather than a sentence, like every other failure in this
    /// API: the client has the words. A wrong key and an unreachable address
    /// both land here, which is the first place an administrator finds out that
    /// what they pasted does not work.
    /// </remarks>
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
    /// Whether it makes pictures.
    /// </summary>
    /// <remarks>
    /// Read from the model's name by the adapter, because none of the three
    /// providers states it. It decides which list the drawing job offers.
    /// </remarks>
    public required bool CanDraw { get; init; }
}
