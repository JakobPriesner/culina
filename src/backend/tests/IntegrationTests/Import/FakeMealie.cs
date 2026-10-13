using System.Globalization;
using System.Net;
using System.Web;

namespace IntegrationTests.Import;

/// <summary>Enough of Mealie v2 to be connected and read: sign-in, a token, a paged list and a detail.</summary>
/// <remarks>The JSON is shaped as Mealie sends it, camelCase except where Mealie itself is not.</remarks>
internal sealed class FakeMealie : IDisposable
{
    /// <summary>The long-lived token it makes at sign-in, and the only one its recipes answer to.</summary>
    public const string ApiToken = "mealie-api-token";

    /// <summary>The session token its sign-in hands out, good only for making the long-lived one.</summary>
    public const string SessionToken = "mealie-session-token";

    private FakeSourceServer server = null!;
    private int recipes;
    private bool signInGivesNoToken;

    public string Address => server.Address;

    public IReadOnlyList<SeenRequest> Requests => server.Requests;

    public static FakeMealie Start(int recipes, bool signInGivesNoToken = false)
    {
        var fake = new FakeMealie { recipes = recipes, signInGivesNoToken = signInGivesNoToken };

        fake.server = FakeSourceServer.Start((context, _) => fake.AnswerAsync(context));

        return fake;
    }

    /// <summary>The id of the n-th recipe, counting from one.</summary>
    public static string IdOf(int number) =>
        $"00000000-0000-0000-0000-{number.ToString("D12", CultureInfo.InvariantCulture)}";

    public void Dispose() => server.Dispose();

    private Task AnswerAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var path = request.Url!.AbsolutePath;
        var authorization = request.Headers["Authorization"];

        return (request.HttpMethod, path) switch
        {
            ("POST", "/api/auth/token") => FakeSourceServer.WriteAsync(
                context,
                200,
                signInGivesNoToken
                    ? """{"token_type":"bearer"}"""
                    : $$"""{"access_token":"{{SessionToken}}","token_type":"bearer","expires_in":172800}"""),
            ("POST", "/api/users/api-tokens") when authorization == $"Bearer {SessionToken}" =>
                FakeSourceServer.WriteAsync(context, 201, $$"""{"name":"Culina","id":1,"token":"{{ApiToken}}"}"""),
            (_, _) when authorization != $"Bearer {ApiToken}" => FakeSourceServer.WriteAsync(context, 401, "{}"),
            ("GET", "/api/recipes") => FakeSourceServer.WriteAsync(context, 200, Page(request.Url)),
            ("GET", _) when Detail(path) is { } number => FakeSourceServer.WriteAsync(context, 200, Recipe(number)),
            _ => FakeSourceServer.WriteAsync(context, 404, "{}")
        };
    }

    private int? Detail(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts is not ["api", "recipes", var id])
        {
            return null;
        }

        var number = Enumerable.Range(1, recipes).FirstOrDefault(candidate => IdOf(candidate) == id);

        return number == 0 ? null : number;
    }

    private string Page(Uri url)
    {
        var query = HttpUtility.ParseQueryString(url.Query);
        var page = int.Parse(query["page"] ?? "1", CultureInfo.InvariantCulture);
        var perPage = int.Parse(query["perPage"] ?? "50", CultureInfo.InvariantCulture);
        var totalPages = (recipes + perPage - 1) / perPage;

        var items = Enumerable.Range(1, recipes)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(number =>
                $$"""{"id":"{{IdOf(number)}}","slug":"recipe-{{number}}","name":"Recipe {{number}}","image":"abc","recipeServings":2,"prepTime":"10 minutes"}""");

        return $$"""
            {"page":{{page}},"perPage":{{perPage}},"total":{{recipes}},"totalPages":{{totalPages}},
             "items":[{{string.Join(',', items)}}],"next":"/api/recipes?page={{page + 1}}","previous":null}
            """;
    }

    private static string Recipe(int number) =>
        $$"""
        {
          "id": "{{IdOf(number)}}",
          "slug": "recipe-{{number}}",
          "name": "Recipe {{number}}",
          "description": "Theirs",
          "image": "abc",
          "recipeServings": 0,
          "recipeYield": "4 Portionen",
          "prepTime": "PT15M",
          "performTime": "1 hour 30 minutes",
          "orgURL": "https://example.com/recipe-{{number}}",
          "tags": [{ "name": "Herbst" }],
          "recipeCategory": [{ "name": "Suppe" }],
          "recipeIngredient": [
            { "title": "Für die Suppe", "quantity": 500, "unit": { "name": "g" }, "food": { "name": "Kürbis" }, "note": "gewürfelt" },
            { "title": "", "quantity": 0, "unit": null, "food": null, "note": "1 Prise Salz", "display": "1 Prise Salz" },
            { "title": "Zum Servieren", "quantity": 2, "unit": { "name": "EL" }, "food": { "name": "Kernöl" }, "note": "" }
          ],
          "recipeInstructions": [
            { "title": "", "text": "Kürbis weich kochen.", "ingredientReferences": [] },
            { "title": "Anrichten", "text": "Mit Kernöl beträufeln.", "ingredientReferences": [] }
          ]
        }
        """;
}
