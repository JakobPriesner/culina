using Domain.Assistance;

namespace Application.Abstractions;

/// <summary>Port for model prices, so tests can fix them.</summary>
public interface IModelPrices
{
    /// <summary>When Culina's own list was last checked against the providers' price pages.</summary>
    DateOnly RecordedOn { get; }

    /// <summary>The cost of a call and whose price it was, or null when the model has no price on record (distinct from free).</summary>
    /// <param name="provider">Which provider.</param>
    /// <param name="model">Which model.</param>
    /// <param name="inputTokens">What was sent.</param>
    /// <param name="outputTokens">What came back.</param>
    /// <param name="pictures">How many images were made.</param>
    Priced? Of(
        AssistantKind provider,
        string model,
        int inputTokens,
        int outputTokens,
        int pictures);

    /// <summary>What Culina's own list says this model costs, ignoring the administrator's prices; null when it does not know it.</summary>
    /// <param name="model">Which model.</param>
    ModelPrice? Listed(string model);
}

/// <summary>What a call came to, and whose price that was.</summary>
/// <param name="Cost">In US dollars.</param>
/// <param name="By">
/// <see cref="PricedBy.Culina"/> for the list in the code, <see cref="PricedBy.Administrator"/> for a price set on the settings screen.
/// </param>
public readonly record struct Priced(decimal Cost, string By);

/// <summary>Who a price came from, as the ledger stores it.</summary>
public static class PricedBy
{
    /// <summary>Culina's own list, as recorded on <see cref="IModelPrices.RecordedOn"/>.</summary>
    public const string Culina = "culina";

    /// <summary>A price the administrator set for their instance.</summary>
    public const string Administrator = "administrator";
}

/// <summary>A model's price, any part of which may be unknown.</summary>
/// <param name="InputPerMillion">Dollars per million input tokens.</param>
/// <param name="OutputPerMillion">Dollars per million output tokens.</param>
/// <param name="PerImage">Dollars per image.</param>
public sealed record ModelPrice(decimal? InputPerMillion, decimal? OutputPerMillion, decimal? PerImage);
