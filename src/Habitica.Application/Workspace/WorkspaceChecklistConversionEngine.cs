using System.Security.Cryptography;
using System.Text;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record ChecklistConversionSubmission(SubmissionDisposition Disposition,WorkspaceJournalSnapshot? Snapshot,
    WorkspaceChecklistConversion? Conversion,string? ErrorCode=null);

public static class WorkspaceChecklistConversionEngine
{
    public static Guid DerivedId(Guid commandId,string label)
    {
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("companion-conversion-v1/"+commandId.ToString("D")+"/"+label))).ToLowerInvariant().ToCharArray();
        hash[12]='4';hash[16]='8';return Guid.ParseExact(new string(hash,0,32),"N");
    }
    public static ChecklistConversionSubmission Accept(WorkspaceJournalSnapshot state,ChecklistConversionCommand command,DateTimeOffset now)
    {
        ChecklistConversionSubmission Reject(string code,SubmissionDisposition disposition=SubmissionDisposition.Invalid)=>new(disposition,state,null,code);
        if(!WorkspaceJournalValidation.IsValid(state))return Reject("invalid_workspace");
        if(!WorkspaceChecklistConversions.ValidCommand(command)||command.WorkspaceId!=state.Document.Id||now==default||now.Offset!=TimeSpan.Zero)return Reject("invalid_conversion_command");
        var document=state.Document;var hash=WorkspaceChecklistConversions.Hash(command);
        var original=document.ChecklistConversions?.SelectMany(row=>row.Receipts.Select(receipt=>(Conversion:row,Receipt:receipt))).FirstOrDefault(row=>row.Receipt.Command.Id==command.Id);
        if(original?.Receipt is {} receipt)return receipt.PayloadHash==hash?new(SubmissionDisposition.Duplicate,state,original.Value.Conversion):Reject("command_id_reused",SubmissionDisposition.IdempotencyConflict);
        if(state.Commands.Any(row=>row.Command.Id==command.Id)||document.OrganizationHistory?.Any(row=>row.Command.Id==command.Id)==true)return Reject("command_id_reused",SubmissionDisposition.IdempotencyConflict);
        if(state.Authority?.Mode is WorkspaceAuthorityMode.Cloud or WorkspaceAuthorityMode.Frozen)return Reject("workspace_owned_elsewhere");
        if(command.ExpectedRevision!=document.Revision)return Reject("stale_revision",SubmissionDisposition.RevisionConflict);
        var source=document.Tasks.FirstOrDefault(task=>task.Id==command.SourceTaskId);
        if(source is not {Kind:CompanionTaskKind.Todo,IsArchived:false}||source.Revision!=command.SourceRevision||source.HabiticaDetails?.Checklist is not {Count:>0} checklist)return Reject("conversion_source_changed");
        if(!checklist.Select(item=>item.Id).SequenceEqual(command.Items.Select(item=>item.SourceItemId)))return Reject("complete_checklist_mapping_required");
        var prior=document.ChecklistConversions?.FirstOrDefault(row=>row.SourceTaskId==source.Id);
        if(prior is not null&&(prior.IsArchived||prior.Id!=command.ConversionId||prior.SourceRevision!=source.Revision||prior.ProjectId!=command.ProjectId))return Reject("resume_original_conversion_required");
        if(prior is null&&document.ChecklistConversions?.Any(row=>row.Id==command.ConversionId)==true)return Reject("conversion_id_reused");
        var project=document.Projects.FirstOrDefault(row=>row.Id==command.ProjectId);
        if(project is not null&&(project.IsArchived||project.IsRemoved||project.Name!=command.ProjectName))return Reject("conversion_project_changed");
        if(prior is not null&&project is null)return Reject("conversion_project_missing");
        var next=state;
        if(project is null)
        {
            var category=source.CategoryId??document.Projects.FirstOrDefault(row=>row.Id==source.ProjectId)?.CategoryId;
            var created=WorkspaceOrganizationEngine.Accept(next,new(DerivedId(command.Id,"project"),document.Id,next.Document.Revision,WorkspaceOrganizationKind.Project,
                WorkspaceOrganizationOperation.Create,command.ProjectId,new(Name:command.ProjectName,CategoryId:category)),now);
            if(created.Disposition!=SubmissionDisposition.Accepted)return Reject(created.ErrorCode??"conversion_project_unavailable");next=created.Snapshot!;
        }
        var bindings=new List<ChecklistConversionBinding>();
        for(var index=0;index<command.Items.Count;index++)
        {
            var item=command.Items[index];var old=prior?.Items[index];
            if(old?.TargetTaskId is {} bound&&(item.TargetTaskId!=bound||item.DesiredState!=old.DesiredState||item.Mode==ChecklistConversionMode.Defer))return Reject("conversion_binding_changed");
            if(old?.TargetTaskId is not null&&item.Notes is not null&&item.Notes!=old.AddedNotes)return Reject("conversion_notes_changed");
            if(item.Mode==ChecklistConversionMode.Defer){bindings.Add(new(item.SourceItemId,null,item.DesiredState,false,null,null,item.DesiredState==ChecklistConversionState.CompletedArchived));continue;}
            if(document.ChecklistConversions?.Any(row=>row.Id!=command.ConversionId&&row.Items.Any(binding=>binding.TargetTaskId==item.TargetTaskId))==true)return Reject("conversion_target_bound_elsewhere");
            var target=next.Document.Tasks.FirstOrDefault(task=>task.Id==item.TargetTaskId);var newlyCreated=false;var createdByConversion=old?.CreatedByConversion==true;
            if(target is null)
            {
                if(item.Mode!=ChecklistConversionMode.Create||old?.TargetTaskId is not null)return Reject(item.DesiredState==ChecklistConversionState.CompletedArchived?"historical_target_required":"conversion_target_missing");
                var notes=source.Notes;
                if(item.Notes is not null)notes=notes is null?item.Notes:notes+"\n\n"+item.Notes;
                if(notes?.Length>10000)return Reject("conversion_note_capacity");
                var created=WorkspaceCommandEngine.Accept(next,new(DerivedId(command.Id,"create:"+item.SourceItemId.ToString("D")),document.Id,next.Document.Revision,
                    WorkspaceOperation.CreateTodo,item.TargetTaskId!.Value,new(Text:checklist[index].Text,Notes:notes,Priority:source.Priority,
                        HabiticaDetails:new(item.GameDetails!.Difficulty,item.GameDetails.Attribute,[])),ProjectToHabitica:true),now);
                if(created.Disposition!=SubmissionDisposition.Accepted)return Reject(created.ErrorCode??"conversion_capture_unavailable");next=created.Snapshot!;target=next.Document.Tasks.Single(task=>task.Id==item.TargetTaskId);createdByConversion=newlyCreated=true;
            }
            else if(item.Mode==ChecklistConversionMode.Create&&!createdByConversion)return Reject("conversion_target_already_exists");
            if(old?.CreatedByConversion==true&&item.Mode==ChecklistConversionMode.Create&&(item.GameDetails?.Difficulty!=old.OriginalTarget?.HabiticaDetails?.Difficulty||item.GameDetails?.Attribute!=old.OriginalTarget?.HabiticaDetails?.Attribute))return Reject("conversion_creation_fields_changed");
            if(target.Id==source.Id||target.Kind!=CompanionTaskKind.Todo||item.DesiredState==ChecklistConversionState.Active&&(target.State!=CompanionTaskState.Active||target.IsArchived)
                ||item.DesiredState==ChecklistConversionState.CompletedArchived&&target.State!=CompanionTaskState.Completed)return Reject("conversion_target_state_changed");
            // A completed historical target is retained and archived; no new completion event or score is fabricated.
            var labels=newlyCreated?(source.LabelIds??[]):target.LabelIds;
            var placed=target with {ProjectId=command.ProjectId,SectionId=target.ProjectId==command.ProjectId?target.SectionId:null,
                CategoryId=newlyCreated?source.CategoryId:target.CategoryId,LabelIds=labels,IsArchived=item.DesiredState==ChecklistConversionState.CompletedArchived,
                Revision=next.Document.Revision+1,UpdatedAtUtc=now};
            if(target.ProjectId!=placed.ProjectId||target.SectionId!=placed.SectionId||target.CategoryId!=placed.CategoryId||target.IsArchived!=placed.IsArchived
                ||!(target.LabelIds??[]).SequenceEqual(placed.LabelIds??[]))
            {
                next=next with {Document=next.Document with {Revision=placed.Revision,Tasks=next.Document.Tasks.Select(task=>task.Id==target.Id?placed:task).ToArray()},StoreVersion=next.StoreVersion+1};
            }
            bindings.Add(new(item.SourceItemId,target.Id,item.DesiredState,createdByConversion,old?.AddedNotes??item.Notes,old?.OriginalTarget??target,item.DesiredState==ChecklistConversionState.CompletedArchived));
        }
        var revision=next.Document.Revision+1;
        if(revision>WorkspaceDocument.MaxRevision||next.StoreVersion>=WorkspaceDocument.MaxRevision)return Reject("revision_exhausted");
        var conversion=new WorkspaceChecklistConversion(command.ConversionId,revision,source.Id,source.Revision,prior?.SourceSnapshot??source,
            prior?.SourceProjection??document.Projections.FirstOrDefault(row=>row.TaskId==source.Id),command.ProjectId,bindings,
            (prior?.Receipts??[]).Append(new(command,hash,now,revision)).ToArray());
        var rows=(document.ChecklistConversions??[]).Where(row=>row.Id!=conversion.Id).Append(conversion).ToArray();
        next=next with {Document=next.Document with {Revision=revision,ChecklistConversions=rows},StoreVersion=next.StoreVersion+1};
        return WorkspaceJournalValidation.IsValid(next)?new(SubmissionDisposition.Accepted,next,conversion):Reject("invalid_conversion_result");
    }
}
