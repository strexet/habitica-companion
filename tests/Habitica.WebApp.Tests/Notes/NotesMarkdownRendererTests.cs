using Habitica.WebApp.Formatting;
namespace Habitica.WebApp.Tests.Notes;
public sealed class NotesMarkdownRendererTests
{
    [Fact]public void Structured_document_is_readable_but_all_untrusted_html_and_remote_images_are_inert()
    {
        var source="# Clothes\n- Coat\n1. Size\n| Item | Size |\n| --- | --- |\n| Coat | L |\n```html\n<script>alert('x')</script>\n```\n<img src=x onerror=alert(1)>\n![photo](https://remote.invalid/pixel)\n[bad](javascript:alert) [data](data:text/html,x) [good](https://example.com/path?a=1&b=2)";
        var html=NotesMarkdownRenderer.Render(source).Value;Assert.Contains("<h1>Clothes</h1>",html);Assert.Contains("<ul>",html);Assert.Contains("<ol>",html);Assert.Contains("<table>",html);Assert.Contains("&lt;script&gt;",html);Assert.Contains("&lt;img",html);Assert.DoesNotContain("<img",html);Assert.DoesNotContain("<script",html);Assert.DoesNotContain("href=\"javascript",html);Assert.DoesNotContain("href=\"data:",html);Assert.DoesNotContain("remote.invalid",html);Assert.Contains("noopener noreferrer",html);Assert.Contains("https://example.com/path?a=1&amp;b=2",html);
    }
    [Theory][InlineData("[x](https://user:secret@host.invalid)")][InlineData("[x](file:///tmp/test)")][InlineData("[x](//host.invalid)")][InlineData("[x](vbscript:bad)")]
    public void Unsafe_links_remain_inert_text(string source)=>Assert.DoesNotContain("<a ",NotesMarkdownRenderer.Render(source).Value);
    [Fact]public void Code_source_and_unicode_remain_literal_and_large_source_is_bounded()
    {var source="```\n# heading\n**not bold** <button> 🧥\n```";var html=NotesMarkdownRenderer.Render(source).Value;Assert.Contains("# heading\n**not bold** &lt;button&gt;",html);Assert.DoesNotContain("<strong>",html);Assert.Contains("Preview unavailable",NotesMarkdownRenderer.Render(new string('x',131073)).Value);}
}
