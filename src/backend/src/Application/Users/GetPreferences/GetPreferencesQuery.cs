using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;
using Response = Contracts.Users.GetPreferences.Response;

namespace Application.Users.GetPreferences;

/// <summary>Reads one person's preferences.</summary>
/// <param name="UserId">Whose preferences.</param>
public sealed record GetPreferencesQuery(Guid UserId);

internal sealed class GetPreferencesQueryHandler(IUserPreferencesRepository preferences)
    : IQueryHandler<GetPreferencesQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Users.GetPreferences");

        var stored = await preferences.GetAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        return tracked.Record(Result<Response>.Success(stored.ToGetResponse()));
    }
}

/// <summary>Maps preferences onto the shape this operation returns.</summary>
internal static class PreferencesMappings
{
    internal static Response ToGetResponse(this UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return new Response
        {
            Locale = PreferenceCodes.Of(preferences.Language),
            Theme = preferences.Theme,
            Mode = PreferenceCodes.Of(preferences.Mode),
            MeasurementSystem = PreferenceCodes.Of(preferences.MeasurementSystem),
            Version = preferences.Version
        };
    }
}
