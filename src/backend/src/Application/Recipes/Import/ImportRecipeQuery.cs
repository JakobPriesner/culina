using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes.Import;
using Domain.Import;
using Domain.Shared;

namespace Application.Recipes.Import;

/// <summary>Reads a recipe from a web page.</summary>
/// <param name="Url">The address a person pasted.</param>
/// <param name="UserId">Who is asking. Only signed-in people may.</param>
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
    /// What the page said, structured if it published structure and as words
    /// if it did not.
    /// </summary>
    /// <remarks>
    /// The fallback is the page's readable text rather than a second parser
    /// here. The client already reads a pasted recipe with heuristics, and one
    /// set of heuristics — on the side where the person correcting them is —
    /// beats two that quietly disagree.
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
                // The same SSRF, redirect, size and deadline checks as the page.
                // A private or unavailable track never prevents reading its caption.
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
            Servings = published.Servings,
            TotalMinutes = published.TotalMinutes
        };
    }
}
