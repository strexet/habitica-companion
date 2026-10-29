using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.JSInterop;

namespace Habitica.Storage;

// A local write-ahead journal; no credentials or spell dispatch authority are stored.
public enum GearRecoverySlotStatus { Prepared, InFlight, Acknowledged, Restored, Superseded }
public sealed record GearRecoverySlot(string Slot, string OriginalKey, string TemporaryKey, GearRecoverySlotStatus Status);
public sealed record SpellGearRecovery(Guid OperationId, long Revision, IReadOnlyList<GearRecoverySlot> Slots);

public sealed class SpellGearRecoveryStore : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> Slots = new(StringComparer.Ordinal)
        { "Head", "Head Accessory", "Eyewear", "Armor", "Body", "Weapon", "Shield", "Back" };
    private readonly Lazy<Task<IJSObjectReference>> _module;

    public SpellGearRecoveryStore(IJSRuntime runtime) => _module = new(() => runtime.InvokeAsync<IJSObjectReference>(
        "import", "./js/storage/indexedDbStorage.js").AsTask());

    public async Task<SpellGearRecovery?> LoadAsync(string accountScope, CancellationToken token)
    {
        ValidateScope(accountScope);
        var json = await (await _module.Value).InvokeAsync<string?>("getGearRecovery", token, accountScope);
        if (json is null) return null;
        var value = JsonSerializer.Deserialize<SpellGearRecovery>(json, JsonOptions)
            ?? throw new JsonException("Invalid gear recovery journal.");
        Validate(value);
        return value;
    }

    public async Task<bool> TryCommitAsync(string accountScope, long expectedRevision, SpellGearRecovery value, CancellationToken token)
    {
        ValidateScope(accountScope); Validate(value);
        if (expectedRevision < 0 || value.Revision != checked(expectedRevision + 1))
            throw new ArgumentException("Invalid gear recovery revision.");
        return await (await _module.Value).InvokeAsync<bool>("commitGearRecovery", token,
            accountScope, expectedRevision, JsonSerializer.Serialize(value, JsonOptions));
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(string scope, CancellationToken token)
    {
        ValidateScope(scope);
        var module = await _module.Value;
        var lease = Guid.NewGuid().ToString();
        return await module.InvokeAsync<bool>("acquireGearRecovery", token, scope, lease)
            ? new Lease(module, lease) : null;
    }

    private sealed class Lease(IJSObjectReference module, string id) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => module.InvokeVoidAsync("releaseGearRecovery", id);
    }

    public static void Validate(SpellGearRecovery value)
    {
        if (value.OperationId == Guid.Empty || value.Revision < 1 || value.Revision > 9007199254740991L
            || value.Slots is null || value.Slots.Count is < 1 or > 8
            || value.Slots.Select(slot => slot?.Slot).Distinct(StringComparer.Ordinal).Count() != value.Slots.Count
            || value.Slots.Any(slot => slot is null || !Slots.Contains(slot.Slot) || !Key(slot.OriginalKey) || !Key(slot.TemporaryKey)
                || slot.OriginalKey == slot.TemporaryKey || !Enum.IsDefined(slot.Status)))
            throw new JsonException("Invalid gear recovery journal.");
    }

    private static bool Key(string value) => value is not null && Regex.IsMatch(value, "^[A-Za-z0-9_]{0,128}$", RegexOptions.CultureInvariant);
    private static void ValidateScope(string scope)
    {
        if (scope is null || !Regex.IsMatch(scope, "^[A-Za-z0-9_-]{1,128}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid local account scope.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated) await (await _module.Value).DisposeAsync();
    }
}
