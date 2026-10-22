export async function purgeExpiredTagReviews(env:Pick<WorkspaceEnv,'WORKSPACE_DB'>){
 const now=new Date().toISOString(),profiles=await env.WORKSPACE_DB.prepare(`SELECT DISTINCT profile_id FROM workspace_tag_reviews WHERE expires_at_utc<=? AND receipt_json IS NULL AND NOT EXISTS(SELECT 1 FROM workspace_tag_work w WHERE w.profile_id=workspace_tag_reviews.profile_id AND w.command_id=workspace_tag_reviews.command_id) LIMIT 32`).bind(now).all<{profile_id:string}>();
 for(const {profile_id} of profiles.results){
  const tag=crypto.randomUUID();await env.WORKSPACE_DB.batch([
   env.WORKSPACE_DB.prepare(`UPDATE workspaces SET archive_bytes=archive_bytes-COALESCE((SELECT SUM(length(CAST(r.payload_json AS BLOB))+length(CAST(r.preview_json AS BLOB))+512) FROM workspace_tag_reviews r WHERE r.profile_id=workspaces.profile_id AND r.expires_at_utc<=? AND r.receipt_json IS NULL AND NOT EXISTS(SELECT 1 FROM workspace_tag_work w WHERE w.profile_id=r.profile_id AND w.command_id=r.command_id)),0),store_version=store_version+1,commit_tag=? WHERE profile_id=? AND store_version<9007199254740991`).bind(now,tag,profile_id),
   env.WORKSPACE_DB.prepare(`DELETE FROM workspace_tag_reviews WHERE profile_id=? AND expires_at_utc<=? AND receipt_json IS NULL AND NOT EXISTS(SELECT 1 FROM workspace_tag_work w WHERE w.profile_id=workspace_tag_reviews.profile_id AND w.command_id=workspace_tag_reviews.command_id) AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)`).bind(profile_id,now,profile_id,tag)
  ]);
 }
}
