import {reviewTypeCopy,type TaskCopyLineage} from '../../shared/task-type-copy.ts';
import {activityWitness} from '../../shared/activity-coverage.ts';
import {commandHash,type WorkspaceCommand} from '../../shared/workspace-commands.ts';
import {workspaceDocument,type WorkspaceDocument} from '../../shared/workspace-domain.ts';
import {WorkspaceError} from '../../shared/workspace-http.ts';
import type {CommandEntry,JournalEvent} from './commands.ts';
export async function acceptTypeCopy(document:WorkspaceDocument,command:WorkspaceCommand,now:string):Promise<{document:WorkspaceDocument;entry:CommandEntry;event:JournalEvent}>{
 const choice=command.patch?.typeCopy;if(!choice||command.projectToHabitica)throw new WorkspaceError('native_type_copy_required',422);
 if(document.tasks.some(task=>task.copyLineage?.commandId===command.id)||document.checklistConversions?.some(row=>row.receipts.some(receipt=>receipt.command.id===command.id))||document.sourceRetirements?.some(row=>row.id===command.id))throw new WorkspaceError('command_id_reused',409);
 const review=reviewTypeCopy(document,choice,command.taskId,now);if(!review.target||!review.source||review.failure)throw new WorkspaceError(review.failure??'type_copy_unavailable',422);
 const payloadHash=await commandHash(command),sourceSnapshot={...review.source};delete sourceSnapshot.copyLineage;
 const copyLineage:TaskCopyLineage={sourceWorkspaceId:document.id,sourceSnapshot,targetTaskId:command.taskId,targetKind:choice.targetKind,copyOrganization:choice.copyOrganization,initialTargetRevision:review.target.revision,commandId:command.id,payloadHash,copiedAtUtc:now,...(choice.recurrence?{recurrence:choice.recurrence}:{})};
 const target={...review.target,copyLineage};const entry:CommandEntry={command,payloadHash,acceptedAtUtc:now,projectionState:'notRequested',effects:[],confirmedEffects:0,failure:'none',retryAtUtc:null,lease:null,dispatchedAtUtc:null,attempts:0,taskSnapshot:target};
 const result=workspaceDocument({...document,revision:target.revision,tasks:[...document.tasks,target],activityCoverage:[...(document.activityCoverage??[]),activityWitness(entry,0)]});
 return {document:result,entry,event:{id:crypto.randomUUID(),commandId:command.id,kind:'accepted',atUtc:now,confirmedEffects:0,failure:'none',attemptToken:null,evidenceReference:null}};
}
