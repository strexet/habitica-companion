using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceOrganizationEngineTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-18T15:00:00Z");
    private static JsonDocument Fixture()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","organization-history-v1.json")));
    private static WorkspaceJournalSnapshot Initial()
    {
        using var fixture=Fixture();var d=WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("cases")[0].GetProperty("document").GetRawText()) with { Revision=10 };
        return new(d,10,[],[]);
    }
    private static WorkspaceOrganizationCommand Create(WorkspaceJournalSnapshot state)=>new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOrganizationKind.Category,WorkspaceOrganizationOperation.Create,Guid.NewGuid(),new(Name:"Local area"));
    private static WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot state,WorkspaceOrganizationCommand command)
    {
        var result=WorkspaceOrganizationEngine.Accept(state,command,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);Assert.True(result.SavedInCompanion);Assert.True(WorkspaceJournalValidation.IsValid(result.Snapshot!));return result.Snapshot!;
    }
    [Fact]
    public void All_fifteen_independent_commands_preserve_unrelated_state_and_original_receipts()
    {
        using var fixture=Fixture();var state=Initial();var initial=state;
        foreach(var value in fixture.RootElement.GetProperty("hashes").EnumerateArray())
        {
            var command=JsonSerializer.Deserialize<WorkspaceOrganizationCommand>(value.GetProperty("command").GetRawText(),new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false)} })!;
            var before=WorkspaceJournalJson.Serialize(state);var next=Apply(state,command);var entry=next.Document.OrganizationHistory!.Last();
            Assert.Equal(value.GetProperty("payloadHash").GetString(),entry.PayloadHash);Assert.Equal(command.ExpectedRevision+1,next.StoreVersion);
            Assert.Same(initial.Document.Tasks,next.Document.Tasks);Assert.Same(initial.Document.Projections,next.Document.Projections);Assert.Same(initial.Document.Completions,next.Document.Completions);Assert.Same(initial.Commands,next.Commands);Assert.Same(initial.Events,next.Events);
            Assert.Equal(before,WorkspaceJournalJson.Serialize(state));state=next;
        }
        Assert.Equal(15,state.Document.OrganizationHistory!.Count);Assert.Equal(25,state.Document.Revision);
        var retained=state.Document.OrganizationHistory[0];var duplicate=WorkspaceOrganizationEngine.Accept(state with { Authority=new(WorkspaceAuthorityMode.Frozen,Guid.NewGuid()) },retained.Command,Now.AddDays(1));
        Assert.Equal(SubmissionDisposition.Duplicate,duplicate.Disposition);Assert.Same(retained,duplicate.Entry);
        Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceOrganizationEngine.Accept(state,retained.Command with { Patch=new(Name:"Changed") },Now).Disposition);
        Assert.Equal(SubmissionDisposition.Invalid,WorkspaceOrganizationEngine.Accept(state,retained.Command with { Id=Guid.NewGuid(),ExpectedRevision=state.Document.Revision,Operation=WorkspaceOrganizationOperation.Update },Now).Disposition);
    }
    [Fact]
    public void Invalid_references_kinds_fields_authority_and_revisions_never_change_the_source()
    {
        var state=Initial();var c=Create(state);var d=state.Document;var before=WorkspaceJournalJson.Serialize(state);
        var invalid=new[]{c with { WorkspaceId=Guid.NewGuid() },c with { Id=Guid.Empty },c with { Kind=(WorkspaceOrganizationKind)99 },c with { Patch=new(Name:" ") },c with { Patch=new(Name:"x",TaskIds:[]) },
            c with { EntityId=d.Tasks[0].Id },c with { EntityId=d.Id },c with { EntityId=d.Categories[0].Id },
            c with { Kind=WorkspaceOrganizationKind.Project,Patch=new(Name:"x",CategoryId:d.Categories.Last().Id) },c with { Kind=WorkspaceOrganizationKind.Project,Patch=new(Name:"x",CategoryId:Guid.NewGuid()) },
            c with { Kind=WorkspaceOrganizationKind.Section,Patch=new(Name:"x",ProjectId:d.Projects.Last().Id) },c with { Kind=WorkspaceOrganizationKind.Section,Patch=new(Name:"x") },
            c with { Kind=WorkspaceOrganizationKind.Section,Operation=WorkspaceOrganizationOperation.Update,EntityId=d.Sections[0].Id,Patch=new(ProjectId:d.Projects[1].Id) },
            c with { Kind=WorkspaceOrganizationKind.Goal,Patch=new(Name:"x",TaskIds:[d.Tasks.First(t=>t.Kind==CompanionTaskKind.Daily).Id]) },
            c with { Kind=WorkspaceOrganizationKind.Goal,Patch=new(Name:"x",TaskIds:[d.Tasks.First(t=>t.IsArchived).Id]) },
            c with { Kind=WorkspaceOrganizationKind.Goal,Patch=new(Name:"x",TaskIds:[Guid.NewGuid()]) },
            c with { Operation=WorkspaceOrganizationOperation.Archive,EntityId=d.Categories[0].Id,Patch=new(Name:"x") },
            c with { Operation=WorkspaceOrganizationOperation.Update,EntityId=d.Categories.Last().Id,Patch=new(Name:"x") }};
        foreach(var command in invalid){var rejected=WorkspaceOrganizationEngine.Accept(state,command,Now);Assert.Equal(SubmissionDisposition.Invalid,rejected.Disposition);Assert.False(rejected.SavedInCompanion);Assert.Same(state,rejected.Snapshot);}
        Assert.Equal(SubmissionDisposition.RevisionConflict,WorkspaceOrganizationEngine.Accept(state,c with { ExpectedRevision=11 },Now).Disposition);
        foreach(var authority in new[]{new WorkspaceExecutionAuthority(WorkspaceAuthorityMode.Frozen,Guid.NewGuid()),new WorkspaceExecutionAuthority(WorkspaceAuthorityMode.Cloud,CloudWorkspaceId:d.Id)})Assert.Equal("workspace_owned_elsewhere",WorkspaceOrganizationEngine.Accept(state with { Authority=authority },c,Now).ErrorCode);
        Assert.Equal("invalid_command",WorkspaceOrganizationEngine.Accept(state,c,Now.ToOffset(TimeSpan.FromHours(1))).ErrorCode);
        Assert.Equal("revision_exhausted",WorkspaceOrganizationEngine.Accept(state with { StoreVersion=WorkspaceDocument.MaxRevision },c,Now).ErrorCode);
        Assert.Equal(before,WorkspaceJournalJson.Serialize(state));
    }
    [Fact]
    public void Legacy_goal_sets_partial_fields_archival_and_uncertain_mappings_remain_native()
    {
        var state=Initial();var d=state.Document;var goal=d.Goals[0];var originalTasks=d.Tasks;var refs=goal.TaskIds;
        state=state with { Document=d with { Projections=[new(Guid.NewGuid(),10,d.Tasks[0].Id,ProjectionState.UnknownOutcome,"60000000-0000-4000-8000-000000000999")] } };
        var c=new WorkspaceOrganizationCommand(Guid.NewGuid(),d.Id,10,WorkspaceOrganizationKind.Goal,WorkspaceOrganizationOperation.Update,goal.Id,new(Name:"Renamed"));state=Apply(state,c);Assert.Same(refs,state.Document.Goals[0].TaskIds);
        c=c with { Id=Guid.NewGuid(),ExpectedRevision=state.Document.Revision,Patch=new(TaskIds:[]) };state=Apply(state,c);Assert.Empty(state.Document.Goals[0].TaskIds);
        c=c with { Id=Guid.NewGuid(),ExpectedRevision=state.Document.Revision,Kind=WorkspaceOrganizationKind.Project,EntityId=d.Projects[0].Id,Patch=new(CategoryId:d.Categories[1].Id) };state=Apply(state,c);Assert.Equal(d.Categories[1].Id,state.Document.Projects[0].CategoryId);
        c=c with { Id=Guid.NewGuid(),ExpectedRevision=state.Document.Revision,Patch=new(ClearCategory:true) };state=Apply(state,c);Assert.Null(state.Document.Projects[0].CategoryId);
        c=c with { Id=Guid.NewGuid(),ExpectedRevision=state.Document.Revision,Operation=WorkspaceOrganizationOperation.Archive,Patch=null };state=Apply(state,c);Assert.True(state.Document.Projects[0].IsArchived);
        Assert.Same(originalTasks,state.Document.Tasks);Assert.Equal(d.Sections,state.Document.Sections);Assert.Single(state.Document.Projections);Assert.Empty(state.Commands);Assert.Empty(state.Events!);
        Assert.Equal(WorkspaceRules.Progress(goal,d.Tasks),WorkspaceRules.Progress(goal,state.Document.Tasks));
        c=c with { Id=Guid.NewGuid(),ExpectedRevision=state.Document.Revision,Operation=WorkspaceOrganizationOperation.Update,Patch=new(Name:"No reopen") };Assert.Equal("organization_unavailable",WorkspaceOrganizationEngine.Accept(state,c,Now).ErrorCode);
    }
    [Fact]
    public void Task_command_identity_and_corrupted_metadata_cannot_supply_native_receipts()
    {
        var state=Initial();var taskCommand=new WorkspaceCommand(Guid.NewGuid(),state.Document.Id,10,WorkspaceOperation.CreateTodo,Guid.NewGuid(),new(Text:"Task"));state=WorkspaceCommandEngine.Accept(state,taskCommand,Now).Snapshot!;
        var c=Create(state) with { Id=taskCommand.Id };Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceOrganizationEngine.Accept(state,c,Now).Disposition);
        c=c with { Id=Guid.NewGuid() };state=Apply(state,c);var entry=state.Document.OrganizationHistory![0];
        state=state with { Document=state.Document with { OrganizationHistory=[entry with { PayloadHash=new string('0',64) }] } };
        Assert.Equal("invalid_workspace",WorkspaceOrganizationEngine.Accept(state,c,Now).ErrorCode);
    }
    [Fact]
    public async Task Atomic_conflicts_lost_acknowledgements_and_storage_failures_preserve_receipt_identity()
    {
        var initial=Initial();var command=Create(initial);var accepted=Apply(initial,command);
        var store=new Store(initial){Competing=accepted};var result=await new WorkspaceOrganizationService(store).SubmitAsync(command,default);Assert.Equal(SubmissionDisposition.Duplicate,result!.Disposition);Assert.Equal(command.Id,result.Entry!.Command.Id);
        store=new Store(initial){Competing=Apply(initial,Create(initial))};Assert.Equal(SubmissionDisposition.RevisionConflict,(await new WorkspaceOrganizationService(store).SubmitAsync(command,default))!.Disposition);Assert.Single(store.State!.Document.OrganizationHistory!);
        store=new Store(initial){Competing=Apply(initial,command with { Patch=new(Name:"Other intent") })};Assert.Equal(SubmissionDisposition.IdempotencyConflict,(await new WorkspaceOrganizationService(store).SubmitAsync(command,default))!.Disposition);
        store=new Store(initial){FailAfter=true};result=await new WorkspaceOrganizationService(store).SubmitAsync(command,default);Assert.Null(result!.SavedInCompanion);Assert.Equal("acceptance_unknown",result.ErrorCode);store.FailAfter=false;
        result=await new WorkspaceOrganizationService(store).SubmitAsync(command,default);Assert.Equal(SubmissionDisposition.Duplicate,result!.Disposition);Assert.Single(store.State!.Document.OrganizationHistory!);Assert.Equal(1,store.Commits);
        store=new Store(initial){FailBefore=true};Assert.Equal("acceptance_unknown",(await new WorkspaceOrganizationService(store).SubmitAsync(command,default))!.ErrorCode);Assert.Same(initial,store.State);
        store=new Store(initial){FailLoad=true};Assert.Equal("storage_unavailable",(await new WorkspaceOrganizationService(store).SubmitAsync(command,default))!.ErrorCode);
        Assert.Null(await new WorkspaceOrganizationService(new Store(null)).SubmitAsync(command,default));
        store=new Store(initial);using var cancelled=new CancellationTokenSource();cancelled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new WorkspaceOrganizationService(store).SubmitAsync(command,cancelled.Token));Assert.Equal(0,store.Commits);
    }
    private sealed class Store(WorkspaceJournalSnapshot? initial):ICompanionWorkspaceStore
    {
        public WorkspaceJournalSnapshot? State=initial;public WorkspaceJournalSnapshot? Competing;public bool FailAfter,FailBefore,FailLoad;public int Commits;
        public Task<WorkspaceJournalSnapshot?> LoadAsync(Guid id,CancellationToken token){token.ThrowIfCancellationRequested();if(FailLoad)throw new IOException("Synthetic unavailable");return Task.FromResult(State?.Document.Id==id?State:null);}
        public Task<bool> TryCommitAsync(long expected,WorkspaceJournalSnapshot next,CancellationToken token){token.ThrowIfCancellationRequested();if(FailBefore)throw new IOException("Synthetic failure");if(Competing is not null){State=Competing;return Task.FromResult(false);}if(State?.StoreVersion!=expected)return Task.FromResult(false);State=WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(next));Commits++;if(FailAfter)throw new IOException("Synthetic lost acknowledgement");return Task.FromResult(true);}
    }
}
