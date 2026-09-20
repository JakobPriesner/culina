using System.Text.Json;
using Application.Abstractions;

namespace Infrastructure.Assistance;

/// <summary>
/// The shape a model must answer in, and the reading of that answer.
/// </summary>
/// <remarks>
/// <para>
/// One schema for both providers. Gemini calls it <c>responseSchema</c> and
/// OpenAI calls it <c>json_schema</c>, but both take the same subset of JSON
/// Schema and both enforce it on the way out — which is what makes asking a
/// model for a recipe a parsing problem rather than a scraping one.
/// </para>
/// <para>
/// Structured outputs rather than "please answer in JSON", which is the
/// difference between a shape the provider enforces while it decodes and a
/// shape it was asked about. It is what makes reading a half-written answer
/// safe: the text arriving is known to be this schema, so the only question a
/// partial parse has to answer is how much of it has arrived.
/// </para>
/// <para>
/// That mode requires every property to be listed as required, and it is the
/// reason every optional one is spelled as a union with <c>null</c>. Required
/// without nullable would be the worst of the three states this could be in:
/// the model may not omit <c>prepMinutes</c>, so it invents one — and an
/// invented cooking time on a recipe read out of a photograph is the single
/// failure of this feature nobody would catch. Spelled this way, "the recipe
/// does not say" has a value the model can give, and every description below
/// tells it to.
/// </para>
/// <para>
/// The <c>required</c> lists are written out in full rather than left to the
/// client library. It derives exactly these lists for a strict request anyway,
/// and a schema that says one thing here and arrives saying another is a schema
/// nobody can reason about from this file.
/// </para>
/// <para>
/// Nothing here is believed. The schema stops a model answering with prose, and
/// that is all it does — every value still has to survive
/// <c>RecipeParsing</c> before it is a recipe.
/// </para>
/// </remarks>
internal static class RecipeSchema
{
    /// <summary>What to call it, where a provider wants a name.</summary>
    internal const string Name = "recipe";

    /// <summary>
    /// The same schema as a parsed document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What <c>Microsoft.Extensions.AI</c> takes, where the dictionary is what
    /// Google's client takes. Built once: it is constant, and parsing it per
    /// request would be parsing the same bytes for the life of the process.
    /// </para>
    /// <para>
    /// Built lazily rather than in a field initialiser, and that is not a
    /// style choice. Static initialisers run in the order they are written, so
    /// one that read <see cref="Definition"/> from above it serialised a field
    /// that was still null — and <c>null</c> is a perfectly good JSON document.
    /// Nothing failed at startup; every OpenAI and Ollama request simply went
    /// out asking for no particular shape, which the SDK refused with a message
    /// about a schema nobody could see. Deferring the read makes the order it
    /// is written in stop mattering.
    /// </para>
    /// </remarks>
    internal static JsonElement AsJson => Serialised.Value;

    private static readonly Lazy<JsonElement> Serialised =
        new(() => JsonSerializer.SerializeToElement(Definition, AssistantHttp.Json));

    /// <summary>The schema, as every provider takes it.</summary>
    internal static IReadOnlyDictionary<string, object> Definition { get; } = Object(
        new Dictionary<string, object>
        {
            ["title"] = Text("What the dish is called."),
            ["description"] = MaybeText(
                "A sentence or two about it, or null where there is nothing to say."),
            ["yieldAmount"] = MaybeNumber("How many it makes, or null if the recipe does not say."),
            ["yieldLabel"] = MaybeText(
                "What it makes: servings, or a cake, or jars. Null if the recipe does not say."),
            ["prepMinutes"] = MaybeInteger(
                "Minutes of hands-on work, or null. Null rather than a guess: a number here is "
                + "read as something the recipe stated."),
            ["cookMinutes"] = MaybeInteger(
                "Minutes of cooking or baking, or null. Null rather than a guess."),
            ["groups"] = Array(
                "The ingredients, grouped. Use one unnamed group unless the recipe "
                + "genuinely has parts, such as a filling and a topping.",
                Object(
                    new Dictionary<string, object>
                    {
                        ["name"] = MaybeText("The heading. Null for the only group."),
                        ["ingredients"] = Array(
                            "The lines of this group, in the order they are used.",
                            Object(
                                new Dictionary<string, object>
                                {
                                    ["quantity"] = MaybeNumber(
                                        "How much, as a number, or null for a line that gives "
                                        + "no amount."),
                                    ["unit"] = MaybeText(
                                        "The unit, as an ordinary abbreviation: g, kg, ml, l, "
                                        + "tsp, tbsp. Null for a bare count."),
                                    ["name"] = Text("The shoppable noun alone: 'butter'."),
                                    ["note"] = MaybeText(
                                        "The preparation: 'finely chopped'. Null where there "
                                        + "is none.")
                                },
                                ["quantity", "unit", "name", "note"]))
                    },
                    ["name", "ingredients"])),
            ["steps"] = Array(
                "The method, one instruction per step.",
                Object(
                    new Dictionary<string, object>
                    {
                        ["title"] = MaybeText(
                            "What this step is called. Null for most steps."),
                        ["text"] = Text("What to do, in plain sentences."),
                        ["durationSeconds"] = MaybeInteger(
                            "How long this step waits, when it waits. Null otherwise.")
                    },
                    ["title", "text", "durationSeconds"])),
            ["tags"] = Array("A few short labels. An empty list where none fit.", Text("One label."))
        },
        [
            "title",
            "description",
            "yieldAmount",
            "yieldLabel",
            "prepMinutes",
            "cookMinutes",
            "groups",
            "steps",
            "tags"
        ]);

