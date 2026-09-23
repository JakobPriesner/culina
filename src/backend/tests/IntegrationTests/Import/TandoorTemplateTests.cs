using Domain.Import;
using Infrastructure.Import.Tandoor;

namespace IntegrationTests.Import;

/// <summary>
/// Reading the template language Tandoor allows inside a step.
/// </summary>
/// <remarks>
/// <para>
/// Everything here goes through <see cref="TandoorMapping.ToSource(TandoorRecipe)"/>
/// rather than through the reader on its own, because the part that is easy to
/// get wrong is not the parsing. It is the two different ways of counting an
/// ingredient that meet here: Tandoor's index is its own step's, from zero,
/// counting the header rows that are not ingredients, while the reference that
/// comes out has to point into the one flat list this app keeps for the whole
/// recipe.
/// </para>
/// <para>
/// No database and no network. This is still the anti-corruption layer.
/// </para>
/// </remarks>
public class TandoorTemplateTests
{
    [Fact]
    public void AWholeIngredient_ShouldBecomeAReference_RatherThanTheWordsItHasToday()
    {
        // Arrange
        // The reason the syntax exists: the amount in the sentence is supposed
        // to move when the cook changes the servings.
        var theirs = Recipe(
            "Das {{ ingredients[0] }} sieben.",
            Line(200m, "g", "Mehl"));

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal(
            [new SourceTextSegment("Das "), new SourceIngredientReference(0), new SourceTextSegment(" sieben.")],
            step.Segments);
    }

    [Fact]
    public void AnIndex_ShouldBeItsOwnStepsRatherThanTheRecipes()
    {
        // Arrange
        // Both steps say ingredients[0], and they mean different ingredients.
        var theirs = new TandoorRecipe
        {
            Id = 1,
            Name = "Lasagne",
            Steps =
            [
                new TandoorStep { Instruction = "{{ ingredients[0] }} anbraten.", Ingredients = [Line(1m, null, "Zwiebel")] },
                new TandoorStep { Instruction = "{{ ingredients[0] }} unterrühren.", Ingredients = [Line(200m, "g", "Mehl")] }
            ]
        };

        // Act
        var recipe = TandoorMapping.ToSource(theirs);

        // Assert
        // Which is why they come out pointing at different places in the one
        // list this app keeps.
        Assert.Equal(new SourceIngredientReference(0), recipe.Steps[0].Segments[0]);
        Assert.Equal(new SourceIngredientReference(1), recipe.Steps[1].Segments[0]);
        Assert.Equal(["Zwiebel", "Mehl"], recipe.Ingredients.Select(line => line.Name));
    }

    [Fact]
    public void AHeaderRow_ShouldStillBeCounted_BecauseTandoorCountsIt()
    {
        // Arrange
        // A header row is not an ingredient here, but it is a row over there,
        // and the index is a position among rows.
        var theirs = Recipe(
            "{{ ingredients[1] }} unterrühren.",
            new TandoorIngredient { IsHeader = true, Note = "Für den Teig" },
            Line(200m, "g", "Mehl"));

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        // Row one over there, ingredient zero here. Dropping the header from
        // the count would have put the wrong amount in the sentence.
        Assert.Equal(new SourceIngredientReference(0), step.Segments[0]);
    }

    [Theory]
    [InlineData("amount", "200")]
    [InlineData("unit", "g")]
    [InlineData("food", "Mehl")]
    [InlineData("note", "gesiebt")]
    public void AField_ShouldBecomeTheWordsTandoorWouldHaveShown(string field, string expected)
    {
        // Arrange
        // A field on its own has no reference to become: none of them is a
        // whole ingredient, and this app has nowhere to hang a lone amount.
        var theirs = Recipe(
            $"Nimm {{{{ ingredients[0].{field} }}}} davon.",
            Line(200m, "g", "Mehl") with { Note = "gesiebt" });

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal($"Nimm {expected} davon.", Words(step));
        Assert.Empty(step.Segments.OfType<SourceIngredientReference>());
    }

