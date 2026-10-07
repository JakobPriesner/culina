namespace Domain.Assistance;

/// <summary>Whether a model a provider listed makes pictures.</summary>
/// <remarks>
/// No provider states this, so it reads the name. It decides which picker a model appears in (drawing job vs
/// the three writing jobs), so a wrong answer either offers a model that fails or hides one somebody paid for.
/// One rule here rather than per adapter, since gateways serve the same names.
/// </remarks>
public static class DrawingModel
{
    // "image" covers gemini-*-image-*, imagen-* and gpt-image-*; dall-e has no "image" in its name; "banana" is
    // Google's nickname in display names; "diffusion" is what self-hosted ones are called.
    private static readonly string[] Marks = ["image", "dall-e", "banana", "diffusion"];

    /// <summary>Whether this one draws; <paramref name="label"/> is read too since Google's image models are named after a fruit in the display name.</summary>
    public static bool Draws(string? id, string? label = null) =>
        Marked(id) || Marked(label);

    private static bool Marked(string? name) =>
        name is not null
        && Array.Exists(Marks, mark => name.Contains(mark, StringComparison.OrdinalIgnoreCase));
}
