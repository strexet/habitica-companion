using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Application.Integration;
using Habitica.Storage;

namespace Habitica.Application.Workspace;

public sealed record CompanionAccountPreference(long Revision, Guid MutationId, bool CloudSelected,
    Guid? WorkspaceId, Guid? NotebookId, IntegrationRecoveryDescriptor? Recovery,
    bool RecoverWorkspace, bool RecoverNotes, bool Conflict, IReadOnlyList<Guid> RevokedConnections);

public sealed record CompanionPreferences(int Version, IReadOnlyDictionary<string, CompanionAccountPreference> Accounts);

public static class CompanionPreferencesJson
{
    public const int MaxBytes = 65536;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static string AccountScope(string account) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Guid.Parse(account).ToString("D")))).ToLowerInvariant();
    public static CompanionPreferences Empty() => new(1, new Dictionary<string, CompanionAccountPreference>(StringComparer.Ordinal));
    public static CompanionAccountPreference Default() => new(0, Guid.Empty, false, null, null, null, false, false, false, []);

    public static CompanionPreferences Read(string? text)
    {
        if (text is null) return Empty();
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes) throw new JsonException("Companion preferences exceed the limit.");
        var value = JsonSerializer.Deserialize<CompanionPreferences>(text, Json) ?? throw new JsonException("Missing Companion preferences.");
        if (value.Version != 1 || value.Accounts is null || value.Accounts.Count > 16) throw new JsonException("Unsupported Companion preferences.");
        foreach (var (scope, entry) in value.Accounts)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(scope, "^[a-f0-9]{64}$") || entry is null
                || entry.Revision is < 1 or > 9007199254740991 || entry.MutationId == Guid.Empty
                || entry.WorkspaceId == Guid.Empty || entry.NotebookId == Guid.Empty
                || entry.CloudSelected && !entry.Conflict && entry.WorkspaceId is null
                || entry.Recovery is not null && !entry.Recovery.IsValid
                || entry.RevokedConnections is null || entry.RevokedConnections.Count > 32
                || entry.RevokedConnections.Any(id => id == Guid.Empty)
                || entry.RevokedConnections.Distinct().Count() != entry.RevokedConnections.Count)
                throw new JsonException("Invalid account preferences.");
        }
        return value;
    }

    public static string Write(CompanionPreferences value)
    {
        var normalized = value with { Accounts = value.Accounts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value with { RevokedConnections = pair.Value.RevokedConnections.Order().ToArray() }, StringComparer.Ordinal) };
        var text = JsonSerializer.Serialize(normalized, Json); _ = Read(text); return text;
    }

    public static string Merge(string? leftText, string rightText)
    {
        var left = Read(leftText); var right = Read(rightText);
        var accounts = left.Accounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var (scope, incoming) in right.Accounts)
        {
            if (!accounts.TryGetValue(scope, out var current)) { accounts.Add(scope, incoming); continue; }
            var revoked = current.RevokedConnections.Concat(incoming.RevokedConnections).Distinct().Order().ToArray();
            CompanionAccountPreference chosen;
            if (current.Revision == incoming.Revision)
            {
                if (current.MutationId == incoming.MutationId)
                {
                    if (JsonSerializer.Serialize(current with { RevokedConnections = current.RevokedConnections.Order().ToArray() }, Json)
                        != JsonSerializer.Serialize(incoming with { RevokedConnections = incoming.RevokedConnections.Order().ToArray() }, Json)) throw new JsonException("Preference mutation identity was reused.");
                    chosen = current;
                }
                else
                {
                    var ids = new[] { current.MutationId.ToString("D"), incoming.MutationId.ToString("D") }.Order(StringComparer.Ordinal);
                    var mutation = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(":", ids))).AsSpan(0, 16));
                    chosen = new(checked(current.Revision + 1), mutation, current.CloudSelected || incoming.CloudSelected,
                        current.WorkspaceId == incoming.WorkspaceId ? current.WorkspaceId : null,
                        current.NotebookId == incoming.NotebookId ? current.NotebookId : null, null, false, false, true, revoked);
                }
            }
            else chosen = current.Revision > incoming.Revision ? current : incoming;
            // Cloud choice is monotonic until a separate reviewed reverse-transfer protocol exists.
            if (!chosen.CloudSelected && (current.CloudSelected || incoming.CloudSelected))
            {
                var cloud = current.CloudSelected ? current : incoming;
                chosen = chosen with { CloudSelected = true, WorkspaceId = cloud.WorkspaceId, Conflict = true, Recovery = null, RecoverWorkspace = false, RecoverNotes = false };
            }
            accounts[scope] = chosen with { RevokedConnections = revoked };
        }
        return Write(new(1, accounts));
    }

    public static string OnlyAccount(string text, string account)
    {
        var document = Read(text); var scope = AccountScope(account);
        return Write(new(1, document.Accounts.Where(pair => pair.Key == scope).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)));
    }
}

public sealed class CompanionPreferencesStore(IKeyValueStorage storage, LocalPreferenceMutationGate gate)
{
    public async Task<CompanionAccountPreference> ReadAsync(string account, CancellationToken token)
    {
        var document = CompanionPreferencesJson.Read(await storage.GetRawJsonAsync(StorageKeys.CompanionPreferences, token));
        return document.Accounts.GetValueOrDefault(CompanionPreferencesJson.AccountScope(account)) ?? CompanionPreferencesJson.Default();
    }

    public async Task<CompanionAccountPreference> UpdateAsync(string account, Func<CompanionAccountPreference, CompanionAccountPreference> update,
        CancellationToken token, Func<bool>? current = null)
    {
        await gate.Lock.WaitAsync(token);
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var old = await storage.GetRawJsonAsync(StorageKeys.CompanionPreferences, token);
                var document = CompanionPreferencesJson.Read(old); var scope = CompanionPreferencesJson.AccountScope(account);
                var previous = document.Accounts.GetValueOrDefault(scope) ?? CompanionPreferencesJson.Default();
                var entry = update(previous) with { Revision = checked(previous.Revision + 1), MutationId = Guid.NewGuid() };
                var accounts = document.Accounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal); accounts[scope] = entry;
                var json = CompanionPreferencesJson.Write(new(1, accounts));
                if (current?.Invoke() == false) throw new OperationCanceledException("The account changed.");
                if (storage is ICompanionPreferencesStorage atomic)
                { if (!await atomic.TryCommitCompanionPreferencesAsync(old, json, token)) continue; }
                else await storage.SetRawJsonAsync(StorageKeys.CompanionPreferences, json, token);
                return entry;
            }
            throw new InvalidOperationException("Account preferences changed concurrently. Reload before retrying.");
        }
        finally { gate.Lock.Release(); }
    }
}

public sealed class LocalPreferenceMutationGate { public SemaphoreSlim Lock { get; } = new(1, 1); }
