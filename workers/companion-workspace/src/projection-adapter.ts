import type { HabiticaTaskDetails } from "../../shared/task-details.ts";
import { workspaceDocument, type WorkspaceDocument, type CompanionTask, type Projection } from "../../shared/workspace-domain.ts";
import { projectionAlias, remotePatchMatches, type RemoteTask, type RemotePatch } from "../../shared/projection.ts";
import { WorkspaceError, uuid } from "../../shared/workspace-http.ts";
import type { CommandEntry } from "./commands";
export interface Observation { task: RemoteTask | null; checkToken: string }
export interface AccountObservation { day: string; needsCron: boolean; accountCheckToken: string }
export type ProjectionStep = { kind: "inspect"; operation: "inspect" | "inspectDaily"; selector: string }
 | { kind: "account" } | { kind: "blocked"; code: string }
 | { kind: "write"; operation: "create" | "update" | "score"; arguments: Record<string,unknown> }
 | { kind: "observedApplied"; task: RemoteTask; evidence: string };
function detailsPatch(details?:HabiticaTaskDetails|null):Partial<RemotePatch>{return {...(details?.difficulty!=null?{priority:details.difficulty}:{}),...(details?.attribute!=null?{attribute:details.attribute}:{}),...(details?.checklist!=null?{checklist:details.checklist}:{} )};}
function createPatch(task: CompanionTask): RemotePatch { return { ...detailsPatch(task.habiticaDetails), text: task.text, notes: task.notes ?? "", ...(task.deadline ? { date: task.deadline } : {}) }; }
function updatePatch(entry: CommandEntry): RemotePatch {
 const patch = entry.command.patch; if (!patch) return {};
 return { ...detailsPatch(patch.habiticaDetails), ...(patch.text === null ? {} : { text: patch.text }), ...(patch.clearNotes || patch.notes !== null ? { notes: patch.clearNotes ? "" : patch.notes! } : {}), ...(patch.clearDeadline || patch.deadline !== null ? { date: patch.clearDeadline ? null : patch.deadline! } : {}) };
}
export const patchMatches = remotePatchMatches;
export function projectionStep(document: WorkspaceDocument, entry: CommandEntry, observation?: Observation, account?: AccountObservation): ProjectionStep {
 const task = entry.taskSnapshot; const mapping = document.projections.find(row=>row.taskId===entry.command.taskId && !row.isArchived);
 const blocked = (code: string): ProjectionStep => ({ kind: "blocked",code });
 if (!task || task.id !== entry.command.taskId || task.revision !== entry.command.expectedRevision+1) return blocked("accepted_task_snapshot_required");
 if (!mapping || ["unlinked","externalConflict"].includes(mapping.state) || mapping.state === "unknownOutcome" && entry.projectionState !== "unknownOutcome") return blocked("projection_review_required");
 const effect = entry.effects[entry.confirmedEffects]; if (!effect) return blocked("no_pending_effect");
 const alias = projectionAlias(document.id, task.id);
 if (mapping.correlationAlias && mapping.correlationAlias !== alias) return blocked("projection_identity_conflict");
 const creating = effect === "create" && !mapping.habiticaTaskId;
 if (creating && task.kind !== "todo") return blocked("adoption_required");
 const target = creating ? alias : mapping.habiticaTaskId;
 if (!target) return blocked("adoption_required");
 if (effect.startsWith("score") && task.kind === "daily" && !account) return {kind:"account"};
 if (!observation) return {kind:"inspect",operation:task.kind==="daily"?"inspectDaily":"inspect",selector:target};
 const remote = observation.task;
 if (creating) {
  const patch = createPatch(task);
  if (remote) return entry.projectionState === "unknownOutcome" && remote.alias === alias && remote.kind === "todo" && patchMatches(remote,patch)
   ? {kind:"observedApplied",task:remote,evidence:"verified-correlated-create"} : blocked("correlation_requires_review");
  if (entry.projectionState === "unknownOutcome") return blocked("unknown_create_not_proven_absent");
  return {kind:"write",operation:"create",arguments:{selector:alias,checkToken:observation.checkToken,patch}};
 }
 if (!remote) return blocked("external_deleted");
 if (remote.id !== target || remote.kind !== task.kind || mapping.correlationAlias && remote.alias !== alias) return blocked("projection_identity_conflict");
 if (entry.projectionState === "unknownOutcome") {
  if (effect === "update" && patchMatches(remote,updatePatch(entry))) return {kind:"observedApplied",task:remote,evidence:"verified-update-state"};
  return blocked("score_outcome_requires_review");
 }
 if (!mapping.fingerprint || mapping.fingerprint !== remote.fingerprint) return blocked("external_edit_review_required");
 if (effect === "create") return {kind:"observedApplied",task:remote,evidence:"verified-existing-adoption"};
 if (effect === "update") {
  const patch = updatePatch(entry); if (Object.keys(patch).length===0) return {kind:"observedApplied",task:remote,evidence:"companion-only-metadata"};
  return {kind:"write",operation:"update",arguments:{selector:target,checkToken:observation.checkToken,expectedFingerprint:remote.fingerprint,patch}};
 }
 if (remote.kind === "habit" ? !(effect==="scoreUp"?remote.up:remote.down) : effect!=="scoreUp" || remote.completed) return blocked("external_completion_or_direction_review");
 if (remote.kind === "daily" && (!remote.isDue || !account || account.needsCron || account.day!==entry.command.occurrenceDate)) return blocked("daily_precondition_unknown");
 return {kind:"write",operation:"score",arguments:{selector:target,checkToken:observation.checkToken,expectedFingerprint:remote.fingerprint,direction:effect==="scoreUp"?"up":"down",...(remote.kind==="daily"?{accountCheckToken:account!.accountCheckToken,occurrenceDate:entry.command.occurrenceDate}:{})}};
}
export function adoptProjection(document: WorkspaceDocument, taskId: string, remote: RemoteTask, expectedFingerprint: string): WorkspaceDocument {
 taskId=uuid(taskId); const task=document.tasks.find(row=>row.id===taskId && !row.isArchived);
 if (!task || task.kind!==remote.kind || remote.fingerprint!==expectedFingerprint) throw new WorkspaceError("adoption_conflict",409);
 if (document.revision===Number.MAX_SAFE_INTEGER || document.projections.some(row=>row.taskId!==taskId && row.habiticaTaskId===remote.id)) throw new WorkspaceError("projection_identity_conflict",409);
 const mapping=document.projections.find(row=>row.taskId===taskId);
 if (mapping?.habiticaTaskId && mapping.habiticaTaskId!==remote.id) throw new WorkspaceError("projection_identity_conflict",409);
 const revision=document.revision+1;
 const adopted: Projection={id:mapping?.id??crypto.randomUUID(),revision,taskId,state:"linked",habiticaTaskId:remote.id,isArchived:false,fingerprint:remote.fingerprint,correlationAlias:remote.alias===projectionAlias(document.id,taskId)?remote.alias:null};
 return workspaceDocument({...document,revision,projections:[...document.projections.filter(row=>row.taskId!==taskId),adopted]});
}
export function confirmMapping(document: WorkspaceDocument, taskId: string, remote: Pick<RemoteTask,"id"|"fingerprint"|"alias">): WorkspaceDocument {
 const mapping=document.projections.find(row=>row.taskId===taskId && !row.isArchived);
 if (!mapping || mapping.habiticaTaskId && mapping.habiticaTaskId!==remote.id || document.projections.some(row=>row.taskId!==taskId && row.habiticaTaskId===remote.id)) throw new WorkspaceError("projection_identity_conflict",409);
 const revision=document.revision+1;
 return workspaceDocument({...document,revision,projections:document.projections.map(row=>row.id!==mapping.id?row:{...row,revision,state:"linked",habiticaTaskId:uuid(remote.id),fingerprint:remote.fingerprint,correlationAlias:remote.alias===projectionAlias(document.id,taskId)?remote.alias:row.correlationAlias??null})});
}
