using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Response = Contracts.Households.Create.Response;

namespace Application.Households.Create;

/// <summary>Creates a household with the caller as its owner.</summary>
/// <param name="UserId">Who is creating it.</param>
/// <param name="Name">What to call it.</param>
public sealed record CreateHouseholdCommand(Guid UserId, string Name);

internal sealed class CreateHouseholdCommandHandler(
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CreateHouseholdCommand, Response>
{
    public async Task<Result<Response>> Handle(
        CreateHouseholdCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.Create");

        var name = HouseholdName.Create(command.Name);

        var result = await name.Match(
            value => StoreAsync(value, command.UserId, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> StoreAsync(
        HouseholdName name,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var household = Household.Create(name, ownerId, time.GetUtcNow());

        return await unitOfWork.InTransactionAsync(
            async token =>
            {
                var added = await households.AddAsync(household, token).ConfigureAwait(false);

                return added.Bind(() => Result<Response>.Success(new Response
                {
                    HouseholdId = household.Id,
                    Name = household.Name.Value,
                    MemberCount = household.Members.Count,
                    YourRole = household.YourRoleIn(ownerId),
                    Version = household.Version
                }));
            },
            cancellationToken).ConfigureAwait(false);
    }
}
