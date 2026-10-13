using System.Net;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions;

/// <summary>
/// The front page remembers what it showed, so "the same five all week" does not happen: a recipe
/// shown on an earlier day steps aside, and showing a list never changes that list.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class SuggestionImpressionTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Impressions_ShouldRememberWhatWasShown_ByDay()
    {
        var world = await SuggestionWorld.NewAsync(postgres, api: postgres.SteadyRanking);

        await world.WriteAsync("Soup");
        await world.WriteAsync("Stew");
        await world.WriteAsync("Salad");

        var shown = SuggestionWorld.Ids(await world.SuggestAsync("&limit=2"));

        var response = await world.ShownAsync(shown);
        await world.ShownAsync(shown);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // Twice is once: a refresh or a second device writes nothing new.
        Assert.Equal(shown.Order(), (await ShownTodayAsync()).Order());
    }

    [Fact]
    public async Task Suggestions_ShouldStayARead_WhateverItReturns()
    {
        var world = await SuggestionWorld.NewAsync(postgres, api: postgres.SteadyRanking);

        await world.WriteAsync("Soup");
        await world.WriteAsync("Stew");
        await world.SuggestAsync("&limit=2");

        // Only the client, once the set has painted, says it was shown.
        Assert.Empty(await ShownTodayAsync());
    }

    [Fact]
    public async Task Suggestions_ShouldKeepTheListSteadyAllDay_WhateverWasShownEarlierToday()
    {
        var world = await SuggestionWorld.NewAsync(postgres, api: postgres.SteadyRanking);

        foreach (var title in new[] { "Soup", "Stew", "Salad", "Pie" })
        {
            await world.WriteAsync(title);
        }

        var morning = await world.SuggestAsync("&limit=2");

        await world.ShownAsync(SuggestionWorld.Ids(morning));

        // Today's own impressions never count, or showing a list would reshuffle it.
        Assert.Equal(SuggestionWorld.Titles(morning), SuggestionWorld.Titles(await world.SuggestAsync("&limit=2")));
    }

    [Fact]
    public async Task Suggestions_ShouldStepAside_ForARecipeShownOnAnEarlierDay()
    {
        var world = await SuggestionWorld.NewAsync(postgres, api: postgres.SteadyRanking);

        await world.WriteAsync("Soup");
        await world.WriteAsync("Stew");
        await world.WriteAsync("Salad");

        var leader = SuggestionWorld.Ids(await world.SuggestAsync("&limit=1")).Single();

        await ShownAsync(leader, daysAgo: 1);

        Assert.NotEqual(leader, SuggestionWorld.Ids(await world.SuggestAsync("&limit=1")).Single());
    }

    [Fact]
    public async Task Suggestions_ShouldLetItReturn_OnceTheDaysHavePassed()
    {
        var world = await SuggestionWorld.NewAsync(postgres, api: postgres.SteadyRanking);

        await world.WriteAsync("Soup");
        await world.WriteAsync("Stew");
        await world.WriteAsync("Salad");

        var leader = SuggestionWorld.Ids(await world.SuggestAsync("&limit=1")).Single();

        await ShownAsync(leader, daysAgo: 60);

        // Sunk the day after, forgotten after two months: a score, never a ban.
        Assert.Equal(leader, SuggestionWorld.Ids(await world.SuggestAsync("&limit=1")).Single());
    }

    [Fact]
    public async Task Suggestions_ShouldNotLeanAway_FromWhatWasShownAsMoreLikeThis()
    {
        var world = await SuggestionWorld.NewAsync(postgres, api: postgres.SteadyRanking);

        var soup = await world.WriteAsync("Soup");

        await world.WriteAsync("Stew");
        await world.WriteAsync("Salad");

        var before = SuggestionWorld.Titles(await world.SuggestAsync($"&likeRecipeId={soup}"));

        await ShownAsync(SuggestionWorld.Ids(await world.SuggestAsync("&limit=3")), daysAgo: 1);

        // Only the shortlist can go stale; a question about one recipe has no "same five".
        Assert.Equal(before, SuggestionWorld.Titles(await world.SuggestAsync($"&likeRecipeId={soup}")));
    }

    [Fact]
    public async Task Impressions_ShouldIgnoreARecipeOutsideTheLibrary()
    {
        var world = await SuggestionWorld.NewAsync(postgres, api: postgres.SteadyRanking);

        await world.WriteAsync("Soup");

        var response = await world.ShownAsync([Guid.NewGuid()]);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await ShownTodayAsync());
    }

    [Fact]
    public async Task Impressions_ShouldRefuseMoreThanASetCanHold()
    {
        var world = await SuggestionWorld.NewAsync(postgres, api: postgres.SteadyRanking);

        var response = await world.ShownAsync(Enumerable.Range(0, 13).Select(_ => Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Impressions_ShouldBeNotFound_ForAHouseholdThatIsNotYours()
    {
        var world = await SuggestionWorld.NewAsync(postgres, api: postgres.SteadyRanking);

        var response = await world.Client.PostAsync(
            "/api/v1/suggestions/impressions",
            new { householdId = Guid.NewGuid(), recipeIds = Array.Empty<Guid>() },
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<List<Guid>> ShownTodayAsync()
    {
        await using var session = postgres.NewSession();

        var rows = await new DbExecutor(session).QueryAsync<Guid>(
            "select recipe_id from suggestion_impressions where shown_on = (now() at time zone 'UTC')::date order by recipe_id;",
            null,
            Token);

        return [.. rows];
    }

    private async Task ShownAsync(IEnumerable<Guid> recipeIds, int daysAgo)
    {
        foreach (var recipeId in recipeIds)
        {
            await ShownAsync(recipeId, daysAgo);
        }
    }

    private async Task ShownAsync(Guid recipeId, int daysAgo)
    {
        await using var session = postgres.NewSession();

        await new DbExecutor(session).ExecuteAsync(
            """
            insert into suggestion_impressions (user_id, recipe_id, shown_on)
            select u.id, @recipeId, (now() at time zone 'UTC')::date - @daysAgo::int from users u
            on conflict do nothing;
            """,
            new { recipeId, daysAgo },
            Token);
    }
}