    private static Dictionary<string, object> Object(
        Dictionary<string, object> properties,
        string[] required) => new()
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };

    private static Dictionary<string, object> Array(string description, object items) => new()
    {
        ["type"] = "array",
        ["description"] = description,
        ["items"] = items
    };

    private static Dictionary<string, object> Text(string description) =>
        new() { ["type"] = "string", ["description"] = description };

    /// <summary>
    /// A value the recipe may simply not have.
    /// </summary>
    /// <param name="type">What it is when it is there.</param>
    /// <param name="description">
    /// What it means, and what null means. Both halves matter: a model told
    /// only what the field is will fill it in.
    /// </param>
    /// <remarks>
    /// A union rather than an absence, because a structured output lists every
    /// property as required. This is how "there is no cooking time" is said in
    /// a schema that does not let anything be left out.
    /// </remarks>
    private static Dictionary<string, object> Maybe(string type, string description) =>
        new() { ["type"] = new[] { type, "null" }, ["description"] = description };

    private static Dictionary<string, object> MaybeText(string description) =>
        Maybe("string", description);

    private static Dictionary<string, object> MaybeNumber(string description) =>
        Maybe("number", description);

    private static Dictionary<string, object> MaybeInteger(string description) =>
        Maybe("integer", description);
}

/// <summary>
/// The answer as it arrives, before anything believes it.
/// </summary>
/// <remarks>
/// Its own type rather than deserialising straight into
/// <see cref="DraftedRecipe"/>, because the two have different jobs: this one
/// tolerates whatever a model sent, and that one is what the rest of the
/// application passes around. The mapping between them is where a missing array
/// becomes an empty one.
/// </remarks>
internal sealed record RecipeAnswer
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public decimal? YieldAmount { get; init; }

    public string? YieldLabel { get; init; }

    public int? PrepMinutes { get; init; }

    public int? CookMinutes { get; init; }

    public IReadOnlyList<GroupAnswer>? Groups { get; init; }

    public IReadOnlyList<StepAnswer>? Steps { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Turns the answer into the shape the application works in.</summary>
    internal DraftedRecipe ToDraft() => new()
    {
        Title = Title,
        Description = Description,
        YieldAmount = YieldAmount,
        YieldLabel = YieldLabel,
        PrepMinutes = PrepMinutes,
        CookMinutes = CookMinutes,
        Groups = [.. (Groups ?? []).Select(group => new DraftedGroup
        {
            Name = group.Name,
            Ingredients = [.. (group.Ingredients ?? []).Select(line => new DraftedIngredient
            {
                Quantity = line.Quantity,
                Unit = line.Unit,
                Name = line.Name,
                Note = line.Note
            })]
        })],
        Steps = [.. (Steps ?? []).Select(step => new DraftedStep
        {
            Title = step.Title,
            Text = step.Text,
            DurationSeconds = step.DurationSeconds
        })],
        Tags = [.. Tags ?? []]
    };
}

/// <summary>A group of ingredient lines, as it arrives.</summary>
internal sealed record GroupAnswer
{
    public string? Name { get; init; }

    public IReadOnlyList<IngredientAnswer>? Ingredients { get; init; }
}

/// <summary>One ingredient line, as it arrives.</summary>
internal sealed record IngredientAnswer
{
    public decimal? Quantity { get; init; }

    public string? Unit { get; init; }

    public string? Name { get; init; }

    public string? Note { get; init; }
}

/// <summary>One instruction, as it arrives.</summary>
internal sealed record StepAnswer
{
    public string? Title { get; init; }

    public string? Text { get; init; }

    public int? DurationSeconds { get; init; }
}
