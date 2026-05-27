using System.IO;
using System.Linq;

namespace Habitica.WebApp.Tests;

public sealed class StaticAssetRegressionTests
{
    [Fact]
    public void Indexeddb_storage_module_imports_dexie_from_published_vendor_path()
    {
        var repositoryRoot = FindRepositoryRoot();
        var modulePath = Path.Combine(
            repositoryRoot,
            "src",
            "Habitica.WebApp",
            "wwwroot",
            "js",
            "storage",
            "indexedDbStorage.js");

        var moduleContents = File.ReadAllText(modulePath);

        Assert.Contains("import Dexie from \"../../vendor/dexie.mjs\";", moduleContents);
    }

    [Fact]
    public void Index_html_does_not_reference_missing_isolated_stylesheet_when_no_component_styles_exist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webAppRoot = Path.Combine(repositoryRoot, "src", "Habitica.WebApp");
        var indexHtmlPath = Path.Combine(webAppRoot, "wwwroot", "index.html");
        var indexHtmlContents = File.ReadAllText(indexHtmlPath);
        var componentStylesheets = Directory.EnumerateFiles(webAppRoot, "*.razor.css", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(componentStylesheets);
        Assert.DoesNotContain("Habitica.WebApp.styles.css", indexHtmlContents);
    }

    [Fact]
    public void Cloudflare_pages_spa_fallback_does_not_ship_redirects_file()
    {
        var repositoryRoot = FindRepositoryRoot();
        var redirectsPath = Path.Combine(
            repositoryRoot,
            "src",
            "Habitica.WebApp",
            "wwwroot",
            "_redirects");

        Assert.False(File.Exists(redirectsPath));
    }

    [Fact]
    public void Index_html_does_not_register_service_worker()
    {
        var repositoryRoot = FindRepositoryRoot();
        var indexHtmlPath = Path.Combine(
            repositoryRoot,
            "src",
            "Habitica.WebApp",
            "wwwroot",
            "index.html");
        var indexHtmlContents = File.ReadAllText(indexHtmlPath);

        Assert.DoesNotContain("serviceWorker.register", indexHtmlContents);
        Assert.DoesNotContain("service-worker.js", indexHtmlContents);
    }

    [Fact]
    public void App_css_routes_reported_theming_surfaces_through_color_scheme_tokens()
    {
        var repositoryRoot = FindRepositoryRoot();
        var stylesheetPath = Path.Combine(
            repositoryRoot,
            "src",
            "Habitica.WebApp",
            "wwwroot",
            "css",
            "app.css");
        var stylesheet = File.ReadAllText(stylesheetPath);

        Assert.Contains("--appbar-bg", stylesheet);
        Assert.Contains("--drawer-bg", stylesheet);
        Assert.Contains("--input-bg", stylesheet);
        Assert.Contains("--disabled-bg", stylesheet);
        Assert.Contains(".topbar", stylesheet);
        Assert.Contains(".app-drawer", stylesheet);
        Assert.Contains(".mud-button:disabled", stylesheet);
        Assert.Contains("input[type=\"file\"].app-input::file-selector-button", stylesheet);
        Assert.Contains(".dashboard-link-card", stylesheet);
        Assert.Contains(".party-cron-chart", stylesheet);
        Assert.Contains(".diagnostics-log-item:active", stylesheet);
        Assert.Contains(".segmented-control button.active", stylesheet);
        Assert.Contains("var(--surface)", stylesheet);
        Assert.Contains("var(--input-bg)", stylesheet);
        Assert.Contains("var(--disabled-text)", stylesheet);
    }

    [Fact]
    public void Party_sync_module_does_not_send_habitica_api_token()
    {
        var repositoryRoot = FindRepositoryRoot();
        var modulePath = Path.Combine(
            repositoryRoot,
            "src",
            "Habitica.WebApp",
            "wwwroot",
            "js",
            "sync",
            "cloudflarePartySync.js");
        var moduleContents = File.ReadAllText(modulePath);

        Assert.DoesNotContain("apiToken", moduleContents);
        Assert.DoesNotContain("x-api-key", moduleContents);
        Assert.DoesNotContain("x-api-user", moduleContents);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "Habitica.sln");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
