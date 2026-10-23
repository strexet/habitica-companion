using System.Security.Cryptography;
using System.Text;
using Habitica.Domain.Auth;
using Habitica.Domain.User;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed partial class AppSessionController
{
    private readonly SpellGearRecoveryStore? _spellGearRecoveryStore;
    private static string RecoveryScope(string userId) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)));
    private static bool Terminal(GearRecoverySlot slot) => slot.Status is GearRecoverySlotStatus.Restored or GearRecoverySlotStatus.Superseded;

    private async Task<GearRecoveryContext> BeginSpellGearRecoveryAsync(HabiticaCredentials credentials, Guid operation,
        GearSlotsSnapshot temporary, CancellationToken token)
    {
        using var preparationDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        preparationDeadline.CancelAfter(TimeSpan.FromSeconds(20)); token = preparationDeadline.Token;
        var store = _spellGearRecoveryStore!; var scope = RecoveryScope(credentials.UserId);
        var lease = await store.TryAcquireAsync(scope, token) ?? throw new InvalidOperationException("Equipment recovery is active in another tab.");
        try
        {
            var old = await store.LoadAsync(scope, token);
            if (old is not null && old.Slots.Any(slot => !Terminal(slot)))
                throw new InvalidOperationException("Resolve saved equipment recovery before another automatic gear change.");
            var fresh = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, token);
            if (!IsCurrentCloudAccount(credentials)) throw new OperationCanceledException(token);
            var original = NormalizePresetSlots(EquipmentSetKind.Battle, fresh.Equipment.Battle);
            var slots = EnumerateSlots(temporary).Select(slot => new GearRecoverySlot(slot.SlotTitle,
                NormalizeGearKey(GetSlotValue(original, slot.SlotTitle)) ?? "", NormalizeGearKey(slot.Key) ?? "", GearRecoverySlotStatus.Prepared))
                .Where(slot => slot.OriginalKey != slot.TemporaryKey).ToArray();
            SpellGearRecovery? journal = slots.Length == 0 ? null : new(operation, (old?.Revision ?? 0) + 1, slots);
            if (journal is not null && !await store.TryCommitAsync(scope, old?.Revision ?? 0, journal, token))
                throw new InvalidOperationException("Equipment recovery changed in another tab.");
            return new(this, credentials, scope, lease, original, journal);
        }
        catch { await lease.DisposeAsync(); throw; }
    }

    public async Task<GearRecoveryReview> ReviewSpellGearRecoveryAsync(bool restore = false, bool keepCurrent = false, Guid? expectedOperationId = null, CancellationToken token = default)
    {
        var credentials = await ResolveCredentialsAsync(token);
        if (credentials is null || !IsCurrentCloudAccount(credentials) || State.IsBusy || _spellGearRecoveryStore is null)
            return new(null, "Sign in and finish the current action before reviewing equipment.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(20)); token = deadline.Token;
        var scope = RecoveryScope(credentials.UserId);
        IAsyncDisposable? lease;
        try { lease = await _spellGearRecoveryStore.TryAcquireAsync(scope, token); }
        catch { return new(null, "Exclusive equipment recovery is unavailable; no equipment was changed. Reload before checking saved work again."); }
        if (lease is null) return new(null, "Equipment is active in another tab; wait before recovery.");
        await using var held = lease;
        SpellGearRecovery? journal = null;
        string? activeSlot = null;
        SetState(State with { IsBusy = true });
        try
        {
            // Verify an authenticated account read before revealing device-local recovery.
            var fresh = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, token);
            if (!IsCurrentCloudAccount(credentials)) throw new OperationCanceledException(token);
            journal = await _spellGearRecoveryStore.LoadAsync(scope, token);
            if (journal is null || journal.Slots.All(Terminal)) return new(null, "No unresolved equipment recovery.");
            if ((restore || keepCurrent) && journal.OperationId != expectedOperationId)
                return new(journal, "Saved recovery changed; review the operation before applying changes.");
            if (!restore && !keepCurrent) return new(journal, "Review original and temporary slots. Recovery never repeats a spell.");
            foreach (var slot in journal.Slots.Where(slot => !Terminal(slot)).ToArray())
            {
                activeSlot = slot.Slot;
                fresh = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, token);
                if (!IsCurrentCloudAccount(credentials)) throw new OperationCanceledException(token);
                var current = NormalizeGearKey(GetSlotValue(fresh.Equipment.Battle, slot.Slot)) ?? "";
                var status = current == slot.OriginalKey ? GearRecoverySlotStatus.Restored
                    : keepCurrent ? GearRecoverySlotStatus.Superseded : GearRecoverySlotStatus.Acknowledged;
                if (current != slot.OriginalKey && current != slot.TemporaryKey && !keepCurrent)
                    return new(journal, $"{slot.Slot}: a later selection is preserved. Choose Keep current gear to supersede recovery explicitly.");
                var next = journal! with { Revision = journal!.Revision + 1,
                    Slots = journal.Slots.Select(entry => entry.Slot == slot.Slot ? entry with {Status = status} : entry).ToArray() };
                if (!await _spellGearRecoveryStore.TryCommitAsync(scope, journal.Revision, next, token))
                    throw new InvalidOperationException("Recovery revision changed.");
                journal = next;
                if (status != GearRecoverySlotStatus.Acknowledged) continue;
                var context = new GearRecoveryContext(this, credentials, scope, lease, fresh.Equipment.Battle, journal);
                await context.BeforeAsync(slot.Slot, slot.OriginalKey, token);
                journal = context.Journal;
                await _habiticaSyncClient.EquipGearAsync(credentials, EquipmentSetKind.Battle,
                    slot.OriginalKey.Length > 0 ? slot.OriginalKey : slot.TemporaryKey, token);
                await context.AfterAsync(slot.Slot, slot.OriginalKey, token);
                journal = context.Journal;
                await DelayBetweenHabiticaRequestsAsync(token);
            }
            return new(null, "Every saved slot is verified restored or explicitly superseded. No spell was replayed.");
        }
        catch (Exception error)
        {
            return new(IsCurrentCloudAccount(credentials) ? journal : null,
                error is OperationCanceledException ? $"{activeSlot ?? "Equipment"}: recovery stopped; unresolved slots remain saved. Inspect before another attempt."
                : $"{activeSlot ?? "Equipment"}: recovery could not verify every slot. Saved work remains; no automatic toggle retry.");
        }
        finally { if (IsCurrentCloudAccount(credentials)) SetState(State with { IsBusy = false }); }
    }

    private sealed class GearRecoveryContext(AppSessionController owner, HabiticaCredentials credentials, string scope,
        IAsyncDisposable lease, GearSlotsSnapshot original, SpellGearRecovery? journal) : IAsyncDisposable
    {
        public GearSlotsSnapshot Original { get; } = original;
        public SpellGearRecovery? Journal => journal;
        private async Task SaveAsync(string slot, GearRecoverySlotStatus status, CancellationToken token)
        {
            if (journal is null) return;
            var next = journal! with { Revision = journal!.Revision + 1,
                Slots = journal.Slots.Select(entry => entry.Slot == slot ? entry with { Status = status } : entry).ToArray() };
            if (!await owner._spellGearRecoveryStore!.TryCommitAsync(scope, journal.Revision, next, token))
                throw new InvalidOperationException("Equipment recovery revision changed; no toggle was repeated.");
            journal = next;
        }
        private async Task<string> ObserveAsync(string slot, CancellationToken token)
        {
            if (!owner.IsCurrentCloudAccount(credentials)) throw new OperationCanceledException(token);
            var fresh = await owner._habiticaSyncClient.GetUserSnapshotAsync(credentials, token);
            if (!owner.IsCurrentCloudAccount(credentials)) throw new OperationCanceledException(token);
            return NormalizeGearKey(GetSlotValue(fresh.Equipment.Battle, slot)) ?? "";
        }
        public async Task BeforeAsync(string slot, string? key, CancellationToken token)
        {
            var entry = journal?.Slots.SingleOrDefault(entry => entry.Slot == slot);
            if (entry is null) throw new InvalidOperationException("Equipment slot is outside the saved recovery plan.");
            var restoring = (NormalizeGearKey(key) ?? "") == entry.OriginalKey;
            var expected = restoring ? entry.TemporaryKey : entry.OriginalKey;
            if (await ObserveAsync(slot, token) != expected)
                throw new InvalidOperationException($"{slot}: equipment changed; inspect recovery before another toggle.");
            await SaveAsync(slot, GearRecoverySlotStatus.InFlight, token);
        }
        public async Task AfterAsync(string slot, string? key, CancellationToken token)
        {
            var entry = journal!.Slots.Single(entry => entry.Slot == slot);
            var observed = await ObserveAsync(slot, token);
            if (observed != (NormalizeGearKey(key) ?? ""))
                throw new InvalidOperationException($"{slot}: equipment acknowledgement did not verify the expected selection.");
            await SaveAsync(slot, observed == entry.OriginalKey ? GearRecoverySlotStatus.Restored : GearRecoverySlotStatus.Acknowledged, token);
        }
        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
