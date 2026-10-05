using Domain.Import;

namespace Domain.UnitTests.Import;

public class SocialRecipeTextTests
{
    [Fact]
    public void Read_ShouldKeepCaptionAndSpeechApart_AndDecodeMetadata()
    {
        var source = SocialRecipeText.Read("""
            <meta content="Chef's soup: 120g beans &amp; salt" property="og:description">
            <script type="application/ld+json">{"@type":"VideoObject","transcript":"add a cup of beans"}</script>
            """);

        Assert.Equal("Chef's soup: 120g beans & salt", source.Caption);
        Assert.Equal("add a cup of beans", source.Transcript);
    }

    [Fact]
    public void Read_ShouldFindThePublicYoutubeSpeechTrack_WithoutExecutingTheScript()
    {
        var source = SocialRecipeText.Read("""
            <script>var ytInitialPlayerResponse = {"videoDetails":{"shortDescription":"200 g flour"},"captions":{"playerCaptionsTracklistRenderer":{"captionTracks":[{"baseUrl":"https://www.youtube.com/api/timedtext?v=abc&lang=en"}]}}}; malicious();</script>
            """);

        Assert.Equal("200 g flour", source.Caption);
        Assert.Equal("https://www.youtube.com/api/timedtext?v=abc&lang=en", source.CaptionTrack);
    }

    [Fact]
    public void Read_ShouldResolveRelativeVttTracks_AndIgnoreMalformedScriptData()
    {
        var source = SocialRecipeText.Read("""
            <script>{broken json</script><track kind='captions' src='/captions/en.vtt'>
            <script type="application/json">{"itemStruct":{"desc":"Fry the onion."}}</script>
            """);

        Assert.Equal("/captions/en.vtt", source.CaptionTrack);
        Assert.Equal("Fry the onion.", source.Caption);
    }

    [Theory]
    [InlineData("WEBVTT\n\n1\n00:00:01.000 --> 00:00:04.000\n<c>Mix &amp; knead.</c>", "Mix & knead.")]
    [InlineData("<transcript><text start='0'>Mix &amp; knead.</text></transcript>", "Mix & knead.")]
    [InlineData("{\"events\":[{\"segs\":[{\"utf8\":\"Mix \"},{\"utf8\":\"& knead.\"}]}]}", "Mix & knead.")]
    public void Transcript_ShouldKeepTheWords_WithoutCueMarkup(string source, string expected)
    {
        Assert.Equal(expected, SocialRecipeText.Transcript(source));
    }

    [Fact]
    public void Transcript_ShouldRefuseAnXmlExternalEntity()
    {
        Assert.Empty(SocialRecipeText.Transcript("""
            <!DOCTYPE transcript [<!ENTITY secret SYSTEM "file:///etc/passwd">]>
            <transcript><text>&secret;</text></transcript>
            """));
    }

    [Fact]
    public void Read_ShouldBoundOversizedCaptions_BeforeReturningThem()
    {
        var source = SocialRecipeText.Read($"<meta name='description' content='{new string('a', 30_000)}'>");
        Assert.Equal(SocialRecipeSource.MaxCharacters, source.Caption.Length);
    }
}
