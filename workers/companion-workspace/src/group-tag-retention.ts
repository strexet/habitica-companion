import {closeTagReview,type TagReview} from './group-tag-outcomes.ts';
export async function purgeExpiredTagReviews(env:Pick<WorkspaceEnv,'WORKSPACE_DB'>){
 const now=new Date().toISOString(),reviews=await env.WORKSPACE_DB.prepare(`SELECT r.*,s.workspace_id FROM workspace_tag_reviews r JOIN workspaces s ON s.profile_id=r.profile_id WHERE r.expires_at_utc<=? AND r.receipt_json IS NULL AND NOT EXISTS(SELECT 1 FROM workspace_tag_work w WHERE w.profile_id=r.profile_id AND w.command_id=r.command_id) LIMIT 32`).bind(now).all<TagReview&{profile_id:string;workspace_id:string}>();
 // Compact terminal records retain original request/hash identity under the existing
 // archive quota. A missing or manually purged record never proves non-acceptance.
 for(const review of reviews.results)await closeTagReview(env,review,review,'tag_preview_expired');
}
