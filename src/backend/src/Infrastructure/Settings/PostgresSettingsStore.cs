using System.Text.Json;
using Application.Abstractions.Settings;
using Domain.Shared;
using Infrastructure.Persistence;

namespace Infrastructure.Settings;

/// <summary>Stores one settings group as a single JSONB row.</summary>
/// <remarks>
/// One row per group, so a new admin checkbox is not a migration; the payload is untyped but has
/// one writer and defaults for anything absent.
/// </remarks>
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
