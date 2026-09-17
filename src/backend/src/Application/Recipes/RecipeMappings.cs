using Contracts.Recipes;
using Domain.Import;
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
    /// <param name="origin">Where it came from, when it was not written here.</param>
    internal static RecipeDetail Describe(this Recipe recipe, RecipeOrigin? origin = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var names = recipe.Ingredients.ToDictionary(ingredient => ingredient.Id);

        // Group order, then order within the group: the order the ingredient
        // list is already shown in, and so the only one a step's needs can be
        // read in without looking like a shuffle.
        var order = recipe.Ingredients.Select(ingredient => ingredient.Id).ToList();

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
            Steps = [.. recipe.Steps.Select(step => step.ToContract(names, order))],
            Tags = recipe.Tags,
            Origin = origin is null ? null : new RecipeProvenance
            {
                Kind = origin.Kind.Code,
                SourceId = origin.SourceId,
                ExternalId = origin.ExternalId,
                SourceUrl = origin.SourceUrl,
                ImportedAt = origin.ImportedAt
            },
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
        IReadOnlyDictionary<Guid, RecipeIngredient> ingredients,
        IReadOnlyList<Guid> order) => new()
        {
            StepId = step.Id,
            DurationSeconds = step.DurationSeconds,
            Segments = [.. step.Segments.Select(segment => segment.ToContract(ingredients))],

            // Filtering the recipe's order by the step's set rather than
            // sorting the set by a lookup: one pass, no dictionary, and an id
            // the recipe somehow lacks drops out instead of throwing.
            Uses = [.. order.Where(step.Uses.Contains)]
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
