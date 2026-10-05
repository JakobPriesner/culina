using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace Domain.Import;

/// <summary>The written recipe and available speech captions published by a page.</summary>
public sealed record SocialRecipeSource(string Caption, string Transcript, string? CaptionTrack)
{
    /// <summary>Bounds source material before it reaches a model or the client.</summary>
    public const int MaxCharacters = 20_000;
}

/// <summary>Reads public metadata and caption tracks, without treating scripts as code.</summary>
public static partial class SocialRecipeText
{
    /// <summary>Extracts captions hidden from the ordinary readable HTML fallback.</summary>
    public static SocialRecipeSource Read(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        List<string> captions = [];
        List<string> transcripts = [];
        string? track = null;

        foreach (Match meta in MetaTags().Matches(html))
        {
            var attributes = Attributes(meta.Value);
            var key = attributes.GetValueOrDefault("property") ?? attributes.GetValueOrDefault("name");
            if (key is "og:description" or "twitter:description" or "description"
                && attributes.TryGetValue("content", out var caption))
            {
                captions.Add(caption);
            }
        }

        foreach (Match script in Scripts().Matches(html))
        {
            var body = script.Groups["body"].Value.Trim();
            var player = body.IndexOf("ytInitialPlayerResponse", StringComparison.Ordinal);
            if (player >= 0)
            {
                var start = body.IndexOf('{', player);
                if (start < 0) { continue; }
                body = body[start..];
            }
            else if (!body.StartsWith('{') && !body.StartsWith('['))
            {
                continue;
            }

            try
            {
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(body), new JsonReaderOptions { MaxDepth = 64 });
                using var json = JsonDocument.ParseValue(ref reader);
                Collect(json.RootElement, captions, transcripts, ref track);
            }
            catch (JsonException)
            {
                // Incomplete metadata is optional; the shared caption is still usable.
            }
        }

        foreach (Match tag in Tracks().Matches(html))
        {
            var attributes = Attributes(tag.Value);
            if (attributes.GetValueOrDefault("kind") is "captions" or "subtitles")
            {
                track ??= attributes.GetValueOrDefault("src");
            }
        }

        return new SocialRecipeSource(Joined(captions), Joined(transcripts), track);
    }

    /// <summary>WebVTT, YouTube XML or JSON speech captions, as chronological words.</summary>
    public static string Transcript(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('<'))
        {
            try
            {
                using var source = new StringReader(content);
                using var reader = XmlReader.Create(source, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 2 * 1024 * 1024
                });
                List<string> lines = [];
                while (reader.Read())
                {
                    if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
                    {
                        lines.Add(reader.Value);
                    }
                }
                return Joined(lines);
            }
            catch (XmlException)
            {
                return string.Empty;
            }
        }

        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var json = JsonDocument.Parse(content);
                if (!json.RootElement.TryGetProperty("events", out var events)) { return string.Empty; }
                return Joined(events.EnumerateArray().Where(e => e.TryGetProperty("segs", out _))
                    .Select(e => string.Concat(e.GetProperty("segs").EnumerateArray()
                        .Select(segment => segment.GetProperty("utf8").GetString()))));
            }
            catch (Exception failure) when (failure is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                return string.Empty;
            }
        }

        // Cue ids and timing information are not recipe facts.
        return Joined(content.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.Contains("-->", StringComparison.Ordinal)
                && !line.StartsWith("WEBVTT", StringComparison.Ordinal)
                && !line.StartsWith("NOTE", StringComparison.Ordinal)
                && !line.StartsWith("Kind:", StringComparison.Ordinal)
                && !line.StartsWith("Language:", StringComparison.Ordinal)
                && !int.TryParse(line, out _))
            .Select(line => HtmlText.ReadableText(line).Trim()));
    }

    private static void Collect(JsonElement node, List<string> captions, List<string> transcripts, ref string? track)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray()) { Collect(child, captions, transcripts, ref track); }
        }
        else if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("@type", out var type) && type.ValueKind == JsonValueKind.String
                && type.GetString() == "VideoObject" && node.TryGetProperty("description", out var description)
                && description.ValueKind == JsonValueKind.String)
            {
                captions.Add(description.GetString()!);
            }

            foreach (var property in node.EnumerateObject())
            {
                if (property.Name is "shortDescription" or "desc" && property.Value.ValueKind == JsonValueKind.String)
                { captions.Add(property.Value.GetString()!); }
                if (property.Name == "transcript" && property.Value.ValueKind == JsonValueKind.String)
                { transcripts.Add(property.Value.GetString()!); }
                if (property.Name == "caption" && property.Value.ValueKind == JsonValueKind.Object
                    && property.Value.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                { captions.Add(text.GetString()!); }
                if (property.Name == "captionTracks" && property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var captionTrack in property.Value.EnumerateArray())
                    {
                        if (captionTrack.TryGetProperty("baseUrl", out var url) && url.ValueKind == JsonValueKind.String)
                        { track ??= url.GetString(); }
                    }
                }
                Collect(property.Value, captions, transcripts, ref track);
            }
        }
    }

    private static Dictionary<string, string> Attributes(string tag) => Attribute().Matches(tag)
        .GroupBy(match => match.Groups["name"].Value.ToLowerInvariant())
        .ToDictionary(group => group.Key, group => WebUtility.HtmlDecode(group.First().Groups["value"].Value));

    private static string Joined(IEnumerable<string> values)
    {
        var joined = string.Join('\n', values.Select(WebUtility.HtmlDecode)
            .Select(value => (value ?? string.Empty).Trim()).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal));
        return joined.Length > SocialRecipeSource.MaxCharacters ? joined[..SocialRecipeSource.MaxCharacters] : joined;
    }

    [GeneratedRegex("<meta\\b[^>]*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
    private static partial Regex MetaTags();
    [GeneratedRegex("<track\\b[^>]*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Tracks();
    [GeneratedRegex("""(?<name>[\w:-]+)\s*=\s*(?<quote>["'])(?<value>.*?)\k<quote>""", RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Attribute();
    [GeneratedRegex("""<script\b[^>]*>(?<body>.*?)</script\s*>""", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Scripts();
}
