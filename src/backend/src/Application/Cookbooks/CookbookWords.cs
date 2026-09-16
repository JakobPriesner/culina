using Domain.Cookbooks;
using Domain.Shared;

namespace Application.Cookbooks;

/// <summary>How a cookbook's kind and rules are spelled on the wire.</summary>
/// <remarks>
/// Words rather than numbers, for the reason the meal plan's slot is text: a
/// client branching on <c>"smart"</c> reads, and one branching on <c>1</c>
/// breaks silently the day somebody inserts a kind in the middle.
/// </remarks>
internal static class CookbookWords
{
    internal const string Manual = "manual";

    internal const string Smart = "smart";

    internal static string Of(CookbookKind kind) =>
        kind == CookbookKind.Smart ? Smart : Manual;

    internal static CookbookKind ToKind(string stored) =>
        stored == Smart ? CookbookKind.Smart : CookbookKind.Manual;

    /// <summary>Reads the rules a request states, or none when it states none.</summary>
    /// <param name="wire">What the caller sent, or null.</param>
    internal static Result<CookbookRules> ToRules(Contracts.Cookbooks.CookbookRulesContract? wire) =>
        wire is null
            ? Result<CookbookRules>.Success(CookbookRules.None)
            : CookbookRules.Create(wire.Tags, wire.Ingredients, wire.MaxMinutes);
}
