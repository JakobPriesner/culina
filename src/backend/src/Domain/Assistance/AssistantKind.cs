namespace Domain.Assistance;

/// <summary>Which model provider this instance talks to.</summary>
/// <remarks>
/// A string rather than an enum, like <see cref="Domain.Import.SourceKind"/>: it is stored in the settings
/// row and <see cref="Parse"/> is the only way in. Ollama runs locally, so it needs no key, must be told its
/// address, and cannot draw.
/// </remarks>
public sealed record AssistantKind
{
    private AssistantKind(string code) => Code = code;

    /// <summary>How it is written, on the wire and in the settings row.</summary>
    public string Code { get; }

    /// <summary>Google's Gemini models.</summary>
    public static AssistantKind Gemini { get; } = new("gemini");

    /// <summary>OpenAI's models, or anything that answers in their shape.</summary>
    public static AssistantKind OpenAi { get; } = new("openai");

    /// <summary>A model running on your own hardware.</summary>
    public static AssistantKind Ollama { get; } = new("ollama");

    /// <summary>Everything that can be connected.</summary>
    public static IReadOnlyList<AssistantKind> All { get; } = [Gemini, OpenAi, Ollama];

    /// <summary>Whether connecting to it means giving it a credential (not for a local model).</summary>
    public bool NeedsApiKey => this != Ollama;

    /// <summary>Whether it has to be told where it is: hosted providers share one address, a local one is wherever you put it.</summary>
    public bool NeedsAddress => this == Ollama;

    /// <summary>Whether it can draw. Ollama reads photographs but makes no images, so settings can decline to offer the switch.</summary>
    public bool CanDraw => this != Ollama;

    /// <summary>Reads a provider, or refuses.</summary>
    public static AssistantKind? Parse(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "gemini" => Gemini,
        "openai" => OpenAi,
        "ollama" => Ollama,
        _ => null
    };

    /// <inheritdoc />
    public override string ToString() => Code;
}
