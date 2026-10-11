using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Domain.Nutrition;
using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Nutrition;

/// <summary>Forgets a household's weight for an ingredient and unit; having none already is fine.</summary>
/// <param name="HouseholdId">Whose weight.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Name">The ingredient name as written; folded here.</param>
/// <param name="Unit">The unit as written.</param>
public sealed record RemoveUnitWeightCommand(Guid HouseholdId, Guid UserId, string Name, string Unit);

internal sealed class RemoveUnitWeightCommandHandler(
    IHouseholdRepository households,
    INutritionWeightRepository weights)
    : ICommandHandler<RemoveUnitWeightCommand>
{
    public async Task<Result> Handle(RemoveUnitWeightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Nutrition.RemoveUnitWeight");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed
            .Bind(() => ItemName.Create(command.Name))
            .Bind(name => Unit.Create(command.Unit).Map(unit => (name, unitKey: UnitKeys.Of(unit))))
            .Match(
                async pair =>
                {
                    await weights
                        .RemoveAsync(command.HouseholdId, pair.name.ComparisonKey, pair.unitKey, cancellationToken)
                        .ConfigureAwait(false);

                    return Result.Success();
                },
                error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
