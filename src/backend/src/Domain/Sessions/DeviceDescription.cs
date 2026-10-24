using System.Text.RegularExpressions;

namespace Domain.Sessions;

/// <summary>
/// "Firefox · macOS": enough to tell two devices apart in a mail without the whole user-agent string; the same rules
/// as the devices screen (frontend <c>describeUserAgent</c>).
/// </summary>
public static class DeviceDescription
{
    // Order matters: Edge and Opera say Chrome too, and Chrome says Safari.
    private static readonly (Regex Pattern, string Name)[] Browsers =
    [
        (new Regex(@"Edg/", RegexOptions.CultureInvariant), "Edge"),
        (new Regex(@"OPR/", RegexOptions.CultureInvariant), "Opera"),
        (new Regex(@"Firefox/|FxiOS/", RegexOptions.CultureInvariant), "Firefox"),
        (new Regex(@"Chrome/|CriOS/", RegexOptions.CultureInvariant), "Chrome"),
        (new Regex(@"Safari/", RegexOptions.CultureInvariant), "Safari")
    ];

    // iPhone and iPad first: an iPad says "Mac OS X".
    private static readonly (Regex Pattern, string Name)[] Systems =
    [
        (new Regex(@"iPhone|iPad|iPod", RegexOptions.CultureInvariant), "iOS"),
        (new Regex(@"Android", RegexOptions.CultureInvariant), "Android"),
        (new Regex(@"Mac OS X|Macintosh", RegexOptions.CultureInvariant), "macOS"),
        (new Regex(@"Windows", RegexOptions.CultureInvariant), "Windows"),
        (new Regex(@"CrOS", RegexOptions.CultureInvariant), "ChromeOS"),
        (new Regex(@"Linux", RegexOptions.CultureInvariant), "Linux")
    ];

    /// <summary>What to call the device a session was started from.</summary>
    /// <param name="userAgent">The browser's user-agent string, if it sent one.</param>
    /// <param name="client">What started the session.</param>
    public static string Of(string? userAgent, SessionClient client) => client switch
    {
        SessionClient.Ios => "Culina · iOS",
        SessionClient.Android => "Culina · Android",
        SessionClient.Instance => "Culina server",
        SessionClient.Agent => "AI agent",
        _ => OfBrowser(userAgent)
    };

    private static string OfBrowser(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return "Browser";
        }

        var browser = Browsers.FirstOrDefault(one => one.Pattern.IsMatch(userAgent)).Name;
        var system = Systems.FirstOrDefault(one => one.Pattern.IsMatch(userAgent)).Name;

        return (browser, system) switch
        {
            ({ } b, { } s) => $"{b} · {s}",
            ({ } b, null) => b,
            (null, { } s) => s,
            _ => "Browser"
        };
    }
}
