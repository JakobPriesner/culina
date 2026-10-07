using Domain.Assistance;

namespace Infrastructure.Assistance;

/// <summary>Where each provider lives, and which of its models to use when nobody said.</summary>
/// <remarks>
/// Both stay overridable. <b>Model names current as of 2026-09-19</b>; revisit with the price
/// table.
/// </remarks>
internal static class AssistantDefaults
{
    /// <summary>The provider's own address, for everything but a local model.</summary>
    internal static string Home(AssistantKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return kind.Code switch
        {
            "gemini" => "https://generativelanguage.googleapis.com",
            "openai" => "https://api.openai.com",
            // Only a hint: Ollama is wherever it was put, so the address is required.
            _ => OllamaAssistant.UsualAddress
        };
    }

    /// <summary>The model that writes recipes, when none was chosen.</summary>
    internal static string ComposeModel(AssistantKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return kind.Code switch
        {
            // The fast model, not the clever one: a recipe in a fixed shape costs several times
            // more for seconds saved.
            "gemini" => "gemini-3-flash-preview",
            "openai" => "gpt-6-astra",
            // Widely installed, laptop-sized, good enough at filling a schema.
            _ => "llama3.2"
        };
    }

    /// <summary>
    /// The model that draws pictures, when none was chosen; empty for a provider that does not
    /// draw.
    /// </summary>
    internal static string DrawModel(AssistantKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return kind.Code switch
        {
            "gemini" => "gemini-3.1-flash-image-preview",
            "openai" => "gpt-image-2.5-flare",
            _ => string.Empty
        };
    }

    /// <summary>What was configured, or the default for this provider.</summary>
    internal static string Or(this string configured, string fallback) =>
        configured.Length > 0 ? configured : fallback;
}