    [Fact]
    public void AField_ShouldBePluralisedTheWayTandoorWouldHave()
    {
        // Arrange
        // Tandoor decides by the amount as written, so this is the last chance
        // to get it right: it is words from here on.
        var theirs = Recipe(
            "{{ ingredients[0].food }} schälen.",
            new TandoorIngredient
            {
                Amount = 3m,
                Food = new TandoorNamed { Name = "Zwiebel", PluralName = "Zwiebeln" }
            });

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal("Zwiebeln schälen.", Words(step));
    }

    [Fact]
    public void AFieldOfARowWithNoAmount_ShouldStaySingular()
    {
        // Arrange
        // "no amount" is Tandoor being told this one is "salt", and it does not
        // pluralise those whatever number it kept around internally.
        var theirs = Recipe(
            "{{ ingredients[0].food }} dazu.",
            new TandoorIngredient
            {
                Amount = 3m,
                NoAmount = true,
                Food = new TandoorNamed { Name = "Salz", PluralName = "Salze" }
            });

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal("Salz dazu.", Words(step));
    }

    [Fact]
    public void AReferenceToARowThatIsNotThere_ShouldLeaveNothingBehind()
    {
        // Arrange
        // Reordering the list over there breaks the reference, which is the
        // documented hazard of the feature. Jinja2 renders it as nothing, so
        // so does this.
        var theirs = Recipe("Das {{ ingredients[9] }} sieben.", Line(200m, "g", "Mehl"));

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal("Das  sieben.", Words(step));
        Assert.Empty(step.Segments.OfType<SourceIngredientReference>());
    }

    [Theory]
    [InlineData("Backen {% if servings %}lange{% endif %}.", "Backen lange.")]
    [InlineData("Backen {{ ingredients|length }}.", "Backen .")]
    [InlineData("Backen {{ ingredients[0].kalorien }}.", "Backen .")]
    public void AnythingElse_ShouldNotSurviveAsBraces(string instruction, string expected)
    {
        // Arrange
        // This is not a Jinja2 engine and must not become one. What matters is
        // that the machinery never reaches a cook: the whole point of the
        // syntax is that it disappears when it is rendered. The words a person
        // wrote between the tags are not machinery, and they stay.
        var theirs = Recipe(instruction, Line(200m, "g", "Mehl"));

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal(expected, Words(step));
    }

    [Fact]
    public void AScaledNumber_ShouldKeepTheNumber()
    {
        // Arrange
        // scale() moves a bare number with the servings, and this app has no
        // way to store that. The number as written beats no number at all.
        var theirs = Recipe("In eine {{ scale(20) }} cm Form geben.", Line(200m, "g", "Mehl"));

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal("In eine 20 cm Form geben.", Words(step));
    }

    [Fact]
    public void AnUnclosedTag_ShouldStayAsWords()
    {
        // Arrange
        // Tandoor refuses the whole instruction here and shows an error in its
        // place. Keeping the characters costs the cook nothing.
        var theirs = Recipe("Das {{ ingredients[0] sieben.", Line(200m, "g", "Mehl"));

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal("Das {{ ingredients[0] sieben.", Words(step));
    }

    [Fact]
    public void AStepThatIsNothingButATemplateWithNothingInIt_ShouldDisappear()
    {
        // Arrange
        var theirs = Recipe("{{ ingredients[9] }}", Line(200m, "g", "Mehl"));

        // Act
        var recipe = TandoorMapping.ToSource(theirs);

        // Assert
        // An empty step is worse than no step, and the ingredient it was
        // carrying has been taken either way.
        Assert.Empty(recipe.Steps);
        Assert.Single(recipe.Ingredients);
    }

    [Fact]
    public void AStepsTitle_ShouldStillBeFoldedIn_AroundTheReferences()
    {
        // Arrange
        var theirs = new TandoorRecipe
        {
            Id = 2,
            Name = "Brot",
            Steps =
            [
                new TandoorStep
                {
                    Name = "Teig",
                    Instruction = "{{ ingredients[0] }} kneten.",
                    Ingredients = [Line(200m, "g", "Mehl")]
                }
            ]
        };

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal(
            [new SourceTextSegment("Teig: "), new SourceIngredientReference(0), new SourceTextSegment(" kneten.")],
            step.Segments);
    }

