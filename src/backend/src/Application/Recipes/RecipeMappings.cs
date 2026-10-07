using Contracts.Recipes;
using Domain.Import;
using Domain.Recipes;
using Domain.Shared;
using SharedRecipe = Contracts.Recipes.GetShared.Response;

namespace Application.Recipes;

/// <summary>Turns a recipe into its wire shape, and back. Shared so a recipe's one detailed representation cannot drift between copies.</summary>
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

        // Group order, then order within the group: the order the list is shown in, so step needs do not look shuffled.
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
            YieldLabel = recipe.Yield.Label,
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
                SourceUrl = origin.SourceUrl?.Value,
                ImportedAt = origin.ImportedAt
            },
            CreatedBy = recipe.CreatedBy,
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = recipe.UpdatedAt,
            Version = recipe.Version
        };
    }

    /// <summary>Describes a recipe for whoever follows its link.</summary>
    /// <param name="recipe">The recipe to describe.</param>
    /// <param name="origin">Where it came from, when it was not written here.</param>
    /// <remarks>Projected from <c>Describe</c> so the two cannot disagree about any amount or step.</remarks>
    internal static SharedRecipe Publish(this Recipe recipe, RecipeOrigin? origin = null)
    {
        var detail = recipe.Describe(origin);

        return new SharedRecipe
        {
            Title = detail.Title,
            Description = detail.Description,
            Language = detail.Language,
            YieldAmount = detail.YieldAmount,
            YieldKind = detail.YieldKind,
            YieldLabel = detail.YieldLabel,
            PrepMinutes = detail.PrepMinutes,
            CookMinutes = detail.CookMinutes,
            TotalMinutes = detail.TotalMinutes,
            HasImage = detail.ImageId is not null,
            Groups = detail.Groups,
            Steps = detail.Steps,
            Tags = detail.Tags,
            SourceUrl = detail.Origin?.SourceUrl
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
            Title = step.Title,
            DurationSeconds = step.DurationSeconds,
            Segments = [.. step.Segments.Select(segment => segment.ToContract(ingredients))],

            // Filtering the recipe's order by the step's set: one pass, and an id the recipe lacks drops out instead of throwing.
            Uses = [.. order.Where(step.Uses.Contains)]
        };

    // An ingredient segment carries the name and the base amount, so the client renders and scales a step without a lookup.
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
