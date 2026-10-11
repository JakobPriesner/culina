using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Domain.Nutrition;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Nutrition;

/// <summary>Says what one unit of an ingredient weighs in a household: "bei uns wiegt 1 Zwiebel 150 g".</summary>
/// <param name="HouseholdId">Whose weight; never the household it inherits from.</param>
/// <param name="UserId">Who is saying it.</param>
/// <param name="Name">The ingredient name as written; folded here.</param>
/// <param name="Unit">The unit as written; a spelling of a known unit is that unit, and anything else is the household's own word.</param>
/// <param name="Grams">Grams of one unit.</param>
public sealed record SetUnitWeightCommand(Guid HouseholdId, Guid UserId, string Name, string Unit, decimal Grams);

internal sealed class SetUnitWeightCommandHandler(
    IHouseholdRepository households,
    INutritionWeightRepository weights)
    : ICommandHandler<SetUnitWeightCommand>
{
    public async Task<Result> Handle(SetUnitWeightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Nutrition.SetUnitWeight");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed
            .Bind(() => ItemName.Create(command.Name))
            .Bind(name => HouseholdWeight.Create(command.Unit, command.Grams).Map(weight => (name, weight)))
            .Match(
                async pair =>
                {
                    await weights
                        .SetAsync(
                            command.HouseholdId,
                            pair.name.ComparisonKey,
                            pair.weight.UnitKey,
                            pair.weight.Grams,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return Result.Success();
                },
                error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
