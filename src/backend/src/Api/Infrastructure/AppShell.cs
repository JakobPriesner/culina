namespace Api.Infrastructure;

/// <summary>
/// The single-page app's HTML document, with a place for the per-response CSP
/// nonce.
/// </summary>
/// <remarks>
/// <para>
/// The shell carries two inline scripts: the few lines that stamp the stored
/// theme on the document before the first paint, and the one SvelteKit writes
/// to start the app. Under <c>script-src 'self' 'nonce-…'</c> each only runs if
/// it carries the nonce this response was issued, so the document cannot be a
/// static file.
/// </para>
/// <para>
/// The theme script is given its slot in <c>app.html</c>. SvelteKit's is not
/// ours to write — it arrives as a bare <c>&lt;script&gt;</c> — so every bare
/// one is given a slot here. Without that the app never starts behind the
/// policy, and nothing short of the real image shows it: the dev server and
/// <c>vite preview</c> set no policy at all.
/// </para>
/// <para>
/// Read once at startup rather than per request. The container ships a built
/// frontend and never changes it, and in development the Vite server serves the
/// app instead — so a rebuild is a restart either way.
/// </para>
/// </remarks>
internal sealed class AppShell
{
    /// <summary>
    /// The token <c>app.html</c> writes in place of a nonce.
    /// </summary>
    /// <remarks>
    /// Deliberately not SvelteKit's own <c>%sveltekit.nonce%</c>: that one is
    /// substituted during the build, which is exactly when the value is not yet
    /// known.
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
