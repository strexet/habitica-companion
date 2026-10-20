using AngleSharp.Html.Parser;

namespace Habitica.WebApp.Tests.Qualification;

public sealed class BoundFieldEvidenceTests
{
    [Fact]
    public void Bound_textarea_values_become_literal_multiline_content_without_changing_source_markup()
    {
        const string markup = "<label>Notes<textarea data-testid=\"bound\" value=\"First line&#10;&lt;/textarea&gt;&lt;script&gt;untrusted note&lt;/script&gt;&amp;\"></textarea></label><textarea data-testid=\"unbound\">Existing content</textarea>";
        var output = PageLayoutEvidence.NormalizeBoundFields(markup);
        var parsed = new HtmlParser().ParseDocument(output);
        Assert.Empty(parsed.QuerySelectorAll("script"));
        var textarea = parsed.QuerySelector("[data-testid='bound']")!;
        Assert.Equal("First line\n</textarea><script>untrusted note</script>&", textarea.TextContent);
        Assert.False(textarea.HasAttribute("value"));
        Assert.Equal("Existing content", parsed.QuerySelector("[data-testid='unbound']")!.TextContent);
        Assert.Contains("value=", markup);
        PageLayoutEvidence.Save("bound-textarea-evidence", markup, "/tasks", "bound-textarea-canary", ["form"], nameof(Bound_textarea_values_become_literal_multiline_content_without_changing_source_markup));
    }
}
