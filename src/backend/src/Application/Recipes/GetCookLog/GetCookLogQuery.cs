using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes.GetCookLog;
using Domain.Cooking;
using Domain.Shared;

namespace Application.Recipes.GetCookLog;

/// <summary>Reads how often you have cooked one recipe.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Whose history.</param>
public sealed record GetCookLogQuery(Guid RecipeId, Guid UserId);

internal sealed class GetCookLogQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    ICookLogRepository log)
    : IQueryHandler<GetCookLogQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetCookLogQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetCookLog");

        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, query.RecipeId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await visible.Match(
            async _ => Result<Response>.Success(
                (await log.ForRecipeAsync(query.RecipeId, query.UserId, cancellationToken)
                    .ConfigureAwait(false)).ToResponse()),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

/// <summary>Maps cook-log entries onto the shape this operation returns.</summary>
internal static class CookLogMappings
{
    internal static Response ToResponse(this IReadOnlyList<CookLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new Response
        {
            Count = entries.Count,
            LastMadeAt = entries.Count == 0 ? null : entries[0].MadeAt,
            Items =
            [
                .. entries.Select(entry => new CookLogItem
                {
                    EntryId = entry.Id,
                    MadeAt = entry.MadeAt,
                    Servings = entry.Servings,
                    Note = entry.Note,
                    HasPhoto = entry.Photo is not null
                })
            ]
        };
    }
}
