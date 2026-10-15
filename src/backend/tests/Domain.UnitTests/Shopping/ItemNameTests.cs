using Domain.Shopping;
using TestSupport;

namespace Domain.UnitTests.Shopping;

/// <summary>What makes two names the same name, and what a name reads as.</summary>
public class ItemNameTests
{
    [Fact]
    public void Equals_ShouldCompareWhatTheNameSays_NotWhetherItWasReadYet()
    {
        var read = ItemName.Create("grüner Spargel").ShouldBeSuccess();
        var unread = ItemName.Create("grüner Spargel").ShouldBeSuccess();

        _ = read.Reading;

        Assert.Equal(unread, read);
        Assert.Equal(unread.GetHashCode(), read.GetHashCode());
        Assert.NotEqual(ItemName.Create("weißer Spargel").ShouldBeSuccess(), read);
    }

    [Fact]
    public void Reading_ShouldGiveTheSameAnswerEveryTime_AndKeepMerging()
    {
        var tomate = ItemName.Create("Tomate").ShouldBeSuccess();
        var tomaten = ItemName.Create("Tomaten").ShouldBeSuccess();

        Assert.Same(tomate.Reading, tomate.Reading);
        Assert.True(ItemMergePolicy.NamesMatch(tomate, tomaten));
        Assert.True(ItemMergePolicy.NamesMatch(tomate, tomaten));
        Assert.False(ItemMergePolicy.NamesMatch(
            ItemName.Create("grüner Spargel").ShouldBeSuccess(),
            ItemName.Create("weißer Spargel").ShouldBeSuccess()));
    }
}
