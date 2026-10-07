namespace Api.Infrastructure;

/// <summary>
/// The single-page app's HTML document, with a place for the per-response CSP nonce.
/// </summary>
/// <remarks>
/// Its two inline scripts (theme stamp, SvelteKit start-up) run only with this response's nonce, so
/// the document cannot be a static file. Every bare <c>&lt;script&gt;</c> gets a slot, as
/// SvelteKit's is not ours to write; without it the app never starts behind the policy, which only
/// the real image shows. Read once at startup.
/// </remarks>
internal sealed class AppShell
{
    /// <summary>The token <c>app.html</c> writes in place of a nonce.</summary>
    /// <remarks>
    /// Not SvelteKit's <c>%sveltekit.nonce%</c>, which is substituted during the build, before the
    /// value is known.
    /// </remarks>
    internal const string NoncePlaceholder = "__CULINA_NONCE__";

    private readonly string[] segments;

    private AppShell(string[] segments) => this.segments = segments;

    /// <summary>False when no frontend has been built into the image.</summary>
    internal bool Exists => segments.Length > 0;

    internal static AppShell Load(string? webRoot)
    {
        var path = Path.Combine(webRoot ?? string.Empty, "index.html");

        return File.Exists(path)
            ? new AppShell(File.ReadAllText(path)
                .Replace("<script>", $"<script nonce=\"{NoncePlaceholder}\">", StringComparison.Ordinal)
                .Split(NoncePlaceholder))
            : new AppShell([]);
    }

    /// <summary>The document, with every nonce slot filled for this response.</summary>
    internal string Render(string nonce) => string.Join(nonce, segments);
}
