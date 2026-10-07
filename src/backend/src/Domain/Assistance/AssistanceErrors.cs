using Domain.Shared;

namespace Domain.Assistance;

/// <summary>
/// What can go wrong when a model is asked for a recipe.
/// </summary>
/// <remarks>
/// Not-configured and disabled are <see cref="ErrorType.NotFound"/> on purpose: a caller learns nothing from which.
/// The rest are distinct because each has a different remedy (wait, replace the key, retry).
/// </remarks>
public static class AssistanceErrors
{
    /// <summary>Nobody has connected a model to this instance.</summary>
    public static readonly Error NotConfigured = new(
        "assistance.not_configured",
        "This kitchen has no assistant. An administrator can connect one in settings.",
        ErrorType.NotFound);

    /// <summary>A model is connected, but this is not one of the things it may do.</summary>
    public static readonly Error Disabled = new(
        "assistance.disabled",
        "The assistant is not set up to do that here.",
        ErrorType.NotFound);

    /// <summary>The provider named in the settings row is not one this knows.</summary>
    public static readonly Error UnknownProvider = new(
        "assistance.unknown_provider",
        "That is not a model provider this knows how to talk to.",
        ErrorType.Validation);

    /// <summary>The month's budget is spent. Rate-limited because the answer is to wait.</summary>
    public static readonly Error BudgetExhausted = new(
        "assistance.budget_exhausted",
        "This month's budget for the assistant is spent. It starts again next month.",
        ErrorType.RateLimited);

    /// <summary>This person has spent their own share of the month's budget.</summary>
    public static readonly Error PersonalBudgetExhausted = new(
        "assistance.personal_budget_exhausted",
        "You have used your share of this month's assistant budget.",
        ErrorType.RateLimited);

    /// <summary>The provider did not answer, or took too long. One error for both: neither is the caller's to fix.</summary>
    public static readonly Error Unavailable = new(
        "assistance.unavailable",
        "The assistant could not be reached just now. Try again in a moment.",
        ErrorType.Unavailable);

    /// <summary>The provider answered, and would not accept the credential.</summary>
    /// <remarks>
    /// Apart from <see cref="Unavailable"/> because waiting will not fix it; also a key that signs fine may lack the catalogue permission.
    /// A cook never sees it: <c>AssistantRun</c> maps it to <see cref="Unavailable"/>.
    /// </remarks>
    public static readonly Error Rejected = new(
        "assistance.rejected",
        "The provider would not accept the key. It may be wrong, or it may not be "
        + "permitted to do this.",
        ErrorType.Unavailable);

    /// <summary>The provider answered, and has no model by the name in the settings.</summary>
    /// <remarks>
    /// Not <see cref="Refused"/>: Ollama answers 404 for an unpulled model, and rewording cannot fix that.
    /// </remarks>
    public static readonly Error ModelMissing = new(
        "assistance.model_missing",
        "The model named in the assistant settings is not available from its provider.",
        ErrorType.Unavailable);

    /// <summary>The provider answered, and said to slow down.</summary>
    public static readonly Error Throttled = new(
        "assistance.throttled",
        "The assistant is busy. Try again in a moment.",
        ErrorType.RateLimited);

    /// <summary>The provider's content filter refused the request.</summary>
    public static readonly Error Refused = new(
        "assistance.refused",
        "The assistant would not answer that.",
        ErrorType.Problem);

    /// <summary>The model answered with something that is not a recipe. The expected failure of the feature.</summary>
    public static readonly Error UnusableAnswer = new(
        "assistance.unusable_answer",
        "The assistant's answer could not be read as a recipe. Asking again usually works.",
        ErrorType.Problem);

    /// <summary>An idea was asked about with nothing in it.</summary>
    public static readonly Error NothingToWorkFrom = new(
        "assistance.nothing_to_work_from",
        "Say a little about what you want to cook.",
        ErrorType.Validation);

    /// <summary>The idea, or the text to read, was longer than this will send.</summary>
    public static readonly Error TooMuchToWorkFrom = new(
        "assistance.too_much_to_work_from",
        "That is more text than the assistant reads at once. Try a shorter piece.",
        ErrorType.Validation);

    /// <summary>The key given for a provider was empty, or far longer than a key.</summary>
    public static readonly Error InvalidApiKey = new(
        "assistance.invalid_api_key",
        "That does not look like an API key.",
        ErrorType.Validation);

    /// <summary>A stored key was to be kept for a different address. A key is only ever sent to the address it was saved with.</summary>
    public static readonly Error ApiKeyRequired = new(
        "assistance.api_key_required",
        "Enter the key again. The address changed, and a stored key is only ever sent to the address it was saved for.",
        ErrorType.Validation);

    /// <summary>A budget was given as a negative amount.</summary>
    public static readonly Error InvalidBudget = new(
        "assistance.invalid_budget",
        "A budget cannot be less than nothing.",
        ErrorType.Validation);

    /// <summary>A model name was empty, or longer than any model is named.</summary>
    public static readonly Error InvalidModel = new(
        "assistance.invalid_model",
        "That does not look like the name of a model.",
        ErrorType.Validation);

    /// <summary>This provider does not make pictures (Ollama today). Backstop for a request the settings screen would not offer.</summary>
    public static readonly Error DrawingNotSupported = new(
        "assistance.drawing_not_supported",
        "The model connected to this kitchen does not draw pictures.",
        ErrorType.Validation);

    /// <summary>The address given for a provider could not be read as one.</summary>
    public static readonly Error InvalidBaseUrl = new(
        "assistance.invalid_base_url",
        "That is not an address this can connect to. It looks like https://api.example.com.",
        ErrorType.Validation);
}
