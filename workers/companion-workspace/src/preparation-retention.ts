// Accepted request-to-command bindings survive with user history. Expired unaccepted previews cannot authorize acceptance.
export async function purgeExpiredPreparations(env:Pick<WorkspaceEnv,'WORKSPACE_DB'>):Promise<void>{
 const now=new Date().toISOString();
 const profiles=await env.WORKSPACE_DB.prepare(`SELECT DISTINCT p.profile_id FROM workspace_preparations p WHERE p.expires_at_utc<=?
  AND NOT EXISTS(SELECT 1 FROM workspace_commands c WHERE c.profile_id=p.profile_id AND c.command_id=p.command_id) AND NOT EXISTS(SELECT 1 FROM workspaces retained_w, json_each(retained_w.document_json,'$.organizationHistory') h WHERE retained_w.profile_id=p.profile_id AND json_extract(h.value,'$.command.id')=p.command_id) AND NOT EXISTS(SELECT 1 FROM workspaces conversion_w, json_each(conversion_w.document_json,'$.checklistConversions') cv, json_each(cv.value,'$.receipts') cr WHERE conversion_w.profile_id=p.profile_id AND json_extract(cr.value,'$.command.id')=p.command_id) LIMIT 32`).bind(now).all<{profile_id:string}>();
 for(const {profile_id} of profiles.results){
  const tag=crypto.randomUUID();
  await env.WORKSPACE_DB.batch([
   env.WORKSPACE_DB.prepare(`UPDATE workspaces SET archive_bytes=archive_bytes-COALESCE((SELECT SUM(length(CAST(p.command_json AS BLOB))+512) FROM workspace_preparations p WHERE p.profile_id=workspaces.profile_id AND p.expires_at_utc<=?
    AND NOT EXISTS(SELECT 1 FROM workspace_commands c WHERE c.profile_id=p.profile_id AND c.command_id=p.command_id) AND NOT EXISTS(SELECT 1 FROM workspaces retained_w, json_each(retained_w.document_json,'$.organizationHistory') h WHERE retained_w.profile_id=p.profile_id AND json_extract(h.value,'$.command.id')=p.command_id) AND NOT EXISTS(SELECT 1 FROM workspaces conversion_w, json_each(conversion_w.document_json,'$.checklistConversions') cv, json_each(cv.value,'$.receipts') cr WHERE conversion_w.profile_id=p.profile_id AND json_extract(cr.value,'$.command.id')=p.command_id)),0),store_version=store_version+1,commit_tag=?
    WHERE profile_id=? AND store_version<9007199254740991`).bind(now,tag,profile_id),
   env.WORKSPACE_DB.prepare(`DELETE FROM workspace_preparations WHERE profile_id=? AND expires_at_utc<=?
    AND NOT EXISTS(SELECT 1 FROM workspace_commands c WHERE c.profile_id=workspace_preparations.profile_id AND c.command_id=workspace_preparations.command_id) AND NOT EXISTS(SELECT 1 FROM workspaces retained_w, json_each(retained_w.document_json,'$.organizationHistory') h WHERE retained_w.profile_id=workspace_preparations.profile_id AND json_extract(h.value,'$.command.id')=workspace_preparations.command_id) AND NOT EXISTS(SELECT 1 FROM workspaces conversion_w, json_each(conversion_w.document_json,'$.checklistConversions') cv, json_each(cv.value,'$.receipts') cr WHERE conversion_w.profile_id=workspace_preparations.profile_id AND json_extract(cr.value,'$.command.id')=workspace_preparations.command_id)
    AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)`).bind(profile_id,now,profile_id,tag),
  ]);
 }
}
