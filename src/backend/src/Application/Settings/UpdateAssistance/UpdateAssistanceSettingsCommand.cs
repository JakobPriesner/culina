using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Assistance;
using Domain.Shared;
using Response = Contracts.Settings.UpdateAssistance.Response;

namespace Application.Settings.UpdateAssistance;

/// <summary>Changes how the assistant is set up.</summary>
/// <param name="Enabled">Whether it is on.</param>
/// <param name="Provider">Which provider.</param>
/// <param name="ApiKey">A new key, empty to clear it, or null to keep it.</param>
/// <param name="BaseUrl">Where the provider is, or empty for its usual address.</param>
/// <param name="ComposeModel">The model that writes recipes.</param>
/// <param name="DrawModel">The model that draws pictures.</param>
/// <param name="ImproveEnabled">Whether it may rewrite a recipe.</param>
/// <param name="DraftEnabled">Whether it may write one from an idea.</param>
/// <param name="ReadEnabled">Whether it may read one out of a photograph.</param>
/// <param name="DrawEnabled">Whether it may draw a picture.</param>
/// <param name="MonthlyBudget">The instance's monthly ceiling.</param>
/// <param name="PersonalBudget">One person's share of it.</param>
public sealed record UpdateAssistanceSettingsCommand(
    bool Enabled,
    string Provider,
    string? ApiKey,
    string BaseUrl,
    string ComposeModel,
    string DrawModel,
    bool ImproveEnabled,
    bool DraftEnabled,
    bool ReadEnabled,
    bool DrawEnabled,
    decimal? MonthlyBudget,
    decimal? PersonalBudget);

internal sealed class UpdateAssistanceSettingsCommandHandler(
    AssistanceSettings settings,
    ISettingsStore<AssistanceSettings> store,
    ISecretProtector protector)
    : ICommandHandler<UpdateAssistanceSettingsCommand, Response>
{
    /// <summary>
    /// More than anybody spends on recipes in a month, by a wide margin.
    /// </summary>
    /// <remarks>
    /// Not a business rule — a typo guard. A budget entered as 100000 instead
    /// of 100.00 is a budget that will never stop anything, and the person who
    /// typed it would have no reason to look at it again.
    /// </remarks>
    private const decimal LargestReasonableBudget = 10_000m;

    public async Task<Result<Response>> Handle(
        UpdateAssistanceSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Settings.UpdateAssistance");

        var result = await Check(command)
            .Bind(() => Prepare(command))
            .Match(
                updated => SaveAsync(updated, cancellationToken),
                error => Task.FromResult(Result<Response>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>
    /// Everything that can be wrong with the form, as field errors.
    /// </summary>
    /// <remarks>
    /// Field errors rather than one refusal, because this is a form with twelve
    /// controls on it and "that is not valid" would send somebody looking
    /// through all of them.
    /// </remarks>
    private static Result Check(UpdateAssistanceSettingsCommand command)
    {
        List<Result> problems = [];

        var kind = AssistantKind.Parse(command.Provider);

        if (kind is null)
        {
            problems.Add(Field("provider", AssistanceErrors.UnknownProvider));
        }

        // A model on your own machine has no address of its own, so an empty
        // box here is not "use the default" — there is no default, and the
        // connection cannot be made at all.
        if (kind is { NeedsAddress: true } && command.BaseUrl.Trim().Length is 0)
        {
            problems.Add(Field("baseUrl", AssistanceErrors.AddressRequired));
        }

        // Only what was sent. Null means "keep the key you have", which is what
        // a form sends when its key box was empty because there was nothing to
        // show in it.
        if (command.ApiKey is { Length: > AssistanceSettings.MaxApiKeyLength })
        {
            problems.Add(Field("apiKey", AssistanceErrors.InvalidApiKey));
        }

        problems.Add(Model("composeModel", command.ComposeModel));
        problems.Add(Model("drawModel", command.DrawModel));
        problems.Add(Budget("monthlyBudget", command.MonthlyBudget));
        problems.Add(Budget("personalBudget", command.PersonalBudget));

        if (command.BaseUrl.Length > 0 && !IsUsableAddress(command.BaseUrl))
        {
            problems.Add(Field("baseUrl", AssistanceErrors.InvalidBaseUrl));
        }

        return Result.Combine(problems);
    }

    /// <summary>
    /// Whether an override address is one this could actually call.
    /// </summary>
    /// <remarks>
    /// Http and https only, and absolute. This is an administrator typing, not
    /// a user, so the private-address rules that guard the import path do not
    /// apply — pointing at a model on the same machine is the ordinary reason
    /// to set this at all.
    /// </remarks>
    private static bool IsUsableAddress(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var address)
        && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);

    private static Result Model(string field, string value) =>
        value.Length > AssistanceSettings.MaxModelLength
            ? Field(field, AssistanceErrors.InvalidModel)
            : Result.Success();

    private static Result Budget(string field, decimal? value) => value switch
    {
        < 0 or > LargestReasonableBudget => Field(field, AssistanceErrors.InvalidBudget),
        _ => Result.Success()
    };

    private static Result Field(string field, Error error) =>
        new FieldError(field, error.Code, error.Description);

    /// <summary>
    /// Builds the settings to store, encrypting a new key on the way.
    /// </summary>
    /// <remarks>
    /// Switching the assistant on before it is connected is quietly corrected
    /// rather than refused, because the form it comes from lets somebody fill
    /// the key box last and the alternative is an error message about a field
    /// they are in the middle of typing. What "connected" means is the
    /// provider's business: a key for the hosted ones, an address for a local
    /// one.
    /// </remarks>
    private Result<AssistanceSettings> Prepare(UpdateAssistanceSettingsCommand command)
    {
        var key = command.ApiKey switch
        {
            null => settings.ProtectedApiKey,
            var entered when entered.Trim().Length is 0 => string.Empty,
            var entered => protector.Protect(entered.Trim())
        };

        var prepared = new AssistanceSettings
        {
            Provider = command.Provider.Trim().ToLowerInvariant(),
            ProtectedApiKey = key,
            BaseUrl = command.BaseUrl.Trim(),
            ComposeModel = command.ComposeModel.Trim(),
            DrawModel = command.DrawModel.Trim(),
            ImproveEnabled = command.ImproveEnabled,
            DraftEnabled = command.DraftEnabled,
            ReadEnabled = command.ReadEnabled,
            DrawEnabled = command.DrawEnabled,
            MonthlyBudget = command.MonthlyBudget,
            PersonalBudget = command.PersonalBudget
        };

        prepared.Enabled = command.Enabled && prepared.IsConnected;

        return prepared;
    }

    private async Task<Result<Response>> SaveAsync(
        AssistanceSettings updated,
        CancellationToken cancellationToken)
    {
        var saved = await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);

        // Persist first, then mutate. A failed save must never leave the
        // process talking to a provider the database has not heard of.
        return saved.Map(() =>
        {
            settings.CopyFrom(updated);

            return new Response
            {
                Enabled = settings.Enabled,
                Provider = settings.Provider,
                ApiKeyConfigured = settings.HasApiKey,
                Connected = settings.IsConnected,
                BaseUrl = settings.BaseUrl,
                ComposeModel = settings.ComposeModel,
                DrawModel = settings.DrawModel,
                ImproveEnabled = settings.ImproveEnabled,
                DraftEnabled = settings.DraftEnabled,
                ReadEnabled = settings.ReadEnabled,
                DrawEnabled = settings.DrawEnabled,
                MonthlyBudget = settings.MonthlyBudget,
                PersonalBudget = settings.PersonalBudget
            };
        });
    }
}
