namespace Domain.Assistance;

/// <summary>
/// Which model provider this instance talks to.
/// </summary>
/// <remarks>
/// <para>
/// A string rather than an enum, for the same reason
/// <see cref="Domain.Import.SourceKind"/> is one: it is written into the
/// settings row and read back out of it, and a stored integer whose meaning
/// lives in a C# file is a column nobody can read. <see cref="Parse"/> is the
/// only way in.
/// </para>
/// <para>
/// Three rather than one, because the choice is not this app's to make.
/// Somebody self-hosting already has an account with one of them, or a proxy
/// that speaks one of their shapes, and an app that picked for them would be an
/// app they could not use.
/// </para>
/// <para>
/// Ollama is the one that is different in kind, and it is the reason this type
/// carries facts rather than just a name. It runs on your own machine, so there
/// is no account and no key to give it; it has to be told where it is, because
/// unlike the other two it has no address of its own; and it does not make
/// pictures at all. A self-hosted recipe app that could not talk to the model
/// already running in the same house would be missing the point.
/// </para>
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

    /// <summary>
    /// Whether connecting to it means giving it a credential.
    /// </summary>
    /// <remarks>
    /// False for a model on your own machine, and that is not a detail: the
    /// settings screen must not ask for a key that does not exist, and
    /// "configured" cannot mean "has a key" for a provider that has none.
    /// </remarks>
    public bool NeedsApiKey => this != Ollama;

    /// <summary>
    /// Whether it has to be told where it is.
    /// </summary>
    /// <remarks>
    /// The hosted two have one address between all their customers. A local
    /// one is wherever you put it, so the address is the connection rather than
    /// an override of it.
    /// </remarks>
    public bool NeedsAddress => this == Ollama;

    /// <summary>
    /// Whether it can draw.
    /// </summary>
    /// <remarks>
    /// Ollama serves language and vision models — it will happily read a
    /// photograph of a cookbook page — but it does not make images. Said here
    /// rather than discovered at the call, so the settings screen can decline
    /// to offer the switch instead of offering one that fails.
    /// </remarks>
    public bool CanDraw => this != Ollama;

    /// <summary>Reads a provider, or refuses.</summary>
    /// <param name="code">What arrived on the wire or came out of the row.</param>
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
