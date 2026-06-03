using System.Text.Json;
using Bunit;
using Habitica.Application.Sync;
using Habitica.Storage;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Habitica.WebApp.Theme;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Components;

public sealed class ColorSchemePanelTests : BunitContext
{
    [Fact]
    public void Renders_random_buttons()
    {
        var cut = RenderPanel(new FakeKeyValueStorage());

        Assert.NotNull(cut.Find("[data-testid='random-preset-scheme']"));
        Assert.NotNull(cut.Find("[data-testid='random-theme-scheme']"));
    }

    [Fact]
    public void Renders_grouped_scheme_sections_in_fixed_order()
    {
        var cut = RenderPanel(new FakeKeyValueStorage());
        var labels = cut.FindAll("optgroup").Select(group => group.GetAttribute("label")).ToArray();

        Assert.Equal(new[] { "Default", "Built-in Light", "Built-in Dark" }, labels);
    }

    [Fact]
    public void Renders_readable_preset_help_variant_toggle_and_advanced_gradient_surfaces()
    {
        var cut = RenderPanel(new FakeKeyValueStorage());

        Assert.Contains("What is this?", cut.Markup);
        Assert.Contains("PageBackground", cut.Markup);
        Assert.Contains("TextShadows.Headings", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='custom-scheme-dark']"));
        Assert.Equal(7, cut.FindAll(".color-scheme-gradient-surface").Count);
    }

    [Fact]
    public void Random_preset_selects_a_scheme_and_persists_selection()
    {
        var storage = new FakeKeyValueStorage();
        var sessionController = new FakeAppSessionController(SessionViewModel.Empty);
        var cut = RenderPanel(storage, sessionController: sessionController);

        cut.Find("[data-testid='random-preset-scheme']").Click();

        Assert.Contains("Random preset:", cut.Markup);
        var preferences = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        Assert.NotNull(preferences);
        Assert.False(string.IsNullOrWhiteSpace(preferences!.SelectedSchemeId));
        Assert.Equal(new[] { CloudSyncSection.ColorSchemes }, sessionController.SyncAppDataSectionCalls);
    }

    [Fact]
    public void Selecting_a_scheme_syncs_color_scheme_section()
    {
        var storage = new FakeKeyValueStorage();
        var sessionController = new FakeAppSessionController(SessionViewModel.Empty);
        var cut = RenderPanel(storage, sessionController: sessionController);

        cut.Find("[data-testid='color-scheme-select']").Change(ColorSchemeCatalog.DefaultDarkSchemeId);

        var preferences = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        Assert.Equal(ColorSchemeCatalog.DefaultDarkSchemeId, preferences!.SelectedSchemeId);
        Assert.Equal(new[] { CloudSyncSection.ColorSchemes }, sessionController.SyncAppDataSectionCalls);
    }

    [Fact]
    public void Random_theme_offers_generated_option_without_persisting_it()
    {
        var storage = new FakeKeyValueStorage();
        var sessionController = new FakeAppSessionController(SessionViewModel.Empty);
        var cut = RenderPanel(storage, sessionController: sessionController);

        cut.Find("[data-testid='random-theme-scheme']").Click();

        // Temp random becomes selectable in the dropdown and offers a save control.
        Assert.Contains("Random theme", cut.Markup);
        Assert.NotNull(cut.Find($"option[value='{ColorSchemeCatalog.RandomSchemeId}']"));
        Assert.NotNull(cut.Find("[data-testid='save-random-scheme']"));

        // Generating a random theme must not write a dangling selection to storage.
        var preferences = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        Assert.True(preferences is null || preferences.SelectedSchemeId != ColorSchemeCatalog.RandomSchemeId);
        Assert.Empty(sessionController.SyncAppDataSectionCalls);

        cut.Find("[data-testid='reroll-random-scheme']").Click();

        Assert.Empty(sessionController.SyncAppDataSectionCalls);
    }

