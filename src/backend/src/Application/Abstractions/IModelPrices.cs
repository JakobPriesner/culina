using Domain.Assistance;

namespace Application.Abstractions;

/// <summary>
/// What a call to a model came to.
/// </summary>
/// <remarks>
/// A port because the prices themselves are a fact about the outside world that
/// goes out of date, and because a test asserting on a budget must be able to
/// fix them — a suite whose arithmetic changes when somebody corrects a price
/// list is a suite that will be quietly disabled.
/// </remarks>
public interface IModelPrices
{
    /// <summary>
    /// What a call cost, or null when there is no price on record for the model.
    /// </summary>
    /// <param name="provider">Which provider.</param>
    /// <param name="model">Which model.</param>
    /// <param name="inputTokens">What was sent.</param>
    /// <param name="outputTokens">What came back.</param>
    /// <param name="pictures">How many images were made.</param>
    /// <remarks>
    /// Null rather than zero for an unknown model, and the difference matters:
    /// a model on your own hardware costs nothing, which is a price, while a
    /// model nobody has listed costs something nobody here knows. Reporting the
    /// second as the first would quietly understate a bill.
    /// </remarks>
    decimal? Of(
        AssistantKind provider,
        string model,
        int inputTokens,
        int outputTokens,
        int pictures);
}
