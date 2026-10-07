using System.Text.Json;
using Application.Abstractions;
using Contracts.Recipes.Intake;
using Domain.Shared;
using Draft = Contracts.Recipes.Drafts.Response;

namespace Infrastructure.Persistence.Import;

internal sealed class RecipeIntakeJobs(DbExecutor db) : IRecipeIntakeJobs
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Columns = "id, household_id, stage, created_at, recipe_id, draft::text, material::text as material, (select count(*) from recipe_intake_photos p where p.job_id=recipe_intake_jobs.id)::int as photo_count, error_code";

    /// <summary>The material as stored; photographs live in their own table.</summary>
    private static string Stored(IntakeMaterial material) => JsonSerializer.Serialize(material with { Photos = [] }, Json);

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
            new { Id = id, UserId = userId, HouseholdId = householdId, Material = Stored(material) }, token).ConfigureAwait(false);

        for (var position = 0; position < material.Photos.Count; position++)
        {
            await db.ExecuteAsync(
                "insert into recipe_intake_photos(job_id,position,media_type,bytes) values(@Id,@Position,@MediaType,@Bytes) on conflict do nothing",
                new { Id = id, Position = position, material.Photos[position].MediaType, Bytes = material.Photos[position].Bytes.ToArray() }, token).ConfigureAwait(false);
        }

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
        return json is null ? null : (JsonSerializer.Deserialize<IntakeMaterial>(json, Json)! with { Photos = await PhotosAsync(id, token).ConfigureAwait(false) });
    }

    public async Task<IntakePhoto?> PhotoAsync(Guid id, Guid userId, int index, CancellationToken token)
    {
        var row = await db.QuerySingleOrDefaultAsync<PhotoRow>(
            "select p.media_type, p.bytes from recipe_intake_photos p join recipe_intake_jobs j on j.id=p.job_id where p.job_id=@Id and j.user_id=@UserId and p.position=@Index",
            new { Id = id, UserId = userId, Index = index }, token).ConfigureAwait(false);
        return row is null ? null : new IntakePhoto(row.Bytes, row.MediaType);
    }

    private async Task<IReadOnlyList<IntakePhoto>> PhotosAsync(Guid id, CancellationToken token)
    {
        var rows = await db.QueryAsync<PhotoRow>(
            "select media_type, bytes from recipe_intake_photos where job_id=@Id order by position",
            new { Id = id }, token).ConfigureAwait(false);
        return [.. rows.Select(row => new IntakePhoto(row.Bytes, row.MediaType))];
    }

    public Task SourceAsync(Guid id, IntakeMaterial material, CancellationToken token) => db.ExecuteAsync(
        "update recipe_intake_jobs set material=@Material::jsonb, updated_at=now() where id=@Id",
        new { Id = id, Material = Stored(material) }, token);

    public Task ReviewAsync(Guid id, Guid userId, CancellationToken token) => db.ExecuteAsync(
        """
        with reviewed as (
            update recipe_intake_jobs set stage='reviewed', updated_at=now()
            where id=@Id and user_id=@UserId and stage in ('ready','failed') returning id)
        delete from recipe_intake_photos where job_id in (select id from reviewed)
        """,
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
            JsonSerializer.Deserialize<IntakeMaterial>(row.Material, Json)! with { Photos = await PhotosAsync(row.Id, token).ConfigureAwait(false) },
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
            // Shown as a link on the intake page, so only an address one may be.
            SourceUrl = Domain.Import.SourceUrl.From(material.SourceUrl)?.Value,
            PhotoCount = row.PhotoCount,
            ErrorCode = row.ErrorCode
        };
    }

    private sealed record JobRow(Guid Id, Guid HouseholdId, string Stage, DateTime CreatedAt, Guid? RecipeId, string? Draft, string Material, int PhotoCount, string? ErrorCode);
    private sealed record PhotoRow(string MediaType, byte[] Bytes);
    private sealed record WorkRow(Guid Id, Guid UserId, Guid HouseholdId, string Material, string? Draft, string Stage);
}
