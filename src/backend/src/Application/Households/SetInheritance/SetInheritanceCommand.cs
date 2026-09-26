using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Response = Contracts.Households.SetInheritance.Response;

namespace Application.Households.SetInheritance;

/// <summary>Makes a household see another one's recipes, or stop. Owners only.</summary>
/// <param name="HouseholdId">The household that inherits.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="ParentId">Whom to inherit from, or null for nobody.</param>
public sealed record SetInheritanceCommand(Guid HouseholdId, Guid UserId, Guid? ParentId);

internal sealed class SetInheritanceCommandHandler(
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetInheritanceCommand, Response>
{
    public async Task<Result<Response>> Handle(
        SetInheritanceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.SetInheritance");

        // One transaction for reading and writing, so the chain the cycle check
        // looked through is the chain that is still there when this one joins
        // it. No If-Match: the body is the whole of the new state rather than
        // an edit of something the caller read, so there is nothing to lose.
        var result = await unitOfWork.InTransactionAsync(
            async token =>
            {
                var found = await households.FindAsync(command.HouseholdId, token).ConfigureAwait(false);

                return await found.Match(
                    household => ChangeAsync(household, command, token),
                    error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> ChangeAsync(
        Household household,
        SetInheritanceCommand command,
        CancellationToken cancellationToken)
    {
        var changed = await HouseholdInheritance
            .ApplyAsync(households, household, command.ParentId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var saved = await changed.Match(
            () => households.UpdateAsync(household, household.Version, cancellationToken),
            error => Task.FromResult(Result<long>.Failure(error))).ConfigureAwait(false);

        return await saved.Match(
            async version =>
            {
                var ancestors = await households
                    .AncestorsAsync(household.Id, cancellationToken)
                    .ConfigureAwait(false);

                return Result<Response>.Success(new Response
                {
                    HouseholdId = household.Id,
                    Name = household.Name.Value,
                    MemberCount = household.Members.Count,
                    YourRole = household.YourRoleIn(command.UserId),
                    Version = version,
                    InheritsFrom = [.. ancestors.Select(ancestor => new Contracts.Households.InheritedHousehold
                    {
                        HouseholdId = ancestor.HouseholdId,
                        Name = ancestor.Name
                    })]
                });
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
    }
}
