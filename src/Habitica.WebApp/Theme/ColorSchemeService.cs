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
        await PersistFastSchemeAsync(activeScheme, preferences);
        return new ColorSchemeState(activeScheme, ColorSchemeCatalog.BuiltInSchemes, preferences.CustomSchemes);
    }

    /// <summary>
    /// Last generated random theme. Held in memory only (never persisted) so the user can switch
    /// to other schemes and return to it within the app session.
    /// </summary>
    public ColorSchemeDefinition? PendingRandomScheme { get; private set; }

    /// <summary>Generate a random theme, hold it as the pending random, and apply it without persisting.</summary>
    /// <param name="chaos">0..1 chaos level: 0 is a calm palette, 1 is maximum hue/saturation madness.</param>
    public async Task<ColorSchemeDefinition> ApplyRandomThemeAsync(double chaos = 0.0, CancellationToken cancellationToken = default)
    {
        PendingRandomScheme = ColorSchemeCatalog.GenerateRandomTheme(chaos: chaos);
        await ApplyTransientAsync(PendingRandomScheme);
        return PendingRandomScheme;
    }

    /// <summary>Re-apply the pending random theme (e.g. after navigating between pages) if one exists.</summary>
    public async Task<bool> ReapplyPendingRandomAsync(CancellationToken cancellationToken = default)
    {
        if (PendingRandomScheme is null)
        {
            return false;
        }

        await ApplyTransientAsync(PendingRandomScheme);
        return true;
    }

    private async Task ApplyTransientAsync(ColorSchemeDefinition scheme)
    {
        await _jsRuntime.InvokeVoidAsync("HabiticaColorScheme.applyColorScheme", scheme);
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
        var customScheme = ColorSchemeCatalog.Complete(scheme) with
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
        var preferences = await _keyValueStorage.GetAsync<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences, cancellationToken)
            ?? await LoadFastPreferencesAsync()
            ?? new ColorSchemePreferences(ColorSchemeCatalog.AlphaId, Array.Empty<ColorSchemeDefinition>());
        return NormalizePreferences(preferences);
    }

    private async Task SavePreferencesAsync(
        ColorSchemePreferences preferences,
        ColorSchemeDefinition activeScheme,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizePreferences(preferences);
        await PersistFastSchemeAsync(activeScheme, normalized);
        await _keyValueStorage.SetAsync(StorageKeys.ColorSchemePreferences, normalized, cancellationToken);
    }

    private async Task PersistFastSchemeAsync(ColorSchemeDefinition activeScheme, ColorSchemePreferences preferences)
    {
        await _jsRuntime.InvokeVoidAsync("HabiticaColorScheme.applyAndStore", activeScheme, preferences);
    }

    private async Task<ColorSchemePreferences?> LoadFastPreferencesAsync()
    {
        try
        {
            var preferences = await _jsRuntime.InvokeAsync<ColorSchemePreferences?>("HabiticaColorScheme.getPreferences");
            return preferences is null ? null : NormalizePreferences(preferences);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    private static ColorSchemePreferences NormalizePreferences(ColorSchemePreferences preferences)
    {
        var customSchemes = preferences.CustomSchemes?
            .Where(static scheme => scheme is not null)
            .Select(ColorSchemeCatalog.Complete)
            .Where(static scheme => !scheme.IsBuiltIn)
            .OrderBy(static scheme => scheme.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<ColorSchemeDefinition>();
        var activeScheme = ColorSchemeCatalog.Resolve(preferences.SelectedSchemeId, customSchemes);
        return new ColorSchemePreferences(activeScheme.Id, customSchemes);
    }
}
