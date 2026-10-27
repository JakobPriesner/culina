using Application.Abstractions;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Import;

/// <summary>A job's lease stops two servers working on it, and must neither outlive a deploy nor be renewed for ever.</summary>
[Collection(RequiresDatabase.Name)]
public sealed class IntakeLeaseTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Release_ShouldLetTheNextStartClaimTheJobAtOnce_WithoutCountingTheCutShortAttempt()
    {
        var (userId, householdId) = await SignedInAsync();
        var job = await JobAsync(userId, householdId, "reading", attempts: 0);
        await using var scope = postgres.Api.Services.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IRecipeIntakeJobs>();

        var claimed = await jobs.ClaimAsync(Token);
        await jobs.ReleaseAsync(job, Token);
        var again = await jobs.ClaimAsync(Token);

        Assert.Equal(job, claimed?.Id);
        Assert.Equal(job, again?.Id);
        Assert.Equal(1, await AttemptsAsync(job));
    }

    [Fact]
    public async Task Claim_ShouldGiveUpOnASaveThatKeepsFailing_InsteadOfRetryingItForEver()
    {
        var (userId, householdId) = await SignedInAsync();
        var job = await JobAsync(userId, householdId, "saving", attempts: 5, leaseExpired: true);
        await using var scope = postgres.Api.Services.CreateAsyncScope();

        var claimed = await scope.ServiceProvider.GetRequiredService<IRecipeIntakeJobs>().ClaimAsync(Token);

        Assert.Null(claimed);
        Assert.Equal(
            "failed RecipeIntake.Interrupted",
            await postgres.QuerySingleAsync<string>($"select stage || ' ' || error_code from recipe_intake_jobs where id = '{job}'", Token));
    }

    [Fact]
    public async Task Claim_ShouldStillRetryASave_ThatHasFailedOnlyAFewTimes()
    {
        var (userId, householdId) = await SignedInAsync();
        var job = await JobAsync(userId, householdId, "saving", attempts: 3, leaseExpired: true);
        await using var scope = postgres.Api.Services.CreateAsyncScope();

        var claimed = await scope.ServiceProvider.GetRequiredService<IRecipeIntakeJobs>().ClaimAsync(Token);

        Assert.Equal(job, claimed?.Id);
    }

    private Task<int> AttemptsAsync(Guid job) =>
        postgres.QuerySingleAsync<int>($"select attempts from recipe_intake_jobs where id = '{job}'", Token);

    private async Task<Guid> JobAsync(Guid userId, Guid householdId, string stage, int attempts, bool leaseExpired = false)
    {
        var id = Guid.NewGuid();
        var lease = leaseExpired ? "now() - interval '1 minute'" : "null";

        await postgres.ExecuteAsync(
            $"""
             insert into recipe_intake_jobs (id, user_id, household_id, material, stage, attempts, lease_until)
             values ('{id}', '{userId}', '{householdId}', jsonb_build_object('text', 'Pfannkuchen', 'transcript', '', 'language', 'de', 'photos', '[]'::jsonb), '{stage}', {attempts}, {lease});
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
