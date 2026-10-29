import {remoteTask,type RemoteTask} from '../../shared/projection.ts';
import {habiticaDay} from '../../shared/cron.ts';
import {workspaceObject,uuid,WorkspaceError} from '../../shared/workspace-http.ts';
import {recoveryStateHash,unknownRecoveryResources,type RecoveryIntent,type RecoveryReview} from '../../shared/linked-task-recovery.ts';
export async function observeTaskRecovery(read:(path:string)=>Promise<unknown>,ownerId:string,intent:RecoveryIntent):Promise<{task:RemoteTask;review:RecoveryReview}> {
 const user=workspaceObject(await read('user')),preferences=workspaceObject(user.preferences);
 if(uuid(user._id??user.id)!==ownerId)throw new WorkspaceError('identity_mismatch',409);
 const day=habiticaDay(Date.now(),preferences.dayStart,preferences.timezoneOffset);
 const lastCron=typeof user.lastCron==='string'?Date.parse(user.lastCron):NaN;
 if(user.needsCron!==false||!Number.isFinite(lastCron)||lastCron<Date.parse(day.dayStartUtc)||lastCron>Date.now()+5000
  ||preferences.timezoneOffsetAtLastCron!==preferences.timezoneOffset)throw new WorkspaceError('recovery_day_unknown',409);
 if(intent.taskKind==='daily'&&intent.occurrenceDate!==day.day)throw new WorkspaceError('recovery_day_mismatch',409);
 let raw:unknown;
 if(intent.taskKind==='daily'){
  const rows=await read('tasks/user?type=dailys&history=false&dueDate='+encodeURIComponent(new Date().toISOString()));
  if(!Array.isArray(rows)||rows.length>1000)throw new WorkspaceError('recovery_task_capacity',413);
  const matching=rows.filter(value=>{const row=workspaceObject(value);return (row.id??row._id)===intent.selector;});
  if(matching.length!==1)throw new WorkspaceError('recovery_task_unavailable',409);raw=matching[0];
 }else raw=await read('tasks/'+encodeURIComponent(intent.selector));
 const value=workspaceObject(raw);
 // Reject linked shared copies even when a group/challenge id is omitted but taskId remains.
 for(const field of ['group','challenge']){
  const link=value[field];if(link!==undefined&&link!==null){const metadata=workspaceObject(link);if(metadata.id||metadata.taskId)throw new WorkspaceError('task_not_owned',409);}
 }
 const task=await remoteTask(raw,ownerId,intent.taskKind==='daily');
 if(task.id!==intent.selector||task.kind!==intent.taskKind||!task.completed||intent.taskKind==='daily'&&task.isDue!==true)throw new WorkspaceError('recovery_task_unavailable',409);
 if(typeof value.value!=='number'||!Number.isFinite(value.value)||intent.taskKind==='daily'&&(!Number.isSafeInteger(value.streak)||Number(value.streak)<0))throw new WorkspaceError('recovery_state_unknown',409);
 const dateCompleted=intent.taskKind==='todo'&&typeof value.dateCompleted==='string'&&Number.isFinite(Date.parse(value.dateCompleted))?new Date(value.dateCompleted).toISOString():null;
 if(intent.taskKind==='todo'&&dateCompleted===null)throw new WorkspaceError('recovery_state_unknown',409);
 const currentDay=habiticaDay(Date.now(),preferences.dayStart,preferences.timezoneOffset);if(currentDay.day!==day.day)throw new WorkspaceError('recovery_day_changed',409);
 const critical={contract:'own-task-recovery-v1',contextHash:intent.contextHash,ownerId,taskId:task.id,taskKind:task.kind,fingerprint:task.fingerprint,completed:task.completed,isDue:task.isDue,
  occurrenceDate:intent.occurrenceDate,accountDay:day.day,lastCron:new Date(lastCron).toISOString(),dayStart:preferences.dayStart,timezoneOffset:preferences.timezoneOffset,
  timezoneOffsetAtLastCron:preferences.timezoneOffsetAtLastCron,value:value.value,streak:intent.taskKind==='daily'?value.streak:null,dateCompleted};
 return {task,review:{contract:'own-task-recovery-v1',contextHash:intent.contextHash,taskId:task.id,taskKind:intent.taskKind,fingerprint:task.fingerprint,occurrenceDate:intent.occurrenceDate,accountDay:day.day,reviewHash:await recoveryStateHash(critical),resourceChanges:unknownRecoveryResources()}};
}
