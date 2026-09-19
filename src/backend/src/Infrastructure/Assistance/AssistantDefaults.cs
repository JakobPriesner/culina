using Domain.Assistance;

namespace Infrastructure.Assistance;

/// <summary>
/// Where each provider lives, and which of its models to use when nobody said.
/// </summary>
/// <remarks>
/// <para>
/// So that connecting an assistant is choosing a provider and pasting a key.
/// Everything here is something the provider decides rather than something an
/// administrator has an opinion about: the address of Google's API is not a
/// deployment choice, and neither is which model is the sensible default one.
/// Both stay overridable, because a gateway and a pinned model are real things
/// people need — but needing them is the exception and the settings screen now
/// treats it as one.
/// </para>
/// <para>
/// <b>Model names current as of 2026-09-19.</b> They move faster than anything
/// else in this file: an administrator who pins one is insulated, and one who
/// does not gets whatever this says. Worth revisiting whenever the price table
/// below it is.
/// </para>
/// </remarks>
internal static class AssistantDefaults
{
    /// <summary>The provider's own address, for everything but a local model.</summary>
    /// <param name="kind">Which provider.</param>
    internal static string Home(AssistantKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return kind.Code switch
        {
            "gemini" => "https://generativelanguage.googleapis.com",
            "openai" => "https://api.openai.com",
            // Only a hint. Ollama is wherever it was put, so the address is
            // required and this is what the settings screen suggests.
            _ => OllamaAssistant.UsualAddress
        };
    }

    /// <summary>The model that writes recipes, when none was chosen.</summary>
    /// <param name="kind">Which provider.</param>
    internal static string ComposeModel(AssistantKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return kind.Code switch
        {
            // The fast one of the current generation rather than the clever
            // one: a recipe is a page of text in a fixed shape, and the
            // difference between the two is a few seconds and several times the
            // price on a task neither finds hard.
            "gemini" => "gemini-3-flash-preview",
            "openai" => "gpt-6-astra",
            // Widely installed, small enough to run on a laptop, and good
            // enough at filling in a schema. Anybody running something better
            // knows what they have.
            _ => "llama3.2"
        };
    }

    /// <summary>The model that draws pictures, when none was chosen.</summary>
    /// <param name="kind">Which provider.</param>
    /// <remarks>
    /// Empty for a provider that does not draw, which is the honest answer
    /// rather than a name that would fail on use.
    /// </remarks>
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
    /// <param name="configured">The administrator's choice, possibly empty.</param>
    /// <param name="fallback">What to use instead.</param>
    internal static string Or(this string configured, string fallback) =>
        configured.Length > 0 ? configured : fallback;
}
