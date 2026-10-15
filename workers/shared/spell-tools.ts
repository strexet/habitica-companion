import {spellScopes} from './spells.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export const spellToolScopes={get_spell_context:spellScopes.context,prepare_spell_cast:spellScopes.cast,execute_spell_cast:spellScopes.cast,get_spell_command:spellScopes.lookup} as const;
export type SpellTool=keyof typeof spellToolScopes;
export function spellInput(tool:string,value:unknown){
 if(!Object.hasOwn(spellToolScopes,tool))throw new WorkspaceError('invalid_tool',422);
 const fields=tool==='get_spell_context'?[]:tool==='prepare_spell_cast'?['requestId','spellId','count']:tool==='execute_spell_cast'?['actionId','executeToken']:['actionId'];
 const input=workspaceObject(value,fields);
 if(tool==='prepare_spell_cast'){
  input.requestId=uuid(input.requestId);
  if(input.spellId!=='healAll'||!Number.isInteger(input.count)||Number(input.count)<1||Number(input.count)>3)throw new WorkspaceError('invalid_spell',422);
 }else if(tool!=='get_spell_context')input.actionId=uuid(input.actionId);
 if(tool==='execute_spell_cast'&&(typeof input.executeToken!=='string'||!/^[A-Za-z0-9_-]{43}$/.test(input.executeToken)))throw new WorkspaceError('invalid_execute_token',422);
 return input;
}
