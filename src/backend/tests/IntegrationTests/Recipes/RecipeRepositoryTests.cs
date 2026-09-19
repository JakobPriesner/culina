using Application.Abstractions;
using Domain.Households;
using Domain.Recipes;
using Domain.Shared;
using Domain.Suggestions;
using Domain.Users;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Households;
using Infrastructure.Persistence.Recipes;
using Infrastructure.Persistence.Users;
using IntegrationTests.Fixtures;
using TestSupport;

namespace IntegrationTests.Recipes;

[Collection(RequiresDatabase.Name)]
public class RecipeRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAndFind_ShouldRoundTripTheWholeAggregate()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var recipe = await scope.SeedRecipeAsync();
        var butter = recipe.Ingredients.First();

        // Act
        var found = (await scope.Recipes.FindAsync(recipe.Id, Token)).ShouldBeSuccess();

        // Assert
        Assert.Equal("Bolognese", found.Title.Value);
        Assert.Equal(4m, found.Yield.Amount);
        Assert.Equal(2, found.Ingredients.Count());
        Assert.Equal(2, found.Steps.Count);
        Assert.Equal([butter.Id], found.Steps[1].Uses);
        Assert.Equal(["quick", "weeknight"], found.Tags.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Find_ShouldPreserveQuantitiesExactly_IncludingThreeDecimalPlaces()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var recipe = await scope.SeedRecipeAsync();

        // Act
        var found = (await scope.Recipes.FindAsync(recipe.Id, Token)).ShouldBeSuccess();

        // Assert
        var butter = found.Ingredients.Single(ingredient => ingredient.Name == "butter");
        Assert.Equal(200.5m, butter.Quantity.Amount);
        Assert.Equal(Unit.Gram, butter.Quantity.Unit);
    }

    [Fact]
    public async Task Update_ShouldReplaceContents_AndLeaveNoOrphans()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var recipe = await scope.SeedRecipeAsync();

        var onions = RecipeIngredient.Create(
            null, 0, Quantity.Create(2m, Unit.Piece).ShouldBeSuccess(), "onions", null)
            .ShouldBeSuccess();
        recipe.SetContents(
            [IngredientGroup.Create(null, null, 0, [onions]).ShouldBeSuccess()],
            [Step.Create(null, 0, [new TextSegment("Chop.")], [], null).ShouldBeSuccess()],
            Now).ShouldBeSuccess();

        // Act
        var version = (await scope.Recipes.UpdateAsync(recipe, 1, Token)).ShouldBeSuccess();

        // Assert
        Assert.Equal(2, version);
        var found = (await scope.Recipes.FindAsync(recipe.Id, Token)).ShouldBeSuccess();
        Assert.Equal("onions", Assert.Single(found.Ingredients).Name);
        Assert.Single(found.Steps);
        // The reference index is rebuilt from the step's own needs, so nothing
        // from the previous save survives.
        var refs = await scope.CountAsync("select count(*) from step_ingredient_refs;");
        Assert.Equal(0, refs);
    }

    [Fact]
    public async Task Find_ShouldReadBackAnIngredientAStepNeedsButDoesNotName()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var recipe = await scope.SeedRecipeAsync();
        var butter = recipe.Ingredients.First();
        var salt = recipe.Ingredients.Last();

        recipe.SetContents(
            [.. recipe.Groups],
            [
                Step.Create(
                    null,
                    0,
                    [new TextSegment("Melt "), new IngredientSegment(butter.Id), new TextSegment(".")],
                    [salt.Id],
                    null).ShouldBeSuccess()
            ],
            Now).ShouldBeSuccess();

        (await scope.Recipes.UpdateAsync(recipe, 1, Token)).ShouldBeSuccess();

        // Act
        var found = (await scope.Recipes.FindAsync(recipe.Id, Token)).ShouldBeSuccess();

        // Assert
        // The one the sentence names and the one it does not, both stored and
        // both read back — which is the whole point of the table being read.
        // Membership, not order: a step's needs are a set, and the order a
        // reader sees is put on them where the recipe is described.
        var uses = Assert.Single(found.Steps).Uses;

        Assert.Equal(2, uses.Count);
        Assert.Contains(butter.Id, uses);
        Assert.Contains(salt.Id, uses);
    }

    [Fact]
    public async Task Update_ShouldFailThePrecondition_WhenSomeoneElseWroteFirst()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var recipe = await scope.SeedRecipeAsync();
        await scope.Recipes.UpdateAsync(recipe, 1, Token);

        // Act
        var result = await scope.Recipes.UpdateAsync(recipe, 1, Token);

        // Assert
        result.ShouldBeFailure(ConcurrencyErrors.VersionMismatch);
    }

    [Fact]
    public async Task Delete_ShouldRemoveEverythingUnderTheRecipe()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var recipe = await scope.SeedRecipeAsync();

        // Act
        await scope.Recipes.DeleteAsync(recipe.Id, Token);

        // Assert
        (await scope.Recipes.FindAsync(recipe.Id, Token)).ShouldBeFailure(RecipeErrors.NotFound(recipe.Id));
        Assert.Equal(0, await scope.CountAsync("select count(*) from recipe_ingredients;"));
        Assert.Equal(0, await scope.CountAsync("select count(*) from steps;"));
    }

    [Fact]
    public async Task Tags_ShouldBeCreatedOnDemand_AndRemovedWhenNothingUsesThem()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var recipe = await scope.SeedRecipeAsync();
        Assert.Equal(2, await scope.CountAsync("select count(*) from tags;"));

        // Act
        recipe.Describe(RecipeScope.Details(recipe, tags: ["quick"]), Now).ShouldBeSuccess();
        await scope.Recipes.UpdateAsync(recipe, 1, Token);

        // Assert
        // Tags have no management screen because nobody wants one: they appear
        // when used and disappear when unused.
        Assert.Equal(1, await scope.CountAsync("select count(*) from tags;"));
    }

    [Theory]
    [InlineData("Süßspeise", "suessspeise")]
    [InlineData("  Quick & Easy  ", "quick-easy")]
    [InlineData("Crème brûlée", "creme-brulee")]
    public void Slugify_ShouldFoldTwoSpellingsOfAWordOntoOneTag(string name, string expected)
    {
        // Arrange & Act
        var slug = TagWriter.Slugify(name);

        // Assert
        Assert.Equal(expected, slug);
    }

    [Fact]
    public async Task OwnUnitsAsync_ShouldReturnOnlyWhatTheHouseholdAddedItself()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var recipe = await scope.SeedRecipeAsync();

        recipe.SetContents(
            [
                IngredientGroup.Create(null, null, 0,
                    [
                        RecipeIngredient.Create(
                            null, 0,
                            Quantity.Create(1m, Unit.Create("Schuss").ShouldBeSuccess()).ShouldBeSuccess(),
                            "milk", null).ShouldBeSuccess(),
                        RecipeIngredient.Create(
                            null, 1, Quantity.Create(200m, Unit.Gram).ShouldBeSuccess(), "butter", null)
                            .ShouldBeSuccess()
                    ]).ShouldBeSuccess()
            ],
            [],
            Now).ShouldBeSuccess();
        (await scope.Recipes.UpdateAsync(recipe, recipe.Version, Token)).ShouldBeSuccess();

        // Act
        var own = await scope.Recipes.OwnUnitsAsync(recipe.HouseholdId, Token);

        // Assert
        // The built-ins are excluded in the query, not afterwards: a household
        // with four hundred recipes must not send four hundred rows back to
        // have thirteen of them filtered out.
        Assert.Equal(["Schuss"], own);
    }

    [Fact]
    public async Task OwnIngredientNamesAsync_ShouldRankAPrefixAboveAMereContains()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var recipe = await scope.SeedRecipeAsync();

        recipe.SetContents(
            [
                IngredientGroup.Create(null, null, 0,
                    [
                        RecipeIngredient.Create(null, 0, Quantity.Unmeasured, "peanut butter", null)
                            .ShouldBeSuccess(),
                        RecipeIngredient.Create(null, 1, Quantity.Unmeasured, "butter", null)
                            .ShouldBeSuccess()
                    ]).ShouldBeSuccess()
            ],
            [],
            Now).ShouldBeSuccess();
        (await scope.Recipes.UpdateAsync(recipe, recipe.Version, Token)).ShouldBeSuccess();

        // Act
        var found = await scope.Recipes.OwnIngredientNamesAsync(recipe.HouseholdId, "butter", 10, Token);

        // Assert
        // Somebody typing "butter" means the butter, not the peanut butter that
        // happens to contain the word.
        Assert.Equal(["butter", "peanut butter"], found);
    }

    [Fact]
    public async Task OwnIngredientNamesAsync_ShouldNotReachIntoAnotherHouseholdsKitchen()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var recipe = await scope.SeedRecipeAsync();

        // Act
        var found = await scope.Recipes
            .OwnIngredientNamesAsync(CulinaId.New(), "butter", 10, Token);

        // Assert
        Assert.Empty(found);
        Assert.NotEmpty(await scope.Recipes
            .OwnIngredientNamesAsync(recipe.HouseholdId, "butter", 10, Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<RecipeScope> NewScopeAsync()
    {
        _ = postgres.Api.Services;
        await postgres.ResetAsync(Token);

        var session = postgres.NewSession();
        var executor = new DbExecutor(session);

        return new RecipeScope(
            session,
            executor,
            new RecipeRepository(
                executor,
                new TagWriter(executor),
                new RecipeSearcher(executor, TimeProvider.System, RankingWeights.Default),
                new SearchDocumentWriter(executor)),
            new UserRepository(executor),
            new HouseholdRepository(executor));
    }

    private sealed record RecipeScope(
        DbSession Db,
        DbExecutor Executor,
        IRecipeRepository Recipes,
        IUserRepository Users,
        IHouseholdRepository Households) : IAsyncDisposable
    {
        internal async Task<Recipe> SeedRecipeAsync()
        {
            var user = User.Register(
                Email.Create("ada@example.com").ShouldBeSuccess(),
                DisplayName.Create("Ada").ShouldBeSuccess(),
                "argon2id$hash",
                Now);
            (await Users.AddAsync(user, false, Token)).ShouldBeSuccess();

            var household = Household.Create(
                HouseholdName.Create("Kitchen").ShouldBeSuccess(),
                user.Id,
                Now);
            (await Households.AddAsync(household, Token)).ShouldBeSuccess();

            var recipe = Recipe.Create(
                household.Id,
                RecipeTitle.Create("Bolognese").ShouldBeSuccess(),
                user.Id,
                Now);

            var butter = RecipeIngredient.Create(
                null, 0, Quantity.Create(200.5m, Unit.Gram).ShouldBeSuccess(), "butter", "cubed")
                .ShouldBeSuccess();
            var salt = RecipeIngredient.Create(null, 1, Quantity.Unmeasured, "salt", null)
                .ShouldBeSuccess();

            recipe.Describe(Details(recipe, ["quick", "weeknight"]), Now).ShouldBeSuccess();
            recipe.SetContents(
                [IngredientGroup.Create(null, null, 0, [butter, salt]).ShouldBeSuccess()],
                [
                    Step.Create(null, 0, [new TextSegment("Preheat the pan.")], [], null)
                        .ShouldBeSuccess(),
                    Step.Create(
                        null,
                        1,
                        [new TextSegment("Melt "), new IngredientSegment(butter.Id), new TextSegment(".")],
                        [],
                        300).ShouldBeSuccess()
                ],
                Now).ShouldBeSuccess();

            (await Recipes.AddAsync(recipe, Token)).ShouldBeSuccess();

            return recipe;
        }

        internal static RecipeDetails Details(Recipe recipe, IReadOnlyList<string> tags) => new(
            recipe.Title,
            "A weeknight standby.",
            Language.En,
            Yield.Default,
            15,
            45,
            tags);

        internal async Task<int> CountAsync(string sql) =>
            await Executor.ExecuteScalarAsync<int>(sql, null, Token);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
