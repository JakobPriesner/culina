namespace Domain.Recipes;

/// <summary>One piece of a step's text.</summary>
/// <remarks>A step is plain text plus ingredient references, so amounts inside a step scale with the servings.</remarks>
public abstract record StepSegment;

/// <summary>Literal words.</summary>
/// <param name="Value">The text.</param>
public sealed record TextSegment(string Value) : StepSegment;

/// <summary>A reference to one of the recipe's ingredients.</summary>
/// <param name="RecipeIngredientId">Which ingredient.</param>
public sealed record IngredientSegment(Guid RecipeIngredientId) : StepSegment;
