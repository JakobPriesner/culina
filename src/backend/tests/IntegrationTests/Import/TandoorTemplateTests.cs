using Domain.Import;
using Infrastructure.Import.Tandoor;

namespace IntegrationTests.Import;

/// <summary>
/// Tandoor step templates, through <see cref="TandoorMapping.ToSource(TandoorRecipe)"/>: Tandoor's
/// index is per step, zero-based and counts header rows, while the reference must point into this
/// app's flat list.
/// </summary>
public class TandoorTemplateTests
{
    [Fact]
    public void AWholeIngredient_ShouldBecomeAReference_RatherThanTheWordsItHasToday()
    {
        // The amount in the sentence must move when the cook changes the servings.
        var theirs = Recipe(
            "Das {{ ingredients[0] }} sieben.",
            Line(200m, "g", "Mehl"));

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal(
            [new SourceTextSegment("Das "), new SourceIngredientReference(0), new SourceTextSegment(" sieben.")],
            step.Segments);
    }

    [Fact]
    public void AnIndex_ShouldBeItsOwnStepsRatherThanTheRecipes()
    {
        // Both steps say ingredients[0] and mean different ingredients.
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

        var recipe = TandoorMapping.ToSource(theirs);

        Assert.Equal(new SourceIngredientReference(0), recipe.Steps[0].Segments[0]);
        Assert.Equal(new SourceIngredientReference(1), recipe.Steps[1].Segments[0]);
        Assert.Equal(["Zwiebel", "Mehl"], recipe.Ingredients.Select(line => line.Name));
    }

    [Fact]
    public void AHeaderRow_ShouldStillBeCounted_BecauseTandoorCountsIt()
    {
        // A header row is not an ingredient here but is a row over there, and the index counts
        // rows.
        var theirs = Recipe(
            "{{ ingredients[1] }} unterrühren.",
            new TandoorIngredient { IsHeader = true, Note = "Für den Teig" },
            Line(200m, "g", "Mehl"));

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        // Row one over there, ingredient zero here; ignoring the header would give the wrong
        // amount.
        Assert.Equal(new SourceIngredientReference(0), step.Segments[0]);
    }

    [Theory]
    [InlineData("amount", "200")]
    [InlineData("unit", "g")]
    [InlineData("food", "Mehl")]
    [InlineData("note", "gesiebt")]
    public void AField_ShouldBecomeTheWordsTandoorWouldHaveShown(string field, string expected)
    {
        // A lone field has no reference to become, so it is written out as words.
        var theirs = Recipe(
            $"Nimm {{{{ ingredients[0].{field} }}}} davon.",
            Line(200m, "g", "Mehl") with { Note = "gesiebt" });

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal($"Nimm {expected} davon.", Words(step));
        Assert.Empty(step.Segments.OfType<SourceIngredientReference>());
    }

    [Fact]
    public void AField_ShouldBePluralisedTheWayTandoorWouldHave()
    {
        // Tandoor decides plurals by the amount as written, so it must be right here: words from
        // now on.
        var theirs = Recipe(
            "{{ ingredients[0].food }} schälen.",
            new TandoorIngredient
            {
                Amount = 3m,
                Food = new TandoorNamed { Name = "Zwiebel", PluralName = "Zwiebeln" }
            });

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal("Zwiebeln schälen.", Words(step));
    }

    [Fact]
    public void AFieldOfARowWithNoAmount_ShouldStaySingular()
    {
        // "no amount" means a bare "salt", which Tandoor never pluralises.
        var theirs = Recipe(
            "{{ ingredients[0].food }} dazu.",
            new TandoorIngredient
            {
                Amount = 3m,
                NoAmount = true,
                Food = new TandoorNamed { Name = "Salz", PluralName = "Salze" }
            });

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal("Salz dazu.", Words(step));
    }

    [Fact]
    public void AReferenceToARowThatIsNotThere_ShouldLeaveNothingBehind()
    {
        // Reordering the list over there breaks the reference; Jinja2 renders it as nothing, so
        // does this.
        var theirs = Recipe("Das {{ ingredients[9] }} sieben.", Line(200m, "g", "Mehl"));

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal("Das  sieben.", Words(step));
        Assert.Empty(step.Segments.OfType<SourceIngredientReference>());
    }

