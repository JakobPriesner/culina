using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Assistance;
using Domain.Shared;
using Response = Contracts.Settings.UpdateAssistance.Response;

namespace Application.Settings.UpdateAssistance;

/// <summary>Changes how the assistant is set up.</summary>
/// <param name="Enabled">Whether it is on at all.</param>
/// <param name="Connections">The providers to keep.</param>
/// <param name="Uses">Which provider and model does each job.</param>
/// <param name="MonthlyBudget">The instance's monthly ceiling.</param>
/// <param name="PersonalBudget">One person's share of it.</param>
public sealed record UpdateAssistanceSettingsCommand(
    bool Enabled,
    IReadOnlyList<ConnectionEdit> Connections,
    IReadOnlyList<UseEdit> Uses,
    decimal? MonthlyBudget,
    decimal? PersonalBudget);

/// <summary>One provider to connect, or to keep connected.</summary>
/// <param name="Provider">Which provider.</param>
/// <param name="ApiKey">A new key, empty to clear it, or null to keep it (only while the address is unchanged).</param>
/// <param name="BaseUrl">Where it is, or empty for its own address.</param>
public sealed record ConnectionEdit(string Provider, string? ApiKey, string BaseUrl);

/// <summary>What should do one job.</summary>
/// <param name="Capability">Which job.</param>
/// <param name="Enabled">Whether it is offered.</param>
/// <param name="Provider">Which provider does it.</param>
/// <param name="Model">Which model, or empty for the current default.</param>
public sealed record UseEdit(string Capability, bool Enabled, string Provider, string Model);

