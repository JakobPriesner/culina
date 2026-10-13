using System.Net;
using Application.Abstractions;
using Application.Recipes.Sources;
using Contracts.Recipes.Sources;
using Domain.Import;
using Domain.Shared;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using TestSupport;

namespace IntegrationTests.Import;

/// <summary>
/// A deleted recipe keeps its origin while it waits in the bin, so importing it again must bring
/// that copy back rather than call it already here (the library shows nothing) or fail on the
/// unique origin.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class ReimportFromBinTests(PostgresFixture postgres)
{
    private const string TheirId = "42";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Import_ShouldRestoreTheBinnedCopy_OntoTheNewShelf()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var world = await WorldAsync(kitchen);

        var first = await ImportAsync(world, await CookbookAsync(kitchen, "First"));
        await BinAsync(kitchen, first.RecipeId!.Value);

        var shelf = await CookbookAsync(kitchen, "Again");
        var again = await ImportAsync(world, shelf);

        Assert.Equal("imported", again.Outcome);
        Assert.Equal(first.RecipeId, again.RecipeId);
        Assert.Equal(
            HttpStatusCode.OK,
            (await kitchen.Client.GetAsync($"/api/v1/recipes/{first.RecipeId}", Token)).StatusCode);
        Assert.Equal(
            1,
            (await kitchen.Client.GetAsync($"/api/v1/cookbooks/{shelf}", Token))
                .Json!.Value.GetProperty("recipeCount").GetInt32());
    }

    [Fact]
    public async Task AlreadyHere_ShouldSayACopyIsInTheBin()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var world = await WorldAsync(kitchen);

        var first = await ImportAsync(world, await CookbookAsync(kitchen, "First"));

        Assert.False((await HereAsync(world))[TheirId].InBin);

        await BinAsync(kitchen, first.RecipeId!.Value);

        // Browsing reads this to offer the recipe again instead of linking to a recipe nobody sees.
        Assert.Equal(new ImportedCopy(first.RecipeId!.Value, InBin: true), (await HereAsync(world))[TheirId]);
    }

    private async Task<World> WorldAsync(Kitchen kitchen)
    {
        var userId = (await kitchen.Client.GetAsync("/api/v1/users/me", Token))
            .Json!.Value.GetProperty("userId").GetGuid();

        var address = SourceAddress.Create("https://recipes.example.com")
            .Match(value => value, error => throw new InvalidOperationException(error.Code));
        var source = RecipeSource.Restore(
            Guid.NewGuid(),
            kitchen.HouseholdId,
            SourceKind.Tandoor,
            "recipes.example.com",
            address,
            "tda_secret",
            userId,
            DateTimeOffset.UtcNow,
            lastUsedAt: null,
            version: 1);

        await using var scope = postgres.Api.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IRecipeSourceRepository>().AddAsync(source, Token))
            .ShouldBeSuccess();

        return new World(source, userId);
    }

    private async Task<ImportedRecipe> ImportAsync(World world, Guid cookbookId)
    {
        await using var scope = postgres.Api.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<RecipeImporter>().ImportAsync(
            new ImportInto(world.Source, new OneRecipeLibrary(), cookbookId, world.UserId, Language.De),
            TheirId,
            Token);
    }

    private async Task<IReadOnlyDictionary<string, ImportedCopy>> HereAsync(World world)
    {
        await using var scope = postgres.Api.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IRecipeOriginRepository>()
            .AlreadyHereAsync(world.Source.HouseholdId, SourceKind.Tandoor, [TheirId], Token);
    }

    private static async Task BinAsync(Kitchen kitchen, Guid recipeId) =>
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await kitchen.Client.DeleteCurrentAsync($"/api/v1/recipes/{recipeId}", Token)).StatusCode);

    private static async Task<Guid> CookbookAsync(Kitchen kitchen, string name) =>
        (await kitchen.Client.PostAsync("/api/v1/cookbooks", new { householdId = kitchen.HouseholdId, name }, Token))
            .Json!.Value.GetProperty("cookbookId").GetGuid();

    private sealed record World(RecipeSource Source, Guid UserId);

    /// <summary>A library holding one recipe, read in-process.</summary>
    private sealed class OneRecipeLibrary : IRecipeLibrary
    {
        public SourceKind Kind => SourceKind.Tandoor;

        public Task<Result<string>> SignInAsync(
            SourceAddress address,
            string username,
            string password,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<string>.Failure(ImportErrors.SignInNotPossible));

        public Task<Result> TestAsync(RecipeSource source, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result<SourcePage>> BrowseAsync(
            RecipeSource source,
            string? page,
            string? query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<Stream>> FetchPictureAsync(
            RecipeSource source,
            string pictureUrl,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<SourceRecipe>> FetchAsync(
            RecipeSource source,
            string externalId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<SourceRecipe>.Success(
                new SourceRecipe { ExternalId = externalId, Title = "Käsespätzle" }));
    }
}
