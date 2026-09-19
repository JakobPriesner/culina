using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Assistance;
using Application.Telemetry;
using Domain.Assistance;
using Domain.Recipes;
using Domain.Shared;
using Response = Contracts.Recipes.Drafts.Response;

namespace Application.Recipes.Drafts;

/// <summary>Asks the assistant for a recipe.</summary>
/// <param name="Kind">Which of the three: idea, text or revision.</param>
/// <param name="HouseholdId">Whose kitchen it is for.</param>
/// <param name="Material">The idea or the pasted text.</param>
/// <param name="RecipeId">Which recipe to rewrite.</param>
/// <param name="Language">The language to answer in, for the first two.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record ComposeRecipeDraftCommand(
    string Kind,
    Guid HouseholdId,
    string? Material,
    Guid? RecipeId,
    string? Language,
    Guid UserId);

internal sealed class ComposeRecipeDraftCommandHandler(
    AssistantRun assistant,
    IRecipeRepository recipes,
    IHouseholdRepository households)
    : ICommandHandler<ComposeRecipeDraftCommand, Response>
{
    /// <summary>
    /// How much text this will send.
    /// </summary>
    /// <remarks>
    /// Generous enough for a long recipe pasted out of a message, short enough
    /// that a pasted book is refused before it becomes a bill. The limit is
    /// about cost rather than about capability: a model would happily read ten
    /// times this, at ten times the price, on a request anybody with an account
    /// can make.
    /// </remarks>
    private const int LongestMaterial = 20_000;

    public async Task<Result<Response>> Handle(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.ComposeDraft");

        var permitted = await RecipeAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await permitted.Match(
            () => AskAsync(command, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> AskAsync(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(command, cancellationToken).ConfigureAwait(false);

        return await prepared.Match(
            request => ComposeAsync(command, request, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// Works out what to ask for, and what to ask it about.
    /// </summary>
    /// <remarks>
    /// The instruction comes from <see cref="AssistantPrompts"/> and the
    /// material from the request or from a stored recipe — never joined. That
    /// separation is structural rather than a convention, because on a shared
    /// instance the material is whatever somebody else typed.
    /// </remarks>
    private async Task<Result<Composition>> PrepareAsync(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Kind == "revision")
        {
            return await ForRevisionAsync(command, cancellationToken).ConfigureAwait(false);
        }

        if (Material(command) is not { } material)
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        if (material.Length > LongestMaterial)
        {
            return AssistanceErrors.TooMuchToWorkFrom;
        }

        return RecipeWords.ToLanguage(command.Language ?? "en").Map(language =>
        {
            var writing = command.Kind == "idea";

            return new Composition
            {
                Capability = writing ? Capability.Draft : Capability.Read,
                Language = language,
                Instruction = writing
                    ? AssistantPrompts.Draft(language)
                    : AssistantPrompts.Read(language),
                Material = material
            };
        });
    }

    private async Task<Result<Composition>> ForRevisionAsync(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        if (command.RecipeId is not { } recipeId)
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, recipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        return visible.Map(recipe => new Composition
        {
            Capability = Capability.Improve,
            // The recipe's own language, not the caller's: rewriting a German
            // recipe into English is not tidying it up.
            Language = recipe.Language,
            Instruction = AssistantPrompts.Improve(recipe.Language),
            Material = RecipeAsText.Of(recipe)
        });
    }

    private async Task<Result<Response>> ComposeAsync(
        ComposeRecipeDraftCommand command,
        Composition request,
        CancellationToken cancellationToken)
    {
        var answered = await assistant
            .ComposeAsync(
                new Asker(command.UserId, command.HouseholdId),
                request,
                cancellationToken)
            .ConfigureAwait(false);

        return answered.Bind(draft => draft.IsUsable()
            ? Result<Response>.Success(draft.ToResponse())
            : AssistanceErrors.UnusableAnswer);
    }

    private static string? Material(ComposeRecipeDraftCommand command) =>
        string.IsNullOrWhiteSpace(command.Material) ? null : command.Material.Trim();
}
