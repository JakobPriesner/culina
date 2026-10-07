using System.Globalization;
using Api.Infrastructure;
using Contracts.Cookbooks;

namespace Api.Endpoints.Cookbooks;

/// <summary>The ETag of one cookbook as its page reads it: not the version alone, since the cover and recipe count change without a cookbook write.</summary>
internal static class CookbookTag
{
    internal static IResult Ok(HttpContext context, CookbookDetail cookbook) =>
        ETag.Ok(
            context,
            cookbook,
            cookbook.Version,
            cookbook.CookbookId,
            ETag.Fingerprint(cookbook.CoverPictures
                // The position is part of each picture: the fingerprint ignores order, and swapped tiles are a change.
                .Select((picture, position) => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{position}:{picture.RecipeId:N}:{picture.ImageId:N}"))
                .Append(cookbook.RecipeCount.ToString(CultureInfo.InvariantCulture))));
}
