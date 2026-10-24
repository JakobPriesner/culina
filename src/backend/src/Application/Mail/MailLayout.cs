using System.Net;
using System.Text;
using Application.Abstractions;

namespace Application.Mail;

/// <summary>What one mail says, before it is laid out.</summary>
/// <param name="Subject">The subject line.</param>
/// <param name="Greeting">The first line, or null for none.</param>
/// <param name="Paragraphs">The message, plain text.</param>
/// <param name="Action">The one thing to press, or null for none.</param>
/// <param name="Link">Where the action goes.</param>
/// <param name="Footer">Why the recipient got this, and what to do if it was not them.</param>
internal sealed record MailWords(
    string Subject,
    string? Greeting,
    IReadOnlyList<string> Paragraphs,
    string? Action,
    Uri? Link,
    string Footer);

/// <summary>Lays a mail out twice: as plain text, and as simple HTML with one button.</summary>
/// <remarks>Every value is encoded: names and household names are typed by people.</remarks>
internal static class MailLayout
{
    internal static OutgoingMail Render(string to, MailWords words)
    {
        ArgumentNullException.ThrowIfNull(words);

        return new OutgoingMail(to, words.Subject, Text(words), Html(words));
    }

    private static string Text(MailWords words)
    {
        var text = new StringBuilder();

        if (words.Greeting is { } greeting)
        {
            text.Append(greeting).Append("\n\n");
        }

        foreach (var paragraph in words.Paragraphs)
        {
            text.Append(paragraph).Append("\n\n");
        }

        if (words is { Action: { } action, Link: { } link })
        {
            text.Append(action).Append(":\n").Append(link.AbsoluteUri).Append("\n\n");
        }

        return text.Append("-- \n").Append(words.Footer).Append('\n').ToString();
    }

    private static string Html(MailWords words)
    {
        var html = new StringBuilder();

        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>")
            .Append(Encode(words.Subject))
            .Append("</title></head><body style=\"margin:0;padding:24px;background:#f6f3ee;")
            .Append("font-family:-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#2b2620;\">")
            .Append("<div style=\"max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;padding:28px;\">");

        if (words.Greeting is { } greeting)
        {
            html.Append(Paragraph(greeting));
        }

        foreach (var paragraph in words.Paragraphs)
        {
            html.Append(Paragraph(paragraph));
        }

        if (words is { Action: { } action, Link: { } link })
        {
            var href = Encode(link.AbsoluteUri);

            html.Append("<p style=\"margin:24px 0;\"><a href=\"").Append(href)
                .Append("\" style=\"display:inline-block;background:#3d6b4f;color:#ffffff;text-decoration:none;")
                .Append("padding:12px 20px;border-radius:999px;font-weight:600;\">")
                .Append(Encode(action)).Append("</a></p>")
                .Append("<p style=\"margin:0 0 16px;font-size:13px;color:#6b635a;word-break:break-all;\">")
                .Append(href).Append("</p>");
        }

        return html
            .Append("<p style=\"margin:24px 0 0;font-size:13px;color:#6b635a;\">").Append(Encode(words.Footer)).Append("</p>")
            .Append("</div></body></html>")
            .ToString();
    }

    private static string Paragraph(string text) =>
        $"<p style=\"margin:0 0 16px;line-height:1.5;\">{Encode(text)}</p>";

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
