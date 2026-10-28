using System.Text.RegularExpressions;
using Domain.Nutrition;
using Domain.Search;
using Infrastructure.Nutrition;

namespace IntegrationTests.Nutrition;

public partial class BlsFoodTableTests
{
    private static readonly BlsFoodTable Table = BlsFoodTable.Load();

    [Fact]
    public void Load_ShouldReadEveryFoodOfTheTable()
    {
        // Assert
        Assert.Equal(7140, Table.Count);
    }

    [Fact]
    public void Find_ShouldFindEveryFood_ByItsCode()
    {
        // Assert
        Assert.All(Table.All, food => Assert.Matches(Code(), food.Code));
        Assert.All(Table.All, food => Assert.Same(food, Table.Find(food.Code)));
    }

    [Fact]
    public void Load_ShouldHoldNoNegativeValue()
    {
        // Assert
        foreach (var food in Table.All)
        {
            var values = food.Per100Grams;

            Assert.All(
                new[] { values.EnergyKj, values.EnergyKcal, values.Fat, values.SaturatedFat, values.Carbohydrate, values.Sugars, values.Protein, values.Salt },
                value => Assert.True(value is null or >= 0m, food.Code));
        }
    }

    [Fact]
    public void Find_ShouldReadAMissingValueAsUnknown_NotZero()
    {
        // Act
        var coffee = Table.Find("N420900");

        // Assert
        Assert.NotNull(coffee);
        Assert.Null(coffee.Per100Grams.SaturatedFat);
        Assert.Equal(1.2m, coffee.Per100Grams.Fat);
    }

    [Fact]
    public void Find_ShouldReadATraceAsZero()
    {
        // Act
        var apple = Table.Find("F110400");

        // Assert
        Assert.Equal(0m, apple?.Per100Grams.Fat);
    }

    [Fact]
    public void Find_ShouldReturnButter()
    {
        // Act
        var butter = Table.Find("Q611000");

        // Assert
        Assert.Equal("Butter mild gesäuert", butter?.NameDe);
        Assert.Equal(747m, butter?.Per100Grams.EnergyKcal);
        Assert.Null(Table.Find("nope"));
    }

    [Fact]
    public void Search_ShouldRankNamesStartingWithTheQueryFirst()
    {
        // Act
        var foods = Table.Search("Butter", 200);

        // Assert
        Assert.Contains(foods, food => food.Code == "Q611000");

        var starting = foods.TakeWhile(StartsWithButter).Count();

        Assert.True(starting > 0);
        Assert.DoesNotContain(foods.Skip(starting), StartsWithButter);
        Assert.Equal(starting, foods.Count(StartsWithButter));
    }

    [Fact]
    public void Search_ShouldPutShorterNamesFirst_WithinARank()
    {
        // Act
        var foods = Table.Search("Butter", 200);
        var starting = foods.Where(StartsWithButter).Select(food => Shortest(food)).ToList();

        // Assert
        Assert.Equal(starting.Order(), starting);
    }

    [Fact]
    public void Search_ShouldReadEnglishNamesToo()
    {
        // Act
        var foods = Table.Search("peanut butter", 5);

        // Assert
        Assert.Contains(foods, food => food.Code == "H880200");
    }

    [Fact]
    public void Search_ShouldFoldUmlauts_AndIgnoreCase()
    {
        // Act & Assert
        Assert.Equal(Table.Search("Müsli", 10), Table.Search("MUESLI", 10));
        Assert.NotEmpty(Table.Search("Müsli", 10));
    }

    [Theory]
    [InlineData("Milch fettarm")]
    [InlineData("fettarme Milch")]
    [InlineData("milch, FETTARM (1,5 %)")]
    public void Search_ShouldFindEveryWord_InAnyOrder_WhateverThePunctuation(string query)
    {
        // Act
        var codes = Table.Search(query, 20).Select(food => food.Code).ToList();

        // Assert: "Milch fettarm, frisch, 1,5 % Fett, pasteurisiert" and "H-Milch fettarm, 1,5 % Fett, …".
        Assert.Contains("M111200", codes);
        Assert.Contains("M113200", codes);
    }

    [Fact]
    public void Search_ShouldPutNamesFirst_WhoseFirstWordIsTheFirstTyped()
    {
        // Act
        var first = Table.Search("Milch fettarm", 20)[0];

        // Assert: before the shorter "H-Milch fettarm, 1,5 % Fett, ultrahocherhitzt".
        Assert.Equal("M111200", first.Code);
    }

    [Fact]
    public void Search_ShouldNeedEveryWord()
    {
        // Act & Assert
        Assert.DoesNotContain(Table.Search("Milch fettarm", 50), food => !food.NameDe.Contains("fettarm", StringComparison.Ordinal));
    }

