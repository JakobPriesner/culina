using System.Text.Json;
using Application.Abstractions.Settings;
using Domain.Shared;
using Infrastructure.Persistence;

namespace Infrastructure.Settings;

/// <summary>
/// Stores one settings group as a single JSONB row.
/// </summary>
/// <remarks>
/// One row per group rather than a column per value, so adding an admin
/// checkbox later is not a schema migration. The trade is that the database
/// cannot type-check the payload — which is acceptable because exactly one
/// writer produces it and the record supplies defaults for anything absent.
/// </remarks>
/// <typeparam name="TSettings">The group.</typeparam>
/// <param name="executor">Runs the SQL.</param>
internal sealed class PostgresSettingsStore<TSettings>(DbExecutor executor) : ISettingsStore<TSettings>
    where TSettings : class, IInstanceSettings<TSettings>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<TSettings?> LoadAsync(CancellationToken cancellationToken)
    {
        var payload = await executor.ExecuteScalarAsync<string?>(
            "select payload::text from settings where group_name = @group;",
            new { group = TSettings.GroupName },
            cancellationToken).ConfigureAwait(false);

        return payload is null ? null : JsonSerializer.Deserialize<TSettings>(payload, Json);
    }

    public async Task<Result> SaveAsync(TSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await executor.ExecuteAsync(
            """
            insert into settings (group_name, payload, updated_at)
            values (@group, cast(@payload as jsonb), now())
            on conflict (group_name) do update
            set payload = excluded.payload, updated_at = excluded.updated_at;
            """,
            new { group = TSettings.GroupName, payload = JsonSerializer.Serialize(settings, Json) },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
