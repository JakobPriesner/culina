using System.Text;
using Infrastructure.Import;

namespace IntegrationTests.Import;

/// <summary>A fetched page read in the charset it declares, so umlauts survive an import.</summary>
public class PageEncodingTests
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    [Theory]
    [InlineData("iso-8859-1")]
    [InlineData("ISO-8859-1")]
    [InlineData("\"windows-1252\"")]
    public void Decode_ShouldReadTheCharsetTheHeaderDeclares(string charset)
    {
        var page = Latin1.GetBytes("<html><h1>Käse</h1></html>");

        Assert.Contains("Käse", PageEncoding.Decode(page, charset), StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_ShouldReadLatin1AsWindows1252_AsBrowsersDo()
    {
        // 0x80 is the euro sign in windows-1252 and a control character in strict Latin-1.
        byte[] page = [.. Latin1.GetBytes("Preis: "), 0x80];

        Assert.Equal("Preis: €", PageEncoding.Decode(page, "iso-8859-1"));
    }

    [Theory]
    [InlineData("<meta charset=\"iso-8859-1\">")]
    [InlineData("<META CHARSET=windows-1252>")]
    [InlineData("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=ISO-8859-1\">")]
    public void Decode_ShouldReadAMetaCharset_WhenTheHeaderSaysNothing(string meta)
    {
        var page = Latin1.GetBytes($"<html><head>{meta}</head><body>Käse</body></html>");

        Assert.Contains("Käse", PageEncoding.Decode(page, null), StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_ShouldTrustTheHeader_OverAMetaCharset()
    {
        var page = Encoding.UTF8.GetBytes("<meta charset=\"iso-8859-1\"><p>Käse</p>");

        Assert.Contains("Käse", PageEncoding.Decode(page, "utf-8"), StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_ShouldTrustAByteOrderMark_OverEverythingElse()
    {
        byte[] page = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Käse")];

        Assert.Equal("Käse", PageEncoding.Decode(page, "iso-8859-1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-such-charset")]
    public void Decode_ShouldFallBackToUtf8_WhenNothingUsableIsDeclared(string? charset)
    {
        var page = Encoding.UTF8.GetBytes("<p>Käse</p>");

        Assert.Equal("<p>Käse</p>", PageEncoding.Decode(page, charset));
    }
}
