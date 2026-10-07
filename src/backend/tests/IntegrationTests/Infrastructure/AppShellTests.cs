using Api.Infrastructure;

namespace IntegrationTests.Infrastructure;

/// <summary>Every inline script in the shell must carry the response's nonce, or <c>script-src 'nonce-…'</c> blocks it.</summary>
public sealed class AppShellTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("culina-shell").FullName;

    [Fact]
    public void Render_ShouldFillEveryNonceSlot_WhenTheDocumentHasThem()
    {
        Write($"<script nonce=\"{AppShell.NoncePlaceholder}\"></script>"
            + $"<script nonce=\"{AppShell.NoncePlaceholder}\"></script>");

        var rendered = AppShell.Load(root).Render("abc123");

        Assert.Equal("<script nonce=\"abc123\"></script><script nonce=\"abc123\"></script>", rendered);
    }

    [Fact]
    public void Render_ShouldNonceABareScript_WhenTheFrameworkWroteOne()
    {
        // SvelteKit's boot script, which app.html has no say over.
        Write("<script>__sveltekit = {};</script>");

        var rendered = AppShell.Load(root).Render("abc123");

        Assert.Equal("<script nonce=\"abc123\">__sveltekit = {};</script>", rendered);
    }

    [Fact]
    public void Render_ShouldLeaveNoPlaceholderBehind_WhenRendering()
    {
        Write($"<html><script nonce=\"{AppShell.NoncePlaceholder}\">x</script></html>");

        var rendered = AppShell.Load(root).Render("nonce-value");

        Assert.DoesNotContain(AppShell.NoncePlaceholder, rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ShouldReturnTheDocumentUnchanged_WhenItCarriesNoPlaceholder()
    {
        Write("<html>no script</html>");

        var rendered = AppShell.Load(root).Render("abc123");

        Assert.Equal("<html>no script</html>", rendered);
    }

    [Fact]
    public void Exists_ShouldBeFalse_WhenNoFrontendHasBeenBuiltIn()
    {
        var shell = AppShell.Load(root);

        Assert.False(shell.Exists);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    private void Write(string html) => File.WriteAllText(Path.Combine(root, "index.html"), html);
}
