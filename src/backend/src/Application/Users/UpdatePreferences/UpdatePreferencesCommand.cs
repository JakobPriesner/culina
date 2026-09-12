using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;
using Response = Contracts.Users.UpdatePreferences.Response;

namespace Application.Users.UpdatePreferences;

/// <summary>Replaces one person's preferences.</summary>
/// <param name="UserId">Whose preferences.</param>
/// <param name="Locale">The chosen language.</param>
/// <param name="Theme">The chosen theme id.</param>
/// <param name="Mode">The chosen appearance.</param>
/// <param name="MeasurementSystem">The chosen units.</param>
public sealed record UpdatePreferencesCommand(
    Guid UserId,
    string Locale,
    string Theme,
    string Mode,
    string MeasurementSystem);

internal sealed class UpdatePreferencesCommandHandler(IUserPreferencesRepository preferences)
    : ICommandHandler<UpdatePreferencesCommand, Response>
{
    public async Task<Result<Response>> Handle(
        UpdatePreferencesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Users.UpdatePreferences");

        var locale = PreferenceWords.ToLocale(command.Locale);
        var mode = PreferenceWords.ToMode(command.Mode);
        var measurement = PreferenceWords.ToMeasurementSystem(command.MeasurementSystem);

        var chosen = Result.Combine(
                Ignoring(locale),
                Ignoring(mode),
                Ignoring(measurement))
            .Bind(() => locale.Bind(l => mode.Bind(m => measurement.Map(s => (Locale: l, Mode: m, System: s)))));

        var result = await chosen.Match(
            choice => SaveAsync(command, choice, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> SaveAsync(
        UpdatePreferencesCommand command,
        (Locale Locale, ThemeMode Mode, MeasurementSystem System) choice,
        CancellationToken cancellationToken)
    {
        var stored = await preferences.GetAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        var changed = stored.Change(choice.Locale, command.Theme, choice.Mode, choice.System);

        return await changed.Match(
            async () =>
            {
                var saved = await preferences.SaveAsync(stored, cancellationToken).ConfigureAwait(false);

                return saved.Map(version => new Response
                {
                    Locale = PreferenceCodes.Of(stored.Locale),
                    Theme = stored.Theme,
                    Mode = PreferenceCodes.Of(stored.Mode),
                    MeasurementSystem = PreferenceCodes.Of(stored.MeasurementSystem),
                    Version = version
                });
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>Reduces a parse to pass/fail so several can be combined.</summary>
    private static Result Ignoring<TValue>(Result<TValue> result) =>
        result.Match(_ => Result.Success(), Result.Failure);
}
