using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Response = Contracts.Households.Rename.Response;

namespace Application.Households.Rename;

/// <summary>Renames a household. Owners only.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Name">The new name.</param>
/// <param name="ExpectedVersion">The version the caller last saw.</param>
public sealed record RenameHouseholdCommand(
    Guid HouseholdId,
    Guid UserId,
    string Name,
    long ExpectedVersion);

internal sealed class RenameHouseholdCommandHandler(
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RenameHouseholdCommand, Response>
{
    public async Task<Result<Response>> Handle(
        RenameHouseholdCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.Rename");

        var found = await households.FindAsync(command.HouseholdId, cancellationToken).ConfigureAwait(false);
        var name = HouseholdName.Create(command.Name);

        var prepared = found.Bind(household => name
            .Bind(value => household.Rename(value, command.UserId).Map(() => household)));

        var result = await prepared.Match(
            household => SaveAsync(household, command, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> SaveAsync(
        Household household,
        RenameHouseholdCommand command,
        CancellationToken cancellationToken) =>
        await unitOfWork.InTransactionAsync(
            async token =>
            {
                var saved = await households
                    .UpdateAsync(household, command.ExpectedVersion, token)
                    .ConfigureAwait(false);

                return saved.Map(version => new Response
                {
                    HouseholdId = household.Id,
                    Name = household.Name.Value,
                    MemberCount = household.Members.Count,
                    YourRole = household.YourRoleIn(command.UserId),
                    Version = version
                });
            },
            cancellationToken).ConfigureAwait(false);
}
