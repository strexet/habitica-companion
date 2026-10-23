using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Habitica.Domain.Notes;
using Microsoft.AspNetCore.Components;
namespace Habitica.WebApp.Formatting;

public static partial class NotesMarkdownRenderer
{
    public static MarkupString Render(string? source)
    {
        if(string.IsNullOrEmpty(source))return new("<p class=\"panel-copy\">Markdown preview appears here.</p>");
        if(Encoding.UTF8.GetByteCount(source)>NotesModel.BodyBytes)return new("<p>Preview unavailable: Markdown exceeds 128 KiB. The source is unchanged.</p>");
        var lines=source.Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n').Split('\n');var html=new StringBuilder();
        for(var i=0;i<lines.Length;)
        {
            var line=lines[i];if(string.IsNullOrWhiteSpace(line)){i++;continue;}
            var trimmed=line.TrimStart();
            if(trimmed.StartsWith("```",StringComparison.Ordinal)||trimmed.StartsWith("~~~",StringComparison.Ordinal))
            {
                var marker=trimmed[0];var length=trimmed.TakeWhile(c=>c==marker).Count();i++;var code=new List<string>();
                while(i<lines.Length){var end=lines[i].Trim();if(end.Length>=length&&end.All(c=>c==marker)){i++;break;}code.Add(lines[i++]);}
                html.Append("<pre><code>").Append(WebUtility.HtmlEncode(string.Join('\n',code))).Append("</code></pre>");continue;
            }
            var heading=Heading().Match(line);if(heading.Success){var level=heading.Groups[1].Length;html.Append("<h").Append(level).Append('>').Append(Inline(heading.Groups[2].Value)).Append("</h").Append(level).Append('>');i++;continue;}
            if(i+1<lines.Length&&line.Contains('|')&&TableDivider().IsMatch(lines[i+1]))
            {
                var columns=Cells(line);html.Append("<div class=\"notes-table-scroll\"><table><thead><tr>");foreach(var column in columns)html.Append("<th scope=\"col\">").Append(Inline(column)).Append("</th>");html.Append("</tr></thead><tbody>");i+=2;
                while(i<lines.Length&&!string.IsNullOrWhiteSpace(lines[i])&&lines[i].Contains('|')){var row=Cells(lines[i++]);html.Append("<tr>");for(var c=0;c<columns.Length;c++)html.Append("<td>").Append(Inline(c<row.Length?row[c]:"")).Append("</td>");html.Append("</tr>");}
                html.Append("</tbody></table></div>");continue;
            }
            var list=ListItem().Match(line);if(list.Success)
            {
                var ordered=list.Groups[1].Success;var tag=ordered?"ol":"ul";html.Append('<').Append(tag).Append('>');
                while(i<lines.Length&&(list=ListItem().Match(lines[i])).Success&&list.Groups[1].Success==ordered){html.Append("<li>").Append(Inline(list.Groups[2].Value)).Append("</li>");i++;}html.Append("</").Append(tag).Append('>');continue;
            }
            if(trimmed.StartsWith('>')){html.Append("<blockquote>").Append(Inline(trimmed[1..].TrimStart())).Append("</blockquote>");i++;continue;}
            html.Append("<p>").Append(Inline(line)).Append("</p>");i++;
        }
        return new(html.ToString());
    }
    private static string[] Cells(string line)=>line.Trim().Trim('|').Split('|').Select(cell=>cell.Trim()).ToArray();
    private static string Inline(string text)
    {
        var html=new StringBuilder();var position=0;
        foreach(Match match in Tokens().Matches(text))
        {
            html.Append(WebUtility.HtmlEncode(text[position..match.Index]));var value=match.Value;
            if(match.Groups["code"].Success)html.Append("<code>").Append(WebUtility.HtmlEncode(value[1..^1])).Append("</code>");
            else if(match.Groups["bold"].Success)html.Append("<strong>").Append(WebUtility.HtmlEncode(value[2..^2])).Append("</strong>");
            else if(match.Groups["em"].Success)html.Append("<em>").Append(WebUtility.HtmlEncode(value[1..^1])).Append("</em>");
            else if(match.Groups["image"].Success){var end=value.IndexOf("](",StringComparison.Ordinal);html.Append("<span class=\"panel-copy\">[Image omitted: ").Append(WebUtility.HtmlEncode(value[2..end])).Append("]</span>");}
            else
            {
                var split=value.IndexOf("](",StringComparison.Ordinal);var label=value[1..split];var href=value[(split+2)..^1];
                if(Uri.TryCreate(href,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http"&&string.IsNullOrEmpty(uri.UserInfo))html.Append("<a rel=\"noopener noreferrer\" target=\"_blank\" href=\"").Append(WebUtility.HtmlEncode(uri.AbsoluteUri)).Append("\">").Append(WebUtility.HtmlEncode(label)).Append("</a>");
                else html.Append(WebUtility.HtmlEncode(value));
            }
            position=match.Index+match.Length;
        }
        html.Append(WebUtility.HtmlEncode(text[position..]));return html.ToString();
    }
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$",RegexOptions.NonBacktracking,100)]private static partial Regex Heading();
    [GeneratedRegex(@"^\s*(?:(\d+)\.|[-+*])\s+(.+)$",RegexOptions.NonBacktracking,100)]private static partial Regex ListItem();
    [GeneratedRegex(@"^\s*\|?\s*:?-{3,}:?\s*(?:\|\s*:?-{3,}:?\s*)+\|?\s*$",RegexOptions.NonBacktracking,100)]private static partial Regex TableDivider();
    [GeneratedRegex(@"(?<code>`[^`\n]+`)|(?<image>!\[[^\]\n]*\]\([^\)\n]*\))|(?<link>\[[^\]\n]*\]\([^\)\n]*\))|(?<bold>\*\*[^*\n]+\*\*)|(?<em>\*[^*\n]+\*)",RegexOptions.NonBacktracking,100)]private static partial Regex Tokens();
}