internal sealed class UpdateAssistanceSettingsCommandHandler(
    AssistanceSettings settings,
    ISettingsStore<AssistanceSettings> store,
    IAssistants assistants,
    ISecretProtector protector)
    : ICommandHandler<UpdateAssistanceSettingsCommand, Response>
{
    /// <summary>A typo guard (100000 for 100.00), not a business rule.</summary>
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

    /// <summary>Everything wrong with the form, as field errors.</summary>
    private Result Check(UpdateAssistanceSettingsCommand command)
    {
        List<Result> problems =
        [
            Budget("monthlyBudget", command.MonthlyBudget),
            Budget("personalBudget", command.PersonalBudget)
        ];

        foreach (var connection in command.Connections)
        {
            problems.Add(CheckConnection(connection));
        }

        foreach (var use in command.Uses)
        {
            problems.Add(CheckUse(use));
        }

        return Result.Combine(problems);
    }

    private Result CheckConnection(ConnectionEdit connection)
    {
        if (AssistantKind.Parse(connection.Provider) is not { } kind)
        {
            return Field("provider", AssistanceErrors.UnknownProvider);
        }

        if (connection.ApiKey is { Length: > AssistanceSettings.MaxApiKeyLength })
        {
            return Field($"{kind.Code}.apiKey", AssistanceErrors.InvalidApiKey);
        }

        var address = connection.BaseUrl.Trim();

        // A blank row is fine: the screen sends every provider on every save. CheckUse refuses pointing at one.
        if (address.Length > 0 && !IsUsableAddress(address))
        {
            return Field($"{kind.Code}.baseUrl", AssistanceErrors.InvalidBaseUrl);
        }

        return KeepsKeyForAnotherAddress(kind, connection.ApiKey, address)
            ? Field($"{kind.Code}.apiKey", AssistanceErrors.ApiKeyRequired)
            : Result.Success();
    }

    /// <summary>Whether a stored key would be kept for a new address; it would be sent there, so it must be retyped.</summary>
    private bool KeepsKeyForAnotherAddress(AssistantKind kind, string? apiKey, string address) =>
        apiKey is null
        && settings.ConnectionFor(kind) is { HasApiKey: true } stored
        && stored.BaseUrl != address;

    /// <summary>Whether a job points at a provider that could ever do it (connection state is not checked).</summary>
    private static Result CheckUse(UseEdit use)
    {
        if (Capability.Parse(use.Capability) is not { } capability)
        {
            return Field("capability", AssistanceErrors.UnknownProvider);
        }

        if (use.Model.Length > AssistanceSettings.MaxModelLength)
        {
            return Field($"{capability.Code}.model", AssistanceErrors.InvalidModel);
        }

        if (use.Provider.Length is 0)
        {
            return Result.Success();
        }

        if (AssistantKind.Parse(use.Provider) is not { } kind)
        {
            return Field($"{capability.Code}.provider", AssistanceErrors.UnknownProvider);
        }

        if (capability == Capability.Draw && !kind.CanDraw)
        {
            return Field($"{capability.Code}.provider", AssistanceErrors.DrawingNotSupported);
        }

        // Nothing about whether the provider is connected yet: that is unfinished, not wrong, and `Allows` handles it.
        // Refusing it would force unassigning every job before clearing a key.
        return Result.Success();
    }

    /// <summary>Whether an override address is absolute http(s); the import path's private-address rules do not apply to administrators.</summary>
    private static bool IsUsableAddress(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var address)
        && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);

    private static Result Budget(string field, decimal? value) => value switch
    {
        < 0 or > LargestReasonableBudget => Field(field, AssistanceErrors.InvalidBudget),
        _ => Result.Success()
    };

    private static Result Field(string field, Error error) =>
        new FieldError(field, error.Code, error.Description);

    /// <summary>Builds the settings to store, encrypting new keys; drops empty connections and switches the assistant off if none is connected.</summary>
    private Result<AssistanceSettings> Prepare(UpdateAssistanceSettingsCommand command)
    {
        var connections = command.Connections
            .Select(Connect)
            .Where(one => one.HasApiKey || one.BaseUrl.Length > 0)
            .ToList();

        var prepared = new AssistanceSettings
        {
            Connections = connections,
            Uses = [.. command.Uses.Select(use => new CapabilityUse
            {
                Capability = use.Capability,
                Enabled = use.Enabled,
                Provider = use.Provider.Trim().ToLowerInvariant(),
                Model = use.Model.Trim()
            })],
            MonthlyBudget = command.MonthlyBudget,
            PersonalBudget = command.PersonalBudget
        };

        prepared.Enabled = command.Enabled && prepared.IsConnected;

        return prepared;
    }

    private AssistanceConnection Connect(ConnectionEdit edit)
    {
        var provider = edit.Provider.Trim().ToLowerInvariant();

        var key = edit.ApiKey switch
        {
            null => settings.Connections
                .FirstOrDefault(one => one.Provider == provider)?.ProtectedApiKey ?? string.Empty,
            var entered when entered.Trim().Length is 0 => string.Empty,
            var entered => protector.Protect(entered.Trim())
        };

        return new AssistanceConnection
        {
            Provider = provider,
            ProtectedApiKey = key,
            BaseUrl = edit.BaseUrl.Trim()
        };
    }

    private async Task<Result<Response>> SaveAsync(
        AssistanceSettings updated,
        CancellationToken cancellationToken)
    {
        var saved = await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);

        // Persist first, then mutate: a failed save must not change the running process.
        return saved.Map(() =>
        {
            settings.CopyFrom(updated);

            return Describe();
        });
    }

    private Response Describe() => new()
    {
        Enabled = settings.Enabled,
        Connections = [.. AssistantKind.All.Select(kind =>
        {
            var connection = settings.ConnectionFor(kind);

            return new Contracts.Settings.UpdateAssistance.ConnectionContract
            {
                Provider = kind.Code,
                ApiKeyConfigured = connection?.HasApiKey ?? false,
                BaseUrl = connection?.BaseUrl ?? string.Empty,
                Usable = connection?.IsUsable ?? false
            };
        })],
        Uses = [.. Capability.All.Select(capability =>
        {
            var use = settings.UseFor(capability);
            var kind = AssistantKind.Parse(use?.Provider);

            return new Contracts.Settings.UpdateAssistance.UseContract
            {
                Capability = capability.Code,
                Enabled = use?.Enabled ?? false,
                Provider = use?.Provider ?? string.Empty,
                Model = use?.Model ?? string.Empty,
                DefaultModel = kind is null
                    ? string.Empty
                    : assistants.DefaultModelFor(kind, capability)
            };
        })],
        MonthlyBudget = settings.MonthlyBudget,
        PersonalBudget = settings.PersonalBudget
    };
}
