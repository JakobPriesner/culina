using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Domain.Nutrition;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Nutrition;

/// <summary>Says what a household means by an ingredient name, or that it should not be counted.</summary>
/// <param name="HouseholdId">Whose choice; never the household it inherits from. Its heirs count with it too.</param>
/// <param name="UserId">Who is choosing.</param>
/// <param name="Name">The ingredient name as written; folded here.</param>
/// <param name="Food">A BLS code, a code of a food the household or one it inherits from typed, or null for "do not count".</param>
public sealed record SetFoodCorrectionCommand(Guid HouseholdId, Guid UserId, string Name, string? Food);

internal sealed class SetFoodCorrectionCommandHandler(
    IHouseholdRepository households,
    INutritionCorrectionRepository choices,
    IFoodTable foods,
    IHouseholdFoodRepository ownFoods)
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
            .Match(
                name => SetAsync(command, name, cancellationToken),
                error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result> SetAsync(SetFoodCorrectionCommand command, ItemName name, CancellationToken cancellationToken)
    {
        if (command.Food is { } code && await FindAsync(command.HouseholdId, code, cancellationToken).ConfigureAwait(false) is null)
        {
            return NutritionErrors.UnknownFood;
        }

        await choices.SetAsync(command.HouseholdId, name.ComparisonKey, command.Food, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    // A household's own food may be one the household it inherits from typed, as its heir counts with it too.
    private async Task<Food?> FindAsync(Guid householdId, string code, CancellationToken cancellationToken)
    {
        if (!HouseholdFoods.IsOwn(code))
        {
            return foods.Find(code);
        }

        var library = await HouseholdAccess.LibraryAsync(households, householdId, cancellationToken).ConfigureAwait(false);
        var own = await ownFoods.ForLibraryAsync(library, cancellationToken).ConfigureAwait(false);

        return own.FirstOrDefault(one => one.Food.Code == code)?.Food;
    }
}
