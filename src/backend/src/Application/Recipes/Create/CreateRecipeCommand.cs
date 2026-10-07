using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Recipes;
using Domain.Import;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Create;

/// <summary>Starts a recipe.</summary>
/// <param name="HouseholdId">Which household will own it.</param>
/// <param name="Title">What to call it.</param>
/// <param name="UserId">Who is writing it down.</param>
/// <param name="DeviceLanguage">The writing device's language, for someone whose interface follows it.</param>
/// <param name="DraftId">The assistant draft it came from, when it came from one.</param>
public sealed record CreateRecipeCommand(
    Guid HouseholdId,
    string Title,
    Guid UserId,
    Language DeviceLanguage,
    Guid? DraftId = null)
{
    /// <summary>The original public recipe page, if there is one.</summary>
    public string? SourceUrl { get; init; }
}

internal sealed class CreateRecipeCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IRecipeOriginRepository origins,
    IUserPreferencesRepository preferences,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CreateRecipeCommand, RecipeDetail>
{
    public async Task<Result<RecipeDetail>> Handle(
        CreateRecipeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.Create");

        var permitted = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        // A given link is refused rather than silently dropped.
        var prepared = permitted.Bind(() =>
            command.SourceUrl is not null && SourceUrl.From(command.SourceUrl) is null
                ? Result<RecipeTitle>.Failure(ImportErrors.UnreachableAddress)
                : RecipeTitle.Create(command.Title));

        var result = await prepared.Match(
            title => StoreAsync(command, title, cancellationToken),
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    // Stored in the author's interface language (a guess the editor can correct): the search stemmer and
    // ingredient suggestions follow it.
    private async Task<Result<RecipeDetail>> StoreAsync(
        CreateRecipeCommand command,
        RecipeTitle title,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var theirs = await preferences.GetAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        var recipe = Recipe.Create(
            command.HouseholdId, title, command.UserId, theirs.Language ?? command.DeviceLanguage, now);

        return await unitOfWork.InTransactionAsync(
            async token =>
            {
                var added = await recipes.AddAsync(recipe, token).ConfigureAwait(false);

                return await added.Match(
                    async () =>
                    {
                        await RememberDraftedAsync(command, recipe, now, token).ConfigureAwait(false);

                        return Result<RecipeDetail>.Success(recipe.Describe());
                    },
                    error => Task.FromResult(Result<RecipeDetail>.Failure(error)))
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    // Same table as an imported recipe's origin; the draft id is the external id so each ask is its own.
    private async Task RememberDraftedAsync(
        CreateRecipeCommand command,
        Recipe recipe,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var original = SourceUrl.From(command.SourceUrl);

        if (command.DraftId is null && original is null)
        {
            return;
        }

        await origins.AddAsync(
                new RecipeOrigin(
                    recipe.Id,
                    recipe.HouseholdId,
                    original is null ? SourceKind.Assistant : SourceKind.Web,
                    SourceId: null,
                    (command.DraftId ?? Guid.CreateVersion7()).ToString(),
                    original,
                    now),
                cancellationToken)
            .ConfigureAwait(false);
    }

}