    [Fact]
    public void Search_ShouldFindAWordInsideACompound()
    {
        // Act & Assert: "Vollmilch frisch, 3,5 % Fett, pasteurisiert".
        Assert.Contains(Table.Search("frische Milch", 20), food => food.Code == "M111300");
    }

    [Theory]
    [InlineData("Hahnchen")]
    [InlineData("Haehnchen")]
    public void Search_ShouldFindUmlauts_TypedEitherWay(string query)
    {
        // Act & Assert
        Assert.Equal(Table.Search("Hähnchen", 20), Table.Search(query, 20));
        Assert.Contains(Table.Search(query + " Brustfilet", 20), food => food.Code == "V416100");
    }

    [Theory]
    [InlineData("Bananne", "F503100")]
    [InlineData("Joghrut", "M141300")]
    [InlineData("Kartofel", "K110100")]
    [InlineData("Hänchenbrustfilet", "Y562032")]
    public void Search_ShouldForgiveASmallTypo(string query, string code)
    {
        // Act & Assert
        Assert.Contains(Table.Search(query, 50), food => food.Code == code);
    }

    [Theory]
    [InlineData("Haferflocken", "C133000")]
    [InlineData("Hähnchenbrustfilet", "V416100")]
    [InlineData("Weizenmehl", "C213100")]
    public void Search_ShouldFindTwoWordsOfAName_TypedAsOne(string query, string code)
    {
        // Act: "Hafer Flocken", "Hähnchen Brustfilet, roh", "Weizen Mehl, Type 812".
        var first = Table.Search(query, 20)[0];

        // Assert
        Assert.Equal(code, first.Code);
    }

    [Fact]
    public void Search_ShouldNotFindAWordAcrossTwoJoined()
    {
        // Act & Assert: "Blutwürste erwärmt" holds "tee" only where its words meet.
        Assert.DoesNotContain(Table.Search("Tee", 50), food => food.Code == "Y024113");
    }

    [Fact]
    public void Search_ShouldNotForgiveATypoInAShortWord()
    {
        // Act & Assert: "Ei" is not "Eis", "Reis" is not "Reiz".
        Assert.DoesNotContain(Table.Search("Reiz", 50), food => SearchText.FoldAe(food.NameDe).Contains("reis", StringComparison.Ordinal));
    }

    [Fact]
    public void Search_ShouldLimitTheResult_AndFindNothingForABlankQuery()
    {
        // Act & Assert
        Assert.Equal(3, Table.Search("butter", 3).Count);
        Assert.Empty(Table.Search("   ", 10));
        Assert.Empty(Table.Search("", 10));
        Assert.Empty(Table.Search("butter", 0));
    }

    [Fact]
    public void FoodNames_ShouldOnlyPointAtFoodsThatExist()
    {
        // Act
        var missing = FoodNames.All.Where(entry => Table.Find(entry.Code) is null).Select(entry => entry.Code);

        // Assert
        Assert.Empty(missing);
    }

    [Fact]
    public void TypicalWeights_ShouldOnlyPointAtFoodsThatExist_AlsoThoseTheyAreCountedAs()
    {
        // Act
        var codes = TypicalWeights.All.Select(row => row.Code).Concat(TypicalWeights.All.Select(row => row.CountAs).OfType<string>());
        var missing = codes.Where(code => Table.Find(code) is null).Distinct();

        // Assert
        Assert.Empty(missing);
    }

    [Fact]
    public void FoodVariants_ShouldOnlyHoldFoodsThatExist()
    {
        // Act
        var missing = FoodVariants.All.SelectMany(group => group).Where(member => Table.Find(member.Code) is null).Select(member => member.Code);

        // Assert
        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("code\tde\ten\tkj\tkcal\tfat\tsat\tcho\tsug\tprot\tsalt\nA000001\tx\ty\t1\t2")]
    [InlineData("code\nA000001\tx\ty\t1\t2\t3\t4\t5\t6\t7\t8\nA000001\tx\ty\t1\t2\t3\t4\t5\t6\t7\t8")]
    [InlineData("code\nA000001\tx\ty\t1\t2\t-1\t4\t5\t6\t7\t8")]
    [InlineData("code\nA000001\tx\ty\t1\t2\toops\t4\t5\t6\t7\t8")]
    public void Parse_ShouldRejectAMalformedTable(string text)
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => BlsFoodTable.Parse(new StringReader(text)));
    }

    private static bool StartsWithButter(Food food) =>
        food.NameDe.StartsWith("Butter", StringComparison.OrdinalIgnoreCase)
        || food.NameEn.StartsWith("Butter", StringComparison.OrdinalIgnoreCase);

    private static int Shortest(Food food) =>
        new[] { food.NameDe, food.NameEn }
            .Where(name => name.StartsWith("Butter", StringComparison.OrdinalIgnoreCase))
            .Min(name => SearchText.FoldAe(name).Length);

    [GeneratedRegex("^[A-Z][0-9A-Z]{6}$")]
    private static partial Regex Code();
}
