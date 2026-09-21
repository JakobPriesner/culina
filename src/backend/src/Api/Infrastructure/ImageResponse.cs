using System.Globalization;
using Application.Abstractions;
using Application.Recipes.GetImage;

namespace Api.Infrastructure;

/// <summary>
/// The half of an image endpoint that is the same for every image.
/// </summary>
/// <remarks>
/// <para>
/// Three endpoints serve bytes — a recipe's picture, the same picture behind a
/// share link, and one person's photograph of an attempt — and they differ only
/// in who may ask and which handler finds the file. Reading the width and
/// writing the response are identical, and were copied into all three.
/// </para>
/// <para>
/// They had already drifted: the attempt photograph was still sending
/// <c>max-age=3600</c> and never looked at <c>If-None-Match</c>, so replacing
/// one left the old picture on screen for an hour. That is the bug the other
/// two carry a comment about having fixed, which is what copied code does.
/// </para>
/// </remarks>
internal static class ImageResponse
{
    /// <summary>
    /// Reads the <c>w</c> query parameter, defaulting to the detail width.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="width">The width to serve.</param>
    /// <returns><c>false</c> when <c>w</c> was given but is not a stored width.</returns>
    internal static bool TryReadWidth(HttpContext context, out int width)
    {
        ArgumentNullException.ThrowIfNull(context);

        width = ImageWidths.Detail;

        if (context.Request.Query["w"].Count == 0)
        {
            return true;
        }

        return int.TryParse(context.Request.Query["w"], CultureInfo.InvariantCulture, out width)
            && ImageWidths.Exists(width);
    }

    /// <summary>
    /// Sends the bytes, or says the client already has them.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="buffer">The image the handler wrote.</param>
    /// <param name="delivery">What was served, and its content hash.</param>
    /// <remarks>
    /// <para>
    /// No-cache rather than an hour's freshness. A picture is replaced under the
    /// address it was served from — that is what setting a new one is — so a
    /// browser told it could reuse its copy for an hour showed the old picture
    /// for an hour, with nothing on the screen to suggest the new one had
    /// arrived.
    /// </para>
    /// <para>
    /// It costs a request per view and almost no bytes: the tag is the content
    /// hash, so an unchanged picture answers 304 and is not sent again.
    /// </para>
    /// </remarks>
    internal static IResult Served(HttpContext context, MemoryStream buffer, ImageDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(buffer);

        var tag = $"\"{delivery.ContentHash}\"";

        context.Response.Headers.ETag = tag;
        context.Response.Headers.CacheControl = "private, no-cache";

        return ETag.Matches(context.Request.Headers.IfNoneMatch, tag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Bytes(buffer.ToArray(), "image/webp");
    }
}
