using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.Storage;
namespace Habitica.Application.Tests.Workspace;
public sealed class WorkspaceSavedViewCommandTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-18T22:00:00Z");
    private static WorkspaceJournalSnapshot Initial(){using var f=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","saved-task-views-v1.json")));var d=WorkspaceJson.Deserialize(f.RootElement.GetProperty("document").GetRawText());return new(d,d.Revision,[],[]);}
    private static WorkspaceOrganizationCommand Create(WorkspaceJournalSnapshot s)=>new(Guid.NewGuid(),s.Document.Id,s.Document.Revision,WorkspaceOrganizationKind.SavedTaskView,WorkspaceOrganizationOperation.Create,Guid.NewGuid(),new(Name:"Next candidates",Query:new(WorkspaceSavedTaskSource.UnblockedTodos,new(ExecutionStage:TodoExecutionStage.Next))));
    private static WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot s,WorkspaceOrganizationCommand c){var result=WorkspaceOrganizationEngine.Accept(s,c,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);Assert.True(WorkspaceJournalValidation.IsValid(result.Snapshot!));Assert.Empty(result.Snapshot!.Commands);Assert.Empty(result.Snapshot.Events!);return result.Snapshot;}
    [Fact]
    public void Independent_v2_identity_covers_full_replacement_rename_archive_and_optional_legacy_defaults()
    {
        using var f=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","saved-view-command-v2.json")));foreach(var row in f.RootElement.GetProperty("cases").EnumerateArray()){var c=WorkspaceTaskApiJson.Deserialize<WorkspaceOrganizationCommand>(row.GetProperty("command").GetRawText());Assert.True(WorkspaceOrganizationCommands.IsValid(c));Assert.Equal(row.GetProperty("payloadHash").GetString(),WorkspaceOrganizationCommands.Hash(c));}
        Assert.Equal(5,(int)WorkspaceOrganizationKind.SavedTaskView);Assert.Equal(4,(int)WorkspaceOrganizationKind.Goal);Assert.DoesNotContain("query",WorkspaceTaskApiJson.Serialize(new WorkspaceOrganizationPatch(Name:"Legacy")));
    }
    [Fact]
    public void Native_edits_preserve_original_queries_receipts_and_all_task_state_without_partial_filter_merging()
    {
        var initial=Initial();var c=Create(initial);var s=Apply(initial,c);Assert.Single(WorkspaceSavedTaskViews.Evaluate(s.Document,c.EntityId,new()).Tasks);
        var rename=c with {Id=Guid.NewGuid(),ExpectedRevision=s.Document.Revision,Operation=WorkspaceOrganizationOperation.Update,Patch=new(Name:"Renamed")};s=Apply(s,rename);Assert.Equal(c.Patch!.Query,s.Document.SavedTaskViews!.Last().Query);
        var query=new WorkspaceSavedTaskQuery(WorkspaceSavedTaskSource.Tasks,new());var replace=rename with {Id=Guid.NewGuid(),ExpectedRevision=s.Document.Revision,Patch=new(Query:query)};s=Apply(s,replace);Assert.Equal(query,s.Document.SavedTaskViews!.Last().Query);Assert.Equal("Renamed",s.Document.SavedTaskViews!.Last().Name);Assert.Equal(20,WorkspaceSavedTaskViews.Evaluate(s.Document,c.EntityId,new()).MatchedCount);
        var archive=replace with {Id=Guid.NewGuid(),ExpectedRevision=s.Document.Revision,Operation=WorkspaceOrganizationOperation.Archive,Patch=null};s=Apply(s,archive);Assert.True(s.Document.SavedTaskViews!.Last().IsArchived);Assert.Equal(query,s.Document.SavedTaskViews!.Last().Query);
        foreach(var original in new[]{c,rename,replace,archive}){var duplicate=WorkspaceOrganizationEngine.Accept(s,original,Now.AddDays(1));Assert.Equal(SubmissionDisposition.Duplicate,duplicate.Disposition);Assert.Equal(original.Id,duplicate.Entry!.Command.Id);}
        Assert.Equal(c.Patch!.Query,s.Document.OrganizationHistory![0].Snapshot.Query);Assert.Same(initial.Document.Tasks,s.Document.Tasks);Assert.Same(initial.Document.Projections,s.Document.Projections);Assert.Same(initial.Document.Completions,s.Document.Completions);Assert.Same(initial.Document.HabitScores,s.Document.HabitScores);
        Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceOrganizationEngine.Accept(s,c with {Patch=c.Patch with {Name="Changed"}},Now).Disposition);Assert.Equal("organization_unavailable",WorkspaceOrganizationEngine.Accept(s,rename with {Id=Guid.NewGuid(),ExpectedRevision=s.Document.Revision},Now).ErrorCode);
        Assert.Equal(WorkspaceJournalJson.Serialize(s),WorkspaceJournalJson.Serialize(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(s))));
    }
    [Fact]
    public void Invalid_reference_shape_kind_revision_and_portable_tampering_fail()
    {
        var s=Initial();var c=Create(s);var invalid=new[]{c with {Patch=new(Name:"Missing query")},c with {Kind=WorkspaceOrganizationKind.Category},c with {Patch=c.Patch! with {TaskIds=[]}},c with {Patch=c.Patch! with {Query=new(WorkspaceSavedTaskSource.UnblockedTodos,new(State:CompanionTaskState.Active))}},c with {Patch=c.Patch! with {Query=new(WorkspaceSavedTaskSource.Tasks,new(ProjectId:Guid.NewGuid()))}},c with {EntityId=s.Document.Tasks[0].Id},c with {Operation=WorkspaceOrganizationOperation.Archive}};
        foreach(var command in invalid)Assert.Equal(SubmissionDisposition.Invalid,WorkspaceOrganizationEngine.Accept(s,command,Now).Disposition);Assert.Equal(SubmissionDisposition.RevisionConflict,WorkspaceOrganizationEngine.Accept(s,c with {ExpectedRevision=19},Now).Disposition);
        var archived=s.Document.SavedTaskViews!.Last().Query.Filter;c=c with {Patch=c.Patch! with {Query=new(WorkspaceSavedTaskSource.Tasks,archived)}};s=Apply(s,c);var entry=s.Document.OrganizationHistory![0];
        Assert.False(WorkspaceJournalValidation.IsValid(s with {Document=s.Document with {OrganizationHistory=[entry with {Snapshot=entry.Snapshot with {Query=new(WorkspaceSavedTaskSource.Tasks,new())}}]}}));Assert.False(WorkspaceJournalValidation.IsValid(s with {Document=s.Document with {OrganizationHistory=[entry with {PayloadHash=new string('0',64)}]}}));
        var raw=WorkspaceJournalJson.Serialize(s);Assert.Throws<JsonException>(()=>WorkspaceJournalJson.Deserialize(raw.Replace("\"payloadHash\":","\"effects\":[],\"payloadHash\":")));
    }
    [Fact]
    public async Task Lost_commit_acknowledgement_recovers_one_original_native_query_receipt()
    {
        var initial=Initial();var c=Create(initial);var store=new Store(initial);var service=new WorkspaceOrganizationService(store);var uncertain=await service.SubmitAsync(c,default);Assert.Equal("acceptance_unknown",uncertain!.ErrorCode);Assert.Null(uncertain.SavedInCompanion);var recovered=await service.SubmitAsync(c,default);Assert.Equal(SubmissionDisposition.Duplicate,recovered!.Disposition);Assert.Equal(c.Id,recovered.Entry!.Command.Id);Assert.Single(store.State.Document.OrganizationHistory!);Assert.Equal(1,store.Commits);Assert.Equal(c.Patch!.Query,store.State.Document.SavedTaskViews!.Last().Query);
    }
    private sealed class Store(WorkspaceJournalSnapshot initial):ICompanionWorkspaceStore
    {
        public WorkspaceJournalSnapshot State=initial;public int Commits;
        public Task<WorkspaceJournalSnapshot?> LoadAsync(Guid id,CancellationToken token)=>Task.FromResult<WorkspaceJournalSnapshot?>(State);
        public Task<bool> TryCommitAsync(long expected,WorkspaceJournalSnapshot next,CancellationToken token){Assert.Equal(State.StoreVersion,expected);State=WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(next));Commits++;throw new IOException("Synthetic lost acknowledgement");}
    }
}
