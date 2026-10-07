using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Settings.UpdateDatabase;

/// <summary>Points this instance at a database.</summary>
/// <param name="Host">The server's host name or address.</param>
/// <param name="Port">The port it listens on.</param>
/// <param name="Name">The database name.</param>
/// <param name="Username">The role to connect as.</param>
/// <param name="Password">
/// A new password, or null to keep the current one — which is only possible
/// while the host, port, database name and user stay what they are.
/// </param>
/// <param name="RequireSsl">Whether the connection must use TLS.</param>
/// <param name="MaxPoolSize">The most connections to keep open.</param>
public sealed record UpdateDatabaseSettingsCommand(
    string Host,
    int Port,
    string Name,
    string Username,
    string? Password,
    bool RequireSsl,
    int MaxPoolSize);

/// <remarks>
/// <para>
/// The connection is tried before anything is saved. A database that cannot be
/// reached, or that the migrations cannot run in, is found here while the old
/// settings still work — rather than after the restart, as a process that
/// stops on every start and leaves nobody a screen to fix it from.
/// </para>
/// <para>
/// A value the deployment pins is tried as the deployment pins it, not as the
/// request says: that is the value the next start will use, and the request
/// cannot change it.
/// </para>
/// </remarks>
internal sealed class UpdateDatabaseSettingsCommandHandler(
    IServerConfiguration configuration,
    IDatabaseConnectionCheck connection,
    IHostRestart host)
    : ICommandHandler<UpdateDatabaseSettingsCommand, ServerChange>
{
    public async Task<Result<ServerChange>> Handle(
        UpdateDatabaseSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Settings.UpdateDatabase");

        var current = ConfiguredDatabase.Read(configuration);

        // None typed, or one the deployment fixes: either way it is the
        // password already set that would be sent.
        var storedPassword = string.IsNullOrEmpty(command.Password) || IsPinned(nameof(DatabaseSettings.Password));
        var proposed = new DatabaseSettings
        {
            Host = Proposed(nameof(DatabaseSettings.Host), current.Host, command.Host.Trim()),
            Port = Proposed(nameof(DatabaseSettings.Port), current.Port, command.Port),
            Name = Proposed(nameof(DatabaseSettings.Name), current.Name, command.Name.Trim()),
            Username = Proposed(nameof(DatabaseSettings.Username), current.Username, command.Username.Trim()),
            Password = storedPassword ? current.Password : command.Password!,
            RequireSsl = Proposed(nameof(DatabaseSettings.RequireSsl), current.RequireSsl, command.RequireSsl),
            MaxPoolSize = Proposed(nameof(DatabaseSettings.MaxPoolSize), current.MaxPoolSize, command.MaxPoolSize)
        };

        var changes = ServerSettingsFile.Changes(
            proposed.ToConfigurationValues(),
            current.ToConfigurationValues(),
            configuration);

        var result = await ServerSettingsFile.Check(proposed.Validate)
            .Bind(() => storedPassword && !SameServer(current, proposed)
                ? SettingsErrors.DatabasePasswordRequired
                : Result.Success())
            .Match(
                () => ApplyAsync(proposed, changes, cancellationToken),
                error => Task.FromResult(Result<ServerChange>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private bool IsPinned(string property) =>
        configuration.IsPinned(SettingsKey.Of(DatabaseSettings.SectionName, property));

    /// <summary>What the request asks for, unless the deployment pins it: then what runs now.</summary>
    private T Proposed<T>(string property, T current, T requested) =>
        IsPinned(property) ? current : requested;

    /// <summary>
    /// Whether the stored password would go to the server it was saved for.
    /// </summary>
    /// <remarks>
    /// Keeping the stored password is only for saving other changes. Sent along
    /// with a new host, port, database or user, it would be handed to whatever
    /// answers there — during setup, an address of anybody's choosing — so a
    /// new server needs the password typed again.
    /// </remarks>
    private static bool SameServer(DatabaseSettings current, DatabaseSettings proposed) =>
        current.Host == proposed.Host
        && current.Port == proposed.Port
        && current.Name == proposed.Name
        && current.Username == proposed.Username;

    private async Task<Result<ServerChange>> ApplyAsync(
        DatabaseSettings proposed,
        Dictionary<string, string> changes,
        CancellationToken cancellationToken)
    {
        if (changes.Count == 0)
        {
            return ServerChange.None;
        }

        // Before the check, which can take the whole connection timeout: there
        // is no point making somebody wait ten seconds to be told the answer
        // could never have been saved.
        if (!configuration.CanSave())
        {
            return SettingsErrors.NotWritable;
        }

        var checkedConnection = await connection.CheckAsync(proposed, cancellationToken).ConfigureAwait(false);

        return await checkedConnection.Match(
            () => ServerSettingsFile.SaveAndRestartAsync(changes, configuration, host, cancellationToken),
            error => Task.FromResult(Result<ServerChange>.Failure(error))).ConfigureAwait(false);
    }
}
