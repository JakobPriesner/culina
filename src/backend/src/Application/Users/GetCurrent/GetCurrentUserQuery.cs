using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Assistance;
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
    IHouseholdRepository households,
    AssistanceSettings assistance)
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

                // One short walk per household. A person is in two or three,
                // and each chain is a link or two long.
                var ancestors = new Dictionary<Guid, IReadOnlyList<InheritedHousehold>>();

                foreach (var household in memberships)
                {
                    ancestors[household.Id] = await households
                        .AncestorsAsync(household.Id, cancellationToken)
                        .ConfigureAwait(false);
                }

                var isAdmin = await users
                    .IsAdminAsync(user.Id, cancellationToken)
                    .ConfigureAwait(false);

                return Result<Response>.Success(
                    user.ToGetCurrentResponse(isAdmin, memberships, ancestors, assistance));
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
        IReadOnlyList<Household> households,
        IReadOnlyDictionary<Guid, IReadOnlyList<InheritedHousehold>> ancestors,
        AssistanceSettings assistance)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(households);
        ArgumentNullException.ThrowIfNull(ancestors);
        ArgumentNullException.ThrowIfNull(assistance);

        return new Response
        {
            UserId = user.Id,
            Email = user.Email.Value,
            DisplayName = user.DisplayName.Value,
            IsAdmin = isAdmin,
            CreatedAt = user.CreatedAt,
            Households = [.. households.Select(household =>
                household.ToMembership(user.Id, ancestors[household.Id]))],
            Assistance = assistance.ToAvailability(),
            Version = user.Version
        };
    }

    /// <summary>What the assistant may be asked for, as the client needs it.</summary>
    /// <param name="settings">The live instance settings.</param>
    /// <remarks>
    /// Asked of <c>Allows</c> rather than read off the four switches, so this
    /// says the same thing the server will say when the request arrives — on,
    /// connected, allowed here, and possible for this provider. A client told a
    /// capability was available and then refused would be a client showing a
    /// button that does not work.
    /// </remarks>
    private static Contracts.Users.GetCurrent.AssistanceAvailability ToAvailability(
        this AssistanceSettings settings) =>
        new()
        {
            Improve = settings.Allows(Capability.Improve),
            Draft = settings.Allows(Capability.Draft),
            Read = settings.Allows(Capability.Read),
            Draw = settings.Allows(Capability.Draw)
        };

    private static Contracts.Users.GetCurrent.HouseholdMembership ToMembership(
        this Household household,
        Guid userId,
        IReadOnlyList<InheritedHousehold> ancestors) =>
        new()
        {
            HouseholdId = household.Id,
            Name = household.Name.Value,
            Role = household.Find(userId)?.Role == HouseholdRole.Owner ? "owner" : "member",
            InheritsFrom = [.. ancestors.Select(ancestor => new Contracts.Users.GetCurrent.InheritedHousehold
            {
                HouseholdId = ancestor.HouseholdId,
                Name = ancestor.Name
            })]
        };
}
