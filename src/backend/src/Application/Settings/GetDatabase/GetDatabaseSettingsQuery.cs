using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Settings.GetDatabase.Response;

namespace Application.Settings.GetDatabase;

/// <summary>Reads how this instance reaches PostgreSQL.</summary>
public sealed record GetDatabaseSettingsQuery;

internal sealed class GetDatabaseSettingsQueryHandler(IServerConfiguration configuration)
    : IQueryHandler<GetDatabaseSettingsQuery, Response>
{
    public Task<Result<Response>> Handle(GetDatabaseSettingsQuery query, CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Settings.GetDatabase");

        var database = ConfiguredDatabase.Read(configuration);

        return Task.FromResult(tracked.Record(Result<Response>.Success(new Response
        {
            Host = database.Host,
            Port = database.Port,
            Name = database.Name,
            Username = database.Username,
            PasswordConfigured = database.Password.Length > 0,
            RequireSsl = database.RequireSsl,
            MaxPoolSize = database.MaxPoolSize,
            Pinned = ServerSettingsFile.Pinned(database.ToConfigurationValues().Keys, configuration),
            Writable = configuration.CanSave()
        })));
    }
}
