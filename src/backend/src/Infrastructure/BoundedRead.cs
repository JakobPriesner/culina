namespace Infrastructure;

/// <summary>Reads an untrusted stream, at most a fixed number of bytes: the one place that rule lives.</summary>
internal static class BoundedRead
{
    /// <summary>
    /// Copies <paramref name="source"/> to <paramref name="destination"/> and says whether it fitted. A body of exactly
    /// <paramref name="limit"/> bytes fits; the copy stops at the first byte over, so an oversized body is never held in full.
    /// </summary>
    internal static async Task<bool> CopyAsync(Stream source, Stream destination, long limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var chunk = new byte[81_920];
        var total = 0L;

        while (true)
        {
            var read = await source.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                return true;
            }

            total += read;

            if (total > limit)
            {
                return false;
            }

            await destination.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
}
