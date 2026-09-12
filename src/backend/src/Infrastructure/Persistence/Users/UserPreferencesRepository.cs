using Application.Abstractions;
using Domain.Shared;
using Domain.Users;

namespace Infrastructure.Persistence.Users;

/// <summary>The <c>user_settings</c> row as PostgreSQL returns it.</summary>
internal sealed record UserPreferencesRow
{
    public Guid UserId { get; init; }

    public string Locale { get; init; } = "en";

    public string Theme { get; init; } = UserPreferences.DefaultTheme;

    public string Mode { get; init; } = "system";

    public string MeasurementSystem { get; init; } = "metric";

    public long Version { get; init; }
}

/// <summary>Stores one person's preferences.</summary>
/// <param name="executor">Runs the SQL.</param>
internal sealed class UserPreferencesRepository(DbExecutor executor) : IUserPreferencesRepository
{
    public async Task<UserPreferences> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<UserPreferencesRow>(
            """
            select user_id, locale, theme, mode, measurement_system, version
            from user_settings where user_id = @userId;
            """,
            new { userId },
            cancellationToken).ConfigureAwait(false);

        return row is null ? UserPreferences.Default(userId) : row.ToDomain();
    }

    public async Task<Result<long>> SaveAsync(
        UserPreferences preferences,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        // Upsert rather than insert-or-update in two statements: the row is
        // created lazily, so the first save is indistinguishable from the
        // hundredth and neither can race the other.
        var version = await executor.ExecuteScalarAsync<long?>(
            """
            insert into user_settings (user_id, locale, theme, mode, measurement_system, version)
            values (@userId, @locale, @theme, @mode, @measurementSystem, 1)
            on conflict (user_id) do update
            set locale = excluded.locale,
                theme = excluded.theme,
                mode = excluded.mode,
                measurement_system = excluded.measurement_system,
                version = user_settings.version + 1
            returning version;
            """,
            new
            {
                userId = preferences.UserId,
                locale = PreferenceCodes.Of(preferences.Language),
                theme = preferences.Theme,
                mode = PreferenceCodes.Of(preferences.Mode),
                measurementSystem = PreferenceCodes.Of(preferences.MeasurementSystem)
            },
            cancellationToken).ConfigureAwait(false);

        return version ?? 1;
    }
}

/// <summary>
/// Turns preference rows into domain values and back.
/// </summary>
/// <remarks>
/// Enums are stored as text so a database dump is readable and inserting an
/// enum member later cannot renumber existing rows. An unknown stored value
/// falls back to the default rather than throwing: a preference is not worth
/// failing a request over, and the next save corrects it.
/// </remarks>
internal static class UserPreferencesRowMappings
{
    internal static UserPreferences ToDomain(this UserPreferencesRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return UserPreferences.Restore(
            row.UserId,
            // An unrecognised stored value falls back to the default rather
            // than throwing: a preference is not worth failing a request over,
            // and the next save corrects it.
            PreferenceCodes.ToLanguage(row.Locale) ?? Language.En,
            row.Theme,
            PreferenceCodes.ToMode(row.Mode) ?? ThemeMode.System,
            PreferenceCodes.ToMeasurementSystem(row.MeasurementSystem) ?? MeasurementSystem.Metric,
            row.Version);
    }

}
