using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
namespace Habitica.WebApp.Tests.Workspace;

internal sealed class SourceRetirementFixture : WorkspaceTaskFixture.Cloud,IWorkspaceSourceRetirementApi
{
    public ConversionRetirementPlan Plan {get;}
    public WorkspaceSourceRetirementView? View;
    public WorkspaceSourceRetirementView? Saved;
    public List<WorkspaceSourceRetirementRequest> Requests=[];
    public List<Guid> Accepts=[];
    public bool LoseReply,FailVerification,AlreadyAbsent;
    public Func<Task>? Gate;
    public SourceRetirementFixture()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","conversion-retirement-prerequisites.json")));
        State=WorkspaceJournalJson.Deserialize(json.RootElement.GetProperty("state").GetRawText());
        var account=json.RootElement.GetProperty("accountId").GetGuid();Plan=WorkspaceConversionRetirementPolicy.Inspect(State,State.Document.ChecklistConversions!.Single().Id,account).Plan!;Connected=true;
    }
    public async Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> PrepareSourceRetirementAsync(WorkspaceSourceRetirementRequest request,CancellationToken token)
    {
        Requests.Add(request);if(Gate is {} gate)await gate();
        View??=new(Guid.NewGuid(),request.RequestId,State.Document.Id,Plan.ConversionId,Plan.ContextHash,Plan.Revision,Plan.Source,Plan.SourceMapping,Plan.HabiticaProjectTagId.ToString("D"),Plan.SourceMapping.HabiticaTaskId!,Plan.Project,Plan.Targets,0,Plan.Targets.Count+2,0,"verifying",AlreadyAbsent,null,DateTimeOffset.UtcNow.AddMinutes(10),false,false,null,null,null,0,"Only this obsolete source will be permanently deleted in Habitica. Full source and conversion history remain archived in Companion. Replacements, descriptions, completion history and tags are retained. No score occurs. External edits may race the final read; no conditional DELETE exists.");
        if(LosePreparation){LosePreparation=false;return new(false,null,"Unresolved original preparation",true);}return new(true,View,"Prepared exact review");
    }
    public Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> VerifySourceRetirementAsync(Guid id,CancellationToken token)
    {
        if(FailVerification)return Task.FromResult(new WorkspaceTaskResponse<WorkspaceSourceRetirementView>(false,null,"Native replacement changed; nothing deleted"));
        var steps=View!.CompletedSteps+1;View=View with {CompletedSteps=steps,VerifiedTargets=Math.Clamp(steps-1,0,Plan.Targets.Count),State=steps==View.VerificationSteps?"ready":"verifying",VerifiedAtUtc=steps==View.VerificationSteps?DateTimeOffset.UtcNow:null};return Task.FromResult(new WorkspaceTaskResponse<WorkspaceSourceRetirementView>(true,View,"Verified next step"));
    }
    public Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> AcceptSourceRetirementAsync(WorkspaceSourceRetirementView reviewed,CancellationToken token)
    {
        Accepts.Add(reviewed.CommandId);Save(AlreadyAbsent?SourceRetirementOutcome.AlreadyAbsent:SourceRetirementOutcome.OriginalReceipt);
        if(LoseReply){LoseReply=false;return Task.FromResult(new WorkspaceTaskResponse<WorkspaceSourceRetirementView>(false,null,"Lost original response",true));}return Task.FromResult(new WorkspaceTaskResponse<WorkspaceSourceRetirementView>(true,Saved,"Confirmed cleanup"));
    }
    public void Save(SourceRetirementOutcome outcome)
    {
        var at=DateTimeOffset.UtcNow;var revision=Plan.Revision+1;var receipt=new WorkspaceSourceRetirement(View!.CommandId,revision,Plan.ConversionId,Plan.Source.Id,Plan.Source.Revision,Plan.Revision,Guid.Parse(WorkspaceTaskFixture.User),Plan.SourceMapping.HabiticaTaskId!,Plan.ContextHash,outcome,outcome==SourceRetirementOutcome.AlreadyAbsent?null:Guid.NewGuid(),View.VerifiedAtUtc??at,at);
        Saved=View with {State="confirmed",SavedInCompanion=true,Receipt=receipt,OutcomeUnknown=false};
        State=State with {StoreVersion=State.StoreVersion+1,Document=State.Document with {Revision=revision,Tasks=State.Document.Tasks.Select(task=>task.Id==Plan.Source.Id?task with {Revision=revision,IsArchived=true,UpdatedAtUtc=at}:task).ToArray(),Projections=State.Document.Projections.Select(mapping=>mapping.TaskId==Plan.Source.Id?mapping with {Revision=revision,IsArchived=true}:mapping).ToArray(),SourceRetirements=[receipt]}};
    }
    public Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> ReadSourceRetirementAsync(Guid id,CancellationToken token)=>Task.FromResult(new WorkspaceTaskResponse<WorkspaceSourceRetirementView>(true,Saved??View,"Original command only"));
    public Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> ReconcileSourceRetirementAsync(Guid id,CancellationToken token){Save(SourceRetirementOutcome.ObservedAbsentAfterUnknown);return ReadSourceRetirementAsync(id,token);}
}
