namespace Domain.Recipes;

/// <summary>One piece of a step's text.</summary>
/// <remarks>
/// A step is a sequence of plain text and references to the recipe's own
/// ingredients. Storing it this way is what lets an amount inside a step scale
/// with the servings — the step says "melt <i>this ingredient</i>", not "melt
/// 120 g butter".
/// </remarks>
public abstract record StepSegment;

/// <summary>Literal words.</summary>
/// <param name="Value">The text.</param>
public sealed record TextSegment(string Value) : StepSegment;

/// <summary>A reference to one of the recipe's ingredients.</summary>
/// <param name="RecipeIngredientId">Which ingredient.</param>
public sealed record IngredientSegment(Guid RecipeIngredientId) : StepSegment;
