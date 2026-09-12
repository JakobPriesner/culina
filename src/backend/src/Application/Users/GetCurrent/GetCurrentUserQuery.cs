using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Domain.Users;
using Response = Contracts.Users.GetCurrent.Response;

namespace Application.Users.GetCurrent;

/// <summary>Reads the signed-in user and their households.</summary>
/// <param name="UserId">Who is asking.</param>
public sealed record GetCurrentUserQuery(Guid UserId);

internal sealed class GetCurrentUserQueryHandler(
    IUserRepository users,
    IHouseholdRepository households)
    : IQueryHandler<GetCurrentUserQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Users.GetCurrent");

        var found = await users.FindAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        var result = await found.Match(
            async user =>
            {
                var memberships = await households
                    .ForUserAsync(user.Id, cancellationToken)
                    .ConfigureAwait(false);
                var isAdmin = await users
                    .IsAdminAsync(user.Id, cancellationToken)
                    .ConfigureAwait(false);

                return Result<Response>.Success(user.ToGetCurrentResponse(isAdmin, memberships));
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

/// <summary>Maps a user and their households onto this operation's shape.</summary>
internal static class CurrentUserMappings
{
    internal static Response ToGetCurrentResponse(
        this User user,
        bool isAdmin,
        IReadOnlyList<Household> households)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(households);

        return new Response
        {
            UserId = user.Id,
            Email = user.Email.Value,
            DisplayName = user.DisplayName.Value,
            IsAdmin = isAdmin,
            CreatedAt = user.CreatedAt,
            Households = [.. households.Select(household => household.ToMembership(user.Id))],
            Version = user.Version
        };
    }

    private static Contracts.Users.GetCurrent.HouseholdMembership ToMembership(
        this Household household,
        Guid userId) =>
        new()
        {
            HouseholdId = household.Id,
            Name = household.Name.Value,
            Role = household.Find(userId)?.Role == HouseholdRole.Owner ? "owner" : "member"
        };
}
