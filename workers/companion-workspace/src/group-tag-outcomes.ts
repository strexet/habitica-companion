import type {WorkspaceRow} from './executor.ts';
export interface TagReview {command_id:string;request_id:string;input_hash:string;workspace_id:string;connection_id:string;access_epoch:string;authority_json:string;expected_revision:number;ledger_hash:string;operation:string;payload_json:string;preview_json:string;receipt_json:string|null;expires_at_utc:string}
export function tagReviewOutcome(receipt:{savedInCompanion:boolean;outcomeUnknown:boolean}){return receipt.savedInCompanion?receipt.outcomeUnknown?'accepted-unknown':'accepted':'terminal-unaccepted';}
const bytes=(value:string)=>new TextEncoder().encode(value).length;
// The null receipt is the acceptance fence. Both acceptance and closure guard their
// first write on it in the same D1 transaction; accepted work always wins recovery.
export async function closeTagReview(env:Pick<WorkspaceEnv,'WORKSPACE_DB'>,row:Pick<WorkspaceRow,'profile_id'|'workspace_id'>,review:TagReview,reason:string):Promise<TagReview>{
 const closedAtUtc=new Date().toISOString(),receipt=JSON.stringify({commandId:review.command_id,requestId:review.request_id,workspaceId:review.workspace_id,operation:review.operation,savedInCompanion:false,outcomeUnknown:false,reviewOutcome:'terminal-unaccepted',gameEffects:0,workIds:[],reason,closedAtUtc});
 const preview=JSON.stringify({commandId:review.command_id,requestId:review.request_id,workspaceId:review.workspace_id,operation:review.operation,expectedRevision:review.expected_revision,expiresAtUtc:review.expires_at_utc,canAccept:false,gameEffects:0,reason});
 const commit=crypto.randomUUID(),delta=bytes('{}')+bytes(preview)+bytes(receipt)-bytes(review.payload_json)-bytes(review.preview_json);
 await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare(`UPDATE workspaces SET archive_bytes=archive_bytes+?,store_version=store_version+1,commit_tag=? WHERE profile_id=? AND store_version<9007199254740991 AND EXISTS(SELECT 1 FROM workspace_tag_reviews r WHERE r.profile_id=workspaces.profile_id AND r.command_id=? AND r.connection_id=? AND r.access_epoch=? AND r.receipt_json IS NULL) AND NOT EXISTS(SELECT 1 FROM workspace_tag_work w WHERE w.profile_id=workspaces.profile_id AND w.command_id=?)`).bind(delta,commit,row.profile_id,review.command_id,review.connection_id,review.access_epoch,review.command_id),
  env.WORKSPACE_DB.prepare(`UPDATE workspace_tag_reviews SET receipt_json=?,payload_json='{}',preview_json=? WHERE profile_id=? AND command_id=? AND receipt_json IS NULL AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)`).bind(receipt,preview,row.profile_id,review.command_id,row.profile_id,commit)
 ]);
 return await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_tag_reviews WHERE profile_id=? AND command_id=?').bind(row.profile_id,review.command_id).first<TagReview>()??review;
}