    [Fact]
    public void Saving_random_theme_with_a_name_stores_a_custom_scheme()
    {
        var storage = new FakeKeyValueStorage();
        var sessionController = new FakeAppSessionController(SessionViewModel.Empty);
        var cut = RenderPanel(storage, sessionController: sessionController);

        cut.Find("[data-testid='random-theme-scheme']").Click();
        cut.Find("[data-testid='random-scheme-name']").Change("Lucky Roll");
        cut.Find("[data-testid='save-random-scheme']").Click();

        var preferences = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        Assert.NotNull(preferences);
        var custom = Assert.Single(preferences!.CustomSchemes);
        Assert.Equal("Lucky Roll", custom.Name);
        Assert.NotEqual(ColorSchemeCatalog.RandomSchemeId, custom.Id);
        Assert.Equal(custom.Id, preferences.SelectedSchemeId);
        Assert.Contains("Saved Lucky Roll.", cut.Markup);
        Assert.Equal(new[] { CloudSyncSection.ColorSchemes }, sessionController.SyncAppDataSectionCalls);
    }

    [Fact]
    public void Compact_mode_hides_advanced_controls_until_toggled()
    {
        var cut = RenderPanel(new FakeKeyValueStorage(), compact: true);

        // Bar controls are present, but the custom-scheme editor stays hidden until expanded.
        Assert.NotNull(cut.Find("[data-testid='random-preset-scheme']"));
        Assert.NotNull(cut.Find("[data-testid='color-scheme-advanced-toggle']"));
        Assert.Empty(cut.FindAll("[data-testid='save-custom-scheme']"));

        cut.Find("[data-testid='color-scheme-advanced-toggle']").Click();

        // Expanding reveals the editor directly — no intermediate "Create Custom Copy" step.
        Assert.NotNull(cut.Find("[data-testid='save-custom-scheme']"));
        Assert.NotNull(cut.Find("[data-testid='paste-custom-scheme']"));
    }

    [Fact]
    public void Compact_mode_auto_reveals_random_save_controls()
    {
        var cut = RenderPanel(new FakeKeyValueStorage(), compact: true);

        cut.Find("[data-testid='random-theme-scheme']").Click();

        // Generating a random theme reveals the save + chaos controls without an explicit expand.
        Assert.NotNull(cut.Find("[data-testid='save-random-scheme']"));
        Assert.NotNull(cut.Find("[data-testid='reroll-random-scheme']"));
        Assert.NotNull(cut.Find("[data-testid='chaos-slider']"));
    }

    [Fact]
    public void Compact_done_closes_customization_without_reverting_random_theme()
    {
        var storage = new FakeKeyValueStorage();
        var sessionController = new FakeAppSessionController(SessionViewModel.Empty);
        var cut = RenderPanel(storage, compact: true, sessionController: sessionController);

        cut.Find("[data-testid='random-theme-scheme']").Click();

        var toggle = cut.Find("[data-testid='color-scheme-advanced-toggle']");
        Assert.Contains("Done", toggle.TextContent);
        Assert.DoesNotContain("Cancel", toggle.TextContent);

        toggle.Click();

        Assert.Empty(cut.FindAll("[data-testid='save-random-scheme']"));
        Assert.NotNull(cut.Find($"option[value='{ColorSchemeCatalog.RandomSchemeId}']"));
        var preferences = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        Assert.True(preferences is null || preferences.SelectedSchemeId != ColorSchemeCatalog.RandomSchemeId);
        Assert.Empty(sessionController.SyncAppDataSectionCalls);
    }

    [Fact]
    public void Rerolled_random_theme_is_saveable()
    {
        var storage = new FakeKeyValueStorage();
        var cut = RenderPanel(storage);

        cut.Find("[data-testid='random-theme-scheme']").Click();
        cut.Find("[data-testid='reroll-random-scheme']").Click();
        cut.Find("[data-testid='random-scheme-name']").Change("Wild One");
        cut.Find("[data-testid='save-random-scheme']").Click();

        var preferences = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        var custom = Assert.Single(preferences!.CustomSchemes);
        Assert.Equal("Wild One", custom.Name);
        Assert.Empty(ColorSchemeCatalog.Validate(custom));
    }

