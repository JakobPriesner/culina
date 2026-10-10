using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes.Import;
using Domain.Import;
using Domain.Shared;

namespace Application.Recipes.Import;

/// <summary>Reads a recipe from a web page.</summary>
/// <param name="Url">The address a person pasted.</param>
/// <param name="UserId">Who is asking; signed-in people only.</param>
public sealed record ImportRecipeQuery(string Url, Guid UserId);

internal sealed class ImportRecipeQueryHandler(IWebPageFetcher pages)
    : IQueryHandler<ImportRecipeQuery, Response>
{
    public async Task<Result<Response>> Handle(
        ImportRecipeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.Import");

        if (!Uri.TryCreate(query.Url.Trim(), UriKind.Absolute, out var address))
        {
            return tracked.Record(Result<Response>.Failure(ImportErrors.UnreachableAddress));
        }

        var fetched = await pages.FetchAsync(address, cancellationToken).ConfigureAwait(false);

        return tracked.Record(await fetched.Match(
            page => ToDraftAsync(page, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false));
    }

    /// <summary>
    /// What the page said: structured if it published structure, else its readable text.
    /// </summary>
    /// <remarks>
    /// The text fallback keeps one set of heuristics, in the client where the person corrects them,
    /// rather than two that disagree.
    /// </remarks>
    private async Task<Result<Response>> ToDraftAsync(WebPage page, CancellationToken cancellationToken)
    {
        var published = HtmlText.JsonLdBlocks(page.Html)
            .Select(RecipeJsonLd.Read)
            .FirstOrDefault(recipe => recipe is not null);

        if (published is null)
        {
            var source = SocialRecipeText.Read(page.Html);
            var transcript = source.Transcript;
            if (transcript.Length == 0 && source.CaptionTrack is { } track
                && Uri.TryCreate(page.Url, track, out var captionUrl))
            {
                // Same SSRF, redirect, size and deadline checks as the page; an unavailable track
                // never blocks its caption.
                var fetched = await pages.FetchAsync(captionUrl, cancellationToken).ConfigureAwait(false);
                transcript = fetched.Match(captions => SocialRecipeText.Transcript(captions.Html), _ => string.Empty);
            }

            return new Response
            {
                SourceUrl = page.Url.ToString(),
                IngredientLines = [],
                Steps = [],
                Text = source.Caption.Length > 0 ? source.Caption : HtmlText.ReadableText(page.Html),
                Caption = source.Caption,
                Transcript = transcript
            };
        }

        return new Response
        {
            SourceUrl = page.Url.ToString(),
            Title = published.Title,
            IngredientLines = published.IngredientLines,
            Steps = published.Steps,
            Servings = published.Yield?.Amount,
            YieldKind = published.Yield is { } made ? RecipeWords.Of(made.Kind) : null,
            YieldLabel = published.Yield?.Label,
            TotalMinutes = published.TotalMinutes
        };
    }
}
