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

    /// <summary>
    /// True while the transient random theme is the active selection. Cleared whenever the user
    /// selects, saves, or deletes a persisted scheme, so re-mounting the panel (e.g. after folding
    /// and reopening the Dashboard appearance section) does not snap back to the random theme.
    /// </summary>
    public bool RandomActive { get; private set; }

    // Seed of the current pending random theme. Reused by AdjustRandomChaosAsync so dragging the
    // chaos slider morphs the same palette continuously instead of rolling an unrelated one.
    private int _randomSeed;

    /// <summary>Roll a new random theme, hold it as the pending random, and apply it without persisting.</summary>
    /// <param name="chaos">0..1 chaos level: 0 is a calm palette, 1 is maximum hue/saturation madness.</param>
    public async Task<ColorSchemeDefinition> ApplyRandomThemeAsync(double chaos = 0.0, CancellationToken cancellationToken = default)
    {
        _randomSeed = Random.Shared.Next();
        PendingRandomScheme = ColorSchemeCatalog.GenerateRandomTheme(new Random(_randomSeed), chaos);
        RandomActive = true;
        await ApplyTransientAsync(PendingRandomScheme);
        return PendingRandomScheme;
    }

    /// <summary>
    /// Re-render the current pending random at a new chaos using the same seed, so the palette
    /// morphs smoothly as the user drags the chaos slider rather than jumping to a new theme.
    /// </summary>
    public async Task<ColorSchemeDefinition?> AdjustRandomChaosAsync(double chaos, CancellationToken cancellationToken = default)
    {
        if (PendingRandomScheme is null)
        {
            return null;
        }

        PendingRandomScheme = ColorSchemeCatalog.GenerateRandomTheme(new Random(_randomSeed), chaos);
        RandomActive = true;
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

        RandomActive = true;
        await ApplyTransientAsync(PendingRandomScheme);
        return true;
    }

    /// <summary>
    /// Apply a scheme to the live theme without persisting it, e.g. to preview a pasted or edited
    /// draft. The stored preferences and selected scheme are left untouched until the user saves.
    /// </summary>
    public Task PreviewAsync(ColorSchemeDefinition scheme, CancellationToken cancellationToken = default)
        => ApplyTransientAsync(scheme);

    private async Task ApplyTransientAsync(ColorSchemeDefinition scheme)
    {
        await _jsRuntime.InvokeVoidAsync("HabiticaColorScheme.applyColorScheme", scheme);
    }

    public async Task<ColorSchemeState> SelectAsync(string schemeId, CancellationToken cancellationToken = default)
    {
        RandomActive = false;
        var preferences = await LoadPreferencesAsync(cancellationToken);
        var activeScheme = ColorSchemeCatalog.Resolve(schemeId, preferences.CustomSchemes);
        // Stamp the selection change so cross-device merge knows which device chose more recently.
        var updated = preferences with
        {
            SelectedSchemeId = activeScheme.Id,
            SelectedAtUtc = DateTimeOffset.UtcNow
        };
        await SavePreferencesAsync(updated, activeScheme, cancellationToken);
        return new ColorSchemeState(activeScheme, ColorSchemeCatalog.BuiltInSchemes, updated.CustomSchemes);
    }

    public async Task<(ColorSchemeState State, IReadOnlyList<string> Errors)> SaveCustomAsync(
        ColorSchemeDefinition scheme,
        bool select,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var customScheme = ColorSchemeCatalog.Complete(scheme) with
        {
            Name = ColorSchemeCatalog.NormalizeName(scheme.Name, "Custom scheme"),
            IsBuiltIn = false,
            // Stamp every save so cross-device merge picks the newer edit per scheme id.
            UpdatedAtUtc = now
        };
        var errors = ColorSchemeCatalog.Validate(customScheme);
        if (errors.Count > 0)
        {
            return (await LoadAsync(cancellationToken), errors);
        }

        if (select)
        {
            RandomActive = false;
        }

        var preferences = await LoadPreferencesAsync(cancellationToken);
        var customSchemes = preferences.CustomSchemes
            .Where(existing => !string.Equals(existing.Id, customScheme.Id, StringComparison.Ordinal))
            .Append(customScheme)
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var selectedSchemeId = select ? customScheme.Id : preferences.SelectedSchemeId;
        var activeScheme = ColorSchemeCatalog.Resolve(selectedSchemeId, customSchemes);
        var selectedAtUtc = select ? now : preferences.SelectedAtUtc;
        var updated = new ColorSchemePreferences(activeScheme.Id, customSchemes, selectedAtUtc);
        await SavePreferencesAsync(updated, activeScheme, cancellationToken);
        return (new ColorSchemeState(activeScheme, ColorSchemeCatalog.BuiltInSchemes, customSchemes), Array.Empty<string>());
    }

    public async Task<ColorSchemeState> DeleteCustomAsync(string schemeId, CancellationToken cancellationToken = default)
    {
        RandomActive = false;
        var preferences = await LoadPreferencesAsync(cancellationToken);
        var customSchemes = preferences.CustomSchemes
            .Where(scheme => !string.Equals(scheme.Id, schemeId, StringComparison.Ordinal))
            .ToArray();
        var deletedActive = string.Equals(preferences.SelectedSchemeId, schemeId, StringComparison.Ordinal);
        var selectedSchemeId = deletedActive ? ColorSchemeCatalog.AlphaId : preferences.SelectedSchemeId;
        var activeScheme = ColorSchemeCatalog.Resolve(selectedSchemeId, customSchemes);
        // Falling back to Alpha because the active scheme was deleted is itself a selection change,
        // so bump the timestamp; otherwise leave the prior selection stamp intact.
        var selectedAtUtc = deletedActive ? DateTimeOffset.UtcNow : preferences.SelectedAtUtc;
        var updated = new ColorSchemePreferences(activeScheme.Id, customSchemes, selectedAtUtc);
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
        return new ColorSchemePreferences(activeScheme.Id, customSchemes, preferences.SelectedAtUtc);
    }
}
