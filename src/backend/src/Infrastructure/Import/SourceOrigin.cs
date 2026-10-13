using Domain.Import;

namespace Infrastructure.Import;

/// <summary>Keeps an address that came from the other server on that server.</summary>
/// <remarks>
/// A picture or page address in a response is data from somebody else's server; followed unchecked, with the
/// household's token attached, it would let that server choose what this one fetches.
/// </remarks>
internal static class SourceOrigin
{
    /// <summary>The address, resolved against the connection, only if it stays on the connection's own origin.</summary>
    internal static Uri? Resolve(SourceAddress address, string? written)
    {
        ArgumentNullException.ThrowIfNull(address);

        var trimmed = written?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (!Uri.TryCreate(address.Origin, trimmed, out var url))
        {
            return null;
        }

        return url.GetLeftPart(UriPartial.Authority) == address.Value ? url : null;
    }
}
