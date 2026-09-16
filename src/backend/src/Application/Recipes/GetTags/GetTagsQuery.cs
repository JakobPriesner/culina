using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes.GetTags;
using Domain.Shared;

namespace Application.Recipes.GetTags;

/// <summary>The tags a household's recipes carry.</summary>
/// <param name="HouseholdId">Whose vocabulary.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetTagsQuery(Guid HouseholdId, Guid UserId);

internal sealed class GetTagsQueryHandler(ITagRepository tags, IHouseholdRepository households)
    : IQueryHandler<GetTagsQuery, Response>
{
    public async Task<Result<Response>> Handle(GetTagsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetTags");

        var allowed = await RecipeAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () =>
            {
                var used = await tags
                    .InUseAsync(query.HouseholdId, cancellationToken)
                    .ConfigureAwait(false);

                return Result<Response>.Success(new Response
                {
                    Items =
                    [
                        .. used.Select(tag => new TagInUse
                        {
                            Slug = tag.Slug,
                            Name = tag.Name,
                            RecipeCount = tag.RecipeCount
                        })
                    ]
                });
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
