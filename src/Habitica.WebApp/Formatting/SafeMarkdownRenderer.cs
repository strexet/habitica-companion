using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Habitica.WebApp.Formatting;

internal static partial class SafeMarkdownRenderer
{
    private static readonly Regex LinkRegex = BuildLinkRegex();
    private static readonly Regex BoldRegex = BuildBoldRegex();
    private static readonly Regex ItalicRegex = BuildItalicRegex();
    private static readonly Regex CodeRegex = BuildCodeRegex();

    public static MarkupString Render(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new MarkupString("No cached summary text.");
        }

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var blocks = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var html = new StringBuilder();

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.TrimEntries);
            if (lines.Length > 0 && lines.All(static line => line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)))
            {
                html.Append("<ul>");
                foreach (var line in lines)
                {
                    html.Append("<li>")
                        .Append(FormatInline(line[2..]))
                        .Append("</li>");
                }

                html.Append("</ul>");
                continue;
            }

            html.Append("<p>")
                .Append(string.Join("<br />", lines.Select(FormatInline)))
                .Append("</p>");
        }

        return new MarkupString(html.ToString());
    }

    private static string FormatInline(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        encoded = LinkRegex.Replace(encoded, match =>
        {
            var label = match.Groups["label"].Value;
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return match.Value;
            }

            return $"<a href=\"{WebUtility.HtmlEncode(uri.ToString())}\" rel=\"noreferrer\" target=\"_blank\">{label}</a>";
        });
        encoded = BoldRegex.Replace(encoded, "<strong>$1</strong>");
        encoded = ItalicRegex.Replace(encoded, "<em>$1</em>");
        encoded = CodeRegex.Replace(encoded, "<code>$1</code>");
        return encoded;
    }

    [GeneratedRegex(@"\[(?<label>[^\]]+)\]\((?<href>[^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex BuildLinkRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Compiled)]
    private static partial Regex BuildBoldRegex();

    [GeneratedRegex(@"(?<!\*)\*(?!\s)(.+?)(?<!\s)\*(?!\*)", RegexOptions.Compiled)]
    private static partial Regex BuildItalicRegex();

    [GeneratedRegex(@"`(.+?)`", RegexOptions.Compiled)]
    private static partial Regex BuildCodeRegex();
}
