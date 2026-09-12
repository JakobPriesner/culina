using Api.Infrastructure;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// The shell carries the one inline script Culina has: the few lines that apply
/// the stored theme before the first paint.
/// </summary>
/// <remarks>
/// Under <c>script-src 'self' 'nonce-…'</c> that script runs only if it carries
/// the nonce this response was issued. A substitution that silently does
/// nothing produces a page that looks right in development and flashes white in
/// production, so it is asserted rather than assumed.
/// </remarks>
public sealed class AppShellTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("culina-shell").FullName;

    [Fact]
    public void Render_ShouldFillEveryNonceSlot_WhenTheDocumentHasThem()
    {
        // Arrange
        Write($"<script nonce=\"{AppShell.NoncePlaceholder}\"></script>"
            + $"<script nonce=\"{AppShell.NoncePlaceholder}\"></script>");

        // Act
        var rendered = AppShell.Load(root).Render("abc123");

        // Assert
        Assert.Equal("<script nonce=\"abc123\"></script><script nonce=\"abc123\"></script>", rendered);
    }

    [Fact]
    public void Render_ShouldLeaveNoPlaceholderBehind_WhenRendering()
    {
        // Arrange
        Write($"<html><script nonce=\"{AppShell.NoncePlaceholder}\">x</script></html>");

        // Act
        var rendered = AppShell.Load(root).Render("nonce-value");

        // Assert
        Assert.DoesNotContain(AppShell.NoncePlaceholder, rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ShouldReturnTheDocumentUnchanged_WhenItCarriesNoPlaceholder()
    {
        // Arrange
        Write("<html>no script</html>");

        // Act
        var rendered = AppShell.Load(root).Render("abc123");

        // Assert
        Assert.Equal("<html>no script</html>", rendered);
    }

    [Fact]
    public void Exists_ShouldBeFalse_WhenNoFrontendHasBeenBuiltIn()
    {
        // Arrange & Act
        var shell = AppShell.Load(root);

        // Assert
        Assert.False(shell.Exists);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    private void Write(string html) => File.WriteAllText(Path.Combine(root, "index.html"), html);
}
