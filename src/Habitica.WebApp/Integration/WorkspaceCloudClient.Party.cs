using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Application.Workspace;

namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient
{
    private static readonly JsonSerializerOptions PartyJson = new(JsonSerializerDefaults.Web);
    public async Task<PartyPlanningResult> ManagePartyAsync(PartyPlanningRequest input, CancellationToken cancellationToken)
    {
        if (!PartyPlanningAvailable) return new(false, "Authenticated party planning is unavailable on this app.");
        var generation = _generation;
        var own = input.Action is PartyPlanningAction.ExportOwn or PartyPlanningAction.RemoveOwn;
        if (!own && WorkspaceId is null) return new(false, "Connect the cloud workspace first.");
        if (input.Action is PartyPlanningAction.Settings or PartyPlanningAction.DeleteNamespace
            && (input.NamespaceId is null || input.NamespaceId == Guid.Empty || input.Revision is null or < 1))
            return new(false, "Load current party planning before confirming this action.");
        if (input.Action == PartyPlanningAction.Consent && (input.ExpectedPartyId is null || input.ExpectedPartyId == Guid.Empty)) return new(false, "Check the current party before consenting.");
        if (input.Action == PartyPlanningAction.Settings && input.MembersCanQueue is null) return new(false, "Choose the queue permission explicitly.");
        try
        {
            var (method, path, body) = input.Action switch
            {
                PartyPlanningAction.Context => (HttpMethod.Get, "party/context", (object?)null),
                PartyPlanningAction.State => (HttpMethod.Get, "party/state", null),
                PartyPlanningAction.Consent => (HttpMethod.Post, "party/consent", new { consentVersion = "party-planning-v1", expectedPartyId = input.ExpectedPartyId }),
                PartyPlanningAction.Settings => (HttpMethod.Post, "party/settings", new { expectedNamespaceId = input.NamespaceId, expectedRevision = input.Revision, membersCanQueue = input.MembersCanQueue }),
                PartyPlanningAction.DeleteNamespace => (HttpMethod.Delete, "party/planning", new { expectedNamespaceId = input.NamespaceId, expectedRevision = input.Revision, confirmNamespaceId = input.NamespaceId }),
                PartyPlanningAction.ExportOwn => (HttpMethod.Get, "party/contributions", null),
                PartyPlanningAction.RemoveOwn => (HttpMethod.Post, "party/contributions", new { confirm = "remove-my-party-contributions" }),
                _ => throw new InvalidOperationException()
            };
            using var request = own ? _vault.WorkspaceHandshake(new Uri(_baseUri!, path)) : Request(method, path);
            request.Method = method; request.Content = body is null ? null : JsonContent.Create(body);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Party planning is unavailable or changed. Revalidate access and load current state before another action.");
            var json = await BoundedTextAsync(response.Content, cancellationToken);
            if (generation != _generation) return new(false, "The account session changed; the party result was discarded.");
            if (input.Action is PartyPlanningAction.Context or PartyPlanningAction.State)
            {
                var value = JsonSerializer.Deserialize<PartyPlanningView>(json, PartyJson);
                if (value?.Party is null || value.Party.Id == Guid.Empty || value.Party.Name is null || value.Party.Name.Length > 256
                    || value.MemberRole is not ("leader" or "member") || value.Provenance != "habitica-verified-party"
                    || value.RetrievedAtUtc < DateTimeOffset.UtcNow.AddMinutes(-2) || value.RetrievedAtUtc > DateTimeOffset.UtcNow.AddSeconds(30)
                    || value.Party.MemberCount is < 1 || input.Action == PartyPlanningAction.State && (value.NamespaceId is null || value.NamespaceId == Guid.Empty || value.Revision is null or < 1 || value.MembersCanQueue is null || value.Queue is null || value.Queue.Count > 100)
                    || value.Queue?.Any(q => q.Id == Guid.Empty || q.ContributorLabel is null || q.ContributorLabel.Length > 120 || !ValidQuest(q.QuestKey) || q.VoteCount is < 0 or > 500) == true)
                    return new(false, "Party planning response could not be validated.");
                return new(true, "Verified party planning received.", value);
            }
            if (input.Action == PartyPlanningAction.ExportOwn)
            {
                var archive = JsonSerializer.Deserialize<ContributionArchive>(json, PartyJson);
                if (archive?.Contributions is null || archive.Commands is null || archive.Contributions.Length > 64 || archive.Commands.Length > 1000
                    || archive.Contributions.Any(c => c.PartyId == Guid.Empty || c.NamespaceId == Guid.Empty || c.Queue is null || c.Queue.Length > 100 || c.Votes is null || c.Votes.Length > 500
                        || c.Queue.Any(q => q.Id == Guid.Empty || !ValidQuest(q.QuestKey) || q.Label is null || q.Label.Length > 120) || c.Votes.Any(v => v.QueueItemId == Guid.Empty))
                    || archive.Commands.Any(c => c.CommandId == Guid.Empty || c.NamespaceId == Guid.Empty || c.QueueItemId == Guid.Empty || !c.SavedInCompanion || c.Revision < 1 || c.Operation is not ("queue_quest" or "vote_quest") || c.HabiticaEffects != "none"))
                    return new(false, "The contribution export could not be validated.");
                return new(true, "Export includes readable own quest contributions and planning receipts. Keep the file private.", ExportJson: JsonSerializer.Serialize(archive, PartyJson));
            }
            using var document = JsonDocument.Parse(json);
            var field = input.Action switch { PartyPlanningAction.Consent => "consented", PartyPlanningAction.Settings => "saved", PartyPlanningAction.DeleteNamespace => "deleted", _ => "removed" };
            if (!document.RootElement.TryGetProperty(field, out var confirmed) || confirmed.ValueKind != JsonValueKind.True) return new(false, "The party action could not be confirmed. Check current state before another action.");
            return new(true, input.Action switch { PartyPlanningAction.Consent => "Authenticated shared planning enabled for the verified party.", PartyPlanningAction.Settings => "Authenticated queue permission saved.", PartyPlanningAction.DeleteNamespace => "Authenticated party planning deleted. Legacy planning and Habitica records are separate.", _ => "Own authenticated contributions and consent removed. Other members' unrelated contributions remain." });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Party action outcome could not be confirmed. Check current state before another action."); }
    }
    private static bool ValidQuest(string? key) => key is not null && System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z0-9_-]{1,128}$");
    private sealed record OwnQueue(Guid Id, string QuestKey, string Label, DateTimeOffset CreatedAtUtc);
    private sealed record OwnVote(Guid QueueItemId);
    private sealed record Contribution(Guid PartyId, Guid NamespaceId, OwnQueue[] Queue, OwnVote[] Votes);
    private sealed record PlanningReceipt(Guid CommandId, string Operation, Guid NamespaceId, long Revision, Guid QueueItemId, bool? Voted, bool SavedInCompanion, string HabiticaEffects, DateTimeOffset AcceptedAtUtc);
    private sealed record ContributionArchive(Contribution[] Contributions, PlanningReceipt[] Commands);
}
