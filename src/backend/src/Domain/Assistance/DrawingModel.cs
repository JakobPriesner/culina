namespace Domain.Assistance;

/// <summary>
/// Whether a model a provider listed is one that makes pictures.
/// </summary>
/// <remarks>
/// <para>
/// Not a fact any of the three providers states. Their listings say which
/// models exist and nothing about what comes out of them, so this reads the
/// name — which is the only thing on offer.
/// </para>
/// <para>
/// It decides which picker a model appears in: the drawing job is offered
/// exactly the models this says yes to, and the three writing jobs exactly the
/// ones it says no to. So a wrong answer here is not a cosmetic one. Missing a
/// drawing model puts it in the list for "improve a recipe", where choosing it
/// buys a failed call; the reverse hides a model somebody paid for.
/// </para>
/// <para>
/// One rule, in one place, rather than a copy per adapter: the names are the
/// same names whichever gateway serves them, and an OpenAI-compatible proxy in
/// front of a drawing model is a thing people run.
/// </para>
/// </remarks>
public static class DrawingModel
{
    /// <summary>
    /// The marks a provider puts on a model that draws.
    /// </summary>
    /// <remarks>
    /// <c>image</c> covers Google's <c>gemini-*-image-*</c> and every
    /// <c>imagen-*</c>, and OpenAI's <c>gpt-image-*</c>. <c>dall-e</c> is
    /// OpenAI's older family, which says nothing about images in its name.
    /// <c>banana</c> is Google's nickname for its image models, which appears
    /// in the display name rather than in the id. <c>diffusion</c> is what
    /// self-hosted ones are called.
    /// </remarks>
    private static readonly string[] Marks = ["image", "dall-e", "banana", "diffusion"];

    /// <summary>
    /// Whether this one draws.
    /// </summary>
    /// <param name="id">The model name as it would be sent.</param>
    /// <param name="label">
    /// What the provider shows it as, where that is something else. Read too,
    /// because Google's image models are named after a fruit in the display
    /// name and after nothing in particular in the id.
    /// </param>
    public static bool Draws(string? id, string? label = null) =>
        Marked(id) || Marked(label);

    private static bool Marked(string? name) =>
        name is not null
        && Array.Exists(Marks, mark => name.Contains(mark, StringComparison.OrdinalIgnoreCase));
}
