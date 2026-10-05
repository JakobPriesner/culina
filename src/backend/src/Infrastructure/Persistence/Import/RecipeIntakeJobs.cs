using System.Text.Json;
using Application.Abstractions;
using Contracts.Recipes.Intake;
using Domain.Shared;
using Draft = Contracts.Recipes.Drafts.Response;

namespace Infrastructure.Persistence.Import;

internal sealed class RecipeIntakeJobs(DbExecutor db) : IRecipeIntakeJobs
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Columns = "id, household_id, stage, created_at, recipe_id, draft::text, jsonb_set(material,'{photos}','[]'::jsonb)::text as material, jsonb_array_length(material->'photos') as photo_count, error_code";

    public async Task<Result<IntakeJob>> EnqueueAsync(Guid id, Guid userId, Guid householdId, IntakeMaterial material, CancellationToken token)
    {
        // An advisory transaction lock makes the queue cap atomic, including
        // submissions from several devices. Existing ids are harmless retries.
        await db.ExecuteAsync("select pg_advisory_xact_lock(hashtextextended(@UserId::text, 0))", new { UserId = userId }, token).ConfigureAwait(false);
        var existing = await GetAsync(id, userId, token).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var count = await db.ExecuteScalarAsync<int>(
            "select count(*) from recipe_intake_jobs where user_id=@UserId and stage not in ('ready','failed','reviewed')",
            new { UserId = userId }, token).ConfigureAwait(false);
        if (count >= 3)
        {
            return new Error("RecipeIntake.QueueFull", "Three imports are already running.", ErrorType.Conflict);
        }

        await db.ExecuteAsync(
            "insert into recipe_intake_jobs(id,user_id,household_id,material) values(@Id,@UserId,@HouseholdId,@Material::jsonb) on conflict(id) do nothing",
            new { Id = id, UserId = userId, HouseholdId = householdId, Material = JsonSerializer.Serialize(material, Json) }, token).ConfigureAwait(false);
        var accepted = await GetAsync(id, userId, token).ConfigureAwait(false);
        return accepted is null ? new Error("RecipeIntake.IdInUse", "This import identity is already in use.", ErrorType.Conflict) : accepted;
    }

    public async Task<IReadOnlyList<IntakeJob>> ListAsync(Guid userId, CancellationToken token)
    {
        var rows = await db.QueryAsync<JobRow>(
            $"select {Columns} from recipe_intake_jobs where user_id=@UserId and stage <> 'reviewed' order by created_at desc limit 30",
            new { UserId = userId }, token).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    public async Task<IntakeJob?> GetAsync(Guid id, Guid userId, CancellationToken token)
    {
        var row = await db.QuerySingleOrDefaultAsync<JobRow>($"select {Columns} from recipe_intake_jobs where id=@Id and user_id=@UserId",
            new { Id = id, UserId = userId }, token).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task<IntakeMaterial?> MaterialAsync(Guid id, Guid userId, CancellationToken token)
    {
        var json = await db.ExecuteScalarAsync<string>("select material::text from recipe_intake_jobs where id=@Id and user_id=@UserId",
            new { Id = id, UserId = userId }, token).ConfigureAwait(false);
        return json is null ? null : JsonSerializer.Deserialize<IntakeMaterial>(json, Json);
    }

    public Task SourceAsync(Guid id, IntakeMaterial material, CancellationToken token) => db.ExecuteAsync(
        "update recipe_intake_jobs set material=@Material::jsonb, updated_at=now() where id=@Id",
        new { Id = id, Material = JsonSerializer.Serialize(material, Json) }, token);

    public Task ReviewAsync(Guid id, Guid userId, CancellationToken token) => db.ExecuteAsync(
        "update recipe_intake_jobs set stage='reviewed', material=jsonb_set(material,'{photos}','[]'::jsonb), updated_at=now() where id=@Id and user_id=@UserId and stage in ('ready','failed')",
        new { Id = id, UserId = userId }, token);

    public async Task<IntakeWork?> ClaimAsync(CancellationToken token)
    {
        await db.ExecuteAsync("update recipe_intake_jobs set stage='failed', error_code='RecipeIntake.Interrupted', lease_until=null where stage not in ('ready','failed','reviewed') and attempts>=2 and stage<>'saving' and lease_until<now()", null, token).ConfigureAwait(false);
        var row = await db.QuerySingleOrDefaultAsync<WorkRow>("""
            update recipe_intake_jobs set lease_until=now()+interval '15 minutes', attempts=attempts+1, updated_at=now()
            where id=(select id from recipe_intake_jobs where stage not in ('ready','failed','reviewed')
                and (lease_until is null or lease_until<now()) and (attempts<2 or stage='saving') order by created_at for update skip locked limit 1)
            returning id,user_id,household_id,material::text,draft::text,stage
            """, null, token).ConfigureAwait(false);
        return row is null ? null : new IntakeWork(row.Id, row.UserId, row.HouseholdId,
            JsonSerializer.Deserialize<IntakeMaterial>(row.Material, Json)!,
            row.Stage == "saving" && row.Draft is not null ? JsonSerializer.Deserialize<Draft>(row.Draft, Json) : null);
    }

    public Task ProgressAsync(Guid id, string stage, Draft? draft, CancellationToken token) => db.ExecuteAsync(
        "update recipe_intake_jobs set stage=@Stage, draft=coalesce(@Draft::jsonb,draft), updated_at=now() where id=@Id",
        new { Id = id, Stage = stage, Draft = draft is null ? null : JsonSerializer.Serialize(draft, Json) }, token);

    public async Task CompleteAsync(Guid id, Guid recipeId, CancellationToken token)
    {
        await db.ExecuteAsync("update recipe_intake_jobs set stage='ready', recipe_id=@RecipeId, lease_until=null, updated_at=now() where id=@Id and stage='saving'",
            new { Id = id, RecipeId = recipeId }, token).ConfigureAwait(false);
        await db.ExecuteAsync("""
            insert into recipe_intake_notifications(job_id,endpoint)
            select j.id,s.endpoint from recipe_intake_jobs j join web_push_subscriptions s on s.user_id=j.user_id
            where j.id=@Id and j.stage='ready' on conflict do nothing
            """, new { Id = id }, token).ConfigureAwait(false);
    }

    public Task FailAsync(Guid id, string errorCode, CancellationToken token) => db.ExecuteAsync(
        "update recipe_intake_jobs set stage='failed',error_code=@ErrorCode,lease_until=null,updated_at=now() where id=@Id and stage not in ('ready','reviewed')",
        new { Id = id, ErrorCode = errorCode }, token);

    private static IntakeJob Map(JobRow row)
    {
        var material = JsonSerializer.Deserialize<IntakeMaterial>(row.Material, Json)!;
        return new IntakeJob
        {
            Id = row.Id,
            HouseholdId = row.HouseholdId,
            Stage = row.Stage,
            CreatedAt = new DateTimeOffset(row.CreatedAt),
            RecipeId = row.RecipeId,
            Draft = row.Draft is null ? null : JsonSerializer.Deserialize<Draft>(row.Draft, Json),
            Material = material.Text,
            Transcript = material.Transcript,
            SourceUrl = material.SourceUrl,
            PhotoCount = row.PhotoCount,
            ErrorCode = row.ErrorCode
        };
    }

    private sealed record JobRow(Guid Id, Guid HouseholdId, string Stage, DateTime CreatedAt, Guid? RecipeId, string? Draft, string Material, int PhotoCount, string? ErrorCode);
    private sealed record WorkRow(Guid Id, Guid UserId, Guid HouseholdId, string Material, string? Draft, string Stage);
}
