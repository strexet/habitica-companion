using System.Text.Json;
using Habitica.WebApp.Theme;
using AngleSharp.Html.Parser;

namespace Habitica.WebApp.Tests.Qualification;

internal static class PageLayoutEvidence
{
    private static readonly object Gate = new();
    internal static string NormalizeBoundFields(string markup)
    {
        var document = new HtmlParser().ParseDocument(markup);
        foreach (var textarea in document.QuerySelectorAll("textarea[value]"))
        {
            textarea.TextContent = textarea.GetAttribute("value") ?? "";
            textarea.RemoveAttribute("value");
        }
        return document.Body!.InnerHtml;
    }
    public static void Save(string name, string markup, string route, string state, string[] surfaces, string interactionTest, bool fullLayout = false)
    {
        var directory = Environment.GetEnvironmentVariable("HABITICA_BROWSER_EVIDENCE");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var filename = name + ".html";
        if (fullLayout && !markup.Contains("--mud-appbar-height:", StringComparison.Ordinal))
            throw new InvalidOperationException("Full-layout evidence requires the current App theme geometry stylesheet.");
        var content = NormalizeBoundFields(fullLayout ? "<div class=\"qualification-full-layout\">" + markup + "</div>" : markup);
        if (!fullLayout) content = "<main style=\"padding:16px;max-width:1100px;margin:auto\"><p>Synthetic Razor output. Component interactions are qualified separately.</p>" + content + "</main>";
        File.WriteAllText(Path.Combine(directory, filename), "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Synthetic " + name + "</title><link rel=\"stylesheet\" href=\"/mud.css\"><link rel=\"stylesheet\" href=\"/app.css\"></head><body>" + content + "<script src=\"/colorSchemes.js\"></script><script type=\"module\" src=\"/qualification-frame.mjs\"></script></body></html>");
        lock (Gate)
        {
            var path = Path.Combine(directory, "coverage.json");
            var entries = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path), options)! : new();
            entries[name] = new { file = filename, route, state, surfaces, interactionTest, runtime = "Synthetic Razor; physical devices and hosted access unqualified" };
            File.WriteAllText(path, JsonSerializer.Serialize(entries, options));
            var allSchemes = Environment.GetEnvironmentVariable("HABITICA_BROWSER_ALL_SCHEMES") == "1";
            var schemes = ColorSchemeCatalog.BuiltInSchemes.Where(s => allSchemes || s.Id is "gryphy-light" or "gryphy-dark" or "obsidian-glow" or "toxic-swamp" or "boss-battle").ToDictionary(s => s.Id);
            schemes["generated-calm"] = ColorSchemeCatalog.GenerateRandomTheme(new Random(7103), 0) with { Id = "generated-calm" };
            schemes["generated-moderate"] = ColorSchemeCatalog.GenerateRandomTheme(new Random(7103), .5) with { Id = "generated-moderate" };
            File.WriteAllText(Path.Combine(directory, "schemes.json"), JsonSerializer.Serialize(schemes, options));
        }
    }
}
