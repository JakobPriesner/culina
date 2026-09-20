using Domain.Shared;

namespace Domain.Assistance;

/// <summary>
/// What can go wrong when a model is asked for a recipe.
/// </summary>
/// <remarks>
/// <para>
/// Two of these are <see cref="ErrorType.NotFound"/> where "forbidden" reads
/// more naturally, and deliberately: an instance with no key and an instance
/// whose owner switched a capability off are both instances where the thing
/// does not exist, and a caller learns nothing from being told which.
/// </para>
/// <para>
/// The rest are told apart from each other because each one has a different
/// answer. A budget that is spent waits for a month or a larger cap; a provider
/// that is down waits for the provider; an answer that could not be read is
/// worth trying again straight away. "Something went wrong" would send every
/// one of those to the same shrug.
/// </para>
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

    /// <summary>
    /// The month's budget is spent.
    /// </summary>
    /// <remarks>
    /// Rate-limited rather than a plain refusal, because that is what it is:
    /// the answer is to wait, and the status code that says so is the one that
    /// carries a hint about how long.
    /// </remarks>
    public static readonly Error BudgetExhausted = new(
        "assistance.budget_exhausted",
        "This month's budget for the assistant is spent. It starts again next month.",
        ErrorType.RateLimited);

    /// <summary>This person has spent their own share of the month's budget.</summary>
    public static readonly Error PersonalBudgetExhausted = new(
        "assistance.personal_budget_exhausted",
        "You have used your share of this month's assistant budget.",
        ErrorType.RateLimited);

    /// <summary>The provider did not answer, or took too long.</summary>
    /// <remarks>
    /// One error for two causes on purpose. Both are somebody else's server
    /// being unavailable to this one, neither is the caller's to fix, and the
    /// administrator has the log line that tells them apart.
    /// </remarks>
    public static readonly Error Unavailable = new(
        "assistance.unavailable",
        "The assistant could not be reached just now. Try again in a moment.",
        ErrorType.Unavailable);

    /// <summary>
    /// The provider answered, and would not accept the credential.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Told apart from <see cref="Unavailable"/> because the answer is a
    /// different one: a provider that is down is waited for, and a key that is
    /// refused is replaced. Nothing waiting will fix the second.
    /// </para>
    /// <para>
    /// Worth its own error mostly because of what it is not. A key that signs
    /// requests perfectly well can still be refused here: providers scope keys,
    /// and listing the catalogue is a permission of its own that an inference
    /// key need not carry. "Check the key" is then advice that sends somebody
    /// to replace a key that was never wrong.
    /// </para>
    /// <para>
    /// A cook never sees it — <c>AssistantRun</c> turns it back into
    /// <see cref="Unavailable"/> before it leaves — because somebody in the
    /// middle of cooking can do nothing with it. It is for the settings screen
    /// and the ledger, where the person reading is the person with the key.
    /// </para>
    /// </remarks>
    public static readonly Error Rejected = new(
        "assistance.rejected",
        "The provider would not accept the key. It may be wrong, or it may not be "
        + "permitted to do this.",
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

    /// <summary>
    /// The model answered with something that is not a recipe.
    /// </summary>
    /// <remarks>
    /// The expected failure of the whole feature, not an exceptional one. A
    /// model asked for structured output still occasionally returns a unit
    /// nobody uses or an amount that is not a number, and the honest thing is
    /// to say so and let the person ask again.
    /// </remarks>
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

    /// <summary>
    /// This provider does not make pictures.
    /// </summary>
    /// <remarks>
    /// Ollama, today. A fact about the provider rather than a failure of the
    /// call, so the settings screen declines to offer the switch and this is
    /// the backstop for a request that arrived anyway.
    /// </remarks>
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
