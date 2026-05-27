using Habitica.Storage;
using Microsoft.JSInterop;

namespace Habitica.WebApp.Theme;

public sealed class ColorSchemeService
{
    private readonly IKeyValueStorage _keyValueStorage;
    private readonly IJSRuntime _jsRuntime;

    public ColorSchemeService(IKeyValueStorage keyValueStorage, IJSRuntime jsRuntime)
    {
        _keyValueStorage = keyValueStorage;
        _jsRuntime = jsRuntime;
    }

    public async Task<ColorSchemeState> LoadAsync(CancellationToken cancellationToken = default)
    {
        var preferences = await LoadPreferencesAsync(cancellationToken);
        var activeScheme = ColorSchemeCatalog.Resolve(preferences.SelectedSchemeId, preferences.CustomSchemes);
        await PersistFastSchemeAsync(activeScheme);
        return new ColorSchemeState(activeScheme, ColorSchemeCatalog.BuiltInSchemes, preferences.CustomSchemes);
    }

    public async Task<ColorSchemeState> SelectAsync(string schemeId, CancellationToken cancellationToken = default)
    {
        var preferences = await LoadPreferencesAsync(cancellationToken);
        var activeScheme = ColorSchemeCatalog.Resolve(schemeId, preferences.CustomSchemes);
        var updated = preferences with { SelectedSchemeId = activeScheme.Id };
        await SavePreferencesAsync(updated, activeScheme, cancellationToken);
        return new ColorSchemeState(activeScheme, ColorSchemeCatalog.BuiltInSchemes, updated.CustomSchemes);
    }

    public async Task<(ColorSchemeState State, IReadOnlyList<string> Errors)> SaveCustomAsync(
        ColorSchemeDefinition scheme,
        bool select,
        CancellationToken cancellationToken = default)
    {
        var customScheme = scheme with
        {
            Name = ColorSchemeCatalog.NormalizeName(scheme.Name, "Custom scheme"),
            IsBuiltIn = false
        };
        var errors = ColorSchemeCatalog.Validate(customScheme);
        if (errors.Count > 0)
        {
            return (await LoadAsync(cancellationToken), errors);
        }

        var preferences = await LoadPreferencesAsync(cancellationToken);
        var customSchemes = preferences.CustomSchemes
            .Where(existing => !string.Equals(existing.Id, customScheme.Id, StringComparison.Ordinal))
            .Append(customScheme)
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var selectedSchemeId = select ? customScheme.Id : preferences.SelectedSchemeId;
        var activeScheme = ColorSchemeCatalog.Resolve(selectedSchemeId, customSchemes);
        var updated = new ColorSchemePreferences(activeScheme.Id, customSchemes);
        await SavePreferencesAsync(updated, activeScheme, cancellationToken);
        return (new ColorSchemeState(activeScheme, ColorSchemeCatalog.BuiltInSchemes, customSchemes), Array.Empty<string>());
    }

    public async Task<ColorSchemeState> DeleteCustomAsync(string schemeId, CancellationToken cancellationToken = default)
    {
        var preferences = await LoadPreferencesAsync(cancellationToken);
        var customSchemes = preferences.CustomSchemes
            .Where(scheme => !string.Equals(scheme.Id, schemeId, StringComparison.Ordinal))
            .ToArray();
        var selectedSchemeId = string.Equals(preferences.SelectedSchemeId, schemeId, StringComparison.Ordinal)
            ? ColorSchemeCatalog.AlphaId
            : preferences.SelectedSchemeId;
        var activeScheme = ColorSchemeCatalog.Resolve(selectedSchemeId, customSchemes);
        var updated = new ColorSchemePreferences(activeScheme.Id, customSchemes);
        await SavePreferencesAsync(updated, activeScheme, cancellationToken);
        return new ColorSchemeState(activeScheme, ColorSchemeCatalog.BuiltInSchemes, customSchemes);
    }

    private async Task<ColorSchemePreferences> LoadPreferencesAsync(CancellationToken cancellationToken)
    {
        return await _keyValueStorage.GetAsync<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences, cancellationToken)
            ?? new ColorSchemePreferences(ColorSchemeCatalog.AlphaId, Array.Empty<ColorSchemeDefinition>());
    }

    private async Task SavePreferencesAsync(
        ColorSchemePreferences preferences,
        ColorSchemeDefinition activeScheme,
        CancellationToken cancellationToken)
    {
        await _keyValueStorage.SetAsync(StorageKeys.ColorSchemePreferences, preferences, cancellationToken);
        await PersistFastSchemeAsync(activeScheme);
    }

    private async Task PersistFastSchemeAsync(ColorSchemeDefinition activeScheme)
    {
        await _jsRuntime.InvokeVoidAsync("HabiticaColorScheme.applyAndStore", activeScheme);
    }
}
