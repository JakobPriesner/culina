using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Infrastructure.Import;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;

namespace IntegrationTests.Assistance;

/// <summary>
/// A recipe typed by a model reaches a browser as several drafts: the whole chain from endpoint to framing, against a localhost stub in OpenAI's streaming shape.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class DraftStreamTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    /// <summary>The recipe, cut mid-value (<c>"Auber</c>) because providers do not send whole fields.</summary>
    private static readonly string[] Written =
    [
        "{\"title\":\"Auber",
        "ginenauflauf\",\"description\":\"Warm und einfach\",",
        "\"groups\":[{\"ingredients\":[{\"quantity\":2,\"name\":\"Auber",
        "ginen\"},{\"quantity\":200,\"unit\":\"g\",\"name\":\"Feta\"}]}],",
        "\"steps\":[{\"text\":\"Alles ko",
        "chen\"}]}"
    ];

    [Fact]
    public async Task Draft_ShouldArriveInPieces_EachOneFullerThanTheLast()
    {
        using var provider = new StubProvider(Written);
        var world = await ConnectedAsync(provider);

        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "something with aubergines",
                language = "de"
            },
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.ContentHeaders.ContentType?.MediaType);

        var events = Events(response.Body!);

        // More than one, or this is a request that answers once in a stream's clothes.
        Assert.True(events.Count > 1, $"The draft arrived as {events.Count} event(s).");

        // The title lands while the steps are still being written.
        var first = events[0];
        Assert.Equal("Auberginenauflauf", first.GetProperty("draft").GetProperty("title").GetString());
        Assert.False(first.GetProperty("finished").GetBoolean());
        Assert.Empty(first.GetProperty("draft").GetProperty("steps").EnumerateArray());

        // One draft arriving, not several.
        var draftIds = events
            .Select(one => one.GetProperty("draft").GetProperty("draftId").GetString())
            .Distinct()
            .ToList();

        Assert.Single(draftIds);

        var last = events[^1];

        Assert.True(last.GetProperty("finished").GetBoolean());
        Assert.False(last.TryGetProperty("problem", out var problem) && problem.ValueKind is not JsonValueKind.Null);

        var draft = last.GetProperty("draft");

        Assert.Equal("Warm und einfach", draft.GetProperty("description").GetString());
        Assert.Equal(2, draft.GetProperty("groups")[0].GetProperty("ingredients").GetArrayLength());
        Assert.Equal("Alles kochen", draft.GetProperty("steps")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Draft_ShouldBeAskedForAsAStructuredOutput_NotAsARequestForJson()
    {
        using var provider = new StubProvider(Written);
        var world = await ConnectedAsync(provider);

        await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "something with aubergines",
                language = "en"
            },
            Token);

        // Asserted on the wire: the client library rewrote the schema (every property required, request not strict).
        var asked = JsonDocument.Parse(provider.LastRequest).RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema");

        Assert.True(asked.GetProperty("strict").GetBoolean());

        var schema = asked.GetProperty("schema");

        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());

        // Strict needs every property required and every optional one nullable; a non-null `prepMinutes` would invent a cooking time.
        Assert.Equal(
            ["title", "description", "yieldAmount", "yieldLabel", "prepMinutes", "cookMinutes", "groups", "steps", "tags"],
            schema.GetProperty("required").EnumerateArray().Select(one => one.GetString()));

        Assert.Equal(
            ["integer", "null"],
            schema.GetProperty("properties").GetProperty("prepMinutes").GetProperty("type")
                .EnumerateArray().Select(one => one.GetString()));

        // A line with an amount and no ingredient is a mistake, not a shorter line.
        Assert.Equal(
            "string",
            schema.GetProperty("properties").GetProperty("groups")
                .GetProperty("items").GetProperty("properties").GetProperty("ingredients")
                .GetProperty("items").GetProperty("properties").GetProperty("name")
                .GetProperty("type").GetString());
    }

    [Fact]
    public async Task Draft_ShouldCapWhatTheModelMayWrite_AtWhatTheReservationCovers()
    {
        using var provider = new StubProvider(Written);
        var world = await ConnectedAsync(provider);

        await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "something with aubergines",
                language = "en"
            },
            Token);

        // The budget reserves a fixed sum, so the answer needs a ceiling.
        var asked = JsonDocument.Parse(provider.LastRequest).RootElement;

        Assert.Equal(
            Composition.MostOutputTokens,
            asked.GetProperty("max_completion_tokens").GetInt32());
    }

    [Fact]
    public async Task Draft_ShouldReadANullAsSilence_NotAsAValue()
    {
        // A strict answer when the recipe is silent: every property present, nulls where it has nothing.
        using var provider = new StubProvider(
        [
            "{\"title\":\"Linsensuppe\",\"description\":null,\"yieldAmount\":null,",
            "\"yieldLabel\":null,\"prepMinutes\":null,\"cookMinutes\":null,",
            "\"groups\":[{\"name\":null,\"ingredients\":[",
            "{\"quantity\":null,\"unit\":null,\"name\":\"Linsen\",\"note\":null}]}],",
            "\"steps\":[{\"title\":null,\"text\":\"Alles kochen\",\"durationSeconds\":null}],",
            "\"tags\":[]}"
        ]);

        var world = await ConnectedAsync(provider);

        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "lentil soup",
                language = "de"
            },
            Token);

        var draft = Events(response.Body!)[^1].GetProperty("draft");

        // A null arrives as an absence, not "null" or zero, so the editor shows an empty cooking time.
        Assert.False(draft.TryGetProperty("prepMinutes", out var prep) && prep.ValueKind is not JsonValueKind.Null);
        Assert.False(draft.TryGetProperty("description", out var about) && about.ValueKind is not JsonValueKind.Null);

        var line = draft.GetProperty("groups")[0].GetProperty("ingredients")[0];

        Assert.Equal("Linsen", line.GetProperty("name").GetString());
        Assert.False(line.TryGetProperty("quantity", out var amount) && amount.ValueKind is not JsonValueKind.Null);
    }

    [Fact]
    public async Task Draft_ShouldSayWhyOnTheLastEvent_WhenTheModelAnswersWithNonsense()
    {
        using var provider = new StubProvider(["I am afraid I cannot help with that."]);
        var world = await ConnectedAsync(provider);

        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "something with aubergines",
                language = "en"
            },
            Token);

        // A begun 200 cannot become a 422, so the failure travels as an event.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var last = Events(response.Body!)[^1];

        Assert.True(last.GetProperty("finished").GetBoolean());
        Assert.Equal(
            "assistance.unusable_answer",
            last.GetProperty("problem").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Draft_ShouldRecordWhatTheModelUsed_WhenTheAnswerIsNonsense()
    {
        using var provider = new StubProvider(["I am afraid I cannot help with that."]);
        var world = await ConnectedAsync(provider);

        await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "something with aubergines",
                language = "en"
            },
            Token);

        var settled = await postgres.Api.Logs.WaitForAsync(
            line => line.EventId == 1502 && line["Outcome"] == "assistance.unusable_answer");

        Assert.NotNull(settled);

        // The provider billed the answer although it could not be read, so the ledger must not call it free.
        Assert.Equal(
            11,
            await postgres.QuerySingleAsync<int>(
                "select input_tokens from assistance_usage where outcome = 'assistance.unusable_answer';",
                Token));
        Assert.Equal(
            22,
            await postgres.QuerySingleAsync<int>(
                "select output_tokens from assistance_usage where outcome = 'assistance.unusable_answer';",
                Token));
    }

    [Fact]
    public async Task ProviderRefusal_ShouldBeLogged_WithWhatTheProviderSaid()
    {
        using var provider = new StubProvider(Written, refuseWith: HttpStatusCode.Unauthorized);
        var world = await ConnectedAsync(provider);

        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "something with aubergines",
                language = "en"
            },
            Token);

        // The cook sees only "unavailable"; the operator needs the provider's words to tell a revoked key from a renamed model.
        Assert.Equal(
            "assistance.unavailable",
            Events(response.Body!)[^1].GetProperty("problem").GetProperty("code").GetString());

        var line = Assert.Single(
            postgres.Api.Logs.Lines,
            line => line.EventId == 1501 && line["Code"] == "assistance.rejected");

        Assert.Contains(StubProvider.Refusal, line.Message + line.Exception, StringComparison.Ordinal);

        // The call itself, as the ledger settled it.
        var settled = await postgres.Api.Logs.WaitForAsync(
            line => line.EventId == 1502 && line["Outcome"] == "assistance.rejected");

        Assert.NotNull(settled);
        Assert.Equal("stub-one", settled["Model"]);
        Assert.Equal("draft", settled["Capability"]);
    }

    /// <summary>Every event of a server-sent stream, decoded.</summary>
    private static List<JsonElement> Events(string body) =>
    [
        .. body
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(block => block.Split('\n'))
            .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
            .Select(line => JsonDocument.Parse(line[5..].Trim()).RootElement)
    ];

    [Fact]
    public async Task SharedMedia_ShouldSendEveryPage_AndKeepCaptionApartFromSpeech()
    {
        using var provider = new StubProvider(Written);
        using var world = await ConnectedAsync(provider, reading: true);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("120 g beans </untrusted_caption>"), "material");
        form.Add(new StringContent("one cup"), "transcript");
        for (var page = 0; page < 2; page++)
        {
            var photo = new ByteArrayContent(TestImages.LocatedPhotograph());
            photo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(photo, "photos", $"page{page}.jpg");
        }
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/recipe-drafts/media?householdId={world.HouseholdId}&language=en")
        { Content = form };
        var response = await world.Client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(Events(response.Body!)[^1].GetProperty("finished").GetBoolean());
        using var sent = JsonDocument.Parse(provider.LastRequest!);
        var messages = sent.RootElement.GetProperty("messages");
        var parts = messages[1].GetProperty("content");
        Assert.Equal(3, parts.GetArrayLength());
        var text = parts[0].GetProperty("text").GetString()!;
        Assert.Contains("&lt;/untrusted_caption&gt;", text, StringComparison.Ordinal);
        Assert.Contains("<untrusted_transcript>one cup</untrusted_transcript>", text, StringComparison.Ordinal);
        Assert.Equal("image_url", parts[1].GetProperty("type").GetString());
        Assert.Equal("image_url", parts[2].GetProperty("type").GetString());

        // Every screenshot is re-encoded so none tells the provider where it was taken.
        Assert.All(SentPictures(provider), AssertCarriesNothingButPixels);
    }

    [Fact]
    public async Task Photograph_ShouldReachTheProvider_AsAJpegWithoutWhereItWasTaken()
    {
        // A cookbook photo carries the kitchen's coordinates like any other, and this one goes to a third party.
        using var provider = new StubProvider(Written);
        using var world = await ConnectedAsync(provider, reading: true);
        var original = TestImages.LocatedPhotograph(3000, 1000);
        Assert.NotNull(Image.Identify(original).Metadata.ExifProfile);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(original);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/heic");
        form.Add(file, "file", "page.heic");

        var response = await world.Client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/v1/recipe-drafts/photographs?householdId={world.HouseholdId}&language=en")
            { Content = form },
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sent = Assert.Single(SentPictures(provider));

        AssertCarriesNothingButPixels(sent);

        // No larger than the provider would read it anyway.
        using var decoded = Image.Load(sent.Bytes);
        Assert.Equal(2048, decoded.Width);
    }

    /// <summary>The pictures in the last request, as the provider received them.</summary>
    private static List<(string MediaType, byte[] Bytes)> SentPictures(StubProvider provider)
    {
        using var sent = JsonDocument.Parse(provider.LastRequest);

        return
        [
            .. sent.RootElement.GetProperty("messages")[1].GetProperty("content").EnumerateArray()
                .Where(part => part.GetProperty("type").GetString() == "image_url")
                .Select(part => part.GetProperty("image_url").GetProperty("url").GetString()!)
                .Select(url => url["data:".Length..].Split(";base64,"))
                .Select(data => (data[0], Convert.FromBase64String(data[1])))
        ];
    }

    private static void AssertCarriesNothingButPixels((string MediaType, byte[] Bytes) picture)
    {
        Assert.Equal("image/jpeg", picture.MediaType);

        using var decoded = Image.Load(picture.Bytes);

        Assert.Null(decoded.Metadata.ExifProfile);
        Assert.Null(decoded.Metadata.XmpProfile);
        Assert.Null(decoded.Metadata.IptcProfile);
    }

    [Fact]
    public async Task Intake_ShouldFinishAfterTheSubmittingClientLeaves_AndSaveBeforeReady()
    {
        using var provider = new StubProvider(Written, paused: true);
        using var world = await ConnectedAsync(provider, reading: true);
        var browser = IntakeNotifications.GenerateKeys();
        var auth = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var key = await world.Client.GetAsync("/api/v1/push/key", Token);
        Assert.Equal(HttpStatusCode.OK, key.StatusCode);
        Assert.Equal(key.Body, (await world.Client.GetAsync("/api/v1/push/key", Token)).Body);
        var subscription = await world.Client.PutAsync("/api/v1/push/subscription", new { endpoint = "https://fcm.googleapis.com/push/test", p256dh = browser.PublicKey, auth, language = "de" }, Token);
        Assert.Equal(HttpStatusCode.NoContent, subscription.StatusCode);
        var before = postgres.Api.PushRequests;
        var id = Guid.NewGuid();
        var accepted = await SubmitIntake(world, id);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var again = await SubmitIntake(world, id);
        Assert.Equal(id, again.Json!.Value.GetProperty("id").GetGuid());
        // The response is complete while the provider still waits; disposing the client cannot cancel the server job.
        Assert.Equal(before, postgres.Api.PushRequests);
        world.Client.Dispose();
        provider.Continue();
        using var returned = postgres.Api.NewApiClient();
        await returned.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);
        var ready = await WaitForIntake(returned, id, "ready");
        var recipeId = ready.GetProperty("recipeId").GetGuid();
        var recipe = await returned.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        Assert.Equal(HttpStatusCode.OK, recipe.StatusCode);
        Assert.Equal("Auberginenauflauf", recipe.Json!.Value.GetProperty("title").GetString());
        Assert.Equal(2, recipe.Json.Value.GetProperty("groups")[0].GetProperty("ingredients").GetArrayLength());
        Assert.Equal("https://example.com/recipe", recipe.Json.Value.GetProperty("origin").GetProperty("sourceUrl").GetString());
        Assert.Single((await returned.GetAsync("/api/v1/recipe-intakes", Token)).Json!.Value.EnumerateArray());
        for (var i = 0; i < 100 && postgres.Api.PushRequests == before; i++)
        {
            await Task.Delay(20, Token);
        }

        Assert.Equal(before + 1, postgres.Api.PushRequests);
        var reviewed = await returned.PostAsync($"/api/v1/recipe-intakes/{id}/reviewed", new { }, Token);
        Assert.Equal(HttpStatusCode.NoContent, reviewed.StatusCode);
        Assert.Empty((await returned.GetAsync("/api/v1/recipe-intakes", Token)).Json!.Value.EnumerateArray());
    }

    [Fact]
    public async Task Intake_ShouldRetainSourceOnFailure_AndHideItFromAnotherAccount()
    {
        using var provider = new StubProvider(["Not a recipe"]);
        using var world = await ConnectedAsync(provider, reading: true);
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitIntake(world, id, photo: true)).StatusCode);
        var failed = await WaitForIntake(world.Client, id, "failed");
        Assert.Equal("120 g beans", failed.GetProperty("material").GetString());
        Assert.Equal(JsonValueKind.Null, failed.GetProperty("recipeId").ValueKind);
        Assert.Equal(1, failed.GetProperty("photoCount").GetInt32());
        var photo = await world.Client.GetAsync($"/api/v1/recipe-intakes/{id}/photos/0", Token);
        Assert.Equal("image/png", photo.ContentHeaders.ContentType?.MediaType);
        Assert.Equal(IntakePage, photo.Bytes.ToArray());
        var registration = postgres.Api.Services.GetRequiredService<RegistrationSettings>();
        registration.OpenRegistration = true;
        registration.RequireInvitation = false;
        using var other = postgres.Api.NewApiClient();
        await other.PostAsync("/api/v1/users", new { email = "other@example.com", displayName = "Other", password = Password }, Token);
        await other.PostAsync("/api/v1/sessions", new { email = "other@example.com", password = Password }, Token);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/recipe-intakes/{id}", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/recipe-intakes/{id}/photos/0", Token)).StatusCode);
        var nextId = Guid.NewGuid();
        var retryUrl = $"/api/v1/recipe-intakes/{id}/retry?nextId={nextId}";
        var retried = await world.Client.PostAsync(retryUrl, new { }, Token);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(nextId, (await world.Client.PostAsync(retryUrl, new { }, Token)).Json!.Value.GetProperty("id").GetGuid());
        await WaitForIntake(world.Client, nextId, "failed");
        Assert.Equal(photo.Bytes.ToArray(), (await world.Client.GetAsync($"/api/v1/recipe-intakes/{nextId}/photos/0", Token)).Bytes.ToArray());
        Assert.Equal(HttpStatusCode.NotFound, (await world.Client.GetAsync($"/api/v1/recipe-intakes/{id}/photos/0", Token)).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Intake_ShouldFetchLinksOnTheServer_AndUseProvidedWordsWhenTheLinkIsUnavailable(bool caption)
    {
        using var provider = new StubProvider(Written);
        using var world = await ConnectedAsync(provider, reading: true);
        var id = Guid.NewGuid();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("http://127.0.0.1/recipe"), "sourceUrl");
        form.Add(new StringContent("true"), "fetchSource");
        if (caption)
        {
            form.Add(new StringContent("120 g beans"), "material");
        }
        var response = await world.Client.SendAsync(new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/recipe-intakes?id={id}&householdId={world.HouseholdId}&language=de")
        { Content = form }, Token);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var completed = await WaitForIntake(world.Client, id, caption ? "ready" : "failed");
        Assert.Equal("http://127.0.0.1/recipe", completed.GetProperty("sourceUrl").GetString());
        if (caption)
        {
            Assert.Equal("120 g beans", completed.GetProperty("material").GetString());
            Assert.Contains("120 g beans", provider.LastRequest, StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(provider.LastRequest);
            Assert.Equal(JsonValueKind.Null, completed.GetProperty("recipeId").ValueKind);
        }
    }

    [Fact]
    public async Task Intake_ShouldRecoverAFinalCheckpoint_WithoutAskingTheProviderAgain()
    {
        using var provider = new StubProvider(Written);
        using var world = await ConnectedAsync(provider, reading: true);
        var id = Guid.NewGuid();
        var userId = (await world.Client.GetAsync("/api/v1/users/me", Token)).Json!.Value.GetProperty("userId").GetGuid();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var material = JsonSerializer.Serialize(new IntakeMaterial("120 g beans", "", "https://example.com/recipe", "de", []), json);
        var draft = JsonSerializer.Deserialize<Contracts.Recipes.Drafts.Response>(string.Concat(Written)[..^1] +
            $",\"draftId\":\"{id}\",\"tags\":[]}}", json);
        var savedDraft = JsonSerializer.Serialize(draft, json);
        // The state a stopped server leaves after its last streamed event, before the recipe commits. An expired lease is reclaimed.
        await postgres.ExecuteAsync($"""
            insert into recipe_intake_jobs(id,user_id,household_id,material,stage,draft,attempts,lease_until)
            values('{id}','{userId}','{world.HouseholdId}',$material${material}$material$::jsonb,
                'saving',$draft${savedDraft}$draft$::jsonb,2,now()-interval '1 minute')
            """, Token);
        var ready = await WaitForIntake(world.Client, id, "ready");
        var recipeId = ready.GetProperty("recipeId").GetGuid();
        var recipe = await world.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        Assert.Equal(HttpStatusCode.OK, recipe.StatusCode);
        Assert.Equal("Auberginenauflauf", recipe.Json!.Value.GetProperty("title").GetString());
        Assert.Empty(provider.LastRequest);
        Assert.Equal(1L, await postgres.QuerySingleAsync<long>($"select count(*) from recipe_origins where external_id='{id}'", Token));
    }

    [Fact]
    public async Task Intake_ShouldNotDeliverAnOldAccountsNotification_ToANewDeviceOwner()
    {
        using var provider = new StubProvider(Written);
        using var world = await ConnectedAsync(provider, reading: true);
        var browser = IntakeNotifications.GenerateKeys();
        var registration = new
        {
            endpoint = "https://fcm.googleapis.com/push/shared-device",
            p256dh = browser.PublicKey,
            auth = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)),
            language = "en"
        };
        Assert.Equal(HttpStatusCode.NoContent, (await world.Client.PutAsync("/api/v1/push/subscription", registration, Token)).StatusCode);
        var before = postgres.Api.PushRequests;
        var id = Guid.NewGuid();
        await SubmitIntake(world, id);
        await WaitForIntake(world.Client, id, "ready");
        for (var i = 0; i < 100 && postgres.Api.PushRequests == before; i++)
        {
            await Task.Delay(20, Token);
        }
        Assert.Equal(before + 1, postgres.Api.PushRequests);
        var settings = postgres.Api.Services.GetRequiredService<RegistrationSettings>();
        settings.OpenRegistration = true;
        settings.RequireInvitation = false;
        using var other = postgres.Api.NewApiClient();
        await other.PostAsync("/api/v1/users", new { email = "other@example.com", displayName = "Other", password = Password }, Token);
        await other.PostAsync("/api/v1/sessions", new { email = "other@example.com", password = Password }, Token);
        Assert.Equal(HttpStatusCode.NoContent, (await other.PutAsync("/api/v1/push/subscription", registration, Token)).StatusCode);
        // A delayed retry stays with the original user even if the device endpoint is now registered by somebody else.
        await postgres.ExecuteAsync($"update recipe_intake_notifications set delivered_at=null,retry_at=now() where job_id='{id}'", Token);
        var scope = postgres.Api.Services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            await scope.ServiceProvider.GetRequiredService<IIntakeNotifications>().DeliverAsync(Token);
        }
        Assert.Equal(before + 1, postgres.Api.PushRequests);
    }

    /// <summary>A screenshot submitted with an intake, kept as it was sent until it is reviewed.</summary>
    private static readonly byte[] IntakePage = TestImages.Png(40, 30);

    private static Task<ApiResponse> SubmitIntake(World world, Guid id, bool photo = false)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent("120 g beans"), "material");
        form.Add(new StringContent("https://example.com/recipe"), "sourceUrl");
        if (photo)
        {
            form.Add(new ByteArrayContent(IntakePage), "photos", "recipe.png");
        }
        return world.Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/recipe-intakes?id={id}&householdId={world.HouseholdId}&language=de") { Content = form }, Token);
    }

    private static async Task<JsonElement> WaitForIntake(ApiClient client, Guid id, string stage)
    {
        for (var i = 0; i < 100; i++)
        {
            var response = await client.GetAsync($"/api/v1/recipe-intakes/{id}", Token);
            if (response.Json!.Value.GetProperty("stage").GetString() == stage)
            {
                return response.Json.Value;
            }

            if (response.Json.Value.GetProperty("stage").GetString() == "failed" && stage != "failed")
            {
                Assert.Fail(response.Body);
            }

            await Task.Delay(100, Token);
        }
        throw new InvalidOperationException("Import did not reach " + stage);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record World(ApiClient Client, Guid HouseholdId) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    /// <summary>An instance with the stub connected and drafting switched on.</summary>
    private async Task<World> ConnectedAsync(StubProvider provider, bool reading = false)
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

        var settings = await client.PutAsync(
            "/api/v1/settings/assistance",
            new
            {
                enabled = true,
                connections = new[]
                {
                    new { provider = "openai", apiKey = "sk-stub", baseUrl = provider.Address }
                },
                uses = new[]
                {
                    new { capability = reading ? "read" : "draft", enabled = true, provider = "openai", model = "stub-one" }
                },
                monthlyBudget = (decimal?)null,
                personalBudget = (decimal?)null
            },
            Token);

        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);

        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        return new World(client, householdId);
    }

    /// <summary>A stub model provider. A stub rather than a mocked client so the adapter and SDK are exercised.</summary>
    private sealed class StubProvider : IDisposable
    {
        private readonly HttpListener listener = new();
        private readonly CancellationTokenSource stopping = new();

        /// <summary>What the stub says when it refuses.</summary>
        internal const string Refusal = "Incorrect API key provided";

        private readonly HttpStatusCode? refuseWith;

        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Continue() => release.TrySetResult();

        internal StubProvider(IReadOnlyList<string> chunks, HttpStatusCode? refuseWith = null, bool paused = false)
        {
            this.refuseWith = refuseWith;
            if (!paused)
            {
                release.TrySetResult();
            }

            Address = $"http://localhost:{FreePort()}";

            listener.Prefixes.Add(Address + "/");
            listener.Start();

            _ = Task.Run(() => ServeAsync(chunks));
        }

        /// <summary>Where an administrator would point the connection.</summary>
        internal string Address { get; }

        /// <summary>The body of the last request, so a test can read what was asked.</summary>
        internal string LastRequest { get; private set; } = string.Empty;

        public void Dispose()
        {
            stopping.Cancel();
            listener.Close();
            stopping.Dispose();
        }

        private async Task ServeAsync(IReadOnlyList<string> chunks)
        {
            while (!stopping.IsCancellationRequested)
            {
                HttpListenerContext call;

                try
                {
                    call = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                using (var body = new StreamReader(call.Request.InputStream))
                {
                    LastRequest = await body.ReadToEndAsync().ConfigureAwait(false);
                }

                await AnswerAsync(call, chunks).ConfigureAwait(false);
            }
        }

        private async Task AnswerAsync(HttpListenerContext call, IReadOnlyList<string> chunks)
        {
            if (refuseWith is { } status)
            {
                call.Response.StatusCode = (int)status;
                call.Response.ContentType = "application/json";

                var refusal = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    error = new { message = Refusal, type = "invalid_request_error", code = "invalid_api_key" }
                }));

                await call.Response.OutputStream.WriteAsync(refusal).ConfigureAwait(false);
                call.Response.Close();

                return;
            }

            await release.Task.WaitAsync(stopping.Token).ConfigureAwait(false);
            call.Response.StatusCode = 200;
            call.Response.ContentType = "text/event-stream";
            call.Response.SendChunked = true;

            var body = call.Response.OutputStream;

            await using (body.ConfigureAwait(false))
            {
                foreach (var chunk in chunks)
                {
                    await WriteAsync(body, Delta(chunk)).ConfigureAwait(false);
                }

                await WriteAsync(body, Finished()).ConfigureAwait(false);
                await WriteAsync(body, "[DONE]").ConfigureAwait(false);
            }
        }

        private static async Task WriteAsync(Stream body, string data)
        {
            await body.WriteAsync(Encoding.UTF8.GetBytes($"data: {data}\n\n")).ConfigureAwait(false);
            await body.FlushAsync().ConfigureAwait(false);
        }

        private static string Delta(string text) => JsonSerializer.Serialize(new
        {
            id = "stub",
            @object = "chat.completion.chunk",
            created = 0,
            model = "stub-one",
            choices = new[] { new { index = 0, delta = new { content = text } } }
        });

        private static string Finished() => JsonSerializer.Serialize(new
        {
            id = "stub",
            @object = "chat.completion.chunk",
            created = 0,
            model = "stub-one",
            choices = new[] { new { index = 0, delta = new { }, finish_reason = "stop" } },
            usage = new { prompt_tokens = 11, completion_tokens = 22, total_tokens = 33 }
        });

        /// <summary>A port nobody is on, as far as the operating system knows.</summary>
        private static int FreePort()
        {
            using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);

            probe.Start();

            var port = ((IPEndPoint)probe.LocalEndpoint).Port;

            probe.Stop();

            return port;
        }
    }
}
