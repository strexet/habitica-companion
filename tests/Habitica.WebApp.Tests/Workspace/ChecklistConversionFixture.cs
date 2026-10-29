using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.Tests.Workspace;

// Actual portable engine fixtures, shared with Worker acceptance tests. No native requests.
internal sealed class ChecklistConversionFixture : WorkspaceTaskFixture.Cloud,IWorkspaceConversionApi
{
    public ChecklistConversionCommand Template {get;}
    public CompanionTask Source=>State.Document.Tasks.Single(task=>task.Id==Template.SourceTaskId);
    public List<WorkspaceConversionRequest> Requests=[];
    public List<Guid> Accepts=[];
    public bool FailStatus {get;set;}
    public Func<Task>? ReadGate;
    public WorkspaceConversionPreview? Preview;
    public WorkspaceConversionReceipt? Receipt;
    public ChecklistConversionFixture(string name="reuse")
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","checklist-conversion-"+name+".json")));
        State=WorkspaceJournalJson.Deserialize(json.RootElement.GetProperty("initial").GetRawText());
        Template=WorkspaceTaskApiJson.Deserialize<ChecklistConversionCommand>(json.RootElement.GetProperty("command").GetRawText())!;Connected=true;
    }
    public WorkspaceConversionStatus Status()
    {
        var conversion=State.Document.ChecklistConversions?.Single();var targets=(conversion?.Items??[]).Select(binding=>
        {
            var task=State.Document.Tasks.SingleOrDefault(row=>row.Id==binding.TargetTaskId);var map=State.Document.Projections.SingleOrDefault(row=>row.TaskId==task?.Id);
            var pending=State.Commands.Where(row=>row.Command.TaskId==task?.Id&&row.ProjectionState is not (ProjectionCommandState.Confirmed or ProjectionCommandState.NotRequested or ProjectionCommandState.Cancelled)).Select(row=>row.Command.Id).ToArray();
            var placed=task is not null&&task.ProjectId==conversion!.ProjectId;
            var stored=placed&&map?.State==ProjectionState.Linked&&pending.Length==0;
            return new WorkspaceConversionTarget(binding,task,map,pending,placed,stored,binding.DesiredState==ChecklistConversionState.CompletedArchived&&(task is null||map?.State!=ProjectionState.Linked));
        }).ToArray();
        return new("companion-canonical",State.Document.Id,State.Document.Revision,Source,conversion,targets,conversion is null||Source.Revision==conversion.SourceRevision,conversion is not null&&Source.Revision==conversion.SourceRevision&&targets.All(row=>row.PlacementComplete),false,null,"notFreshlyVerified",false,"Stored audit only; source retained.");
    }
    public async Task<WorkspaceTaskResponse<WorkspaceConversionStatus>> ReadConversionAsync(Guid sourceTaskId,CancellationToken token)
    {if(ReadGate is {} wait)await wait();return FailStatus?new(false,null,"Unavailable"):new(true,Status(),"Read exact source");}
    public Task<WorkspaceTaskResponse<WorkspaceConversionPreview>> PrepareConversionAsync(WorkspaceConversionRequest request,CancellationToken token)
    {
        Requests.Add(request);if(RejectPreparation)return Task.FromResult(new WorkspaceTaskResponse<WorkspaceConversionPreview>(false,null,"Source changed; read current source"));
        if(Preview is null||Preview.CorrelationId!=request.RequestId)
        {
            var command=Template with {Id=Guid.NewGuid(),ExpectedRevision=request.ExpectedRevision,SourceRevision=request.SourceRevision,ProjectId=request.ProjectId??Template.ProjectId,ProjectName=request.ProjectName??Template.ProjectName,
                Items=request.Items.Select(item=>new ChecklistConversionItem(item.SourceItemId,item.Mode,item.Mode==ChecklistConversionMode.Create?Guid.NewGuid():item.TargetTaskId,item.DesiredState,item.Notes,item.GameDetails)).ToArray()};
            var result=WorkspaceChecklistConversionEngine.Accept(State,command,DateTimeOffset.UtcNow);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);
            Preview=new(command.Id,request.RequestId,DateTimeOffset.UtcNow.AddMinutes(10),false,command,Source,result.Conversion!,result.Conversion!.Items.Select(binding=>new WorkspaceConversionPreviewTarget(binding,result.Snapshot!.Document.Tasks.SingleOrDefault(task=>task.Id==binding.TargetTaskId))).ToArray(),new(true,true,false,[]),command.Items.Count(item=>item.Mode==ChecklistConversionMode.Create),0,false);
        }
        if(LosePreparation){LosePreparation=false;return Task.FromResult(new WorkspaceTaskResponse<WorkspaceConversionPreview>(false,null,"Lost preview",true));}
        return Task.FromResult(new WorkspaceTaskResponse<WorkspaceConversionPreview>(true,Preview,"Reviewed mapping"));
    }
    public Task<WorkspaceTaskResponse<WorkspaceConversionReceipt>> AcceptConversionAsync(WorkspaceConversionPreview preview,CancellationToken token)
    {
        Accepts.Add(preview.PreparedCommandId);var result=WorkspaceChecklistConversionEngine.Accept(State,preview.Command,DateTimeOffset.UtcNow);Assert.True(result.Disposition is SubmissionDisposition.Accepted or SubmissionDisposition.Duplicate);State=result.Snapshot!;
        Receipt=new(preview.PreparedCommandId,preview.CorrelationId,preview.Command.ConversionId,preview.Command.ProjectId,preview.Command.SourceTaskId,true,false,result.Conversion!.Receipts.Single(row=>row.Command.Id==preview.PreparedCommandId),0,"Canonical save only");
        if(LoseAcceptance){LoseAcceptance=false;return Task.FromResult(new WorkspaceTaskResponse<WorkspaceConversionReceipt>(false,null,"Lost reply",true));}
        return Task.FromResult(new WorkspaceTaskResponse<WorkspaceConversionReceipt>(true,Receipt,"Original receipt"));
    }
    public Task<WorkspaceTaskResponse<WorkspaceConversionReceipt>> ReadConversionCommandAsync(Guid commandId,CancellationToken token)=>Task.FromResult(Receipt is {} receipt?new WorkspaceTaskResponse<WorkspaceConversionReceipt>(true,receipt,"Original receipt"):new(false,null,"No original receipt confirmed",true));
    public void ChooseReuse(Habitica.WebApp.State.WorkspaceConversionController controller)
    {foreach(var draft in controller.Drafts){var item=Template.Items.Single(item=>item.SourceItemId==draft.SourceItemId);draft.Mode=item.Mode;draft.DesiredState=item.DesiredState;draft.TargetTaskId=item.TargetTaskId;}}
}
