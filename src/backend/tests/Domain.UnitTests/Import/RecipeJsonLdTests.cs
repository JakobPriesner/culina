using Domain.Import;

namespace Domain.UnitTests.Import;

/// <summary>Reading the structured data a recipe site publishes; every shape here is one real sites emit.</summary>
public class RecipeJsonLdTests
{
    [Fact]
    public void Read_ShouldTakeTheOrdinaryShape()
    {
        const string json = """
            {
              "@context": "https://schema.org",
              "@type": "Recipe",
              "name": "Lemon orzo",
              "recipeIngredient": ["200 g orzo", "2 courgettes"],
              "recipeInstructions": ["Boil the orzo.", "Fry the courgettes."],
              "recipeYield": "4 servings",
              "totalTime": "PT35M"
            }
            """;

        var recipe = RecipeJsonLd.Read(json);

        Assert.NotNull(recipe);
        Assert.Equal("Lemon orzo", recipe.Title);
        Assert.Equal(["200 g orzo", "2 courgettes"], recipe.IngredientLines);
        Assert.Equal(["Boil the orzo.", "Fry the courgettes."], recipe.Steps);
        Assert.Equal(4m, recipe.Servings);
        Assert.Equal(35, recipe.TotalMinutes);
    }

    [Fact]
    public void Read_ShouldFindTheRecipeInsideAGraph()
    {
        // What most WordPress sites emit: the recipe is one node among page, organisation and author.
        const string json = """
            {
              "@context": "https://schema.org",
              "@graph": [
                { "@type": "WebSite", "name": "A blog" },
                { "@type": ["Recipe", "NewsArticle"], "name": "Pancakes",
                  "recipeIngredient": ["125 g flour"] }
              ]
            }
            """;

        var recipe = RecipeJsonLd.Read(json);

        Assert.NotNull(recipe);
        Assert.Equal("Pancakes", recipe.Title);
    }

    [Fact]
    public void Read_ShouldTakeTheWordsOutOfHowToSteps()
    {
        const string json = """
            {
              "@type": "Recipe",
              "recipeInstructions": [
                { "@type": "HowToStep", "text": "Heat the oven." },
                { "@type": "HowToStep", "name": "Ignored", "text": "Mix it all." }
              ]
            }
            """;

        var recipe = RecipeJsonLd.Read(json);

        // `text` before `name`: a step with both means the words; the name is usually a repeated heading.
        Assert.Equal(["Heat the oven.", "Mix it all."], recipe!.Steps);
    }

    [Fact]
    public void Read_ShouldFlattenASectionIntoItsSteps()
    {
        const string json = """
            {
              "@type": "Recipe",
              "recipeInstructions": [
                { "@type": "HowToSection", "name": "For the sauce",
                  "itemListElement": [
                    { "@type": "HowToStep", "text": "Soften the onion." },
                    { "@type": "HowToStep", "text": "Add the tomatoes." }
                  ] }
              ]
            }
            """;

        var recipe = RecipeJsonLd.Read(json);

        // The section's own name is a heading, not a step.
        Assert.Equal(["Soften the onion.", "Add the tomatoes."], recipe!.Steps);
    }

    [Fact]
    public void Read_ShouldTakeInstructionsWrittenAsOneString()
    {
        const string json = """
            { "@type": "Recipe", "recipeInstructions": "Mix everything and bake." }
            """;

        Assert.Equal(["Mix everything and bake."], RecipeJsonLd.Read(json)!.Steps);
    }

    [Theory]
    [InlineData("\"4\"", 4)]
    [InlineData("\"4 servings\"", 4)]
    [InlineData("\"4-6\"", 4)]
    [InlineData("[\"6\", \"6 portions\"]", 6)]
    [InlineData("\"Serves 4\"", 4)]
    [InlineData("\"Makes 12 cookies\"", 12)]
    [InlineData("\"F\u00fcr 4 Personen\"", 4)]
    public void Read_ShouldTakeTheFirstNumberOfTheYield(string yield, int expected)
    {
        var json = $$"""{ "@type": "Recipe", "recipeYield": {{yield}} }""";

        // A range takes its lower bound, as the paste import does.
        Assert.Equal(expected, RecipeJsonLd.Read(json)!.Servings);
    }

    [Theory]
    [InlineData("\"a dozen cookies\"")]
    [InlineData("\"\"")]
    public void Read_ShouldLeaveAYieldItCannotCountAlone(string yield)
    {
        var json = $$"""{ "@type": "Recipe", "recipeYield": {{yield}} }""";

        // No guess: a made-up serving count scales every amount by a lie.
        Assert.Null(RecipeJsonLd.Read(json)!.Servings);
    }

    [Theory]
    [InlineData("about an hour")]
    [InlineData("P99999999D")]
    public void Read_ShouldLeaveATotalTimeItCannotRead(string totalTime)
    {
        var json = $$"""{ "@type": "Recipe", "name": "Stew", "totalTime": "{{totalTime}}" }""";

        var recipe = RecipeJsonLd.Read(json);

        // A duration too long for a TimeSpan is a valid XSD duration that overflows; the rest of the recipe is kept.
        Assert.NotNull(recipe);
        Assert.Equal("Stew", recipe.Title);
        Assert.Null(recipe.TotalMinutes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"@type\": \"NewsArticle\", \"name\": \"Not a recipe\" }")]
    [InlineData("{ \"name\": \"No type\" }")]
    public void Read_ShouldFindNothing_WhenThereIsNoRecipe(string? json)
    {
        // Broken or absent structured data is a page with none: the caller falls back to the words.
        Assert.Null(RecipeJsonLd.Read(json));
    }
}
