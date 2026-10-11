using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Nutrition;
using Domain.Shared;

namespace Application.Nutrition;

/// <summary>Reads how a household's nutrition figures are worked out.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetNutritionSettingsQuery(Guid HouseholdId, Guid UserId);

/// <summary>Says whether typical weights may count lines for a household. Any member may.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="UseTypicalWeights">Whether they may.</param>
public sealed record SetNutritionSettingsCommand(Guid HouseholdId, Guid UserId, bool UseTypicalWeights);

internal sealed class GetNutritionSettingsQueryHandler(
    IHouseholdRepository households,
    INutritionWeightRepository weights)
    : IQueryHandler<GetNutritionSettingsQuery, NutritionSettings>
{
    public async Task<Result<NutritionSettings>> Handle(
        GetNutritionSettingsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Nutrition.GetSettings");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () => Result<NutritionSettings>.Success(new NutritionSettings
            {
                UseTypicalWeights = await weights
                    .UsesTypicalWeightsAsync(query.HouseholdId, cancellationToken)
                    .ConfigureAwait(false)
            }),
            error => Task.FromResult(Result<NutritionSettings>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class SetNutritionSettingsCommandHandler(
    IHouseholdRepository households,
    INutritionWeightRepository weights)
    : ICommandHandler<SetNutritionSettingsCommand, NutritionSettings>
{
    public async Task<Result<NutritionSettings>> Handle(
        SetNutritionSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Nutrition.SetSettings");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () =>
            {
                await weights
                    .SetUsesTypicalWeightsAsync(command.HouseholdId, command.UseTypicalWeights, cancellationToken)
                    .ConfigureAwait(false);

                return Result<NutritionSettings>.Success(
                    new NutritionSettings { UseTypicalWeights = command.UseTypicalWeights });
            },
            error => Task.FromResult(Result<NutritionSettings>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