    [Fact]
    public async Task Selecting_a_scheme_clears_random_active_but_keeps_pending_random()
    {
        // Regression: after rolling a random theme then picking another scheme, re-mounting the
        // panel (e.g. folding/reopening the Dashboard appearance section) must not snap back to the
        // random theme. The service's RandomActive flag gates that reapply.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var service = Services.GetRequiredService<ColorSchemeService>();

        await service.ApplyRandomThemeAsync(0.5);
        Assert.True(service.RandomActive);

        await service.SelectAsync(ColorSchemeCatalog.AlphaId);

        Assert.False(service.RandomActive);
        // The pending random is still remembered so the "Generated" dropdown entry stays available.
        Assert.NotNull(service.PendingRandomScheme);
    }

    [Fact]
    public void Saved_custom_scheme_offers_delete_in_editor()
    {
        // A saved custom scheme must be deletable from the editor card, which is the only custom
        // card reachable on the compact Dashboard panel once a custom scheme is active.
        var storage = new FakeKeyValueStorage();
        var sessionController = new FakeAppSessionController(SessionViewModel.Empty);
        var cut = RenderPanel(storage, sessionController: sessionController);

        cut.Find("[data-testid='random-theme-scheme']").Click();
        cut.Find("[data-testid='random-scheme-name']").Change("Mine");
        cut.Find("[data-testid='save-random-scheme']").Click();

        Assert.NotNull(cut.Find("[data-testid='delete-custom-scheme']"));

        cut.Find("[data-testid='delete-custom-scheme']").Click();

        var preferences = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        Assert.True(preferences is null || preferences.CustomSchemes.Count == 0);
        Assert.Equal(
            new[] { CloudSyncSection.ColorSchemes, CloudSyncSection.ColorSchemes },
            sessionController.SyncAppDataSectionCalls);
    }

