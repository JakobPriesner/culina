using Domain.Import;
using Infrastructure.Import.Mealie;

namespace IntegrationTests.Import;

/// <summary>Reading Mealie's shapes into ours.</summary>
/// <remarks>No database, no network: the anti-corruption layer, whose job is a difference in modelling.</remarks>
public class MealieMappingTests
{
    private static readonly Guid Id = Guid.Parse("6f1c3a5e-2b7d-4e8f-9a0b-1c2d3e4f5a6b");

    [Fact]
    public void ARecipeWithoutHeadings_ShouldBecomeOneUnnamedIngredientList()
    {
        var recipe = MealieMapping.ToSource(Recipe(Line(200m, "g", "Mehl"), Line(1m, null, "Ei")));

        var group = Assert.Single(recipe.Groups);
        Assert.Null(group.Name);
        Assert.Equal(["Mehl", "Ei"], group.Ingredients.Select(ingredient => ingredient.Name));
    }

    [Fact]
    public void AHeadingOnALine_ShouldStartANamedGroupThatIncludesThatLine()
    {
        // Mealie's section title sits on the first line of its section, which is still an ingredient.
        var recipe = MealieMapping.ToSource(Recipe(
            Line(1m, null, "Zwiebel") with { Title = "Für die Soße" },
            Line(2m, "EL", "Öl"),
            Line(300m, "g", "Mehl") with { Title = "Für den Teig" }));

        Assert.Equal(["Für die Soße", "Für den Teig"], recipe.Groups.Select(group => group.Name));
        Assert.Equal(["Zwiebel", "Öl"], recipe.Groups[0].Ingredients.Select(ingredient => ingredient.Name));
    }

    [Fact]
    public void ALineBeforeTheFirstHeading_ShouldBeInAnUnnamedGroup()
    {
        var recipe = MealieMapping.ToSource(Recipe(
            Line(1m, null, "Zwiebel"),
            Line(300m, "g", "Mehl") with { Title = "Für den Teig" }));

        Assert.Equal([null, "Für den Teig"], recipe.Groups.Select(group => group.Name));
    }

    [Fact]
    public void AnUnparsedLine_ShouldKeepItsWordsAsTheName_AndNoAmount()
    {
        // Mealie keeps an unparsed line's text in the note, with a quantity nobody wrote.
        var recipe = MealieMapping.ToSource(Recipe(new MealieIngredient { Quantity = 1m, Note = "2 Tassen Mehl" }));

        var ingredient = Assert.Single(recipe.Ingredients);
        Assert.Equal("2 Tassen Mehl", ingredient.Name);
        Assert.Null(ingredient.Amount);
        Assert.Null(ingredient.Unit);
    }

    [Fact]
    public void AnUnparsedLineWithNoNote_ShouldFallBackToWhatMealieShows()
    {
        var recipe = MealieMapping.ToSource(Recipe(new MealieIngredient { Note = " ", Display = "Salz" }));

        Assert.Equal("Salz", Assert.Single(recipe.Ingredients).Name);
    }

    [Fact]
    public void AnEmptyLine_ShouldBeLeftOut()
    {
        var recipe = MealieMapping.ToSource(Recipe(new MealieIngredient(), Line(1m, null, "Ei")));

        Assert.Equal("Ei", Assert.Single(recipe.Ingredients).Name);
    }

    [Fact]
    public void AQuantityOfNothing_ShouldBeNoAmount()
    {
        // Mealie's default quantity is 0: "salt", not "0 salt".
        var recipe = MealieMapping.ToSource(Recipe(Line(0m, null, "Salz") with { Note = "nach Geschmack" }));

        var ingredient = Assert.Single(recipe.Ingredients);
        Assert.Null(ingredient.Amount);
        Assert.Equal("nach Geschmack", ingredient.Note);
    }

