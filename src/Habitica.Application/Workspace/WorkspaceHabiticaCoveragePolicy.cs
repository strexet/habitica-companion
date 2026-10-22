using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public enum HabiticaCoverageState { Missing, Pending, Linked, Retained, NeedsReview }
public sealed record WorkspaceHabiticaTaskCoverage(Guid TaskId, CompanionTaskKind Kind, bool Archived,
    HabiticaCoverageState State, string? HabiticaTaskId);
public sealed record WorkspaceHabiticaTagRequirement(Guid EntityId, WorkspaceOrganizationKind Kind, bool Archived,
    HabiticaCoverageState State = HabiticaCoverageState.Missing, string? HabiticaTagId = null,
    HabiticaCoverageState MembershipState = HabiticaCoverageState.Missing);
public sealed record WorkspaceHabiticaCoverage(IReadOnlyList<WorkspaceHabiticaTaskCoverage> Tasks,
    IReadOnlyList<WorkspaceHabiticaTagRequirement> RequiredTags)
{
    public int MissingTaskLinks => Tasks.Count(task => task.State == HabiticaCoverageState.Missing);
    public int PendingTaskLinks => Tasks.Count(task => task.State == HabiticaCoverageState.Pending);
    public int TaskLinksNeedingReview => Tasks.Count(task => task.State == HabiticaCoverageState.NeedsReview);
    public int StoredTaskLinks => Tasks.Count(task => task.State is HabiticaCoverageState.Linked or HabiticaCoverageState.Retained);
    public int StoredGroupTagLinks => RequiredTags.Count(tag => tag.State is HabiticaCoverageState.Linked or HabiticaCoverageState.Retained);
    public int PendingGroupTagLinks => RequiredTags.Count(tag => tag.State == HabiticaCoverageState.Pending);
    public int GroupTagLinksNeedingReview => RequiredTags.Count(tag => tag.State == HabiticaCoverageState.NeedsReview);
    public int MissingGroupTagLinks => RequiredTags.Count - StoredGroupTagLinks;
    public int ConfirmedRecordedMemberships => RequiredTags.Count(tag => tag.MembershipState is HabiticaCoverageState.Linked or HabiticaCoverageState.Retained);
    public bool HasCompleteStoredCoverage => StoredTaskLinks == Tasks.Count && MissingGroupTagLinks == 0;
}

