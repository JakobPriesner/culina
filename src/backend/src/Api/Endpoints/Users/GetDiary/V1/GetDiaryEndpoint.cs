using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.GetDiary;
using Response = Contracts.Users.GetDiary.Response;

namespace Api.Endpoints.Users.GetDiary.V1;

/// <summary>Reads days of your food diary.</summary>
internal sealed class GetDiaryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/users/me/diary", async (
                DateOnly from,
                DateOnly? to,
                HttpContext context,
                IQueryHandler<GetDiaryQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetDiaryQuery(context.CurrentUser().UserId, from, to), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithQueryParameters("from", "to")
            .WithName("getDiaryV1")
            .WithTags(Tags.Users)
            .WithSummary("Read days of your diary")
            .WithDescription(
                "Every day from `from` to `to` (inclusive, at most 62 days; `to` defaults to `from`), logged or not, "
                + "with each day's entries and totals. 403 diary.disabled while the diary is switched off.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();
    }
}
