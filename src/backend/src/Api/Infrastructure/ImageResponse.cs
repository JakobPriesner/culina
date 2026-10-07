using System.Globalization;
using Application.Abstractions;
using Application.Recipes.GetImage;

namespace Api.Infrastructure;

/// <summary>The half of an image endpoint that is the same for every image (recipe picture, shared picture, cook photo).</summary>
internal static class ImageResponse
{
    /// <summary>Reads the <c>w</c> query parameter, defaulting to the detail width; false when it is not a stored width.</summary>
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

    /// <summary>Sends the bytes, or says the client already has them.</summary>
    /// <remarks>
    /// No-cache, because a picture is replaced under the same address and an hour's freshness showed the old one.
    /// The tag is the content hash, so an unchanged picture answers 304 before the file is opened.
    /// </remarks>
    internal static async Task<IResult> ServedAsync(
        HttpContext context,
        ImageDelivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(delivery);

        var tag = $"\"{delivery.ContentHash}\"";

        context.Response.Headers.ETag = tag;
        context.Response.Headers.CacheControl = "private, no-cache";

        if (ETag.Matches(context.Request.Headers.IfNoneMatch, tag))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        // Buffered, so a rendition missing from the store is still a 404 and not a truncated 200.
        var buffer = new MemoryStream();

        await using (buffer.ConfigureAwait(false))
        {
            var written = await delivery.WriteToAsync(buffer, cancellationToken).ConfigureAwait(false);

            return written.Match(
                () => Results.Bytes(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), "image/webp"),
                CustomResults.Problem);
        }
    }
}