/// <summary>Required correspondence, including history. This read audit creates no game effects.</summary>
public static class WorkspaceHabiticaCoveragePolicy
{
    public static WorkspaceHabiticaCoverage Audit(WorkspaceDocument document, Guid? expectedAccountId = null)
    {
        var mappings = document.Projections.ToLookup(mapping => mapping.TaskId);
        var tasks = document.Tasks.Select(task =>
        {
            var candidates = mappings[task.Id].ToArray();
            var mapping = candidates.Length == 1 ? candidates[0] : null;
            var state = candidates.Length > 1 ? HabiticaCoverageState.NeedsReview : mapping switch
            {
                null => HabiticaCoverageState.Missing,
                { State: ProjectionState.Unlinked, HabiticaTaskId: null } => HabiticaCoverageState.Missing,
                { IsArchived: true } when !task.IsArchived => HabiticaCoverageState.NeedsReview,
                { State: ProjectionState.Pending } => HabiticaCoverageState.Pending,
                { State: ProjectionState.Linked, HabiticaTaskId: { Length: > 0 } } => task.IsArchived
                    ? HabiticaCoverageState.Retained : HabiticaCoverageState.Linked,
                _ => HabiticaCoverageState.NeedsReview
            };
            return new WorkspaceHabiticaTaskCoverage(task.Id, task.Kind, task.IsArchived, state, mapping?.HabiticaTaskId);
        }).ToArray();
        var tags = new List<WorkspaceHabiticaTagRequirement>();
        var tagLedgerValid=WorkspaceTagProjection.IsValid(document);
        var tagMappings=(tagLedgerValid?document.TagLedger?.Mappings:null)?.ToLookup(row=>row.EntityId);
        var tagIntents=(tagLedgerValid?document.TagLedger?.Intents:null)?.ToLookup(row=>row.MappingId);
        void Add(IEnumerable<WorkspaceEntity> entities, WorkspaceOrganizationKind kind)
        {
            foreach (var entity in entities)
            {
                var ledger=document.TagLedger;var mapping=tagMappings?[entity.Id].FirstOrDefault(row=>row.Kind==kind);
                var state=tagLedgerValid?HabiticaCoverageState.Missing:HabiticaCoverageState.NeedsReview;var membership=HabiticaCoverageState.Missing;
                if(mapping is not null)
                {
                    var currentName=entity switch { WorkspaceCategory row=>row.Name,WorkspaceProject row=>row.Name,WorkspaceSection row=>row.Name,WorkspaceLabel row=>row.Name,WorkspaceGoal row=>row.Name,WorkspaceSavedTaskView row=>row.Name,_=>null };
                    if(expectedAccountId is null||ledger!.AccountId!=expectedAccountId||!tagLedgerValid
                        ||mapping.IsArchived!=entity.IsArchived||mapping.DesiredRevision<entity.Revision||mapping.DesiredName!=currentName)
                        state=HabiticaCoverageState.NeedsReview;
                    else state=mapping.State switch { WorkspaceTagState.Pending=>HabiticaCoverageState.Pending,
                        WorkspaceTagState.Confirmed=>entity.IsArchived?HabiticaCoverageState.Retained:HabiticaCoverageState.Linked,
                        _=>HabiticaCoverageState.NeedsReview };
                    var intents=tagIntents![mapping.Id].Where(row=>row.Operation==WorkspaceTagOperation.SetMembership).OrderByDescending(row=>row.DesiredRevision).ThenByDescending(row=>row.Revision).ToArray();
                    var intent=intents.FirstOrDefault();
                    if(intent is not null)
                    {
                        if(intents.Count(row=>row.DesiredRevision==intent.DesiredRevision&&row.Revision==intent.Revision)>1
                            ||state is HabiticaCoverageState.Missing or HabiticaCoverageState.NeedsReview||intent.IsArchived&&!entity.IsArchived
                            ||intent.DesiredRevision!=document.Revision||intent.HabiticaTagId!=mapping.HabiticaTagId)
                            membership=HabiticaCoverageState.NeedsReview;
                        else membership=intent.State switch { WorkspaceTagState.Pending=>HabiticaCoverageState.Pending,
                            WorkspaceTagState.Confirmed when state is HabiticaCoverageState.Linked or HabiticaCoverageState.Retained && intent.Members!.All(member=>mappings[member.TaskId].Any(projection=>projection.HabiticaTaskId==member.HabiticaTaskId))
                                =>entity.IsArchived?HabiticaCoverageState.Retained:HabiticaCoverageState.Linked,
                            _=>HabiticaCoverageState.NeedsReview };
                    }
                }
                tags.Add(new(entity.Id,kind,entity.IsArchived,state,state is HabiticaCoverageState.Linked or HabiticaCoverageState.Retained?mapping?.HabiticaTagId:null,membership));
            }
        }
        // Native hierarchy is flattened in Habitica: every subtask above remains a real task.
        // Organization and saved selections correspond to tags, never extra reward-bearing Todos.
        Add(document.Projects, WorkspaceOrganizationKind.Project);
        Add(document.Categories, WorkspaceOrganizationKind.Category);
        Add(document.Sections, WorkspaceOrganizationKind.Section);
        Add(document.Labels, WorkspaceOrganizationKind.Label);
        Add(document.Goals, WorkspaceOrganizationKind.Goal);
        Add(document.SavedTaskViews ?? [], WorkspaceOrganizationKind.SavedTaskView);
        return new(tasks, tags);
    }
}
