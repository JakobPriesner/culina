using System.Text.Json;
using Application.Abstractions;

namespace Infrastructure.Assistance;

/// <summary>The shape a model must answer in, and the reading of that answer.</summary>
/// <remarks>
/// One schema for both providers, enforced by structured outputs while they decode. Every property
/// must be required, so each optional one is a union with <c>null</c>: otherwise the model invents
/// a cooking time. Nothing here is believed: every value still has to survive <c>RecipeParsing</c>.
/// </remarks>
internal static class RecipeSchema
{
    /// <summary>What to call it, where a provider wants a name.</summary>
    internal const string Name = "recipe";

    /// <summary>The same schema as a parsed document, for <c>Microsoft.Extensions.AI</c>.</summary>
    /// <remarks>
    /// Lazy, not a field initialiser: static initialisers run in written order, and reading
    /// <see cref="Definition"/> from above it serialised a null, so OpenAI and Ollama asked for no
    /// shape at all.
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
    /// A value the recipe may not have: a union with null, since every property is required. The
    /// description must say what null means, or the model fills it in.
    /// </summary>
    private static Dictionary<string, object> Maybe(string type, string description) =>
        new() { ["type"] = new[] { type, "null" }, ["description"] = description };

    private static Dictionary<string, object> MaybeText(string description) =>
        Maybe("string", description);

    private static Dictionary<string, object> MaybeNumber(string description) =>
        Maybe("number", description);

    private static Dictionary<string, object> MaybeInteger(string description) =>
        Maybe("integer", description);
}

/// <summary>The answer as it arrives, before anything believes it.</summary>
/// <remarks>
/// Its own type, not <see cref="DraftedRecipe"/>: this one tolerates whatever a model sent, and the
/// mapping is where a missing array becomes an empty one.
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
