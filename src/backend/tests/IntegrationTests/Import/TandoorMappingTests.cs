using Domain.Import;
using Infrastructure.Import.Tandoor;

namespace IntegrationTests.Import;

/// <summary>
/// Reading Tandoor's shapes into ours.
/// </summary>
/// <remarks>
/// No database and no network: this is the anti-corruption layer, and what it
/// has to get right is a difference in modelling rather than anything about
/// either app's storage. Tandoor hangs ingredients off steps; this app keeps
/// one list per recipe. Everything here is about that seam.
/// </remarks>
public class TandoorMappingTests
{
    [Fact]
    public void ARecipeWithoutHeaders_ShouldBecomeOneUnnamedIngredientList()
    {
        // Arrange
        // The ordinary case: every ingredient on step one.
        var theirs = new TandoorRecipe
        {
            Id = 7,
            Name = "Zwiebelkuchen",
            Steps =
            [
                new TandoorStep
                {
                    Name = "Zubereitung",
                    Instruction = "Teig kneten.",
                    Ingredients = [Line(200m, "g", "Mehl")]
                }
            ]
        };

        // Act
        var recipe = TandoorMapping.ToSource(theirs);

        // Assert
        // Unnamed, because "Zubereitung" as a heading over the whole ingredient
        // list is a heading nobody wrote.
        var group = Assert.Single(recipe.Groups);
        Assert.Null(group.Name);
        Assert.Equal("Mehl", Assert.Single(group.Ingredients).Name);
    }

    [Fact]
    public void AHeaderStepWithIngredients_ShouldBecomeANamedGroup()
    {
        // Arrange
        // "For the sauce:" with things under it is exactly an ingredient group.
        var theirs = new TandoorRecipe
        {
            Id = 8,
            Name = "Lasagne",
            Steps =
            [
                new TandoorStep { Name = "Für die Soße", ShowAsHeader = true, Ingredients = [Line(1m, null, "Zwiebel")] },
                new TandoorStep { Name = "Für den Teig", ShowAsHeader = true, Ingredients = [Line(300m, "g", "Mehl")] }
            ]
        };

        // Act
        var recipe = TandoorMapping.ToSource(theirs);

        // Assert
        Assert.Equal(["Für die Soße", "Für den Teig"], recipe.Groups.Select(group => group.Name));
    }

    [Fact]
    public void AHeaderStepWithNoInstruction_ShouldNotBecomeAnEmptyStep()
    {
        // Arrange
        var theirs = new TandoorRecipe
        {
            Id = 9,
            Name = "Lasagne",
            Steps =
            [
                new TandoorStep { Name = "Für die Soße", ShowAsHeader = true, Ingredients = [Line(1m, null, "Zwiebel")] },
                new TandoorStep { Instruction = "Alles schichten.", Time = 15 }
            ]
        };

        // Act
        var recipe = TandoorMapping.ToSource(theirs);

        // Assert
        // The header carried only its ingredients, and those have been taken.
        var step = Assert.Single(recipe.Steps);
        Assert.Equal("Alles schichten.", Words(step));

        // Minutes over there, seconds here, because a step can be "rest 30
        // seconds".
        Assert.Equal(900, step.Seconds);
    }

    [Fact]
    public void AnIngredientMarkedNoAmount_ShouldKeepNoAmount()
    {
        // Arrange
        // Tandoor keeps a 1 around internally for these; honouring the flag is
        // the difference between "salt" and "1 salt".
        var theirs = Recipe(new TandoorIngredient
        {
            Amount = 1m,
            NoAmount = true,
            Food = new TandoorNamed { Name = "Salz" }
        });

        // Act
        var recipe = TandoorMapping.ToSource(theirs);

        // Assert
        var line = Assert.Single(recipe.Ingredients);
        Assert.Null(line.Amount);
        Assert.Equal("Salz", line.Name);
    }

    [Fact]
    public void AnIngredientWithNoFood_ShouldFallBackToWhatWasOriginallyWritten()
    {
        // Arrange
        var theirs = Recipe(new TandoorIngredient { OriginalText = "etwas Petersilie" });

        // Act
        var recipe = TandoorMapping.ToSource(theirs);

        // Assert
        Assert.Equal("etwas Petersilie", Assert.Single(recipe.Ingredients).Name);
    }

    [Fact]
    public void AHeaderRowInsideTheIngredientList_ShouldNotBecomeAnIngredient()
    {
        // Arrange
        var theirs = Recipe(
            new TandoorIngredient { IsHeader = true, Food = new TandoorNamed { Name = "Für die Soße" } },
            Line(1m, null, "Zwiebel"));

        // Act
        var recipe = TandoorMapping.ToSource(theirs);

        // Assert
        Assert.Equal("Zwiebel", Assert.Single(recipe.Ingredients).Name);
    }

    [Fact]
    public void AKeyword_ShouldBecomeItsLeafName_AndNotItsPath()
    {
        // Arrange
        var theirs = new TandoorRecipe
        {
            Id = 11,
            Name = "Pasta",
            Keywords = [new TandoorKeyword { Name = "Italienisch", Label = "Küche > Italienisch" }]
        };

        // Act
        var recipe = TandoorMapping.ToSource(theirs);

        // Assert
        // A tag with a path in it is a tag nobody ever types.
        Assert.Equal(["Italienisch"], recipe.Tags);
    }

    [Fact]
    public void ARecipeWithNothingInIt_ShouldStillMap()
    {
        // Arrange
        // Five years of Tandoor versions leave rows like this behind, and the
        // import has to survive them rather than stop on them.
        var theirs = new TandoorRecipe { Id = 12 };

        // Act
        var recipe = TandoorMapping.ToSource(theirs);

        // Assert
        Assert.Equal("12", recipe.ExternalId);
        Assert.Empty(recipe.Groups);
        Assert.Empty(recipe.Steps);
    }

    /// <summary>What a step says, references aside.</summary>
    private static string Words(SourceStep step) =>
        string.Concat(step.Segments.OfType<SourceTextSegment>().Select(segment => segment.Value));

    private static TandoorRecipe Recipe(params TandoorIngredient[] ingredients) =>
        new()
        {
            Id = 10,
            Name = "Etwas",
            Steps = [new TandoorStep { Instruction = "Kochen.", Ingredients = ingredients }]
        };

    private static TandoorIngredient Line(decimal amount, string? unit, string food) =>
        new()
        {
            Amount = amount,
            Unit = unit is null ? null : new TandoorNamed { Name = unit },
            Food = new TandoorNamed { Name = food }
        };
}
