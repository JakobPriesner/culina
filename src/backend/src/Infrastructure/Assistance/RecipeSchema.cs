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
/// Not OpenAI's <c>strict</c> mode, deliberately. Strict requires every
/// property to be listed as required and every optional one to be spelled as a
/// nullable union, which doubles the size of this file to express "a recipe may
/// not say how long it takes". The looser mode still constrains the shape; what
/// it does not constrain, the domain refuses a step later.
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
    /// What <c>Microsoft.Extensions.AI</c> takes, where the dictionary is what
    /// Google's client takes. Built once: it is constant, and parsing it per
    /// request would be parsing the same bytes for the life of the process.
    /// </remarks>
    internal static JsonElement AsJson { get; } =
        JsonSerializer.SerializeToElement(Definition, AssistantHttp.Json);

    /// <summary>The schema, as both providers take it.</summary>
    internal static IReadOnlyDictionary<string, object> Definition { get; } = Object(
        new Dictionary<string, object>
        {
            ["title"] = Text("What the dish is called."),
            ["description"] = Text("A sentence or two about it. May be omitted."),
            ["yieldAmount"] = Number("How many it makes."),
            ["yieldLabel"] = Text("What it makes: servings, or a cake, or jars."),
            ["prepMinutes"] = Integer("Minutes of hands-on work."),
            ["cookMinutes"] = Integer("Minutes of cooking or baking."),
            ["groups"] = Array(
                "The ingredients, grouped. Use one unnamed group unless the recipe "
                + "genuinely has parts, such as a filling and a topping.",
                Object(
                    new Dictionary<string, object>
                    {
                        ["name"] = Text("The heading. Omit for the only group."),
                        ["ingredients"] = Array(
                            "The lines of this group, in the order they are used.",
                            Object(
                                new Dictionary<string, object>
                                {
                                    ["quantity"] = Number("How much, as a number."),
                                    ["unit"] = Text(
                                        "The unit, as an ordinary abbreviation: g, kg, ml, l, "
                                        + "tsp, tbsp. Omit for a bare count."),
                                    ["name"] = Text("The shoppable noun alone: 'butter'."),
                                    ["note"] = Text("The preparation: 'finely chopped'.")
                                },
                                ["name"]))
                    },
                    ["ingredients"])),
            ["steps"] = Array(
                "The method, one instruction per step.",
                Object(
                    new Dictionary<string, object>
                    {
                        ["title"] = Text("What this step is called. Omit for most steps."),
                        ["text"] = Text("What to do, in plain sentences."),
                        ["durationSeconds"] = Integer(
                            "How long this step waits, when it waits. Omit otherwise.")
                    },
                    ["text"])),
            ["tags"] = Array("A few short labels.", Text("One label."))
        },
        ["title", "groups", "steps"]);

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

    private static Dictionary<string, object> Number(string description) =>
        new() { ["type"] = "number", ["description"] = description };

    private static Dictionary<string, object> Integer(string description) =>
        new() { ["type"] = "integer", ["description"] = description };
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
