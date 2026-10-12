namespace Contracts.Ingredients;

/// <summary>The ingredient names a household's own recipes use.</summary>
/// <remarks>Not paged: a kitchen writes a few hundred at most, and the screen filters them as you type.</remarks>
public sealed record IngredientNamesResponse
{
    /// <summary>The names, most used first.</summary>
    public required IReadOnlyList<IngredientNameUse> Items { get; init; }
}

/// <summary>One name as the household writes it.</summary>
public sealed record IngredientNameUse
{
    /// <summary>The name, as written.</summary>
    public required string Name { get; init; }

    /// <summary>How many of the household's recipes use it.</summary>
    public required int RecipeCount { get; init; }
}

/// <summary>The merges a household has made.</summary>
public sealed record IngredientMergesResponse
{
    /// <summary>The merges, by the name that goes away.</summary>
    public required IReadOnlyList<IngredientMergeDetail> Items { get; init; }
}

/// <summary>One merge: lines written <c>from</c> are stored as <c>to</c>.</summary>
public sealed record IngredientMergeDetail
{
    /// <summary>The name that goes away, as it was written when merged.</summary>
    public required string From { get; init; }

    /// <summary>The name that is kept.</summary>
    public required string To { get; init; }
}

/// <summary>Merges one ingredient name into another.</summary>
public sealed record CreateIngredientMergeRequest
{
    /// <summary>The name that goes away; every spelling that folds the same goes with it.</summary>
    public required string From { get; init; }

    /// <summary>The name to keep. It need not be in use yet, which makes a merge a rename.</summary>
    public required string To { get; init; }
}

/// <summary>A merge that was made, and what it changed.</summary>
public sealed record CreatedIngredientMerge
{
    /// <summary>The name that went away.</summary>
    public required string From { get; init; }

    /// <summary>The name kept: the one asked for, or the one it was itself merged into.</summary>
    public required string To { get; init; }

    /// <summary>How many of the household's recipes were rewritten.</summary>
    public required int RecipeCount { get; init; }
}

/// <summary>The words a household writes in both numbers, beyond the ones Culina knows.</summary>
public sealed record IngredientNounsResponse
{
    /// <summary>The words, by singular.</summary>
    public required IReadOnlyList<IngredientNounDetail> Items { get; init; }
}

/// <summary>A word in both numbers: "1 Maultasche", "2 Maultaschen".</summary>
public sealed record IngredientNounDetail
{
    /// <summary>The singular.</summary>
    public required string One { get; init; }

    /// <summary>The plural.</summary>
    public required string Many { get; init; }
}

/// <summary>The plural of a word whose singular is in the path.</summary>
public sealed record SetIngredientNounRequest
{
    /// <summary>The plural: one word, not the singular.</summary>
    public required string Many { get; init; }
}

/// <summary>Names in a household's recipes that are probably one ingredient.</summary>
public sealed record MergeSuggestionsResponse
{
    /// <summary>The proposals, the most used name first.</summary>
    public required IReadOnlyList<MergeSuggestionDetail> Items { get; init; }
}

/// <summary>A proposal to merge one name into another.</summary>
public sealed record MergeSuggestionDetail
{
    /// <summary>The name proposed to go away.</summary>
    public required IngredientNameUse From { get; init; }

    /// <summary>The name proposed to be kept: the most used of the lookalikes.</summary>
    public required IngredientNameUse To { get; init; }

    /// <summary>
    /// Why: <c>spelling</c> (they fold to one name), <c>same_food</c> (they name one food) or
    /// <c>typo</c> (a letter or two apart).
    /// </summary>
    public required string Reason { get; init; }
}
