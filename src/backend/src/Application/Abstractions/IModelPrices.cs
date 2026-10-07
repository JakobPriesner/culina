using Domain.Assistance;

namespace Application.Abstractions;

/// <summary>Port for model prices, so tests can fix them.</summary>
public interface IModelPrices
{
    /// <summary>The cost of a call, or null when the model has no price on record (distinct from free).</summary>
    /// <param name="provider">Which provider.</param>
    /// <param name="model">Which model.</param>
    /// <param name="inputTokens">What was sent.</param>
    /// <param name="outputTokens">What came back.</param>
    /// <param name="pictures">How many images were made.</param>
    decimal? Of(
        AssistantKind provider,
        string model,
        int inputTokens,
        int outputTokens,
        int pictures);
}
