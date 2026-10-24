using Application.Abstractions;
using Domain.Diary;
using Domain.Shared;

namespace Infrastructure.Persistence.Users;

/// <summary>The <c>diary_settings</c> row.</summary>
internal sealed record DiarySettingsRow
{
    public Guid UserId { get; init; }

    public bool Enabled { get; init; }

    public DateTimeOffset? ConsentedAt { get; init; }

    public int? ConsentVersion { get; init; }

    public int? KcalTarget { get; init; }

    public int? ProteinTarget { get; init; }

    public string Headline { get; init; } = "eaten";

    public long Version { get; init; }
}

/// <summary>The <c>diary_entries</c> row.</summary>
internal sealed record DiaryEntryRow
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public DateOnly OnDate { get; init; }

    public string Meal { get; init; } = "snack";

    public string Source { get; init; } = "typed";

    public Guid? RecipeId { get; init; }

    public Guid? HouseholdId { get; init; }

    public string? FoodCode { get; init; }

    public Guid? CookLogEntryId { get; init; }

    public decimal? Amount { get; init; }

    public string? Unit { get; init; }

    public decimal? Grams { get; init; }

    public string Label { get; init; } = string.Empty;

    public decimal? Kcal { get; init; }

    public decimal? ProteinG { get; init; }

    public decimal? CarbsG { get; init; }

    public decimal? FatG { get; init; }

    public decimal? SatFatG { get; init; }

    public decimal? SugarsG { get; init; }

    public decimal? SaltG { get; init; }

    public bool AtLeast { get; init; }

    public bool Estimated { get; init; }

    public string? Basis { get; init; }

    public int NutritionVersion { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public long Version { get; init; }
}

/// <summary>Stores one person's food diary. Every statement is scoped to the user in SQL, so "only your own" cannot be forgotten.</summary>
internal sealed class DiaryRepository(DbExecutor executor) : IDiaryRepository
{
    private const string EntryColumns =
        """
        id, user_id, on_date, meal, source, recipe_id, household_id, food_code, cook_log_entry_id,
        amount, unit, grams, label, kcal, protein_g, carbs_g, fat_g, sat_fat_g, sugars_g, salt_g,
        at_least, estimated, basis, nutrition_version, created_at, version
        """;

    public async Task<DiarySettings> SettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<DiarySettingsRow>(
            """
            select user_id, enabled, consented_at, consent_version, kcal_target, protein_target, headline, version
            from diary_settings where user_id = @userId;
            """,
            new { userId },
            cancellationToken).ConfigureAwait(false);

