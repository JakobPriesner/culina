namespace Contracts.LogRecords.Create;

/// <summary>
/// The browser and device the records came from, as far as the page can tell.
/// </summary>
/// <remarks>
/// Every field is optional, because no browser offers all of them, and the
/// server cuts any text to 128 characters and any list to ten entries rather
/// than refusing the report it came with.
/// </remarks>
public sealed record Client
{
    /// <summary>Random per page load, so records from one page can be told apart from another's.</summary>
    public string? SessionId { get; init; }

    /// <summary>The language the app is showing, such as <c>de</c>.</summary>
    public string? Locale { get; init; }

    /// <summary>The languages the browser asks for, most preferred first.</summary>
    public IReadOnlyList<string>? Languages { get; init; }

    /// <summary>The device's time zone, such as <c>Europe/Berlin</c>.</summary>
    public string? TimeZone { get; init; }

    /// <summary>The browser's brands and their major versions, such as <c>Chromium 140</c>.</summary>
    public IReadOnlyList<string>? Brands { get; init; }

    /// <summary>The operating system, such as <c>Android</c> or <c>macOS</c>.</summary>
    public string? Platform { get; init; }

    /// <summary>The operating system's version.</summary>
    public string? PlatformVersion { get; init; }

    /// <summary>The processor architecture, such as <c>arm</c> or <c>x86</c>.</summary>
    public string? Architecture { get; init; }

    /// <summary>The device model, where the browser names one.</summary>
    public string? Model { get; init; }

    /// <summary>Whether the browser says it is on a phone.</summary>
    public bool? Mobile { get; init; }

    /// <summary>The screen's width in CSS pixels.</summary>
    public int? ScreenWidth { get; init; }

    /// <summary>The screen's height in CSS pixels.</summary>
    public int? ScreenHeight { get; init; }

    /// <summary>The page's width in CSS pixels.</summary>
    public int? ViewportWidth { get; init; }

    /// <summary>The page's height in CSS pixels.</summary>
    public int? ViewportHeight { get; init; }

    /// <summary>Device pixels per CSS pixel.</summary>
    public decimal? PixelRatio { get; init; }

    /// <summary>The screen's orientation, such as <c>portrait-primary</c>.</summary>
    public string? Orientation { get; init; }

    /// <summary><c>light</c> or <c>dark</c>, as the device prefers.</summary>
    public string? ColorScheme { get; init; }

    /// <summary>Whether the device asks for less motion.</summary>
    public bool? ReducedMotion { get; init; }

    /// <summary>How the app is open: <c>standalone</c> when installed, otherwise <c>browser</c>.</summary>
    public string? DisplayMode { get; init; }

    /// <summary>Logical processors the browser reports.</summary>
    public int? Cores { get; init; }

    /// <summary>The device's memory in gibibytes, as the browser rounds it.</summary>
    public decimal? DeviceMemory { get; init; }

    /// <summary>How many touch points the screen takes at once; 0 without a touch screen.</summary>
    public int? TouchPoints { get; init; }

    /// <summary>The connection's effective type, such as <c>4g</c>.</summary>
    public string? Connection { get; init; }

    /// <summary>The connection's estimated bandwidth, in megabits per second.</summary>
    public decimal? Downlink { get; init; }

    /// <summary>The connection's estimated round trip, in milliseconds.</summary>
    public int? RoundTrip { get; init; }

    /// <summary>Whether the user asked to save data.</summary>
    public bool? SaveData { get; init; }

    /// <summary>How the page was reached: <c>navigate</c>, <c>reload</c>, <c>back_forward</c> or <c>prerender</c>.</summary>
    public string? NavigationType { get; init; }

    /// <summary>Whether a service worker controls the page.</summary>
    public bool? ServiceWorker { get; init; }
}
