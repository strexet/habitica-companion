import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
import type {WorkspaceDocument} from './workspace-domain.ts';
import type {CommandEntry} from '../companion-workspace/src/commands.ts';
export interface CompletionScoreSource {contract:'companion-completion-score-v1';workspaceId:string;taskId:string;commandId:string;commandPayloadHash:string;completionId:string;effectIndex:number;taskKind:'todo'|'daily';occurrenceDate:string|null}
export function completionScoreSource(input:unknown):CompletionScoreSource|null {
 if(input===undefined||input===null)return null;
 const row=workspaceObject(input,['contract','workspaceId','taskId','commandId','commandPayloadHash','completionId','effectIndex','taskKind','occurrenceDate']);
 if(row.contract!=='companion-completion-score-v1'||!['todo','daily'].includes(String(row.taskKind))||typeof row.commandPayloadHash!=='string'||!/^[a-f0-9]{64}$/.test(row.commandPayloadHash)
  ||!Number.isInteger(row.effectIndex)||Number(row.effectIndex)<0||Number(row.effectIndex)>10)throw new WorkspaceError('invalid_completion_source',422);
 const date=row.occurrenceDate??null;
 if(row.taskKind==='daily'?(typeof date!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(date)||date.startsWith('0000')||!Number.isFinite(Date.parse(date))||new Date(date).toISOString().slice(0,10)!==date):date!==null)throw new WorkspaceError('invalid_completion_source',422);
 return {contract:'companion-completion-score-v1',workspaceId:uuid(row.workspaceId),taskId:uuid(row.taskId),commandId:uuid(row.commandId),commandPayloadHash:row.commandPayloadHash,completionId:uuid(row.completionId),effectIndex:Number(row.effectIndex),taskKind:row.taskKind as 'todo'|'daily',occurrenceDate:date as string|null};
}
export function sameCompletionScoreSource(a:unknown,b:unknown):boolean {
 try{const first=completionScoreSource(a),second=completionScoreSource(b);return first!==null&&second!==null&&JSON.stringify(first)===JSON.stringify(second);}catch{return false;}
}
export function completionScoreBinding(document:WorkspaceDocument,entry:CommandEntry):CompletionScoreSource|null {
 if(entry.command.operation!=='completeTask'||entry.effects[entry.confirmedEffects]!=='scoreUp')return null;
 const task=document.tasks.find(row=>row.id===entry.command.taskId),events=document.completions.filter(row=>row.commandId===entry.command.id&&row.taskId===entry.command.taskId);
 if(!task||!['todo','daily'].includes(task.kind)||events.length!==1)return null;
 const event=events[0];
 if(task.kind==='daily'?!document.occurrences.some(row=>row.id===event.occurrenceId&&row.taskId===task.id&&row.date===entry.command.occurrenceDate):event.occurrenceId!==null||entry.command.occurrenceDate!==null)return null;
 return completionScoreSource({contract:'companion-completion-score-v1',workspaceId:document.id,taskId:task.id,commandId:entry.command.id,commandPayloadHash:entry.payloadHash,completionId:event.id,effectIndex:entry.confirmedEffects,taskKind:task.kind,occurrenceDate:entry.command.occurrenceDate});
}
