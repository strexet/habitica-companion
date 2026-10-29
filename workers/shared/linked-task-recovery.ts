import {uuid,workspaceObject,workspaceTokenHash,WorkspaceError} from './workspace-http.ts';
export interface RecoveryIntent {selector:string;taskKind:'todo'|'daily';occurrenceDate:string|null;contextHash:string}
export interface RecoveryReview {contract:'own-task-recovery-v1';contextHash:string;taskId:string;taskKind:'todo'|'daily';fingerprint:string;occurrenceDate:string|null;accountDay:string;reviewHash:string;resourceChanges:{hp:'unknown';mp:'unknown';gp:'unknown';xp:'unknown';streak:'unknown'}}
export function recoveryIntent(input:unknown):RecoveryIntent {
 const row=workspaceObject(input,['selector','taskKind','occurrenceDate','contextHash']);
 if(!['todo','daily'].includes(String(row.taskKind))||typeof row.contextHash!=='string'||!/^[a-f0-9]{64}$/.test(row.contextHash))throw new WorkspaceError('invalid_recovery_context',422);
 const occurrence=row.occurrenceDate??null;
 if(row.taskKind==='daily'?(typeof occurrence!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(occurrence)||occurrence.startsWith('0000')||!Number.isFinite(Date.parse(occurrence))||new Date(occurrence).toISOString().slice(0,10)!==occurrence):occurrence!==null)throw new WorkspaceError('invalid_recovery_context',422);
 return {selector:uuid(row.selector),taskKind:row.taskKind as RecoveryIntent['taskKind'],occurrenceDate:occurrence as string|null,contextHash:row.contextHash};
}
export const unknownRecoveryResources=()=>({hp:'unknown',mp:'unknown',gp:'unknown',xp:'unknown',streak:'unknown'} as const);
export async function recoveryStateHash(value:unknown){return workspaceTokenHash(JSON.stringify(value));}
