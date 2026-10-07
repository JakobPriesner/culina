namespace Contracts.Recipes.GetUnits;

/// <summary>The units a household can measure in.</summary>
/// <remarks>
/// Two lists because they mean different things: the built-in ones convert (published as a closed
/// set so clients keep the table honest); the household's own are whatever its recipes used: they
/// scale and add up but never convert, as nobody knows how much a Schuss is.
/// </remarks>
public sealed record Response
{
    /// <summary>The units every household starts with, in picker order.</summary>
    public required IReadOnlyList<string> BuiltIn { get; init; }

    /// <summary>What this household has written, that is not built in.</summary>
    public required IReadOnlyList<string> Own { get; init; }
}