    [Theory]
    [InlineData("Backen {% if servings %}lange{% endif %}.", "Backen lange.")]
    [InlineData("Backen {{ ingredients|length }}.", "Backen .")]
    [InlineData("Backen {{ ingredients[0].kalorien }}.", "Backen .")]
    public void AnythingElse_ShouldNotSurviveAsBraces(string instruction, string expected)
    {
        // Not a Jinja2 engine: the machinery must never reach a cook, but the words between tags
        // stay.
        var theirs = Recipe(instruction, Line(200m, "g", "Mehl"));

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal(expected, Words(step));
    }

    [Fact]
    public void AScaledNumber_ShouldKeepTheNumber()
    {
        // scale() moves a bare number with the servings, which this app cannot store; keep the
        // number.
        var theirs = Recipe("In eine {{ scale(20) }} cm Form geben.", Line(200m, "g", "Mehl"));

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal("In eine 20 cm Form geben.", Words(step));
    }

    [Fact]
    public void AnUnclosedTag_ShouldStayAsWords()
    {
        // Tandoor shows an error here; keeping the characters costs the cook nothing.
        var theirs = Recipe("Das {{ ingredients[0] sieben.", Line(200m, "g", "Mehl"));

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal("Das {{ ingredients[0] sieben.", Words(step));
    }

    [Fact]
    public void AStepThatIsNothingButATemplateWithNothingInIt_ShouldDisappear()
    {
        var theirs = Recipe("{{ ingredients[9] }}", Line(200m, "g", "Mehl"));

        var recipe = TandoorMapping.ToSource(theirs);

        Assert.Empty(recipe.Steps);
        Assert.Single(recipe.Ingredients);
    }

    [Fact]
    public void AStepsTitle_ShouldStillBeFoldedIn_AroundTheReferences()
    {
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

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal(
            [new SourceTextSegment("Teig: "), new SourceIngredientReference(0), new SourceTextSegment(" kneten.")],
            step.Segments);
    }

    [Fact]
    public void LineBreaks_ShouldSurvive_BecauseTheyAreLineBreaksOverThereToo()
    {
        // Tandoor renders single newlines as line breaks, so they are the lines a cook reads.
        var theirs = Recipe("Backen.\nRuhen lassen.\n\nAufschneiden.", Line(200m, "g", "Mehl"));

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal("Backen.\nRuhen lassen.\n\nAufschneiden.", Words(step));
    }

    [Fact]
    public void LineBreaks_ShouldBeTidied_WithoutBeingFlattened()
    {
        // A carriage return, trailing spaces and four newlines collapse to what Tandoor renders.
        var theirs = Recipe("Backen.  \r\nRuhen.\n\n\n\nAufschneiden.", Line(200m, "g", "Mehl"));

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

        Assert.Equal("Backen.\nRuhen.\n\nAufschneiden.", Words(step));
    }

    [Fact]
    public void AReferenceAtTheStartOfALine_ShouldNotSwallowTheBreak()
    {
        var theirs = Recipe(
            "Teig:\n{{ ingredients[0] }} sieben.",
            Line(200m, "g", "Mehl"));

        var step = Assert.Single(TandoorMapping.ToSource(theirs).Steps);

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
    // Emphasis is decoration, and a step here is plain text.
    [InlineData("**Tipp:** gut kühlen.", "Tipp: gut kühlen.")]
    [InlineData("__Tipp:__ gut kühlen.", "Tipp: gut kühlen.")]
    [InlineData("Das *sofort* servieren.", "Das sofort servieren.")]
    [InlineData("Das _sofort_ servieren.", "Das sofort servieren.")]
    [InlineData("Auf `180 °C` vorheizen.", "Auf 180 °C vorheizen.")]
    [InlineData("# Vorbereitung\nMehl sieben.", "Vorbereitung\nMehl sieben.")]
    [InlineData("### Vorbereitung\nMehl sieben.", "Vorbereitung\nMehl sieben.")]
    public void MarkdownMarkers_ShouldArriveAsTheWordsTheyWrapped(string theirs, string expected)
    {
        var step = Assert.Single(TandoorMapping.ToSource(Recipe(theirs)).Steps);

        Assert.Equal([new SourceTextSegment(expected)], step.Segments);
    }

    [Theory]
    // Timid on purpose: "2 * 3" must not become "2 3".
    [InlineData("2 * 3 Portionen.")]
    [InlineData("Creme_fraiche unterheben.")]
    [InlineData("Ein * allein.")]
    [InlineData("- Mehl\n- Zucker")]
    [InlineData("Salz & Pfeffer (nach Gefühl).")]
    public void ThingsThatOnlyLookLikeMarkdown_ShouldSurviveUntouched(string theirs)
    {
        var step = Assert.Single(TandoorMapping.ToSource(Recipe(theirs)).Steps);

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