    [Fact]
    public void LineBreaks_ShouldSurvive_BecauseTheyAreLineBreaksOverThereToo()
    {
        // Arrange
        // Tandoor renders a step's Markdown with the extension that turns a
        // single newline into a line break, so these are the lines a cook
        // reads rather than an accident of how the text was typed.
        var theirs = Recipe("Backen.\nRuhen lassen.\n\nAufschneiden.", Line(200m, "g", "Mehl"));

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal("Backen.\nRuhen lassen.\n\nAufschneiden.", Words(step));
    }

    [Fact]
    public void LineBreaks_ShouldBeTidied_WithoutBeingFlattened()
    {
        // Arrange
        // A carriage return is somebody's editor, two trailing spaces are the
        // other way of asking for a line break and are redundant here, and
        // four newlines are the one gap Tandoor renders them as.
        var theirs = Recipe("Backen.  \r\nRuhen.\n\n\n\nAufschneiden.", Line(200m, "g", "Mehl"));

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal("Backen.\nRuhen.\n\nAufschneiden.", Words(step));
    }

    [Fact]
    public void AReferenceAtTheStartOfALine_ShouldNotSwallowTheBreak()
    {
        // Arrange
        var theirs = Recipe(
            "Teig:\n{{ ingredients[0] }} sieben.",
            Line(200m, "g", "Mehl"));

        // Act
        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Assert
        Assert.Equal(
            [
                new SourceTextSegment("Teig:\n"),
                new SourceIngredientReference(0),
                new SourceTextSegment(" sieben.")
            ],
            step.Segments);
    }

    private static string Words(SourceStep step) =>
        string.Concat(step.Segments.OfType<SourceTextSegment>().Select(segment => segment.Value));

    [Theory]
    // Emphasis is decoration, and a step here is plain text with nowhere to put
    // it. The words are what somebody wrote; the asterisks are how Tandoor was
    // told to draw them.
    [InlineData("**Tipp:** gut kühlen.", "Tipp: gut kühlen.")]
    [InlineData("__Tipp:__ gut kühlen.", "Tipp: gut kühlen.")]
    [InlineData("Das *sofort* servieren.", "Das sofort servieren.")]
    [InlineData("Das _sofort_ servieren.", "Das sofort servieren.")]
    [InlineData("Auf `180 °C` vorheizen.", "Auf 180 °C vorheizen.")]
    [InlineData("# Vorbereitung\nMehl sieben.", "Vorbereitung\nMehl sieben.")]
    [InlineData("### Vorbereitung\nMehl sieben.", "Vorbereitung\nMehl sieben.")]
    public void MarkdownMarkers_ShouldArriveAsTheWordsTheyWrapped(string theirs, string expected)
    {
        // Act
        var step = Assert.Single(TandoorMapping.ToSource(Recipe(theirs)).Steps);

        // Assert
        Assert.Equal([new SourceTextSegment(expected)], step.Segments);
    }

    [Theory]
    // Timid on purpose: a recipe is full of characters that only look like
    // Markdown, and turning "2 * 3" into "2 3" would be a worse bug than the
    // one being fixed.
    [InlineData("2 * 3 Portionen.")]
    [InlineData("Creme_fraiche unterheben.")]
    [InlineData("Ein * allein.")]
    [InlineData("- Mehl\n- Zucker")]
    [InlineData("Salz & Pfeffer (nach Gefühl).")]
    public void ThingsThatOnlyLookLikeMarkdown_ShouldSurviveUntouched(string theirs)
    {
        // Act
        var step = Assert.Single(TandoorMapping.ToSource(Recipe(theirs)).Steps);

        // Assert
        Assert.Equal([new SourceTextSegment(theirs)], step.Segments);
    }

    private static TandoorRecipe Recipe(string instruction, params TandoorIngredient[] ingredients) =>
        new()
        {
            Id = 42,
            Name = "Etwas",
            Steps = [new TandoorStep { Instruction = instruction, Ingredients = ingredients }]
        };

    private static TandoorIngredient Line(decimal amount, string? unit, string food) =>
        new()
        {
            Amount = amount,
            Unit = unit is null ? null : new TandoorNamed { Name = unit },
            Food = new TandoorNamed { Name = food }
        };
}
