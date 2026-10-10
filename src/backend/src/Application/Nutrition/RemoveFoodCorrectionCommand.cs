using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Nutrition;

/// <summary>Goes back to the default for an ingredient name; having no choice already is fine.</summary>
/// <param name="HouseholdId">Whose choice.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Name">The ingredient name as written; folded here.</param>
public sealed record RemoveFoodCorrectionCommand(Guid HouseholdId, Guid UserId, string Name);

internal sealed class RemoveFoodCorrectionCommandHandler(
    IHouseholdRepository households,
    INutritionCorrectionRepository choices)
    : ICommandHandler<RemoveFoodCorrectionCommand>
{
    public async Task<Result> Handle(RemoveFoodCorrectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Nutrition.RemoveFoodCorrection");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed
            .Bind(() => ItemName.Create(command.Name))
            .Match(
                async name =>
                {
                    await choices
                        .RemoveAsync(command.HouseholdId, name.ComparisonKey, cancellationToken)
                        .ConfigureAwait(false);

                    return Result.Success();
                },
                error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
