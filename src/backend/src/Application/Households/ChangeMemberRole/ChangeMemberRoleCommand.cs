using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Response = Contracts.Households.ChangeMemberRole.Response;

namespace Application.Households.ChangeMemberRole;

/// <summary>Changes what a member may do. Owners only.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="MemberId">Whose role changes.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Role">The new role.</param>
public sealed record ChangeMemberRoleCommand(
    Guid HouseholdId,
    Guid MemberId,
    Guid UserId,
    string Role);

internal sealed class ChangeMemberRoleCommandHandler(
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeMemberRoleCommand, Response>
{
    public async Task<Result<Response>> Handle(
        ChangeMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.ChangeMemberRole");

        if (HouseholdMappings.ToRole(command.Role) is not { } role)
        {
            return tracked.Record(Result<Response>.Failure(
                new FieldError("role", HouseholdErrors.InvalidRole.Code, "Role must be 'owner' or 'member'.")));
        }

        var found = await households.FindAsync(command.HouseholdId, cancellationToken).ConfigureAwait(false);

        var changed = found.Bind(household => household
            .ChangeRole(command.MemberId, role, command.UserId)
            .Map(() => household));

        var result = await changed.Match(
            household => SaveAsync(household, command, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> SaveAsync(
        Household household,
        ChangeMemberRoleCommand command,
        CancellationToken cancellationToken) =>
        await unitOfWork.InTransactionAsync(
            async token =>
            {
                var saved = await households
                    .UpdateAsync(household, household.Version, token)
                    .ConfigureAwait(false);

                var members = await households.MembersAsync(household.Id, token).ConfigureAwait(false);
                var member = members.Single(candidate => candidate.UserId == command.MemberId);

                return saved.Map(version => new Response
                {
                    Member = member.ToContract(),
                    Version = version
                });
            },
            cancellationToken).ConfigureAwait(false);
}
