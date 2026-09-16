using Domain.Import;

namespace Domain.UnitTests.Import;

/// <summary>
/// Reading the structured data a recipe site publishes.
/// </summary>
/// <remarks>
/// It is the honest way to import a recipe — the data the site chose to
/// publish, rather than a guess at what its markup means — and every shape
/// below is one that real sites actually emit. A decade of plugins wrote the
/// same idea six ways.
/// </remarks>
public class RecipeJsonLdTests
{
    [Fact]
    public void Read_ShouldTakeTheOrdinaryShape()
    {
        // Arrange
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

        // Act
        var recipe = RecipeJsonLd.Read(json);

        // Assert
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
        // Arrange
        // What most WordPress sites emit: the recipe is one node among the
        // page, the organisation and the author.
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

        // Act
        var recipe = RecipeJsonLd.Read(json);

        // Assert
        Assert.NotNull(recipe);
        Assert.Equal("Pancakes", recipe.Title);
    }

    [Fact]
    public void Read_ShouldTakeTheWordsOutOfHowToSteps()
    {
        // Arrange
        const string json = """
            {
              "@type": "Recipe",
              "recipeInstructions": [
                { "@type": "HowToStep", "text": "Heat the oven." },
                { "@type": "HowToStep", "name": "Ignored", "text": "Mix it all." }
              ]
            }
            """;

        // Act
        var recipe = RecipeJsonLd.Read(json);

        // Assert
        // `text` before `name`: a step that carries both means the words, and
        // the name is usually a heading repeated.
        Assert.Equal(["Heat the oven.", "Mix it all."], recipe!.Steps);
    }

    [Fact]
    public void Read_ShouldFlattenASectionIntoItsSteps()
    {
        // Arrange
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

        // Act
        var recipe = RecipeJsonLd.Read(json);

        // Assert
        // The section's own name is not a step. "For the sauce" in the method
        // is a heading somebody would try to follow.
        Assert.Equal(["Soften the onion.", "Add the tomatoes."], recipe!.Steps);
    }

    [Fact]
    public void Read_ShouldTakeInstructionsWrittenAsOneString()
    {
        // Arrange
        const string json = """
            { "@type": "Recipe", "recipeInstructions": "Mix everything and bake." }
            """;

        // Act & Assert
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
        // Arrange
        var json = $$"""{ "@type": "Recipe", "recipeYield": {{yield}} }""";

        // Act & Assert
        // A range takes its lower bound, which is the same reading the paste
        // import gives.
        Assert.Equal(expected, RecipeJsonLd.Read(json)!.Servings);
    }

    [Theory]
    [InlineData("\"a dozen cookies\"")]
    [InlineData("\"\"")]
    public void Read_ShouldLeaveAYieldItCannotCountAlone(string yield)
    {
        // Arrange
        var json = $$"""{ "@type": "Recipe", "recipeYield": {{yield}} }""";

        // Act & Assert
        // Rather than guessing. A made-up number of servings scales every
        // amount in the recipe by a lie.
        Assert.Null(RecipeJsonLd.Read(json)!.Servings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"@type\": \"NewsArticle\", \"name\": \"Not a recipe\" }")]
    [InlineData("{ \"name\": \"No type\" }")]
    public void Read_ShouldFindNothing_WhenThereIsNoRecipe(string? json)
    {
        // Arrange & Act & Assert
        // A page with broken or absent structured data is a page with none.
        // Nothing is lost: the caller falls back to reading the words.
        Assert.Null(RecipeJsonLd.Read(json));
    }
}
