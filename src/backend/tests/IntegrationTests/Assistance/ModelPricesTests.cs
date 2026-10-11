using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Assistance;
using Infrastructure.Assistance;

namespace IntegrationTests.Assistance;

/// <summary>Culina's own price list, and the administrator's prices that win over it.</summary>
public class ModelPricesTests
{
    [Fact]
    public void Of_ShouldUseCulinasList_WhenTheAdministratorSetNoPrice()
    {
        var prices = new ModelPrices(new AssistanceSettings());

        var priced = prices.Of(AssistantKind.OpenAi, "gpt-6-astra", 1_000_000, 1_000_000, 0);

        Assert.Equal(new Priced(11.25m, PricedBy.Culina), priced);
    }

    [Fact]
    public void Of_ShouldUseTheAdministratorsPrice_ForTheModelTheyNamed()
    {
        var prices = new ModelPrices(Settings(new AssistancePrice
        {
            Model = "gpt-6-astra",
            InputPerMillion = 1m,
            OutputPerMillion = 2m
        }));

        var priced = prices.Of(AssistantKind.OpenAi, "GPT-6-Astra", 1_000_000, 1_000_000, 0);

        // Their rate, negotiated or regional, is the one on their invoice.
        Assert.Equal(new Priced(3m, PricedBy.Administrator), priced);
    }

    [Fact]
    public void Of_ShouldPriceAModelCulinaNeverHeardOf_OnceTheAdministratorHasSetOne()
    {
        var prices = new ModelPrices(Settings(new AssistancePrice { Model = "gpt-image-9", PerImage = 0.05m }));

        Assert.Null(prices.Of(AssistantKind.OpenAi, "gpt-7", 10, 10, 0));
        Assert.Equal(
            new Priced(0.10m, PricedBy.Administrator),
            prices.Of(AssistantKind.OpenAi, "gpt-image-9", 0, 0, 2));
    }

    [Fact]
    public void Of_ShouldNotLetOnePriceCoverAnotherModel_ThatMerelyStartsTheSame()
    {
        var prices = new ModelPrices(Settings(new AssistancePrice
        {
            Model = "gpt-4o",
            InputPerMillion = 100m,
            OutputPerMillion = 100m
        }));

        // Culina's list matches on a prefix for dated snapshots; an administrator's price names one model.
        Assert.Equal(PricedBy.Culina, prices.Of(AssistantKind.OpenAi, "gpt-4o-mini", 10, 10, 0)!.Value.By);
    }

    [Fact]
    public void Of_ShouldFallBackToCulinasList_ForThePartTheAdministratorLeftEmpty()
    {
        var prices = new ModelPrices(Settings(new AssistancePrice { Model = "gpt-image-2.5-flare", PerImage = null }));

        Assert.Equal(
            new Priced(0.04m, PricedBy.Culina),
            prices.Of(AssistantKind.OpenAi, "gpt-image-2.5-flare", 0, 0, 1));
    }

    [Fact]
    public void Of_ShouldIgnoreHalfATokenPrice_UntilBothPartsAreThere()
    {
        var prices = new ModelPrices(Settings(new AssistancePrice { Model = "gpt-6-astra", InputPerMillion = 0m }));

        // Half the administrator's price would make a total that looks right and is not.
        Assert.Equal(
            new Priced(11.25m, PricedBy.Culina),
            prices.Of(AssistantKind.OpenAi, "gpt-6-astra", 1_000_000, 1_000_000, 0));
    }

    [Fact]
    public void Of_ShouldFollowTheLiveSettings_WithoutARestart()
    {
        var settings = new AssistanceSettings();
        var prices = new ModelPrices(settings);

        settings.Prices = [new AssistancePrice { Model = "gpt-6-astra", InputPerMillion = 0m, OutputPerMillion = 0m }];

        Assert.Equal(new Priced(0m, PricedBy.Administrator), prices.Of(AssistantKind.OpenAi, "gpt-6-astra", 5, 5, 0));
    }

    [Fact]
    public void Listed_ShouldShowCulinasPrice_IgnoringTheAdministrators()
    {
        var prices = new ModelPrices(Settings(new AssistancePrice
        {
            Model = "gpt-6-astra",
            InputPerMillion = 1m,
            OutputPerMillion = 2m
        }));

        Assert.Equal(new ModelPrice(1.25m, 10.00m, null), prices.Listed("gpt-6-astra"));
        Assert.Null(prices.Listed("gpt-7"));
        Assert.Equal(new DateOnly(2026, 9, 19), prices.RecordedOn);
    }

    private static AssistanceSettings Settings(AssistancePrice price) => new() { Prices = [price] };
}
