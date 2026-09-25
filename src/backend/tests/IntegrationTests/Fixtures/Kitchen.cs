using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Fixtures;

/// <summary>
/// One signed-in cook and their household, with a way to write recipes into it
/// through the API — exactly as the app does, so the search documents are built
/// by the same writes that build them in production.
/// </summary>
/// <param name="Client">Signed in.</param>
/// <param name="HouseholdId">Their household.</param>
internal sealed record Kitchen(ApiClient Client, Guid HouseholdId)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh database, one account, and its household.</summary>
    internal static async Task<Kitchen> OpenAsync(PostgresFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);

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

        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        return new Kitchen(client, householdId);
    }

    /// <summary>
    /// Another cook, signed in and in no household of this kitchen — the one a
    /// recipe has to be invisible to.
    /// </summary>
    internal static async Task<ApiClient> StrangerAsync(PostgresFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);

        var settings = postgres.Api.Services
            .GetRequiredService<Application.Abstractions.Settings.RegistrationSettings>();
        settings.OpenRegistration = true;
        settings.RequireInvitation = false;

        var client = postgres.Api.NewApiClient();
        await client.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "grace@example.com", password = Password },
            Token);

        return client;
    }

    /// <summary>Creates a recipe and fills it in, as the editor would.</summary>
    internal async Task<Guid> SaveAsync(
        string title,
        string language,
        int? prep,
        int? cook,
        (string Name, string? Unit)[] ingredients,
        string[] tags,
        string step)
    {
        var created = await Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = HouseholdId, title },
            Token);

        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title,
                language,
                yieldAmount = 4,
                yieldKind = "servings",
                prepMinutes = prep,
                cookMinutes = cook,
                groups = new[]
                {
                    new
                    {
                        name = (string?)null,
                        ingredients = ingredients
                            .Select(line => new { name = line.Name, unit = line.Unit })
                            .ToArray()
                    }
                },
                steps = new[]
                {
                    new { segments = new[] { new { type = "text", value = step } } }
                },
                tags
            })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        await Client.SendAsync(request, Token);

        return recipeId;
    }

    /// <summary>A search, as the library asks it.</summary>
    internal Task<ApiResponse> SearchAsync(string query) =>
        Client.GetAsync(
            $"/api/v1/recipes?householdId={HouseholdId}&query={Uri.EscapeDataString(query)}&limit=100",
            Token);
}
