using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceChecklistConversionTests
{
    private static readonly DateTimeOffset Now=new(2026,10,22,0,0,0,TimeSpan.Zero);
    private static WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot state,WorkspaceCommand command)
    {var result=WorkspaceCommandEngine.Accept(state,command,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);return result.Snapshot!;}
    private static (WorkspaceJournalSnapshot State,CompanionTask Source,ChecklistConversionCommand Command) Setup(bool reuse=true)
    {
        var state=new WorkspaceJournalSnapshot(new WorkspaceDocument {Id=Guid.NewGuid()},1,[]);var sourceId=Guid.NewGuid();
        // Historical fixture commands deliberately retain legacy nonprojecting intent.
        var checklist=Enumerable.Range(0,10).Select(index=>new TaskChecklistItem(Guid.NewGuid(),index%2==0?"Same name":"Пункт "+index,index<5)).ToArray();
        state=Apply(state,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.CreateTodo,sourceId,new(Text:"Legacy checklist project",Notes:new string('Ж',4000),Priority:CompanionPriority.Urgent,HabiticaDetails:new(Checklist:checklist))));
        var source=state.Document.Tasks.Single();var items=new List<ChecklistConversionItem>();
        foreach(var (item,index) in checklist.Select((item,index)=>(item,index)))
        {
            var id=Guid.NewGuid();
            if(reuse)
            {
                state=Apply(state,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.CreateTodo,id,new(Text:item.Text,Notes:"Independent existing notes "+index,Priority:CompanionPriority.Low)));
                if(index>=8){state=Apply(state,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.CompleteTask,id));state=Apply(state,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.DeleteTask,id));}
                items.Add(new(item.Id,ChecklistConversionMode.Reuse,id,index<8?ChecklistConversionState.Active:ChecklistConversionState.CompletedArchived));
            }
            else items.Add(index<8?new(item.Id,ChecklistConversionMode.Create,id,ChecklistConversionState.Active,"Own note "+index,new(0.1,HabiticaTaskAttribute.Int)):
                new(item.Id,ChecklistConversionMode.Defer,null,ChecklistConversionState.CompletedArchived));
        }
        return(state,source,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,Guid.NewGuid(),sourceId,source.Revision,Guid.NewGuid(),"Companion Development <codev>",items));
    }
    private static void Save(string name,WorkspaceJournalSnapshot initial,ChecklistConversionCommand command,WorkspaceJournalSnapshot accepted)
    {
        if(Environment.GetEnvironmentVariable("HABITICA_CONVERSION_FIXTURES") is not {} directory)return;Directory.CreateDirectory(directory);
        var options=new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false)}};
        File.WriteAllText(Path.Combine(directory,name+".json"),JsonSerializer.Serialize(new {format="checklist-conversion-v1",command,hash=WorkspaceChecklistConversions.Hash(command),derivedId=WorkspaceChecklistConversionEngine.DerivedId(command.Id,"project"),initial=JsonDocument.Parse(WorkspaceJournalJson.Serialize(initial)).RootElement.Clone(),acceptedDocument=JsonDocument.Parse(WorkspaceJournalJson.Serialize(accepted)).RootElement.GetProperty("document").Clone(),acceptedStoreVersion=accepted.StoreVersion},options));
    }
    [Theory]
    [InlineData("reuse")]
    [InlineData("create")]
    public void Shared_fixtures_preserve_portable_identity_and_conversion_journal_validation(string name)
    {
        using var root=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","checklist-conversion-"+name+".json")));
        var options=new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.CamelCase,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false)}};
        var command=root.RootElement.GetProperty("command").Deserialize<ChecklistConversionCommand>(options)!;
        Assert.Equal(root.RootElement.GetProperty("hash").GetString(),WorkspaceChecklistConversions.Hash(command));Assert.Equal(root.RootElement.GetProperty("derivedId").GetGuid(),WorkspaceChecklistConversionEngine.DerivedId(command.Id,"project"));
        var initial=WorkspaceJournalJson.Deserialize(root.RootElement.GetProperty("initial").GetRawText());Assert.DoesNotContain("checklistConversions",WorkspaceJournalJson.Serialize(initial));
        var document=root.RootElement.GetProperty("acceptedDocument").Deserialize<WorkspaceDocument>(options)!;Assert.Empty(WorkspaceRules.Validate(document));
        var portable=new WorkspaceJournalSnapshot(document,root.RootElement.GetProperty("acceptedStoreVersion").GetInt64(),[],[]);Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(portable))));
    }
    [Fact]
    public void Ten_item_conversion_reuses_exact_targets_keeps_eight_active_two_archived_and_preserves_source_and_history()
    {
        var x=Setup();var previous=x.State.Document.Tasks.ToDictionary(task=>task.Id);var result=WorkspaceChecklistConversionEngine.Accept(x.State,x.Command,Now);
        Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);var state=result.Snapshot!;Save("checklist-conversion-reuse",x.State,x.Command,state);Assert.Equal(11,state.Document.Tasks.Count);Assert.Equal(x.Source,state.Document.Tasks.Single(task=>task.Id==x.Source.Id));
        Assert.Equal(x.State.Document.Completions,state.Document.Completions);Assert.Equal(x.State.Commands,state.Commands);Assert.Equal(x.State.Events,state.Events);Assert.Equal(2,state.Document.Tasks.Count(task=>task.IsArchived));
        foreach(var binding in result.Conversion!.Items){var target=state.Document.Tasks.Single(task=>task.Id==binding.TargetTaskId);Assert.Equal(x.Command.ProjectId,target.ProjectId);Assert.Equal(previous[target.Id].Text,target.Text);Assert.Equal(previous[target.Id].Notes,target.Notes);Assert.Equal(previous[target.Id].Priority,target.Priority);Assert.Equal(previous[target.Id],binding.OriginalTarget);}
        Assert.All(result.Conversion.Items.Take(5),binding=>Assert.Equal(ChecklistConversionState.Active,binding.DesiredState));Assert.All(result.Conversion.Items.Skip(8),binding=>Assert.True(binding.HistoricalTimeUncertain));
        Assert.Equal(x.Source.Notes,result.Conversion.SourceSnapshot.Notes);Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
        var duplicate=WorkspaceChecklistConversionEngine.Accept(state,x.Command,Now.AddHours(1));Assert.Equal(SubmissionDisposition.Duplicate,duplicate.Disposition);Assert.Same(state,duplicate.Snapshot);
        var resume=x.Command with {Id=Guid.NewGuid(),ExpectedRevision=state.Document.Revision};var resumed=WorkspaceChecklistConversionEngine.Accept(state,resume,Now.AddHours(1));Assert.Equal(SubmissionDisposition.Accepted,resumed.Disposition);Assert.Equal(11,resumed.Snapshot!.Document.Tasks.Count);Assert.Equal(state.Document.Completions,resumed.Snapshot.Document.Completions);Assert.Equal(2,resumed.Conversion!.Receipts.Count);
        var impossible=resumed.Conversion.Receipts.ToArray();var oldCommand=impossible[^1].Command with {ExpectedRevision=x.Source.Revision};impossible[^1]=impossible[^1] with {Command=oldCommand,PayloadHash=WorkspaceChecklistConversions.Hash(oldCommand)};
        Assert.NotEmpty(WorkspaceRules.Validate(resumed.Snapshot.Document with {ChecklistConversions=[resumed.Conversion with {Receipts=impossible}]}));
    }
    [Fact]
    public void New_active_targets_use_normal_pending_create_commands_while_missing_historical_matches_remain_unresolved()
    {
        var x=Setup(false);var result=WorkspaceChecklistConversionEngine.Accept(x.State,x.Command,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);var state=result.Snapshot!;Save("checklist-conversion-create",x.State,x.Command,state);
        Assert.Equal(9,state.Document.Tasks.Count);Assert.Empty(state.Document.Completions);Assert.All(state.Commands.Skip(x.State.Commands.Count),entry=>{Assert.Equal(WorkspaceOperation.CreateTodo,entry.Command.Operation);Assert.True(entry.Command.ProjectToHabitica);Assert.Equal([ProjectionEffectKind.Create],entry.Effects);Assert.Equal(0,entry.ConfirmedEffects);});
        Assert.All(result.Conversion!.Items.Skip(8),binding=>Assert.Null(binding.TargetTaskId));
        var target=state.Document.Tasks.Single(task=>task.Id==x.Command.Items[0].TargetTaskId);Assert.Equal(x.Source.Priority,target.Priority);Assert.Equal(x.Source.Notes+"\n\nOwn note 0",target.Notes);
        var items=x.Command.Items.Select(item=>item.TargetTaskId is null?item:item with {Mode=ChecklistConversionMode.Reuse,Notes=null,GameDetails=null}).ToArray();
        var resumed=WorkspaceChecklistConversionEngine.Accept(state,x.Command with {Id=Guid.NewGuid(),ExpectedRevision=state.Document.Revision,Items=items},Now.AddHours(1));Assert.Equal(SubmissionDisposition.Accepted,resumed.Disposition);Assert.Equal(9,resumed.Snapshot!.Document.Tasks.Count);Assert.Equal(state.Commands.Count,resumed.Snapshot.Commands.Count);Assert.Empty(resumed.Snapshot.Document.Completions);
    }
    [Fact]
    public void Changed_sources_rebound_targets_duplicate_names_and_command_reuse_never_create_replacements()
    {
        var x=Setup();var saved=WorkspaceChecklistConversionEngine.Accept(x.State,x.Command,Now).Snapshot!;
        var wrong=x.Command with {Id=Guid.NewGuid(),ExpectedRevision=saved.Document.Revision,ConversionId=Guid.NewGuid()};Assert.Equal("resume_original_conversion_required",WorkspaceChecklistConversionEngine.Accept(saved,wrong,Now).ErrorCode);
        var changes=x.Command.Items.ToArray();changes[0]=changes[0] with {TargetTaskId=Guid.NewGuid()};wrong=x.Command with {Id=Guid.NewGuid(),ExpectedRevision=saved.Document.Revision,Items=changes};Assert.Equal("conversion_binding_changed",WorkspaceChecklistConversionEngine.Accept(saved,wrong,Now).ErrorCode);
        Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceChecklistConversionEngine.Accept(saved,x.Command with {ProjectName="Other name"},Now).Disposition);
        var edited=Apply(saved,new(Guid.NewGuid(),saved.Document.Id,saved.Document.Revision,WorkspaceOperation.UpdateTask,x.Source.Id,new(Notes:"New source description")));
        Assert.Equal("conversion_source_changed",WorkspaceChecklistConversionEngine.Accept(edited,x.Command with {Id=Guid.NewGuid(),ExpectedRevision=edited.Document.Revision},Now).ErrorCode);
        Assert.Equal(SubmissionDisposition.Duplicate,WorkspaceChecklistConversionEngine.Accept(edited,x.Command,Now).Disposition);
        var conversion=saved.Document.ChecklistConversions!.Single();var forged=conversion.Items.ToArray();forged[0]=forged[0] with {TargetTaskId=changes[0].TargetTaskId};Assert.NotEmpty(WorkspaceRules.Validate(saved.Document with {ChecklistConversions=[conversion with {Items=forged}]}));
    }
}
