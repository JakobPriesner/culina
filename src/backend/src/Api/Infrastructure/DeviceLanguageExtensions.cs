using Domain.Shared;
using Domain.Users;

namespace Api.Infrastructure;

/// <summary>Reads which language the calling device reads in, from <c>Accept-Language</c>, for people whose preference is <c>system</c>.</summary>
internal static class DeviceLanguageExtensions
{
    /// <summary>The first language the device asks for that Culina speaks, or English.</summary>
    internal static Language DeviceLanguage(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.GetTypedHeaders().AcceptLanguage
            .Where(one => one.Quality is not 0)
            // Stable, so languages of equal weight keep the order they were sent in.
            .OrderByDescending(one => one.Quality ?? 1)
            .Select(one => PreferenceCodes.ToLanguage(Primary(one.Value.Value)))
            .FirstOrDefault(language => language is not null) ?? Language.En;
    }

    /// <summary><c>de</c> for <c>de-AT</c>: the language, without the region.</summary>
    private static string? Primary(string? tag) =>
        tag?.Split('-')[0].ToLowerInvariant();
}
