using Domain.Import;

namespace Domain.UnitTests.Import;

public class HtmlTextTests
{
    [Fact]
    public void JsonLdBlocks_ShouldFindEveryBlock_HoweverTheAttributesAreWritten()
    {
        // Arrange
        const string html = """
            <html><head>
            <script type="application/ld+json">{"a":1}</script>
            <script type='application/ld+json' data-x>{"b":2}</script>
            <script type="text/javascript">var c = 3;</script>
            </head></html>
            """;

        // Act
        var blocks = HtmlText.JsonLdBlocks(html);

        // Assert
        // And only those blocks: the page's own JavaScript is not structured
        // data, however much it looks like an object.
        Assert.Equal(["{\"a\":1}", "{\"b\":2}"], blocks);
    }

    [Fact]
    public void ReadableText_ShouldLeaveOutWhatNobodyReads()
    {
        // Arrange
        const string html = """
            <html><head><style>.a { color: red }</style>
            <script>var hidden = "Not a step";</script></head>
            <body><h1>Pancakes</h1><p>125 g flour</p><p>Whisk it.</p></body></html>
            """;

        // Act
        var text = HtmlText.ReadableText(html);

        // Assert
        Assert.DoesNotContain("Not a step", text, StringComparison.Ordinal);
        Assert.DoesNotContain("color: red", text, StringComparison.Ordinal);
        Assert.Contains("Pancakes", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadableText_ShouldKeepBlocksOnSeparateLines()
    {
        // Arrange
        const string html = "<ul><li>200 g flour</li><li>2 eggs</li></ul>";

        // Act
        var lines = HtmlText.ReadableText(html).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        // The line is what the paste parser reads. A page flattened onto one
        // line is one very long ingredient.
        Assert.Equal(["200 g flour", "2 eggs"], lines);
    }

    [Fact]
    public void ReadableText_ShouldDecodeWhatAPageEscaped()
    {
        // Arrange & Act
        var text = HtmlText.ReadableText("<p>Cr&egrave;me fra&icirc;che &amp; sugar</p>");

        // Assert
        Assert.Contains("Crème fraîche & sugar", text, StringComparison.Ordinal);
    }
}