    [Fact]
    public void Custom_dark_toggle_survives_save()
    {
        var storage = new FakeKeyValueStorage();
        var sessionController = new FakeAppSessionController(SessionViewModel.Empty);
        var cut = RenderPanel(storage, sessionController: sessionController);

        cut.Find("[data-testid='custom-scheme-dark']").Change(true);
        cut.Find("[data-testid='custom-scheme-name']").Change("Night custom");
        cut.Find("[data-testid='save-custom-scheme']").Click();

        var custom = Assert.Single(storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences)!.CustomSchemes);
        Assert.True(custom.IsDark);
        Assert.Equal(new[] { CloudSyncSection.ColorSchemes }, sessionController.SyncAppDataSectionCalls);
    }

    [Fact]
    public void Paste_preview_does_not_sync_until_saved()
    {
        var storage = new FakeKeyValueStorage();
        var sessionController = new FakeAppSessionController(SessionViewModel.Empty);
        var cut = RenderPanel(storage, sessionController: sessionController);
        var pasted = ColorSchemeCatalog.CreateCustomCopy(ColorSchemeCatalog.DefaultDark, "Preview");
        JSInterop.Setup<string>("navigator.clipboard.readText").SetResult(string.Empty);

        cut.Find("[data-testid='paste-custom-scheme']").Click();
        cut.Find("[data-testid='paste-fallback-input']").Input(ReadableSchemeParser.Serialize(pasted));
        cut.Find("[data-testid='apply-pasted-scheme']").Click();

        Assert.Contains("Review the live colors and save to keep them.", cut.Markup);
        Assert.Empty(sessionController.SyncAppDataSectionCalls);
    }

    [Fact]
    public void Cloud_sync_failure_does_not_undo_local_scheme_selection()
    {
        var storage = new FakeKeyValueStorage();
        var sessionController = new FakeAppSessionController(SessionViewModel.Empty)
        {
            LocalDataResult = LocalDataActionResult.Failure("Cloud sync failed.")
        };
        var cut = RenderPanel(storage, sessionController: sessionController);

        cut.Find("[data-testid='color-scheme-select']").Change(ColorSchemeCatalog.DefaultDarkSchemeId);

        Assert.Equal(ColorSchemeCatalog.DefaultDarkSchemeId, storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences)!.SelectedSchemeId);
        Assert.Contains("Using Gryphy (Dark).", cut.Markup);
        Assert.DoesNotContain("Cloud sync failed.", cut.Markup);
        Assert.Equal(new[] { CloudSyncSection.ColorSchemes }, sessionController.SyncAppDataSectionCalls);
    }

    [Fact]
    public void Signed_out_sync_skip_result_does_not_show_failure()
    {
        var storage = new FakeKeyValueStorage();
        var sessionController = new FakeAppSessionController(SessionViewModel.Empty)
        {
            LocalDataResult = LocalDataActionResult.Success("Cloud sync skipped because no active Habitica credentials are available.")
        };
        var cut = RenderPanel(storage, sessionController: sessionController);

        cut.Find("[data-testid='color-scheme-select']").Change(ColorSchemeCatalog.DefaultDarkSchemeId);

        Assert.Equal(ColorSchemeCatalog.DefaultDarkSchemeId, storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences)!.SelectedSchemeId);
        Assert.Contains("Using Gryphy (Dark).", cut.Markup);
        Assert.DoesNotContain("Cloud sync skipped", cut.Markup);
    }

    [Fact]
    public async Task Deleting_active_custom_scheme_uses_matching_variant_default_and_persists_reduced_list()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = new FakeKeyValueStorage();
        var custom = ColorSchemeCatalog.CreateCustomCopy(ColorSchemeCatalog.DefaultDark, "Night custom") with { IsDark = true };
        await storage.SetAsync(
            StorageKeys.ColorSchemePreferences,
            new ColorSchemePreferences(custom.Id, new[] { custom }),
            CancellationToken.None);
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddScoped<ColorSchemeService>();

        var service = Services.GetRequiredService<ColorSchemeService>();
        var state = await service.DeleteCustomAsync(custom.Id);

        Assert.Equal(ColorSchemeCatalog.DefaultDarkSchemeId, state.ActiveScheme.Id);
        Assert.Empty(state.CustomSchemes);
        var stored = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        Assert.Equal(ColorSchemeCatalog.DefaultDarkSchemeId, stored!.SelectedSchemeId);
        Assert.Empty(stored.CustomSchemes);
    }

    [Fact]
    public async Task Loading_legacy_preferences_migrates_selection_schema_and_custom_variant()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = new FakeKeyValueStorage();
        var legacyCustom = ColorSchemeCatalog.CreateCustomCopy(ColorSchemeCatalog.DefaultDark, "Legacy") with { IsDark = false };
        await storage.SetAsync(
            StorageKeys.ColorSchemePreferences,
            new ColorSchemePreferences(ColorSchemeCatalog.AlphaId, new[] { legacyCustom }, SchemaVersion: 1),
            CancellationToken.None);
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddScoped<ColorSchemeService>();

        var service = Services.GetRequiredService<ColorSchemeService>();
        var state = await service.LoadAsync();

        Assert.Equal(ColorSchemeCatalog.ForestLegacyId, state.ActiveScheme.Id);
        var stored = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        Assert.Equal(ColorSchemePreferences.CurrentSchemaVersion, stored!.SchemaVersion);
        Assert.True(Assert.Single(stored.CustomSchemes).IsDark);
    }

    private IRenderedComponent<ColorSchemePanel> RenderPanel(
        FakeKeyValueStorage storage,
        bool compact = false,
        FakeAppSessionController? sessionController = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddSingleton<IAppSessionController>(sessionController ?? new FakeAppSessionController(SessionViewModel.Empty));
        Services.AddScoped<ColorSchemeService>();
        return Render<ColorSchemePanel>(parameters => parameters.Add(p => p.Compact, compact));
    }

    private sealed class FakeKeyValueStorage : IKeyValueStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public TValue? Get<TValue>(string key)
        {
            return _values.TryGetValue(key, out var json)
                ? JsonSerializer.Deserialize<TValue>(json, JsonOptions)
                : default;
        }

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(Get<TValue>(key));
        }

        public Task<string?> GetRawJsonAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.GetValueOrDefault(key));
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
        {
            _values[key] = JsonSerializer.Serialize(value, JsonOptions);
            return Task.CompletedTask;
        }

        public Task SetRawJsonAsync(string key, string jsonText, CancellationToken cancellationToken)
        {
            _values[key] = jsonText;
            return Task.CompletedTask;
        }
    }
}
