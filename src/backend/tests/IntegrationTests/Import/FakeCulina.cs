using System.Globalization;
using System.Net;
using System.Web;

namespace IntegrationTests.Import;

/// <summary>Enough of another Culina to be connected and read: sign-in, households, a paged list and a detail.</summary>
/// <remarks>
/// Two kitchens, the second listing a recipe the first lends it. Pages hold two recipes so a browse has to follow
/// a cursor within a kitchen as well as move on to the next one.
/// </remarks>
internal sealed class FakeCulina : IDisposable
{
    /// <summary>The token its sign-in hands a client <c>instance</c>, and the only one it answers to.</summary>
    public const string SessionToken = "culina-session-token";

    /// <summary>Sorted first by id, so it is the kitchen a browse starts in.</summary>
    public static readonly Guid Home = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    /// <summary>The second kitchen, with one recipe of its own and one <see cref="Home"/> lends it.</summary>
    public static readonly Guid Holiday = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private const int PageSize = 2;

    private FakeSourceServer server = null!;
    private bool oldVersion;

    public string Address => server.Address;

    public IReadOnlyList<SeenRequest> Requests => server.Requests;

    /// <param name="oldVersion">Answers sign-in as a Culina from before client <c>instance</c>: no token in the body.</param>
    public static FakeCulina Start(bool oldVersion = false)
    {
        var fake = new FakeCulina { oldVersion = oldVersion };

        fake.server = FakeSourceServer.Start((context, _) => fake.AnswerAsync(context));

        return fake;
    }

    /// <summary>The id of the n-th recipe, counting from one.</summary>
    public static Guid IdOf(int number) =>
        Guid.Parse($"00000000-0000-0000-0001-{number.ToString("D12", CultureInfo.InvariantCulture)}");

    public void Dispose() => server.Dispose();

    // Home has recipes 1 to 3; Holiday has 4, and lists 1 because Home lends it.
    private static readonly (Guid Kitchen, int Number)[] Listed =
        [(Home, 1), (Home, 2), (Home, 3), (Holiday, 1), (Holiday, 4)];

    private Task AnswerAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var path = request.Url!.AbsolutePath;

        return (request.HttpMethod, path) switch
        {
            ("POST", "/api/v1/sessions") => FakeSourceServer.WriteAsync(
                context,
                201,
                oldVersion
                    ? """{"userId":"00000000-0000-0000-0000-000000000001","displayName":"Ada","email":"ada@example.com","isAdmin":false,"csrfToken":"x"}"""
                    : $$"""{"userId":"00000000-0000-0000-0000-000000000001","displayName":"Ada","email":"ada@example.com","isAdmin":false,"csrfToken":"x","sessionToken":"{{SessionToken}}"}"""),
            (_, _) when request.Headers["Authorization"] != $"Bearer {SessionToken}" =>
                FakeSourceServer.WriteAsync(context, 401, "{}"),
            ("GET", "/api/v1/households") => FakeSourceServer.WriteAsync(
                context,
                200,
                // Listed out of order, and with a kitchen in the bin, so the reader has to sort and skip.
                $$"""{"items":[{"householdId":"{{Holiday}}","name":"Holiday"},{"householdId":"00000000-0000-0000-0000-000000000009","name":"Gone","deletedAt":"2026-10-01T00:00:00Z"},{"householdId":"{{Home}}","name":"Home"}]}"""),
            ("GET", "/api/v1/recipes") => FakeSourceServer.WriteAsync(context, 200, Page(request.Url)),
            ("GET", _) when path == $"/api/v1/recipes/{IdOf(1)}" => FakeSourceServer.WriteAsync(context, 200, Recipe()),
            _ => FakeSourceServer.WriteAsync(context, 404, "{}")
        };
    }

    private static string Page(Uri url)
    {
        var query = HttpUtility.ParseQueryString(url.Query);
        var kitchen = Guid.Parse(query["householdId"]!);
        var skip = int.Parse(query["cursor"] ?? "0", CultureInfo.InvariantCulture);
        var limit = Math.Min(PageSize, int.Parse(query["limit"] ?? "24", CultureInfo.InvariantCulture));
        var all = Listed.Where(entry => entry.Kitchen == kitchen).ToList();

        var items = all
            .Skip(skip)
            .Take(limit)
            .Select(entry =>
            {
                var owner = entry.Number == 1 ? Home : kitchen;

                return $$"""{"recipeId":"{{IdOf(entry.Number)}}","householdId":"{{owner}}","title":"Recipe {{entry.Number}}","imageId":"{{IdOf(entry.Number)}}","totalMinutes":30,"yieldAmount":2,"yieldKind":"servings","tags":[],"cookCount":0,"updatedAt":"2026-10-01T00:00:00Z"}""";
            });

        var next = skip + limit < all.Count ? $"\"{skip + limit}\"" : "null";

        return $$"""{"items":[{{string.Join(',', items)}}],"nextCursor":{{next}},"total":{{all.Count}}}""";
    }

    private static string Recipe() =>
        $$"""
        {
          "recipeId": "{{IdOf(1)}}",
          "householdId": "{{Home}}",
          "title": "Kürbissuppe",
          "description": "Unsere",
          "language": "de",
          "yieldAmount": 6,
          "yieldKind": "pieces",
          "yieldLabel": "Gläser",
          "prepMinutes": 15,
          "cookMinutes": 40,
          "imageId": "{{IdOf(1)}}",
          "groups": [
            { "groupId": "00000000-0000-0000-0002-000000000001", "name": "Suppe", "ingredients": [
              { "ingredientId": "00000000-0000-0000-0003-000000000001", "quantity": 500, "unit": "g", "name": "Kürbis", "note": "gewürfelt" },
              { "ingredientId": "00000000-0000-0000-0003-000000000002", "quantity": 1, "unit": "l", "name": "Brühe" }
            ] },
            { "groupId": "00000000-0000-0000-0002-000000000002", "name": "Zum Servieren", "ingredients": [
              { "ingredientId": "00000000-0000-0000-0003-000000000003", "quantity": 2, "unit": "EL", "name": "Kernöl" }
            ] }
          ],
          "steps": [
            { "title": "Kochen", "durationSeconds": 1200, "uses": ["00000000-0000-0000-0003-000000000002"], "segments": [
              { "type": "ingredient", "recipeIngredientId": "00000000-0000-0000-0003-000000000001", "name": "Kürbis", "quantity": 500, "unit": "g" },
              { "type": "text", "value": " weich kochen." }
            ] },
            { "segments": [
              { "type": "text", "value": "Mit " },
              { "type": "ingredient", "recipeIngredientId": "00000000-0000-0000-0003-000000000003" },
              { "type": "text", "value": " beträufeln." },
              { "type": "ingredient", "recipeIngredientId": "00000000-0000-0000-0003-0000000000ff" }
            ] }
          ],
          "tags": ["herbst", "suppe"],
          "origin": { "kind": "web", "externalId": "x", "sourceUrl": "https://example.com/kuerbissuppe", "importedAt": "2026-09-01T00:00:00Z" },
          "createdBy": "00000000-0000-0000-0000-000000000001",
          "createdAt": "2026-09-01T00:00:00Z",
          "updatedAt": "2026-09-01T00:00:00Z",
          "version": 3
        }
        """;
}
