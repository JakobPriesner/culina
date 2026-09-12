using Contracts.Recipes;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>
/// Turns a recipe into its wire shape, and a wire shape back into domain
/// objects.
/// </summary>
/// <remarks>
/// Shared by every recipe operation because a recipe has exactly one detailed
/// representation, and five copies of a twenty-field projection is how two of
/// them drift apart.
/// </remarks>
internal static class RecipeMappings
{
    internal const string TextSegmentType = "text";

    internal const string IngredientSegmentType = "ingredient";

    /// <summary>Describes a recipe in full.</summary>
    /// <param name="recipe">The recipe to describe.</param>
    internal static RecipeDetail Describe(this Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var names = recipe.Ingredients.ToDictionary(ingredient => ingredient.Id);

        return new RecipeDetail
        {
            RecipeId = recipe.Id,
            HouseholdId = recipe.HouseholdId,
            Title = recipe.Title.Value,
            Description = recipe.Description,
            Language = RecipeWords.Of(recipe.Language),
            YieldAmount = recipe.Yield.Amount,
            YieldKind = RecipeWords.Of(recipe.Yield.Kind),
            PrepMinutes = recipe.PrepMinutes,
            CookMinutes = recipe.CookMinutes,
            TotalMinutes = recipe.TotalMinutes,
            ImageId = recipe.ImageId,
            Groups = [.. recipe.Groups.Select(ToContract)],
            Steps = [.. recipe.Steps.Select(step => step.ToContract(names))],
            Tags = recipe.Tags,
            CreatedBy = recipe.CreatedBy,
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = recipe.UpdatedAt,
            Version = recipe.Version
        };
    }

    private static IngredientGroupContract ToContract(IngredientGroup group) => new()
    {
        GroupId = group.Id,
        Name = group.Name,
        Ingredients = [.. group.Ingredients.Select(ToContract)]
    };

    private static IngredientContract ToContract(RecipeIngredient ingredient) => new()
    {
        IngredientId = ingredient.Id,
        Quantity = ingredient.Quantity.Amount,
        Unit = RecipeWords.Of(ingredient.Quantity.Unit),
        Name = ingredient.Name,
        Note = ingredient.Note
    };

    private static StepContract ToContract(
        this Step step,
        IReadOnlyDictionary<Guid, RecipeIngredient> ingredients) => new()
        {
            StepId = step.Id,
            DurationSeconds = step.DurationSeconds,
            Segments = [.. step.Segments.Select(segment => segment.ToContract(ingredients))]
        };

    /// <summary>
    /// An ingredient segment carries the name and the <b>base</b> amount, so the
    /// client renders the step without a lookup and scales it without a round
    /// trip.
    /// </summary>
    private static StepSegmentContract ToContract(
        this StepSegment segment,
        IReadOnlyDictionary<Guid, RecipeIngredient> ingredients) => segment switch
        {
            TextSegment text => new StepSegmentContract { Type = TextSegmentType, Value = text.Value },
            IngredientSegment ingredient => Describe(ingredient, ingredients),
            _ => throw new ArgumentOutOfRangeException(nameof(segment), segment, "Unknown segment.")
        };

    private static StepSegmentContract Describe(
        IngredientSegment segment,
        IReadOnlyDictionary<Guid, RecipeIngredient> ingredients)
    {
        var ingredient = ingredients[segment.RecipeIngredientId];

        return new StepSegmentContract
        {
            Type = IngredientSegmentType,
            RecipeIngredientId = segment.RecipeIngredientId,
            Name = ingredient.Name,
            Quantity = ingredient.Quantity.Amount,
            Unit = RecipeWords.Of(ingredient.Quantity.Unit)
        };
    }
}
