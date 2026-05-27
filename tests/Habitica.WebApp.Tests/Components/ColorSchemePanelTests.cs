using System.Text.Json;
using Bunit;
using Habitica.Storage;
using Habitica.WebApp.Components;
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
    public void Random_preset_selects_a_scheme_and_persists_selection()
    {
        var storage = new FakeKeyValueStorage();
        var cut = RenderPanel(storage);

        cut.Find("[data-testid='random-preset-scheme']").Click();

        Assert.Contains("Random preset:", cut.Markup);
        var preferences = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        Assert.NotNull(preferences);
        Assert.False(string.IsNullOrWhiteSpace(preferences!.SelectedSchemeId));
    }

    [Fact]
    public void Random_theme_offers_generated_option_without_persisting_it()
    {
        var storage = new FakeKeyValueStorage();
        var cut = RenderPanel(storage);

        cut.Find("[data-testid='random-theme-scheme']").Click();

        // Temp random becomes selectable in the dropdown and offers a save control.
        Assert.Contains("Random theme", cut.Markup);
        Assert.NotNull(cut.Find($"option[value='{ColorSchemeCatalog.RandomSchemeId}']"));
        Assert.NotNull(cut.Find("[data-testid='save-random-scheme']"));

        // Generating a random theme must not write a dangling selection to storage.
        var preferences = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        Assert.True(preferences is null || preferences.SelectedSchemeId != ColorSchemeCatalog.RandomSchemeId);
    }

    [Fact]
    public void Saving_random_theme_with_a_name_stores_a_custom_scheme()
    {
        var storage = new FakeKeyValueStorage();
        var cut = RenderPanel(storage);

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
    }

    [Fact]
    public void Compact_mode_hides_advanced_controls_until_toggled()
    {
        var cut = RenderPanel(new FakeKeyValueStorage(), compact: true);

        // Bar controls are present, but Create Custom Copy stays hidden until expanded.
        Assert.NotNull(cut.Find("[data-testid='random-preset-scheme']"));
        Assert.NotNull(cut.Find("[data-testid='color-scheme-advanced-toggle']"));
        Assert.Empty(cut.FindAll("[data-testid='create-custom-scheme']"));

        cut.Find("[data-testid='color-scheme-advanced-toggle']").Click();

        Assert.NotNull(cut.Find("[data-testid='create-custom-scheme']"));
    }

    [Fact]
    public void Compact_mode_auto_reveals_random_save_controls()
    {
        var cut = RenderPanel(new FakeKeyValueStorage(), compact: true);

        cut.Find("[data-testid='random-theme-scheme']").Click();

        // Generating a random theme reveals the save + Go Crazy controls without an explicit expand.
        Assert.NotNull(cut.Find("[data-testid='save-random-scheme']"));
        Assert.NotNull(cut.Find("[data-testid='go-crazy-scheme']"));
    }

    [Fact]
    public void Go_crazy_keeps_a_saveable_random_theme()
    {
        var storage = new FakeKeyValueStorage();
        var cut = RenderPanel(storage);

        cut.Find("[data-testid='random-theme-scheme']").Click();
        cut.Find("[data-testid='go-crazy-scheme']").Click();
        cut.Find("[data-testid='random-scheme-name']").Change("Wild One");
        cut.Find("[data-testid='save-random-scheme']").Click();

        var preferences = storage.Get<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences);
        var custom = Assert.Single(preferences!.CustomSchemes);
        Assert.Equal("Wild One", custom.Name);
        Assert.Empty(ColorSchemeCatalog.Validate(custom));
    }

    private IRenderedComponent<ColorSchemePanel> RenderPanel(FakeKeyValueStorage storage, bool compact = false)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage);
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
