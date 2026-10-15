using Application.Recipes.Sources;
using Domain.Cookbooks;

namespace Application.UnitTests.Recipes;

/// <summary>How the cookbook an import puts its recipes on is named.</summary>
public class ImportShelfNameTests
{
    [Fact]
    public void ShelfName_ShouldDateTheImport_WithoutAMonthNameInAnyLanguage()
    {
        var name = ImportFromSourceCommandHandler.ShelfName(
            "recipes.example.org",
            new DateTimeOffset(2026, 10, 15, 9, 30, 0, TimeSpan.Zero));

        Assert.Equal("recipes.example.org · 2026-10-15", name);
    }

    [Fact]
    public void ShelfName_ShouldUseTheUtcDay_WhateverTheOffsetItWasStartedAt()
    {
        var name = ImportFromSourceCommandHandler.ShelfName(
            "Mealie",
            new DateTimeOffset(2026, 10, 16, 0, 30, 0, TimeSpan.FromHours(2)));

        Assert.Equal("Mealie · 2026-10-15", name);
    }

    [Fact]
    public void ShelfName_ShouldStayWithinTheLongestCookbookName()
    {
        var name = ImportFromSourceCommandHandler.ShelfName(
            new string('a', CookbookName.MaxLength),
            DateTimeOffset.UnixEpoch);

        Assert.Equal(CookbookName.MaxLength, name.Length);
    }
}
