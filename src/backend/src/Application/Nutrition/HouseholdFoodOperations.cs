using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Nutrition;
using Domain.Nutrition;
using Domain.Shared;

namespace Application.Nutrition;

/// <summary>The foods a household can choose beyond the table: its own, and those of the households it inherits from.</summary>
public sealed record GetHouseholdFoodsQuery(Guid HouseholdId, Guid UserId);

/// <summary>Keeps a food typed from a package's label, per 100 g.</summary>
public sealed record CreateHouseholdFoodCommand(Guid HouseholdId, Guid UserId, string Name, Nutrients Per100Grams);

/// <summary>Removes one of the household's foods, and every choice of it.</summary>
public sealed record DeleteHouseholdFoodCommand(Guid HouseholdId, Guid UserId, Guid FoodId);

internal sealed class GetHouseholdFoodsQueryHandler(
    IHouseholdRepository households,
    IHouseholdFoodRepository ownFoods)
    : IQueryHandler<GetHouseholdFoodsQuery, HouseholdFoodsResponse>
{
    public async Task<Result<HouseholdFoodsResponse>> Handle(
        GetHouseholdFoodsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Nutrition.GetHouseholdFoods");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () =>
            {
                var library = await HouseholdAccess
                    .LibraryAsync(households, query.HouseholdId, cancellationToken)
                    .ConfigureAwait(false);
                var foods = await ownFoods.ForLibraryAsync(library, cancellationToken).ConfigureAwait(false);

                return Result<HouseholdFoodsResponse>.Success(new HouseholdFoodsResponse
                {
                    Items = [.. foods.Select(food => food.ToContract(query.HouseholdId))]
                });
            },
            error => Task.FromResult(Result<HouseholdFoodsResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class CreateHouseholdFoodCommandHandler(
    IHouseholdRepository households,
    IHouseholdFoodRepository ownFoods,
    TimeProvider time)
    : ICommandHandler<CreateHouseholdFoodCommand, HouseholdFoodDetail>
{
    public async Task<Result<HouseholdFoodDetail>> Handle(
        CreateHouseholdFoodCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Nutrition.CreateHouseholdFood");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var id = Guid.CreateVersion7();

        var result = await allowed
            .Bind(() => HouseholdFoods.Create(id, command.Name, command.Per100Grams))
            .Match(
                async food =>
                {
                    await ownFoods
                        .AddAsync(command.HouseholdId, id, food, time.GetUtcNow(), cancellationToken)
                        .ConfigureAwait(false);

                    return Result<HouseholdFoodDetail>.Success(
                        new HouseholdFood(id, food, command.HouseholdId).ToContract(command.HouseholdId));
                },
                error => Task.FromResult(Result<HouseholdFoodDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class DeleteHouseholdFoodCommandHandler(
    IHouseholdRepository households,
    IHouseholdFoodRepository ownFoods)
    : ICommandHandler<DeleteHouseholdFoodCommand>
{
    public async Task<Result> Handle(DeleteHouseholdFoodCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Nutrition.DeleteHouseholdFood");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () =>
            {
                await ownFoods.RemoveAsync(command.HouseholdId, command.FoodId, cancellationToken).ConfigureAwait(false);

                return Result.Success();
            },
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

/// <summary>Household foods to their wire shape.</summary>
internal static class HouseholdFoodMappings
{
    internal static HouseholdFoodDetail ToContract(this HouseholdFood food, Guid asking)
    {
        var values = food.Food.Per100Grams;

        return new HouseholdFoodDetail
        {
            Id = food.Id,
            Code = food.Food.Code,
            Name = food.Food.NameDe,
            EnergyKj = values.EnergyKj ?? 0,
            EnergyKcal = values.EnergyKcal ?? 0,
            Fat = values.Fat,
            SaturatedFat = values.SaturatedFat,
            Carbohydrate = values.Carbohydrate,
            Sugars = values.Sugars,
            Protein = values.Protein,
            Salt = values.Salt,
            Own = food.HouseholdId == asking
        };
    }
}
