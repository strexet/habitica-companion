using System.Text.Json;
using Bunit;
using Habitica.Application.Sync;
using Habitica.Domain.Sync;
using Habitica.Storage;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Habitica.WebApp.Theme;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class SettingsPageTests : BunitContext
{
    [Fact]
    public void Renders_export_import_and_cloud_sync_controls()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = new FakeKeyValueStorage();
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddScoped<ColorSchemeService>();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-05-13T02:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                CloudSyncSectionStatuses: new[]
                {
                    new CloudSyncSectionStatus(
                        CloudSyncSection.UserProfile,
                        "user-profile",
                        CloudSyncDirection.Upload,
                        CloudSyncSectionStatusKind.Succeeded,
                        DateTimeOffset.Parse("2026-05-13T02:00:00Z"),
                        128,
                        "Uploaded")
                },
                CloudSyncExcludedSections: new[] { CloudSyncSection.Diagnostics })));

        var cut = Render<SettingsPage>();

        Assert.Contains("Download backup", cut.Markup);
        Assert.Contains("Restore backup", cut.Markup);
        Assert.Contains("Private device sync", cut.Markup);
        Assert.Contains("Color scheme", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='color-scheme-select']"));
        Assert.NotNull(cut.Find("[data-testid='export-local-data']"));
        Assert.NotNull(cut.Find("[data-testid='import-local-data']"));
        Assert.NotNull(cut.Find("[data-testid='push-cloud-sync']"));
        Assert.NotNull(cut.Find("[data-testid='download-cloud-sync']"));
        Assert.Contains("Cloud sync sections", cut.Markup);
        Assert.Contains("User profile", cut.Markup);
        Assert.Contains("Diagnostics", cut.Markup);
        Assert.Contains("Excluded", cut.Markup);
    }

    [Fact]
    public async Task Color_scheme_controls_save_custom_scheme_preferences()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = new FakeKeyValueStorage();
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddScoped<ColorSchemeService>();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty));

        var cut = Render<SettingsPage>();

        cut.Find("[data-testid='color-scheme-select']").Change("habitica");
        cut.Find("[data-testid='create-custom-scheme']").Click();
        cut.Find("[data-testid='custom-scheme-name']").Change("Evening");
        cut.Find("[data-testid='color-token-Primary']").Change("#7354d6");
        cut.Find("[data-testid='save-custom-scheme']").Click();

        var preferences = await storage.GetAsync<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences, CancellationToken.None);
        Assert.NotNull(preferences);
        var custom = Assert.Single(preferences!.CustomSchemes);
        Assert.Equal("Evening", custom.Name);
        Assert.Equal("#7354d6", custom.Tokens.Primary);
        Assert.Equal(custom.Id, preferences.SelectedSchemeId);
        Assert.Contains("Saved Evening.", cut.Markup);
    }

    [Fact]
    public void Color_scheme_controls_reject_invalid_custom_colors()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = new FakeKeyValueStorage();
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddScoped<ColorSchemeService>();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty));

        var cut = Render<SettingsPage>();

        cut.Find("[data-testid='create-custom-scheme']").Click();
        cut.Find("[data-testid='color-token-Primary']").Change("not a color?");
        cut.Find("[data-testid='save-custom-scheme']").Click();

        Assert.Contains("Primary is not a supported color value.", cut.Markup);
    }

    private sealed class FakeKeyValueStorage : IKeyValueStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.TryGetValue(key, out var json)
                ? JsonSerializer.Deserialize<TValue>(json, JsonOptions)
                : default);
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
