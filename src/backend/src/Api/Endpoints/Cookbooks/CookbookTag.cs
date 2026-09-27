using System.Globalization;
using Api.Infrastructure;
using Contracts.Cookbooks;

namespace Api.Endpoints.Cookbooks;

/// <summary>
/// The ETag of one cookbook as its own page reads it.
/// </summary>
/// <remarks>
/// Not the version alone. The body carries how many recipes are on the shelf
/// and the pictures on its cover, and neither is the cookbook's own write: a
/// recipe that gains a picture, or matches a smart shelf's rules, changes the
/// cover and leaves the version where it was. A tag of the version alone
/// answered 304 over a cover that had moved on.
/// </remarks>
internal static class CookbookTag
{
    internal static IResult Ok(HttpContext context, CookbookDetail cookbook) =>
        ETag.Ok(
            context,
            cookbook,
            cookbook.Version,
            cookbook.CookbookId,
            ETag.Fingerprint(cookbook.CoverPictures
                // The position is part of each picture, because the fingerprint
                // ignores order and a cover whose tiles swapped places has changed.
                .Select((picture, position) => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{position}:{picture.RecipeId:N}:{picture.ImageId:N}"))
                .Append(cookbook.RecipeCount.ToString(CultureInfo.InvariantCulture))));
}
