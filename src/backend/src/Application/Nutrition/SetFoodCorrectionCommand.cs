using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Domain.Nutrition;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Nutrition;

/// <summary>Says what a household means by an ingredient name, or that it should not be counted.</summary>
/// <param name="HouseholdId">Whose choice; never the household it inherits from.</param>
/// <param name="UserId">Who is choosing.</param>
/// <param name="Name">The ingredient name as written; folded here.</param>
/// <param name="Food">A BLS code, or null for "do not count".</param>
public sealed record SetFoodCorrectionCommand(Guid HouseholdId, Guid UserId, string Name, string? Food);

internal sealed class SetFoodCorrectionCommandHandler(
    IHouseholdRepository households,
    INutritionCorrectionRepository choices,
    IFoodTable foods)
    : ICommandHandler<SetFoodCorrectionCommand>
{
    public async Task<Result> Handle(SetFoodCorrectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Nutrition.SetFoodCorrection");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed
            .Bind(() => ItemName.Create(command.Name))
            .Bind(name => command.Food is { } code && foods.Find(code) is null
                ? NutritionErrors.UnknownFood
                : Result<ItemName>.Success(name))
            .Match(
                async name =>
                {
                    await choices
                        .SetAsync(command.HouseholdId, name.ComparisonKey, command.Food, cancellationToken)
                        .ConfigureAwait(false);

                    return Result.Success();
                },
                error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
