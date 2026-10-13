using Application.Abstractions;

namespace Infrastructure.Persistence.Suggestions;

/// <summary>Stores what the front page showed a person.</summary>
internal sealed class SuggestionImpressionRepository(DbExecutor executor) : ISuggestionImpressions
{
    /// <summary>How long an impression is kept; the ranker looks back a few days, the rest is only for measuring.</summary>
    private const int RetentionDays = 90;

    public async Task RecordAsync(
        Guid userId,
        IReadOnlyCollection<Guid> recipeIds,
        IReadOnlyCollection<Guid> library,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        if (recipeIds.Count == 0)
        {
            return;
        }

        // Swept here rather than by a service of its own: it only ever grows by what a person is shown, and this is where that happens.
        await executor.ExecuteAsync(
            """
            insert into suggestion_impressions (user_id, recipe_id, shown_on)
            select @userId, r.id, @day
            from recipes r
            where r.id = any(@recipeIds::uuid[]) and r.household_id = any(@library::uuid[])
            on conflict do nothing;

            delete from suggestion_impressions
            where user_id = @userId and shown_on < @day - @retentionDays::int;
            """,
            new { userId, recipeIds = recipeIds.ToArray(), library = library.ToArray(), day, retentionDays = RetentionDays },
            cancellationToken).ConfigureAwait(false);
    }
}