    [Fact]
    public void Servings_ShouldComeFromTheNumber_OrElseFromTheYieldText()
    {
        Assert.Equal(3m, MealieMapping.ToSource(Recipe() with { RecipeServings = 3m }).Servings);
        Assert.Equal(4m, MealieMapping.ToSource(Recipe() with { RecipeServings = 0m, RecipeYield = "4 Portionen" }).Servings);
        Assert.Null(MealieMapping.ToSource(Recipe() with { RecipeServings = 0m, RecipeYield = "ein Blech" }).Servings);
    }

    [Fact]
    public void CookingTime_ShouldBeWhatMealieShowsAsCookingTime()
    {
        var recipe = MealieMapping.ToSource(Recipe() with { PerformTime = "45 minutes", CookTime = "10 minutes" });

        Assert.Equal(45, recipe.CookMinutes);
    }

    [Theory]
    [InlineData("PT1H30M", 90)]
    [InlineData("1 hour 30 minutes", 90)]
    [InlineData("1 Stunde 30 Minuten", 90)]
    [InlineData("1h30m", 90)]
    [InlineData("1,5 Std.", 90)]
    [InlineData("45 Min.", 45)]
    [InlineData("20", 20)]
    [InlineData("2 hours", 120)]
    [InlineData("", null)]
    [InlineData("a while", null)]
    [InlineData("2 days", null)]
    [InlineData("0", null)]
    public void Minutes_ShouldReadHowMealieAndItsPeopleWriteTimes(string written, int? expected)
    {
        Assert.Equal(expected, MealieMapping.Minutes(written));
    }

    [Fact]
    public void TagsAndCategories_ShouldAllBecomeTags_OnceEach()
    {
        var recipe = MealieMapping.ToSource(Recipe() with
        {
            Tags = [new MealieNamed { Name = "Herbst" }, new MealieNamed { Name = " " }],
            RecipeCategory = [new MealieNamed { Name = "Suppe" }, new MealieNamed { Name = "herbst" }]
        });

        Assert.Equal(["Herbst", "Suppe"], recipe.Tags);
    }

    [Fact]
    public void APicture_ShouldBeReadFromMealiesMediaPath_OnlyWhenThereIsOne()
    {
        Assert.Equal(
            $"/api/media/recipes/{Id:D}/images/original.webp",
            MealieMapping.ToSource(Recipe() with { Image = "Zx9q" }).ImageUrl);
        Assert.Null(MealieMapping.ToSource(Recipe() with { Image = null }).ImageUrl);
    }

    [Fact]
    public void AStepHeading_ShouldLeadItsStep_AndAHeadingAlone_ShouldStandAlone()
    {
        var recipe = MealieMapping.ToSource(Recipe() with
        {
            RecipeInstructions =
            [
                new MealieStep { Title = "Anrichten", Text = "Mit Öl beträufeln." },
                new MealieStep { Title = "Guten Appetit", Text = "" },
                new MealieStep { Title = "", Text = "  " }
            ]
        });

        Assert.Equal(["Anrichten: Mit Öl beträufeln.", "Guten Appetit"], recipe.Steps.Select(Words));
    }

    [Fact]
    public void TheIdentity_ShouldBeTheIdNotTheSlug()
    {
        // A slug changes with the name; the id is what an import is remembered by.
        var recipe = MealieMapping.ToSource(Recipe());

        Assert.Equal(Id.ToString("D"), recipe.ExternalId);
    }

    private static string Words(SourceStep step) =>
        string.Concat(step.Segments.OfType<SourceTextSegment>().Select(segment => segment.Value));

    private static MealieRecipe Recipe(params MealieIngredient[] ingredients) =>
        new()
        {
            Id = Id,
            Name = "Etwas",
            RecipeIngredient = ingredients,
            RecipeInstructions = [new MealieStep { Text = "Kochen." }]
        };

    private static MealieIngredient Line(decimal quantity, string? unit, string food) =>
        new()
        {
            Quantity = quantity,
            Unit = unit is null ? null : new MealieNamed { Name = unit },
            Food = new MealieNamed { Name = food }
        };
}
