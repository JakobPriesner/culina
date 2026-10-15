using Application.Abstractions;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Import;

/// <summary>Finished intake work is not kept for ever: pasted text, transcripts and outbox rows go after a while.</summary>
[Collection(RequiresDatabase.Name)]
public sealed class IntakePurgeTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Purge_ShouldRemoveOldReviewedAndFailedJobs_AndKeepEverythingStillInUse()
    {
        var (userId, householdId) = await SignedInAsync();
        var oldReviewed = await JobAsync(userId, householdId, "reviewed", daysOld: 40);
        var oldFailed = await JobAsync(userId, householdId, "failed", daysOld: 40);
        var recentReviewed = await JobAsync(userId, householdId, "reviewed", daysOld: 2);
        var oldReady = await JobAsync(userId, householdId, "ready", daysOld: 40);
        var oldRunning = await JobAsync(userId, householdId, "reading", daysOld: 40);

        var purged = await PurgeAsync();

        Assert.Equal(2, purged.Jobs);
        var remaining = await postgres.QuerySingleAsync<string>("select string_agg(id::text, ',') from recipe_intake_jobs", Token);
        Assert.DoesNotContain(oldReviewed.ToString(), remaining, StringComparison.Ordinal);
        Assert.DoesNotContain(oldFailed.ToString(), remaining, StringComparison.Ordinal);
        Assert.Contains(recentReviewed.ToString(), remaining, StringComparison.Ordinal);
        Assert.Contains(oldReady.ToString(), remaining, StringComparison.Ordinal);
        Assert.Contains(oldRunning.ToString(), remaining, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Purge_ShouldRemoveSpentNotifications_OfOldJobs_ButNotPendingOnes()
    {
        var (userId, householdId) = await SignedInAsync();
        var job = await JobAsync(userId, householdId, "ready", daysOld: 10);
        await postgres.ExecuteAsync(
            $"""
             insert into web_push_subscriptions (endpoint, user_id, p256dh, auth) values
                 ('https://fcm.googleapis.com/a', '{userId}', 'k', 'a'),
                 ('https://fcm.googleapis.com/b', '{userId}', 'k', 'a'),
                 ('https://fcm.googleapis.com/c', '{userId}', 'k', 'a');
             insert into recipe_intake_notifications (job_id, endpoint, delivered_at, attempts) values
                 ('{job}', 'https://fcm.googleapis.com/a', now(), 1),
                 ('{job}', 'https://fcm.googleapis.com/b', null, 12),
                 ('{job}', 'https://fcm.googleapis.com/c', null, 1);
             """,
            Token);

        var purged = await PurgeAsync();

        Assert.Equal(2, purged.Notifications);
        Assert.Equal(
            "https://fcm.googleapis.com/c",
            await postgres.QuerySingleAsync<string>("select endpoint from recipe_intake_notifications", Token));
    }

    private async Task<IntakePurged> PurgeAsync()
    {
        await using var scope = postgres.Api.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IIntakePurge>().PurgeAsync(DateTimeOffset.UtcNow, Token);
    }

    private async Task<Guid> JobAsync(Guid userId, Guid householdId, string stage, int daysOld)
    {
        var id = Guid.NewGuid();

        await postgres.ExecuteAsync(
            $"""
             insert into recipe_intake_jobs (id, user_id, household_id, material, stage, created_at, updated_at)
             values ('{id}', '{userId}', '{householdId}', jsonb_build_object(), '{stage}', now() - interval '{daysOld} days', now() - interval '{daysOld} days');
             """,
            Token);

        return id;
    }

    private async Task<(Guid UserId, Guid HouseholdId)> SignedInAsync()
    {
        await postgres.ResetAsync(Token);

        using var client = postgres.Api.NewApiClient();
        await client.PostAsync("/api/v1/users", new { email = "ada@example.com", displayName = "Ada", password = Password }, Token);
        await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);
        var me = await client.GetAsync("/api/v1/users/me", Token);

        return (
            me.Json!.Value.GetProperty("userId").GetGuid(),
            me.Json.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid());
    }
}
