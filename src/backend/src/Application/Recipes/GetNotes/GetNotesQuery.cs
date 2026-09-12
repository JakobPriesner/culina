using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes.GetNotes;
using Domain.Cooking;
using Domain.Shared;

namespace Application.Recipes.GetNotes;

/// <summary>Reads your notes on one recipe.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Whose notes.</param>
public sealed record GetNotesQuery(Guid RecipeId, Guid UserId);

internal sealed class GetNotesQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IPersonalNoteRepository notes)
    : IQueryHandler<GetNotesQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetNotesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetNotes");

        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, query.RecipeId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await visible.Match(
            async _ => Result<Response>.Success(
                (await notes.ForRecipeAsync(query.RecipeId, query.UserId, cancellationToken)
                    .ConfigureAwait(false)).ToResponse()),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

/// <summary>Maps stored notes onto the shape this operation returns.</summary>
internal static class NoteMappings
{
    internal static Response ToResponse(this IReadOnlyList<PersonalNote> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        return new Response
        {
            Overall = notes.FirstOrDefault(note => note.StepId is null)?.Body,
            Steps =
            [
                .. notes.Where(note => note.StepId is not null)
                    .Select(note => new StepNote { StepId = note.StepId!.Value, Body = note.Body })
            ]
        };
    }
}
