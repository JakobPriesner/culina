using Infrastructure.Assistance;

namespace IntegrationTests.Assistance;

/// <summary>
/// Reading a recipe out of JSON that has not finished arriving (<c>PartialRecipe</c> is internal to
/// Infrastructure; no database needed).
/// </summary>
public class PartialRecipeTests
{
    [Fact]
    public void Read_ShouldSayNothing_BeforeTheFirstFieldIsFinished()
    {
        var answer = new PartialRecipe();

        answer.Add("{\"title\":\"Linsen");

        // The title is being typed: never display a value the model has not finished saying.
        Assert.Null(answer.Read());
    }

    [Fact]
    public void Read_ShouldGiveTheTitle_OnceTheModelHasMovedOn()
    {
        var answer = new PartialRecipe();

        answer.Add("{\"title\":\"Linsensuppe\",\"desc");

        Assert.Equal("Linsensuppe", answer.Read()?.Title);
    }

    [Fact]
    public void Read_ShouldKeepTheFinishedLines_AndDropTheOneStillBeingWritten()
    {
        var answer = new PartialRecipe();

        answer.Add(
            "{\"title\":\"Linsensuppe\",\"groups\":[{\"ingredients\":["
            + "{\"name\":\"Linsen\"},{\"name\":\"Suppeng");

        var read = answer.Read();

        var lines = read?.Groups.SelectMany(group => group.Ingredients).ToList();

        Assert.NotNull(lines);
        Assert.Single(lines);
        Assert.Equal("Linsen", lines[0].Name);
    }

    [Fact]
    public void Read_ShouldSayNothingTwice_WhenNothingNewCanBeRead()
    {
        var answer = new PartialRecipe();

        answer.Add("{\"title\":\"Linsensuppe\",\"description\":\"");

        Assert.NotNull(answer.Read());

        answer.Add("Eine warme");

        // An event per delta would repeat the same draft many times.
        Assert.Null(answer.Read());
    }

    [Fact]
    public void Read_ShouldNotBeFooled_ByAKeywordHalfWritten()
    {
        var answer = new PartialRecipe();

        // A structured answer says "null" out loud, so a half-written keyword is the commonest
        // buffer end.
        answer.Add("{\"title\":\"Linsensuppe\",\"description\":nul");

        var read = answer.Read();

        // Cut back to the comma: the keyword is never closed or guessed, just not part of the
        // prefix yet.
        Assert.Equal("Linsensuppe", read?.Title);
        Assert.Null(read?.Description);
    }

    [Fact]
    public void Read_ShouldKeepANullAsAnAbsence()
    {
        var answer = new PartialRecipe();

        answer.Add("{\"title\":\"Linsensuppe\",\"prepMinutes\":null,\"cookMinutes\":30,");

        var read = answer.Read();

        // Different facts: the recipe gives no prep time but says cooking takes half an hour.
        Assert.Null(read?.PrepMinutes);
        Assert.Equal(30, read?.CookMinutes);
    }

    [Fact]
    public void Read_ShouldNotBeFooled_ByPunctuationInsideAValue()
    {
        var answer = new PartialRecipe();

        // A comma or brace inside a string is not the end of anything.
        answer.Add("{\"title\":\"Suppe, mit {Linsen}\",\"steps\":[");

        Assert.Equal("Suppe, mit {Linsen}", answer.Read()?.Title);
    }

    [Fact]
    public void ReadWhole_ShouldRefuseAnAnswerThatStops()
    {
        var answer = new PartialRecipe();

        answer.Add("{\"title\":\"Linsensuppe\",\"steps\":[{\"text\":\"Alles kochen\"}");

        // The strict read: quietly repairing a cut-off recipe would hide the failure from the
        // person who asked.
        Assert.Null(answer.ReadWhole());
    }

    [Fact]
    public void ReadWhole_ShouldGiveTheRecipe_WhenTheAnswerIsWhole()
    {
        var answer = new PartialRecipe();

        answer.Add(
            "{\"title\":\"Linsensuppe\",\"groups\":[{\"ingredients\":[{\"name\":\"Linsen\"}]}],"
            + "\"steps\":[{\"text\":\"Alles kochen\"}]}");

        var read = answer.ReadWhole();

        Assert.NotNull(read);
        Assert.Equal("Linsensuppe", read.Title);
        Assert.Equal("Alles kochen", Assert.Single(read.Steps).Text);
    }

    [Fact]
    public void SoFar_ShouldStillGiveWhatWasWritten_AfterReadHasSeenIt()
    {
        var answer = new PartialRecipe();

        answer.Add("{\"title\":\"Linsensuppe\",\"steps\":[");

        Assert.NotNull(answer.Read());

        var soFar = answer.SoFar();

        // What a broken stream offers: a title that was paid for is worth keeping.
        Assert.Equal("Linsensuppe", soFar.Title);
    }

    [Fact]
    public void SoFar_ShouldBeEmptyRatherThanNull_WhenNothingCanBeRead()
    {
        var answer = new PartialRecipe();

        answer.Add("I am afraid I cannot help with that.");

        var soFar = answer.SoFar();

        Assert.Null(soFar.Title);
        Assert.Empty(soFar.Groups);
        Assert.Empty(soFar.Steps);
    }
}
