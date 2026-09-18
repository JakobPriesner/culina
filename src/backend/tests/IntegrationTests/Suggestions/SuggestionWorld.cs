using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Suggestions;

/// <summary>
/// A kitchen, built through the API the way a person would build one.
/// </summary>
/// <remarks>
/// Seeded through HTTP rather than by inserting rows, because the suggestion
/// ranking reads eight tables written by six different handlers. A fixture that
/// wrote them itself would be a second opinion about what a cooked recipe looks
/// like in the database, and the first thing to rot.
/// </remarks>
internal sealed record SuggestionWorld(ApiClient Client, Guid HouseholdId)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    internal static async Task<SuggestionWorld> NewAsync(
        PostgresFixture postgres,
        string email = "ada@example.com")
    {
        await postgres.ResetAsync(Token);

        var client = await SignUpAsync(postgres, email, "Ada");

        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        return new SuggestionWorld(client, householdId);
    }

    /// <summary>A second person in the same kitchen, for the household-taste rules.</summary>
    internal async Task<ApiClient> InviteAsync(
        PostgresFixture postgres,
        string email,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(postgres);

        // An instance refuses a second account by default, which is the right
        // default and would otherwise make every request this guest sends a
        // silent 401 — and a household-taste rule asserted against a member who
        // never joined passes or fails for no reason anybody could see.
        var settings = postgres.Api.Services.GetRequiredService<RegistrationSettings>();
        settings.OpenRegistration = true;
        settings.RequireInvitation = false;

        var invitation = await Client.PostAsync(
            $"/api/v1/households/{HouseholdId}/invitations",
            new { },
            Token);

        var code = invitation.Json!.Value.GetProperty("code").GetString();

        var guest = await SignUpAsync(postgres, email, displayName, joinWith: code);

        var members = await Client.GetAsync($"/api/v1/households/{HouseholdId}/members", Token);

        Assert.Equal(2, members.Json!.Value.GetProperty("items").GetArrayLength());

        return guest;
    }

    private static async Task<ApiClient> SignUpAsync(
        PostgresFixture postgres,
        string email,
        string displayName,
        string? joinWith = null)
    {
        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email, displayName, password = Password },
            Token);
        await client.PostAsync("/api/v1/sessions", new { email, password = Password }, Token);

        if (joinWith is not null)
        {
            await client.PostAsync($"/api/v1/invitations/{joinWith}/redemptions", new { }, Token);
        }

        return client;
    }

    /// <summary>Writes a recipe down, with as much or as little as the test needs.</summary>
    internal async Task<Guid> WriteAsync(
        string title,
        string[]? ingredients = null,
        string[]? tags = null,
        int? prep = null,
        int? cook = null,
        int steps = 1)
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
                language = "en",
                yieldAmount = 4,
                yieldKind = "servings",
                prepMinutes = prep,
                cookMinutes = cook,
                groups = new[]
                {
                    new
                    {
                        name = (string?)null,
                        ingredients = (ingredients ?? ["water"])
                            .Select(name => new { name, unit = (string?)null })
                            .ToArray()
                    }
                },
                steps = Enumerable.Range(0, steps)
                    .Select(index => new
                    {
                        segments = new[] { new { type = "text", value = $"Step {index + 1}." } }
                    })
                    .ToArray(),
                tags = tags ?? []
            })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        await Client.SendAsync(request, Token);

        return recipeId;
    }

    /// <summary>Records that somebody cooked it, however long ago.</summary>
    internal Task CookedAsync(Guid recipeId, int daysAgo, ApiClient? by = null) =>
        (by ?? Client).PostAsync(
            $"/api/v1/recipes/{recipeId}/cook-log",
            new { madeAt = DateTimeOffset.UtcNow.AddDays(-daysAgo) },
            Token);

    internal Task PlanAsync(Guid recipeId, DateOnly date, string slot) =>
        Client.PostAsync(
            $"/api/v1/households/{HouseholdId}/meal-plan",
            new { date, recipeId, slot },
            Token);

    internal Task DismissAsync(Guid recipeId) =>
        Client.PutAsync($"/api/v1/recipes/{recipeId}/suggestion-dismissal", new { }, Token);

    internal Task<ApiResponse> SuggestAsync(string query = "") =>
        Client.GetAsync($"/api/v1/suggestions?householdId={HouseholdId}{query}", Token);

    /// <summary>The suggested titles, in the order they came back.</summary>
    internal static List<string> Titles(ApiResponse response) =>
        [.. response.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("title").GetString()!)];

    internal static List<Guid> Ids(ApiResponse response) =>
        [.. response.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("recipeId").GetGuid())];

    /// <summary>Where a recipe came in the order, or -1 when it did not.</summary>
    internal static int PositionOf(ApiResponse response, string title) =>
        Titles(response).IndexOf(title);

    internal static string? ReasonOf(ApiResponse response, string title)
    {
        foreach (var item in response.Json!.Value.GetProperty("items").EnumerateArray())
        {
            if (item.GetProperty("title").GetString() != title)
            {
                continue;
            }

            var reason = item.GetProperty("reason");

            return reason.ValueKind == JsonValueKind.Null
                ? null
                : reason.GetProperty("code").GetString();
        }

        return null;
    }
}
