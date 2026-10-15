using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Assistance;
using Application.Households;
using Application.Recipes.Drafts;
using Contracts.Recipes.Intake;
using Domain.Assistance;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Intake;

/// <summary>Asks for a composer draft to be written as a job.</summary>
public sealed record StartRecipeDraftCommand(
    Guid Id,
    Guid UserId,
    Guid HouseholdId,
    string Kind,
    string? Material,
    Guid? RecipeId,
    string? Language);

/// <summary>
/// Queues a composer draft (docs/native-app-design.md, part 8): written by the worker whatever the page does, and
/// ending at the draft, never a recipe. Everything that refuses the ask cheaply is decided here, so it stays a status code.
/// </summary>
internal sealed class StartRecipeDraftCommandHandler(
    IRecipeIntakeJobs jobs,
    IHouseholdRepository households,
    IRecipeRepository recipes,
    IUnitOfWork transactions,
    IntakeSubmission submission,
    AssistantRun assistant)
    : ICommandHandler<StartRecipeDraftCommand, IntakeJob>
{
    private static readonly string[] Kinds = [IntakeKinds.Idea, IntakeKinds.Revision];

    public async Task<Result<IntakeJob>> Handle(StartRecipeDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Id == Guid.Empty || !Kinds.Contains(command.Kind))
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        var revision = command.Kind == IntakeKinds.Revision;
        var text = command.Material?.Trim() ?? "";

        if (!revision && text.Length == 0)
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        if (text.Length > DraftLimits.MaxMaterialCharacters)
        {
            return AssistanceErrors.TooMuchToWorkFrom;
        }

        var offered = assistant.Offers(revision ? Capability.Improve : Capability.Draft);
        var access = await offered.Match(
            () => HouseholdAccess.MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
        var language = revision
            ? await access.Match(
                () => RevisableAsync(command, cancellationToken),
                error => Task.FromResult(Result<string>.Failure(error))).ConfigureAwait(false)
            : access.Bind(() => RecipeWords.ToLanguage(command.Language ?? "en")).Map(RecipeWords.Of);

        var started = await language.Match(
            recipeLanguage => transactions.InTransactionAsync(
                token => jobs.EnqueueAsync(
                    command.Id,
                    command.UserId,
                    command.HouseholdId,
                    new IntakeMaterial(text, "", null, recipeLanguage, [], DraftKind: command.Kind, RecipeId: revision ? command.RecipeId : null),
                    token),
                cancellationToken),
            error => Task.FromResult(Result<IntakeJob>.Failure(error))).ConfigureAwait(false);

        return started.Tap(job => submission.Announce(command.UserId, job));
    }

    /// <summary>The language of a recipe this person may rewrite for this household, which a revision keeps.</summary>
    private async Task<Result<string>> RevisableAsync(StartRecipeDraftCommand command, CancellationToken token)
    {
        if (command.RecipeId is not { } id)
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        var editable = await RecipeAccess.EditableAsync(recipes, households, id, command.UserId, token).ConfigureAwait(false);

        // Billed to the household asked for, so the recipe must be its own.
        return editable.Bind(recipe => recipe.HouseholdId == command.HouseholdId
            ? Result<string>.Success(RecipeWords.Of(recipe.Language))
            : RecipeErrors.NotFound(id));
    }
}
