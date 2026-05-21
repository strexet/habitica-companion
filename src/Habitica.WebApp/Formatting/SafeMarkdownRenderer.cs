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
    private static readonly Regex HtmlBreakRegex = BuildHtmlBreakRegex();
    private static readonly Regex HtmlStrongOpenRegex = BuildHtmlStrongOpenRegex();
    private static readonly Regex HtmlStrongCloseRegex = BuildHtmlStrongCloseRegex();
    private static readonly Regex HtmlEmphasisOpenRegex = BuildHtmlEmphasisOpenRegex();
    private static readonly Regex HtmlEmphasisCloseRegex = BuildHtmlEmphasisCloseRegex();
    private static readonly Regex HtmlCodeOpenRegex = BuildHtmlCodeOpenRegex();
    private static readonly Regex HtmlCodeCloseRegex = BuildHtmlCodeCloseRegex();

    public static MarkupString Render(string? markdown, string emptyText = "No cached summary text.")
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new MarkupString(WebUtility.HtmlEncode(emptyText));
        }

        var normalized = NormalizeLineBreaks(markdown).Trim();
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

    private static string NormalizeLineBreaks(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        normalized = normalized.Replace('\r', '\n');
        return HtmlBreakRegex.Replace(normalized, "\n");
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
        encoded = RestoreSafeHtmlFormatting(encoded);
        return encoded;
    }

    private static string RestoreSafeHtmlFormatting(string encoded)
    {
        encoded = HtmlStrongOpenRegex.Replace(encoded, "<strong>");
        encoded = HtmlStrongCloseRegex.Replace(encoded, "</strong>");
        encoded = HtmlEmphasisOpenRegex.Replace(encoded, "<em>");
        encoded = HtmlEmphasisCloseRegex.Replace(encoded, "</em>");
        encoded = HtmlCodeOpenRegex.Replace(encoded, "<code>");
        return HtmlCodeCloseRegex.Replace(encoded, "</code>");
    }

    [GeneratedRegex(@"\[(?<label>[^\]]+)\]\((?<href>[^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex BuildLinkRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Compiled)]
    private static partial Regex BuildBoldRegex();

    [GeneratedRegex(@"(?<!\*)\*(?!\s)(.+?)(?<!\s)\*(?!\*)", RegexOptions.Compiled)]
    private static partial Regex BuildItalicRegex();

    [GeneratedRegex(@"`(.+?)`", RegexOptions.Compiled)]
    private static partial Regex BuildCodeRegex();

    [GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BuildHtmlBreakRegex();

    [GeneratedRegex(@"&lt;\s*(?:strong|b)\s*&gt;", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BuildHtmlStrongOpenRegex();

    [GeneratedRegex(@"&lt;\s*/\s*(?:strong|b)\s*&gt;", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BuildHtmlStrongCloseRegex();

    [GeneratedRegex(@"&lt;\s*(?:em|i)\s*&gt;", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BuildHtmlEmphasisOpenRegex();

    [GeneratedRegex(@"&lt;\s*/\s*(?:em|i)\s*&gt;", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BuildHtmlEmphasisCloseRegex();

    [GeneratedRegex(@"&lt;\s*code\s*&gt;", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BuildHtmlCodeOpenRegex();

    [GeneratedRegex(@"&lt;\s*/\s*code\s*&gt;", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BuildHtmlCodeCloseRegex();
}
