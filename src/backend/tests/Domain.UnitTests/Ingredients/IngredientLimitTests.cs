using Domain.Ingredients;
using TestSupport;

namespace Domain.UnitTests.Ingredients;

public class IngredientLimitTests
{
    [Fact]
    public void MergeEnsureRoom_ShouldRefuseANewName_WhenTheHouseholdIsAtItsLimit()
    {
        var existing = Merges(IngredientMerge.MaxPerHousehold);
        var merge = IngredientMerge.Create("Neu", "Salz").ShouldBeSuccess();

        merge.EnsureRoom(existing).ShouldBeFailure(IngredientErrors.TooManyMerges);
    }

    [Fact]
    public void MergeEnsureRoom_ShouldAllowReplacingAMerge_WhenTheHouseholdIsAtItsLimit()
    {
        var existing = Merges(IngredientMerge.MaxPerHousehold);
        var merge = IngredientMerge.Create("Name 7", "Salz").ShouldBeSuccess();

        merge.EnsureRoom(existing).ShouldBeSuccess();
    }

    [Fact]
    public void MergeEnsureRoom_ShouldAccept_BelowTheLimit()
    {
        var merge = IngredientMerge.Create("Neu", "Salz").ShouldBeSuccess();

        merge.EnsureRoom(Merges(IngredientMerge.MaxPerHousehold - 1)).ShouldBeSuccess();
    }

    [Fact]
    public void NounEnsureRoom_ShouldRefuseANewWord_WhenTheHouseholdIsAtItsLimit()
    {
        var existing = Nouns(IngredientNouns.MaxOwnPerHousehold);
        var noun = IngredientNouns.Own("Zucchini", "Zucchinis").ShouldBeSuccess();

        IngredientNouns.EnsureRoom(existing, noun).ShouldBeFailure(IngredientErrors.TooManyNouns);
    }

    [Fact]
    public void NounEnsureRoom_ShouldAllowChangingAListedPlural_WhenTheHouseholdIsAtItsLimit()
    {
        var existing = Nouns(IngredientNouns.MaxOwnPerHousehold);
        var noun = IngredientNouns.Own("Wort", "Wörter").ShouldBeSuccess();

        IngredientNouns.EnsureRoom(existing, noun).ShouldBeSuccess();
    }

    private static List<IngredientMerge> Merges(int count) =>
    [
        .. Enumerable.Range(0, count)
            .Select(number => IngredientMerge.Create($"Name {number}", "Salz").ShouldBeSuccess())
    ];

    // "Wort" is among them, so a test can change a listed one.
    private static List<IngredientNouns.Noun> Nouns(int count) =>
    [
        new("Wort", "Wörter", null),
        .. Enumerable.Range(1, count - 1).Select(number => new IngredientNouns.Noun($"Wort{number}", $"Wörter{number}", null))
    ];
}
