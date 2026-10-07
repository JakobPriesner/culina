using System.Net;
using Application.Abstractions;
using Domain.Import;
using Infrastructure.Persistence.Import;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestSupport;

namespace IntegrationTests.Import;

/// <summary>
/// A connected source's API token, at rest.
/// </summary>
/// <remarks>
/// The token is a working credential for somebody's other app, so what is
/// proved here is what a database dump shows: never the token itself. And when
/// the key ring that encrypted it is gone, the connection asks to be made again
/// rather than breaking every screen that lists it.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class RecipeSourceTokenTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private const string TheToken = "tda_0123456789abcdef";

    private static readonly string[] OneRecipe = ["1"];

    private Guid householdId;
    private Guid userId;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddAsync_ShouldStoreTheTokenEncrypted_AndReadItBack()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var sourceId = await ConnectAsync();

        // Assert
        var stored = await postgres.QuerySingleAsync<string>("select secret from recipe_sources", Token);
        Assert.DoesNotContain(TheToken, stored, StringComparison.Ordinal);
        Assert.True(await postgres.QuerySingleAsync<bool>("select secret_protected from recipe_sources", Token));

        var found = (await FindAsync(sourceId)).ShouldBeSuccess();
        Assert.Equal(TheToken, found.Secret);
        Assert.False(found.NeedsReconnecting);
    }

    [Fact]
    public async Task StartAsync_ShouldEncryptATokenStoredBeforeTokensWereEncrypted()
    {
        // Arrange
        // A row exactly as one written before migration 0027: the plain token,
        // and the flag the migration gave every row that existed then.
        using var client = await SignedInAsync();
        var sourceId = Guid.CreateVersion7();
        await postgres.ExecuteAsync(
            $"""
             insert into recipe_sources
                 (id, household_id, kind, label, base_url, secret, created_by, created_at)
             values
                 ('{sourceId}', '{householdId}', 'tandoor', 'Old', 'https://old.example.com',
                  '{TheToken}', '{userId}', now());
             """,
            Token);
        var encryption = postgres.Api.Services
            .GetServices<IHostedService>()
            .OfType<RecipeSourceTokenEncryption>()
            .Single();

        // Act
        await encryption.StartAsync(Token);

        // Assert
        var stored = await postgres.QuerySingleAsync<string>("select secret from recipe_sources", Token);
        Assert.DoesNotContain(TheToken, stored, StringComparison.Ordinal);
        Assert.True(await postgres.QuerySingleAsync<bool>("select secret_protected from recipe_sources", Token));
        Assert.Equal(TheToken, (await FindAsync(sourceId)).ShouldBeSuccess().Secret);
    }

    [Fact]
    public async Task Browse_ShouldAskToReconnect_WhenTheStoredTokenCannotBeDecrypted()
    {
        // Arrange
        // What an instance restored without its key ring finds: ciphertext,
        // intact, that nothing here can read.
        using var client = await SignedInAsync();
        var sourceId = await ConnectAsync();
        await postgres.ExecuteAsync("update recipe_sources set secret = 'CfDJ8-not-a-payload';", Token);

        // Act
        var browsed = await client.GetAsync($"/api/v1/recipe-sources/{sourceId}/recipes", Token);
        var imported = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = OneRecipe },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, browsed.StatusCode);
        Assert.Equal("import.source_needs_reconnecting", browsed.Json!.Value.GetProperty("code").GetString());
        Assert.Equal("import.source_needs_reconnecting", imported.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Disconnect_ShouldStillWork_WhenTheStoredTokenCannotBeDecrypted()
    {
        // Arrange
        // Disconnecting is how such a connection gets fixed, so neither it nor
        // the list it is chosen from may depend on the token.
        using var client = await SignedInAsync();
        var sourceId = await ConnectAsync();
        await postgres.ExecuteAsync("update recipe_sources set secret = 'CfDJ8-not-a-payload';", Token);

        // Act
        var listed = await client.GetAsync($"/api/v1/recipe-sources?householdId={householdId}", Token);
        var disconnected = await client.DeleteAsync($"/api/v1/recipe-sources/{sourceId}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Single(listed.Json!.Value.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NoContent, disconnected.StatusCode);
        Assert.Equal(0L, await postgres.QuerySingleAsync<long>("select count(*) from recipe_sources", Token));
    }

    private async Task<Guid> ConnectAsync()
    {
        var source = RecipeSource.Create(
            householdId,
            SourceKind.Tandoor,
            label: null,
            SourceAddress.Create("https://tandoor.example.com").ShouldBeSuccess(),
            TheToken,
            userId,
            DateTimeOffset.UtcNow).ShouldBeSuccess();

        var scope = postgres.Api.Services.CreateAsyncScope();

        await using (scope.ConfigureAwait(true))
        {
            (await scope.ServiceProvider.GetRequiredService<IRecipeSourceRepository>()
                .AddAsync(source, Token)).ShouldBeSuccess();
        }

        return source.Id;
    }

    private async Task<Domain.Shared.Result<RecipeSource>> FindAsync(Guid sourceId)
    {
        var scope = postgres.Api.Services.CreateAsyncScope();

        await using (scope.ConfigureAwait(true))
        {
            return await scope.ServiceProvider.GetRequiredService<IRecipeSourceRepository>()
                .FindAsync(sourceId, Token);
        }
    }

    private async Task<ApiClient> SignedInAsync()
    {
        await postgres.ResetAsync(Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        var me = (await client.GetAsync("/api/v1/users/me", Token)).Json!.Value;

        householdId = me.GetProperty("households")[0].GetProperty("householdId").GetGuid();
        userId = me.GetProperty("userId").GetGuid();

        return client;
    }
}
