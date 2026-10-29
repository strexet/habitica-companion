import {workspaceCommand} from '../../../shared/workspace-commands.ts';
import {accept} from '../../src/commands.ts';
// Synthetic retained journal from before todo-required-v1. Never a production write path.
export async function seedLegacyCommand(f,input){
 const row=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE workspace_id=?').bind(input.workspaceId).first();
 const command=workspaceCommand(input),result=await accept(JSON.parse(row.document_json),command,new Date().toISOString());
 // This fixture intentionally represents retained commands predating coverage witnesses.
 result.document.activityCoverage=result.document.activityCoverage.filter(row=>row.commandId!==command.id);
 if(!result.document.activityCoverage.length)delete result.document.activityCoverage;
 const document=JSON.stringify(result.document),entry=JSON.stringify(result.entry),event=JSON.stringify(result.event),version=row.store_version+1;
 const size=row.archive_bytes-Buffer.byteLength(row.document_json)+Buffer.byteLength(document)+Buffer.byteLength(entry)+Buffer.byteLength(event)+2;
 await f.workspaceDb.batch([
  f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=?,archive_bytes=? WHERE profile_id=?').bind(document,version,size,row.profile_id),
  f.workspaceDb.prepare('INSERT INTO workspace_commands(profile_id,command_id,sequence,payload_hash,entry_json) VALUES(?,?,?,?,?)').bind(row.profile_id,command.id,version,result.entry.payloadHash,entry),
  f.workspaceDb.prepare('INSERT INTO workspace_events(profile_id,event_id,sequence,event_json) VALUES(?,?,?,?)').bind(row.profile_id,result.event.id,version,event)
 ]);
 return result;
}
