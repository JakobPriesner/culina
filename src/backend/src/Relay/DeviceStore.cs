using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Relay;

/// <summary>Which platform a device token belongs to.</summary>
internal enum Platform
{
    /// <summary>An iPhone or iPad, through APNs.</summary>
    Ios,

    /// <summary>An Android phone, through FCM.</summary>
    Android
}

/// <summary>A registered device: where its notifications go.</summary>
internal sealed record Device(Platform Platform, string DeviceToken);

/// <summary>
/// The relay's whole state: a relay token's hash to the device it stands for. Only the hash is kept, so a copy of the
/// file cannot push to anybody; the token itself lives on the phone and on the one server it signed into.
/// </summary>
internal sealed class DeviceStore(RelayOptions options)
{
    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = options.DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = true
    }.ToString();

    /// <summary>Creates the table on first start.</summary>
    public async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.DatabasePath));

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        await ExecuteAsync(
            """
            pragma journal_mode = wal;
            create table if not exists devices (
                token_hash   blob    not null primary key,
                platform     text    not null check (platform in ('ios', 'android')),
                device_token text    not null,
                created_at   integer not null
            );
            """,
            _ => { },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Registers a device and hands back the relay token that stands for it.</summary>
    public async Task<string> AddAsync(Device device, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        await ExecuteAsync(
            "insert into devices (token_hash, platform, device_token, created_at) values ($hash, $platform, $device, $now);",
            parameters =>
            {
                parameters.AddWithValue("$hash", Hash(token));
                parameters.AddWithValue("$platform", Name(device.Platform));
                parameters.AddWithValue("$device", device.DeviceToken);
                parameters.AddWithValue("$now", now.ToUnixTimeSeconds());
            },
            cancellationToken).ConfigureAwait(false);

        return token;
    }

    /// <summary>The device a relay token stands for, or null for one never issued or revoked.</summary>
    public async Task<Device?> FindAsync(string relayToken, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);

        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var command = connection.CreateCommand();

            await using (command.ConfigureAwait(false))
            {
                command.CommandText = "select platform, device_token from devices where token_hash = $hash;";
                command.Parameters.AddWithValue("$hash", Hash(relayToken));

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                await using (reader.ConfigureAwait(false))
                {
                    return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                        ? new Device(reader.GetString(0) == "ios" ? Platform.Ios : Platform.Android, reader.GetString(1))
                        : null;
                }
            }
        }
    }

    /// <summary>Forgets a relay token; forgetting one already gone is not an error.</summary>
    public Task RemoveAsync(string relayToken, CancellationToken cancellationToken) =>
        ExecuteAsync(
            "delete from devices where token_hash = $hash;",
            parameters => parameters.AddWithValue("$hash", Hash(relayToken)),
            cancellationToken);

    internal static string Name(Platform platform) => platform == Platform.Ios ? "ios" : "android";

    private static byte[] Hash(string relayToken) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(relayToken));

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Every caller passes a literal; values travel as parameters.")]
    private async Task ExecuteAsync(
        string sql,
        Action<SqliteParameterCollection> bind,
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);

        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var command = connection.CreateCommand();

            await using (command.ConfigureAwait(false))
            {
                command.CommandText = sql;
                bind(command.Parameters);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
