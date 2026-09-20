using Infrastructure.Assistance;

namespace IntegrationTests.Assistance;

/// <summary>
/// Reading a recipe out of JSON that has not finished arriving.
/// </summary>
/// <remarks>
/// In the integration project because <c>PartialRecipe</c> is internal to
/// Infrastructure, which is the assembly this one can see. Nothing here needs a
/// database; it is here for visibility rather than for a fixture.
/// </remarks>
public class PartialRecipeTests
{
    [Fact]
    public void Read_ShouldSayNothing_BeforeTheFirstFieldIsFinished()
    {
        // Arrange
        var answer = new PartialRecipe();

        // Act
        answer.Add("{\"title\":\"Linsen");

        // Assert
        // The title is being typed. Showing "Linsen" and then "Linsensuppe" a
        // moment later is harder to read than showing nothing and then the
        // name, and the whole design here is to never display a value the
        // model has not actually finished saying.
        Assert.Null(answer.Read());
    }

    [Fact]
    public void Read_ShouldGiveTheTitle_OnceTheModelHasMovedOn()
    {
        // Arrange
        var answer = new PartialRecipe();

        // Act
        answer.Add("{\"title\":\"Linsensuppe\",\"desc");

        // Assert
        Assert.Equal("Linsensuppe", answer.Read()?.Title);
    }

    [Fact]
    public void Read_ShouldKeepTheFinishedLines_AndDropTheOneStillBeingWritten()
    {
        // Arrange
        var answer = new PartialRecipe();

        // Act
        answer.Add(
            "{\"title\":\"Linsensuppe\",\"groups\":[{\"ingredients\":["
            + "{\"name\":\"Linsen\"},{\"name\":\"Suppeng");

        var read = answer.Read();

        // Assert
        var lines = read?.Groups.SelectMany(group => group.Ingredients).ToList();

        Assert.NotNull(lines);
        Assert.Single(lines);
        Assert.Equal("Linsen", lines[0].Name);
    }

    [Fact]
    public void Read_ShouldSayNothingTwice_WhenNothingNewCanBeRead()
    {
        // Arrange
        var answer = new PartialRecipe();

        answer.Add("{\"title\":\"Linsensuppe\",\"description\":\"");

        Assert.NotNull(answer.Read());

        // Act
        // Several characters of a description, none of which finishes a value.
        answer.Add("Eine warme");

        // Assert
        // An event per delta would be an event per handful of characters, and
        // every one of them would carry exactly the draft the last one did.
        Assert.Null(answer.Read());
    }

    [Fact]
    public void Read_ShouldNotBeFooled_ByAKeywordHalfWritten()
    {
        // Arrange
        var answer = new PartialRecipe();

        // Act
        // A structured answer says "null" out loud for everything the recipe
        // does not state, so a half-written keyword is now the commonest thing
        // to find at the end of the buffer rather than a curiosity.
        answer.Add("{\"title\":\"Linsensuppe\",\"description\":nul");

        var read = answer.Read();

        // Assert
        // Cut back to the comma, which is why this works: the keyword is never
        // closed or guessed at, it is simply not part of the prefix yet.
        Assert.Equal("Linsensuppe", read?.Title);
        Assert.Null(read?.Description);
    }

    [Fact]
    public void Read_ShouldKeepANullAsAnAbsence()
    {
        // Arrange
        var answer = new PartialRecipe();

        // Act
        answer.Add("{\"title\":\"Linsensuppe\",\"prepMinutes\":null,\"cookMinutes\":30,");

        var read = answer.Read();

        // Assert
        // The two are different facts and stay different: one recipe does not
        // say how long the chopping takes, and it says the cooking takes half
        // an hour.
        Assert.Null(read?.PrepMinutes);
        Assert.Equal(30, read?.CookMinutes);
    }

    [Fact]
    public void Read_ShouldNotBeFooled_ByPunctuationInsideAValue()
    {
        // Arrange
        var answer = new PartialRecipe();

        // Act
        // A comma and a brace inside a string are not the end of anything, and
        // a scanner that thought otherwise would cut the text mid-value and
        // produce a title with half a sentence in it.
        answer.Add("{\"title\":\"Suppe, mit {Linsen}\",\"steps\":[");

        // Assert
        Assert.Equal("Suppe, mit {Linsen}", answer.Read()?.Title);
    }

    [Fact]
    public void ReadWhole_ShouldRefuseAnAnswerThatStops()
    {
        // Arrange
        var answer = new PartialRecipe();

        answer.Add("{\"title\":\"Linsensuppe\",\"steps\":[{\"text\":\"Alles kochen\"}");

        // Assert
        // The strict read, deliberately. A provider that cut out has not
        // written a recipe, and quietly repairing it into one with the last two
        // steps missing would hide that from the person who asked.
        Assert.Null(answer.ReadWhole());
    }

    [Fact]
    public void ReadWhole_ShouldGiveTheRecipe_WhenTheAnswerIsWhole()
    {
        // Arrange
        var answer = new PartialRecipe();

        answer.Add(
            "{\"title\":\"Linsensuppe\",\"groups\":[{\"ingredients\":[{\"name\":\"Linsen\"}]}],"
            + "\"steps\":[{\"text\":\"Alles kochen\"}]}");

        // Act
        var read = answer.ReadWhole();

        // Assert
        Assert.NotNull(read);
        Assert.Equal("Linsensuppe", read.Title);
        Assert.Equal("Alles kochen", Assert.Single(read.Steps).Text);
    }

    [Fact]
    public void SoFar_ShouldStillGiveWhatWasWritten_AfterReadHasSeenIt()
    {
        // Arrange
        var answer = new PartialRecipe();

        answer.Add("{\"title\":\"Linsensuppe\",\"steps\":[");

        Assert.NotNull(answer.Read());

        // Act
        var soFar = answer.SoFar();

        // Assert
        // What the end of a broken stream offers. `Read` has already reported
        // this draft and will not report it again, but a provider that gives up
        // here still wrote a title that was paid for and is worth keeping.
        Assert.Equal("Linsensuppe", soFar.Title);
    }

    [Fact]
    public void SoFar_ShouldBeEmptyRatherThanNull_WhenNothingCanBeRead()
    {
        // Arrange
        var answer = new PartialRecipe();

        answer.Add("I am afraid I cannot help with that.");

        // Act
        var soFar = answer.SoFar();

        // Assert
        Assert.Null(soFar.Title);
        Assert.Empty(soFar.Groups);
        Assert.Empty(soFar.Steps);
    }
}
