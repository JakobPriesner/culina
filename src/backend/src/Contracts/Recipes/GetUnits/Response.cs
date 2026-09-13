namespace Contracts.Recipes.GetUnits;

/// <summary>The units a household can measure in.</summary>
/// <remarks>
/// Two lists rather than one, because they mean different things. The built-in
/// units are the ones that convert — a kilo is a thousand grams, everywhere,
/// for everyone — and the document publishes them as a closed set so a
/// generated client keeps the conversion table honest.
///
/// The household's own are whatever its recipes have used. They scale with the
/// portions and they add to themselves, and they never convert to anything:
/// nobody knows how much a Schuss is, and a shopping list that claimed to would
/// be inventing the number.
/// </remarks>
public sealed record Response
{
    /// <summary>The units every household starts with, in picker order.</summary>
    public required IReadOnlyList<string> BuiltIn { get; init; }

    /// <summary>What this household has written, that is not built in.</summary>
    public required IReadOnlyList<string> Own { get; init; }
}