        return row is null ? DiarySettings.Default(userId) : row.ToDomain();
    }

    public async Task SaveSettingsAsync(DiarySettings settings, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Upsert: the row is created the first time someone opens the settings, as user_settings is.
        await executor.ExecuteAsync(
            """
            insert into diary_settings
                (user_id, enabled, consented_at, consent_version, kcal_target, protein_target, headline, version, updated_at)
            values
                (@userId, @enabled, @consentedAt, @consentVersion, @kcalTarget, @proteinTarget, @headline, @version, @now)
            on conflict (user_id) do update
            set enabled = excluded.enabled,
                consented_at = excluded.consented_at,
                consent_version = excluded.consent_version,
                kcal_target = excluded.kcal_target,
                protein_target = excluded.protein_target,
                headline = excluded.headline,
                version = excluded.version,
                updated_at = excluded.updated_at;
            """,
            new
            {
                userId = settings.UserId,
                enabled = settings.Enabled,
                consentedAt = settings.ConsentedAt,
                consentVersion = settings.ConsentVersion,
                kcalTarget = settings.KcalTarget,
                proteinTarget = settings.ProteinTarget,
                headline = settings.Headline == DiaryHeadline.Remaining ? "remaining" : "eaten",
                version = settings.Version,
                now
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DiaryEntry>> EntriesAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<DiaryEntryRow>(
            $"""
             select {EntryColumns}
             from diary_entries
             where user_id = @userId and on_date between @from and @to
             order by on_date, created_at;
             """,
            new { userId, from, to },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => row.ToDomain())];
    }

    public async Task<Result<DiaryEntry>> FindAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<DiaryEntryRow>(
            $"""
             select {EntryColumns}
             from diary_entries
             where id = @entryId and user_id = @userId;
             """,
            new { userId, entryId },
            cancellationToken).ConfigureAwait(false);

        return row is null ? DiaryErrors.EntryNotFound : row.ToDomain();
    }

    public async Task AddAsync(DiaryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await executor.ExecuteAsync(
            $"""
             insert into diary_entries ({EntryColumns})
             values (@id, @userId, @onDate, @meal, @source, @recipeId, @householdId, @foodCode, @cookLogEntryId,
                     @amount, @unit, @grams, @label, @kcal, @proteinG, @carbsG, @fatG, @satFatG, @sugarsG, @saltG,
                     @atLeast, @estimated, @basis, @nutritionVersion, @createdAt, @version);
             """,
            entry.ToRow(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> UpdateAsync(DiaryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var changed = await executor.ExecuteAsync(
            """
            update diary_entries
            set on_date = @onDate, meal = @meal, source = @source, recipe_id = @recipeId, household_id = @householdId,
                food_code = @foodCode, cook_log_entry_id = @cookLogEntryId, amount = @amount, unit = @unit,
                grams = @grams, label = @label, kcal = @kcal, protein_g = @proteinG, carbs_g = @carbsG, fat_g = @fatG,
                sat_fat_g = @satFatG, sugars_g = @sugarsG, salt_g = @saltG, at_least = @atLeast,
                estimated = @estimated, basis = @basis, nutrition_version = @nutritionVersion, version = @version
            where id = @id and user_id = @userId;
            """,
            entry.ToRow(),
            cancellationToken).ConfigureAwait(false);

        return changed == 0 ? DiaryErrors.EntryNotFound : Result.Success();
    }

    public async Task<Result> RemoveAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        var removed = await executor.ExecuteAsync(
            "delete from diary_entries where id = @entryId and user_id = @userId;",
            new { userId, entryId },
            cancellationToken).ConfigureAwait(false);

        return removed == 0 ? DiaryErrors.EntryNotFound : Result.Success();
    }

    public async Task RemoveAllAsync(Guid userId, CancellationToken cancellationToken) =>
        await executor.ExecuteAsync(
            "delete from diary_entries where user_id = @userId;",
            new { userId },
            cancellationToken).ConfigureAwait(false);
}

/// <summary>Turns diary rows into domain values and back.</summary>
internal static class DiaryRowMappings
{
    internal static DiarySettings ToDomain(this DiarySettingsRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return DiarySettings.Restore(
            row.UserId,
            row.Enabled,
            row.ConsentedAt,
            row.ConsentVersion,
            row.KcalTarget,
            row.ProteinTarget,
            row.Headline == "remaining" ? DiaryHeadline.Remaining : DiaryHeadline.Eaten,
            row.Version);
    }

    internal static DiaryEntry ToDomain(this DiaryEntryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return DiaryEntry.Restore(
            row.Id,
            row.UserId,
            row.OnDate,
            DiaryCodes.ToMeal(row.Meal) ?? DiaryMeal.Snack,
            DiaryCodes.ToSource(row.Source),
            new DiaryLink(row.RecipeId, row.HouseholdId, row.FoodCode, row.CookLogEntryId),
            new DiaryAmount(row.Amount ?? 1, row.Unit ?? DiaryAmount.Portion, row.Grams),
            row.Label,
            new DiaryFigures(row.Kcal, row.ProteinG, row.CarbsG, row.FatG, row.SatFatG, row.SugarsG, row.SaltG),
            new DiaryTrust(row.AtLeast, row.Estimated, row.Basis),
            row.NutritionVersion,
            row.CreatedAt,
            row.Version);
    }

    internal static DiaryEntryRow ToRow(this DiaryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new DiaryEntryRow
        {
            Id = entry.Id,
            UserId = entry.UserId,
            OnDate = entry.Date,
            Meal = DiaryCodes.Of(entry.Meal),
            Source = DiaryCodes.Of(entry.Source),
            RecipeId = entry.Link.RecipeId,
            HouseholdId = entry.Link.HouseholdId,
            FoodCode = entry.Link.FoodCode,
            CookLogEntryId = entry.Link.CookLogEntryId,
            Amount = entry.Amount.Value,
            Unit = entry.Amount.Unit,
            Grams = entry.Amount.Grams,
            Label = entry.Label,
            Kcal = entry.Figures.Kcal,
            ProteinG = entry.Figures.Protein,
            CarbsG = entry.Figures.Carbohydrate,
            FatG = entry.Figures.Fat,
            SatFatG = entry.Figures.SaturatedFat,
            SugarsG = entry.Figures.Sugars,
            SaltG = entry.Figures.Salt,
            AtLeast = entry.Trust.AtLeast,
            Estimated = entry.Trust.Estimated,
            Basis = entry.Trust.Basis,
            NutritionVersion = entry.NutritionVersion,
            CreatedAt = entry.CreatedAt,
            Version = entry.Version
        };
    }
}
