using Habitica.Domain.Workspace;
namespace Habitica.Application.Workspace;

public static class WorkspaceTaskTypeCopyEngine
{
    // Common validation, original duplicate recovery, ownership and root revision run before this bounded branch.
    internal static CommandSubmission Accept(WorkspaceJournalSnapshot snapshot,WorkspaceCommand command,string hash,DateTimeOffset now)
    {
        CommandSubmission Refuse(string code)=>new(SubmissionDisposition.Invalid,snapshot,null,code);
        if(command.ProjectToHabitica||command.Patch?.TypeCopy is not {} choice)return Refuse("native_type_copy_required");
        var document=snapshot.Document;
        if(document.Tasks.Any(task=>task.CopyLineage?.CommandId==command.Id)
            ||document.ChecklistConversions?.Any(row=>row.Receipts.Any(receipt=>receipt.Command.Id==command.Id))==true
            ||document.SourceRetirements?.Any(row=>row.Id==command.Id)==true)return Refuse("command_id_reused");
        var review=WorkspaceTaskTypeCopyPolicy.Review(document,choice,command.TaskId,now);
        if(!review.Ready)return Refuse(review.Failure??"type_copy_unavailable");
        var target=review.Target!;var source=review.Source! with{CopyLineage=null};
        target=target with{CopyLineage=new(document.Id,source,target.Id,target.Kind,choice.CopyOrganization,target.Revision,command.Id,hash,now,choice.Recurrence)};
        var entry=new WorkspaceCommandEntry(command,hash,now,ProjectionCommandState.NotRequested,[],TaskSnapshot:target);
        var result=document with{Revision=target.Revision,Tasks=document.Tasks.Append(target).ToArray(),ActivityCoverage=(document.ActivityCoverage??[]).Append(WorkspaceActivityCoverage.Witness(entry,0)).ToArray()};
        var updated=new WorkspaceJournalSnapshot(result,snapshot.StoreVersion+1,snapshot.Commands.Append(entry).ToArray(),(snapshot.Events??[]).Append(new WorkspaceJournalEvent(Guid.NewGuid(),command.Id,JournalEventKind.Accepted,now,0,ProjectionFailure.None)).ToArray(),snapshot.Authority);
        return WorkspaceJournalValidation.IsValid(updated)?new(SubmissionDisposition.Accepted,updated,entry):Refuse("invalid_type_copy_result");
    }
}
