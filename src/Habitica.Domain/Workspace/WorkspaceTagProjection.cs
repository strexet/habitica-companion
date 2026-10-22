using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public enum WorkspaceTagState { Pending, Confirmed, UnknownOutcome, NeedsReview }
public enum WorkspaceTagOperation { EnsureTag, SetMembership }
public sealed record WorkspaceTagEvidence(Guid ReceiptId, DateTimeOffset ConfirmedAtUtc, string Fingerprint);
public sealed record WorkspaceTagMember(Guid TaskId, string? HabiticaTaskId, [property: JsonRequired] bool Present,
    [property: JsonRequired] WorkspaceTagState State = WorkspaceTagState.Pending, WorkspaceTagEvidence? Evidence = null);
public sealed record WorkspaceTagMapping(Guid Id, long Revision, Guid EntityId,
    [property: JsonRequired] WorkspaceOrganizationKind Kind, [property: JsonRequired] WorkspaceTagState State,
    long DesiredRevision, Guid CorrelationId, string CorrelationAlias, string DesiredName,
    string? HabiticaTagId = null, WorkspaceTagEvidence? Evidence = null, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record WorkspaceTagIntent(Guid Id, long Revision, Guid MappingId, Guid CommandId, Guid CorrelationId,
    long DesiredRevision, [property: JsonRequired] WorkspaceTagOperation Operation, [property: JsonRequired] WorkspaceTagState State,
    string? HabiticaTagId = null, string? DesiredName = null, IReadOnlyList<WorkspaceTagMember>? Members = null, WorkspaceTagEvidence? Evidence = null,
    bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record WorkspaceTagLedger(Guid AccountId, [property: JsonRequired] IReadOnlyList<WorkspaceTagMapping> Mappings,
    [property: JsonRequired] IReadOnlyList<WorkspaceTagIntent> Intents);

/// <summary>Portable evidence only. Importing the ledger grants neither consent nor dispatch authority.</summary>
public static class WorkspaceTagProjection
{
    public static string Alias(Guid workspaceId, WorkspaceOrganizationKind kind, Guid entityId) =>
        $"companion_tag_{workspaceId:N}_{System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(kind.ToString())}_{entityId:N}";
    private static bool RemoteId(string? value) => value is null || Guid.TryParseExact(value,"D",out var id) && id!=Guid.Empty;
    private static bool Evidence(WorkspaceTagEvidence? value) => value is null || value.ReceiptId!=Guid.Empty
        && value.ConfirmedAtUtc!=default && value.ConfirmedAtUtc.Offset==TimeSpan.Zero && value.Fingerprint is not null
        && System.Text.RegularExpressions.Regex.IsMatch(value.Fingerprint,"^[a-f0-9]{64}$");
    public static bool IsValid(WorkspaceDocument document)
    {
        var ledger=document.TagLedger;if(ledger is null)return true;
        if(ledger.AccountId==Guid.Empty||ledger.Mappings is null||ledger.Intents is null)return false;
        var knownGroups=new HashSet<(WorkspaceOrganizationKind,Guid)>();
        void Groups(IEnumerable<WorkspaceEntity> rows,WorkspaceOrganizationKind kind) { foreach(var row in rows)knownGroups.Add((kind,row.Id)); }
        Groups(document.Categories,WorkspaceOrganizationKind.Category);Groups(document.Projects,WorkspaceOrganizationKind.Project);
        Groups(document.Sections,WorkspaceOrganizationKind.Section);Groups(document.Labels,WorkspaceOrganizationKind.Label);
        Groups(document.Goals,WorkspaceOrganizationKind.Goal);Groups(document.SavedTaskViews??[],WorkspaceOrganizationKind.SavedTaskView);
        var groups=new HashSet<Guid>();var remote=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappings=new Dictionary<Guid,WorkspaceTagMapping>();var ids=new HashSet<Guid>();
        foreach(var mapping in ledger.Mappings)
        {
            if(mapping is null||mapping.Id==Guid.Empty||!ids.Add(mapping.Id)||mapping.Revision<1||mapping.Revision>document.Revision||!Enum.IsDefined(mapping.Kind)||!Enum.IsDefined(mapping.State)||!groups.Add(mapping.EntityId)
                ||!mappings.TryAdd(mapping.Id,mapping)||!knownGroups.Contains((mapping.Kind,mapping.EntityId))
                ||mapping.DesiredRevision<1||mapping.DesiredRevision>mapping.Revision||mapping.CorrelationId==Guid.Empty
                ||mapping.CorrelationAlias!=Alias(document.Id,mapping.Kind,mapping.EntityId)
                ||string.IsNullOrWhiteSpace(mapping.DesiredName)||mapping.DesiredName.Length>256
                ||!RemoteId(mapping.HabiticaTagId)||mapping.HabiticaTagId is {} id&&!remote.Add(id)||!Evidence(mapping.Evidence)
                ||mapping.State==WorkspaceTagState.Confirmed&&(mapping.HabiticaTagId is null||mapping.Evidence is null)
                ||mapping.Evidence is not null&&mapping.HabiticaTagId is null)return false;
        }
        var commands=new HashSet<(Guid,Guid,WorkspaceTagOperation)>();var tasks=document.Tasks.Select(task=>task.Id).ToHashSet();
        foreach(var intent in ledger.Intents)
        {
            if(intent is null||intent.Id==Guid.Empty||!ids.Add(intent.Id)||intent.Revision<1||intent.Revision>document.Revision||!Enum.IsDefined(intent.Operation)||!Enum.IsDefined(intent.State)
                ||!mappings.TryGetValue(intent.MappingId,out var mapping)||intent.CommandId==Guid.Empty||intent.CorrelationId==Guid.Empty
                ||!commands.Add((intent.CommandId,intent.MappingId,intent.Operation))||intent.DesiredRevision<1
                ||intent.DesiredRevision>intent.Revision||intent.DesiredRevision>mapping.DesiredRevision||!Evidence(intent.Evidence)||!RemoteId(intent.HabiticaTagId)
                ||intent.Evidence is not null&&intent.HabiticaTagId is null
                ||intent.State==WorkspaceTagState.Confirmed&&(intent.Evidence is null||intent.HabiticaTagId is null))return false;
            if(intent.Operation==WorkspaceTagOperation.EnsureTag)
            {
                if(string.IsNullOrWhiteSpace(intent.DesiredName)||intent.DesiredName.Length>256||intent.Members is not null)return false;
            }
            else if(intent.DesiredName is not null||intent.Members is null||intent.Members.Count>100000
                ||intent.Members.Any(member=>member is null||!tasks.Contains(member.TaskId)||!RemoteId(member.HabiticaTaskId)
                    ||!Enum.IsDefined(member.State)||!Evidence(member.Evidence)
                    ||member.Evidence is not null&&(member.HabiticaTaskId is null||intent.HabiticaTagId is null)
                    ||member.State==WorkspaceTagState.Confirmed&&(member.Evidence is null||member.HabiticaTaskId is null||intent.HabiticaTagId is null)
                    ||intent.State==WorkspaceTagState.Confirmed&&member.State!=WorkspaceTagState.Confirmed)
                ||intent.Members.Select(member=>member.TaskId).Distinct().Count()!=intent.Members.Count
                ||intent.Members.Where(member=>member.HabiticaTaskId is not null).Select(member=>member.HabiticaTaskId!).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=intent.Members.Count(member=>member.HabiticaTaskId is not null))return false;
        }
        return true;
    }
}
