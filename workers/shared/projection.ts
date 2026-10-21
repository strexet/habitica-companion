import {readDailySchedule,sameDailySchedule,type DailySchedule} from './daily-projection.ts';
import { WorkspaceError, workspaceObject, uuid, workspaceTokenHash } from "./workspace-http.ts";
export interface RemoteTask {
 id: string; kind: "todo" | "daily" | "habit"; text: string; notes: string; alias: string | null;
 priority: number; attribute: string; date: string | null; checklist: { id: string; text: string; completed: boolean }[];
 schedule?: DailySchedule | null; completed: boolean; up: boolean; down: boolean; isDue: boolean | null; fingerprint: string;
}
export interface RemotePatch extends Partial<DailySchedule> { up?: boolean; down?: boolean; text?: string; notes?: string; date?: string | null; priority?: number; attribute?: string; checklist?: { id?: string; text: string; completed: boolean }[] }
export function projectionAlias(workspaceId: string, taskId: string): string { return `companion_${uuid(workspaceId).replaceAll("-", "")}_${uuid(taskId).replaceAll("-", "")}`; }
export function selector(value: unknown): string {
 if (typeof value === "string" && /^companion_[0-9a-f]{32}_[0-9a-f]{32}$/.test(value)) return value;
 return uuid(value);
}
export function remotePatch(input: unknown, creating = false, kind?: string): RemotePatch {
 const value = workspaceObject(input, ["text", "notes", "date", "priority", "attribute", "checklist", "up", "down", "frequency", "everyX", "startDate", "repeat", "daysOfMonth", "weeksOfMonth"]); const patch: RemotePatch = {};
 for (const field of ["text", "notes"] as const) if (value[field] !== undefined) {
  if (typeof value[field] !== "string" || value[field].length > (field === "text" ? 2048 : 10000) || field === "text" && !value[field].trim()) throw new WorkspaceError("invalid_patch", 422);
  patch[field] = value[field];
 }
 for(const field of ['up','down'] as const)if(value[field]!==undefined){if(kind!=='habit'||typeof value[field]!=='boolean')throw new WorkspaceError('invalid_patch',422);patch[field]=value[field];}
 if(kind==='daily'&&value.date!==undefined)throw new WorkspaceError('unsupported_daily_fields',422);
 const scheduleFields=['frequency','everyX','startDate','repeat','daysOfMonth','weeksOfMonth'];
 if(scheduleFields.some(field=>value[field]!==undefined)){if(kind!=='daily'||!creating)throw new WorkspaceError('unsupported_schedule_edit',422);Object.assign(patch,readDailySchedule(value));}
 if(creating&&kind==='daily'&&patch.frequency===undefined)throw new WorkspaceError('daily_schedule_required',422);
 if(kind==='habit'&&(value.date!==undefined||value.checklist!==undefined))throw new WorkspaceError('unsupported_habit_fields',422);
 if(creating&&kind==='habit'){patch.up??=true;patch.down??=true;}
 if (creating && patch.text === undefined) throw new WorkspaceError("invalid_patch", 422);
 if (value.priority !== undefined) { if (![0.1, 1, 1.5, 2].includes(value.priority as number)) throw new WorkspaceError("invalid_patch", 422); patch.priority = value.priority as number; }
 if (value.attribute !== undefined) { if (!["str", "int", "per", "con"].includes(value.attribute as string)) throw new WorkspaceError("invalid_patch", 422); patch.attribute = value.attribute as string; }
 if (value.date !== undefined) {
  if (value.date !== null && (typeof value.date !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(value.date) || value.date.startsWith("0000") || !Number.isFinite(Date.parse(value.date)) || new Date(value.date).toISOString().slice(0,10) !== value.date)) throw new WorkspaceError("invalid_patch", 422);
  patch.date = value.date as string | null;
 }
 if (value.checklist !== undefined) {
  if (!Array.isArray(value.checklist) || value.checklist.length > 100) throw new WorkspaceError("invalid_patch", 422);
  patch.checklist = value.checklist.map(item => { const row = workspaceObject(item, ["id", "text", "completed"]); if (typeof row.text !== "string" || !row.text.trim() || row.text.length > 2048 || typeof row.completed !== "boolean") throw new WorkspaceError("invalid_patch", 422); return { ...(row.id === undefined ? {} : { id: uuid(row.id) }), text: row.text, completed: row.completed }; });
  const ids = patch.checklist.flatMap(row => row.id ? [row.id] : []); if (new Set(ids).size !== ids.length) throw new WorkspaceError("invalid_patch", 422);
 }
 if (Object.keys(patch).length === 0) throw new WorkspaceError("invalid_patch", 422); return patch;
}
export async function remoteTask(input: unknown, userId: string, dueKnown = false): Promise<RemoteTask> {
 const value = workspaceObject(input); const id = uuid(value.id ?? value._id);
 const group = value.group && typeof value.group === "object" ? value.group as Record<string,unknown> : {};
 const challenge = value.challenge && typeof value.challenge === "object" ? value.challenge as Record<string,unknown> : {};
 if (typeof value.userId !== "string" || value.userId.toLowerCase() !== userId.toLowerCase() || group.id || challenge.id || !["todo","daily","habit"].includes(value.type as string)) throw new WorkspaceError("task_not_owned", 409);
 if (typeof value.text !== "string" || value.text.length > 2048 || typeof value.notes !== "string" || value.notes.length > 10000 || (value.type!=="habit"&&typeof value.completed !== "boolean" || value.type==="habit"&&value.completed!==undefined&&value.completed!==false)) throw new WorkspaceError("invalid_upstream_response", 502);
 if(value.type==='habit'&&(typeof value.up!=='boolean'||typeof value.down!=='boolean'))throw new WorkspaceError('invalid_upstream_response',502);
 const checklist = value.checklist === undefined ? [] : value.checklist;
 if (!Array.isArray(checklist) || checklist.length > 100) throw new WorkspaceError("invalid_upstream_response", 502);
 const items = checklist.map(input => { const row = workspaceObject(input); if (typeof row.text !== "string" || row.text.length > 2048 || typeof row.completed !== "boolean") throw new WorkspaceError("invalid_upstream_response", 502); return { id: uuid(row.id), text: row.text, completed: row.completed }; });
 if (new Set(items.map(item => item.id)).size !== items.length || ![0.1,1,1.5,2].includes(value.priority as number) || !["str","int","per","con"].includes(value.attribute as string)) throw new WorkspaceError("invalid_upstream_response", 502);
 const date = value.date === undefined || value.date === null ? null : typeof value.date === "string" && Number.isFinite(Date.parse(value.date)) ? value.date : (() => { throw new WorkspaceError("invalid_upstream_response",502); })();
 const alias = typeof value.alias === "string" && value.alias.length <= 256 ? value.alias : null;
 const schedule=value.type==='daily'&&['frequency','everyX','startDate','repeat','daysOfMonth','weeksOfMonth'].some(field=>value[field]!==undefined)?readDailySchedule(value):null;
 const fingerprint = await workspaceTokenHash(JSON.stringify({ id, kind: value.type, text: value.text, notes: value.notes, priority: value.priority, attribute: value.attribute, date, checklist: items, alias, up: value.up === true, down: value.down === true, ...(value.type==='daily'?{schedule}:{}) }));
 return { ...(value.type==='daily'?{schedule}:{}), id, kind: value.type as RemoteTask["kind"], text: value.text, notes: value.notes, priority: value.priority as number, attribute: value.attribute as string, date, checklist: items, alias, completed: value.completed===true, up: value.up === true, down: value.down === true, isDue: dueKnown && typeof value.isDue === "boolean" ? value.isDue : null, fingerprint };
}

export function remotePatchMatches(remote: RemoteTask, patch: RemotePatch): boolean {
 return (patch.frequency===undefined || !!remote.schedule&&sameDailySchedule(remote.schedule,readDailySchedule(patch))) && (patch.up === undefined || remote.up === patch.up) && (patch.down === undefined || remote.down === patch.down) && (patch.text === undefined || remote.text === patch.text) && (patch.notes === undefined || remote.notes === patch.notes)
  && (patch.date === undefined || (remote.date?.slice(0,10) ?? null) === patch.date)
  && (patch.priority === undefined || remote.priority === patch.priority) && (patch.attribute === undefined || remote.attribute === patch.attribute)
  && (patch.checklist === undefined || JSON.stringify(remote.checklist.map(row=>({text:row.text,completed:row.completed}))) === JSON.stringify(patch.checklist.map(row=>({text:row.text,completed:row.completed}))));
}
