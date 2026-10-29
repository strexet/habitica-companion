import { workspaceObject, uuid, WorkspaceError } from './workspace-http.ts';
export interface TaskChecklistItem {id:string;text:string;completed:boolean}
export interface HabiticaTaskDetails {difficulty:number|null;attribute:'str'|'int'|'per'|'con'|null;checklist:TaskChecklistItem[]|null;up?:boolean;down?:boolean}
export function habiticaDetails(value:unknown,allowEmpty=true):HabiticaTaskDetails|null{
 if(value===undefined||value===null)return null;
 const row=workspaceObject(value,['difficulty','attribute','checklist','up','down']);const invalid=()=>{throw new WorkspaceError('invalid_habitica_details',422);};
 for(const key of ['up','down'])if(row[key]!==undefined&&typeof row[key]!=='boolean')invalid();
 if(row.difficulty!=null&&![0.1,1,1.5,2].includes(row.difficulty as number))invalid();
 if(row.attribute!=null&&!['str','int','per','con'].includes(row.attribute as string))invalid();
 let checklist:TaskChecklistItem[]|null=null;
 if(row.checklist!=null){if(!Array.isArray(row.checklist)||row.checklist.length>100)invalid();checklist=(row.checklist as unknown[]).map(value=>{const item=workspaceObject(value,['id','text','completed']);if(typeof item.text!=='string'||!item.text.trim()||item.text.length>256||item.completed!==undefined&&typeof item.completed!=='boolean')invalid();return {id:uuid(item.id),text:item.text as string,completed:item.completed===true};});if(new Set(checklist.map(i=>i.id)).size!==checklist.length)invalid();}
 const result={difficulty:row.difficulty as number??null,attribute:row.attribute as HabiticaTaskDetails['attribute']??null,checklist,...(row.up===undefined?{}:{up:row.up as boolean}),...(row.down===undefined?{}:{down:row.down as boolean})};
 if(!allowEmpty&&Object.values(result).every(v=>v===null))invalid();return result;
}
export function mergeDetails(current:HabiticaTaskDetails|null|undefined,patch:HabiticaTaskDetails|null|undefined):HabiticaTaskDetails|null{
 return !patch?current??null:{difficulty:patch.difficulty??current?.difficulty??null,attribute:patch.attribute??current?.attribute??null,checklist:patch.checklist??current?.checklist??null,...(patch.up!==undefined||current?.up!==undefined?{up:patch.up??current?.up}:{}),...(patch.down!==undefined||current?.down!==undefined?{down:patch.down??current?.down}:{})};
}
