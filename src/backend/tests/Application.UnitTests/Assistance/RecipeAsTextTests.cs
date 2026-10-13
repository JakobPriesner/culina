using Application.Assistance;
using Domain.Recipes;
using Domain.Shared;
using TestSupport;

namespace Application.UnitTests.Assistance;

/// <summary>The recipe as the improve job reads it.</summary>
public class RecipeAsTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Of_ShouldWriteALinkedIngredientAsAnAtMention()
    {
        var recipe = Recipe.Create(
            Guid.CreateVersion7(),
            RecipeTitle.Create("Kuchen").ShouldBeSuccess(),
            Guid.CreateVersion7(),
            Language.De,
            Now);
        var sugar = RecipeIngredient.Create(null, 0, Quantity.Unmeasured, "Zucker", null).ShouldBeSuccess();
        var group = IngredientGroup.Create(null, null, 0, [sugar]).ShouldBeSuccess();
        var step = Step.Create(
            null,
            0,
            [new TextSegment("Den "), new IngredientSegment(sugar.Id), new TextSegment(" einrühren.")],
            [],
            null).ShouldBeSuccess();
        recipe.SetContents([group], [step], Now).ShouldBeSuccess();

        var text = RecipeAsText.Of(recipe);

        // Written as the editor writes it, so the tidied step can be linked to its line again.
        Assert.Contains("1. Den @Zucker einrühren.", text, StringComparison.Ordinal);
    }
}
