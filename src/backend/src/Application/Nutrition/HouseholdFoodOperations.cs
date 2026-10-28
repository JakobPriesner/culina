using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Nutrition;
using Domain.Nutrition;
using Domain.Products;
using Domain.Shared;

namespace Application.Nutrition;

/// <summary>The foods a household can choose beyond the table: its own, and those of the households it inherits from.</summary>
public sealed record GetHouseholdFoodsQuery(Guid HouseholdId, Guid UserId);

/// <summary>Keeps a food typed from a package's label, per 100 g, or taken over from Open Food Facts for a barcode.</summary>
public sealed record CreateHouseholdFoodCommand(
    Guid HouseholdId,
    Guid UserId,
    string Name,
    Nutrients Per100Grams,
    string? Gtin = null,
    bool FromCatalogue = false);

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
    IProductRepository products,
    IProductCatalogue catalogue,
    IUnitOfWork unitOfWork,
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

        // A barcode is optional, but one that is sent must be one.
        var package = command.Gtin is null
            ? Result<Package>.Success(new Package(null))
            : Gtin.Create(command.Gtin).Map(gtin => new Package(gtin));

        var result = await allowed
            .Bind(() => package)
            .Bind(scanned => HouseholdFoods.Create(id, command.Name, command.Per100Grams).Map(food => (scanned, food)))
            .Match(
                async made => Result<HouseholdFoodDetail>.Success(
                    await KeepAsync(command, id, made.food, made.scanned.Gtin, cancellationToken).ConfigureAwait(false)),
                error => Task.FromResult(Result<HouseholdFoodDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<HouseholdFoodDetail> KeepAsync(
        CreateHouseholdFoodCommand command,
        Guid id,
        Food food,
        Gtin? gtin,
        CancellationToken cancellationToken)
    {
        // What the person was shown, not a fresh ask: the values are compared with what they confirmed.
        var offered = gtin is not null && command.FromCatalogue
            ? await catalogue.RememberedAsync(gtin, cancellationToken).ConfigureAwait(false)
            : null;
        var kept = new HouseholdFood(
            id,
            food,
            command.HouseholdId,
            gtin?.Value,
            HouseholdFoods.SourceOf(food.Per100Grams, gtin is not null && command.FromCatalogue, offered?.Per100Grams));

        await unitOfWork.InTransactionAsync(
            async token =>
            {
                await ownFoods.AddAsync(kept, time.GetUtcNow(), token).ConfigureAwait(false);

                // The barcode becomes known to the household's memory as well, so a scan in the shop or the kitchen
                // finds it by name; a code it already named keeps that name (AlreadyNamed is fine here).
                if (Named(food, gtin, offered) is { } product)
                {
                    _ = await products.AddAsync(command.HouseholdId, command.UserId, product, token).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);

        return kept.ToContract(command.HouseholdId);
    }

    private static HouseholdProduct? Named(Food food, Gtin? gtin, CatalogueProduct? offered)
    {
        if (gtin is null)
        {
            return null;
        }

        // The pack's size only when it is a weight or a volume a recipe line would write.
        var named = HouseholdProduct.Create(
            gtin.Value,
            food.NameDe,
            food.NameDe,
            offered?.PackGrams,
            offered?.PackGrams is null ? null : offered.PackUnit,
            null,
            null);

        return named.Match<HouseholdProduct?>(product => product, _ => null);
    }

    /// <summary>The package a food was typed from, when its barcode was scanned.</summary>
    private sealed record Package(Gtin? Gtin);
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
            Own = food.HouseholdId == asking,
            Gtin = food.Gtin,
            Source = food.Source
        };
    }
}
