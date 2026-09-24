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
/// <param name="Password">A new password, or null to keep the current one.</param>
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
/// The connection is tried before anything is saved. A database that cannot be
/// reached, or that the migrations cannot run in, is found here while the old
/// settings still work — rather than after the restart, as a process that
/// stops on every start and leaves nobody a screen to fix it from.
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
        var proposed = new DatabaseSettings
        {
            Host = command.Host.Trim(),
            Port = command.Port,
            Name = command.Name.Trim(),
            Username = command.Username.Trim(),
            Password = string.IsNullOrEmpty(command.Password) ? current.Password : command.Password,
            RequireSsl = command.RequireSsl,
            MaxPoolSize = command.MaxPoolSize
        };

        var changes = ServerSettingsFile.Changes(
            proposed.ToConfigurationValues(),
            current.ToConfigurationValues(),
            configuration);

        var result = await ServerSettingsFile.Check(proposed.Validate).Match(
            () => ApplyAsync(proposed, changes, cancellationToken),
            error => Task.FromResult(Result<ServerChange>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

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
