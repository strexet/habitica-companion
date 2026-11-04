import {deriveProfileId, decryptCredential, newManagementToken, tokenHash, tokenMatches} from './cryptography';
import {activeProfile, boundedJson, consentVersion, credentialKeys, loadProfile, managedProfile, verifyHabitica, VaultError} from './vault';
import {charge, trafficFailure} from './traffic';
import {workspaceObject, uuid, WorkspaceError} from '../../shared/workspace-http';

const headers = {'cache-control':'no-store', 'x-content-type-options':'nosniff'};
import {recoveryVersion,recoveryDescriptor} from '../../shared/connection-recovery';

export async function recoveryRoute(request:Request, env:AuthEnv):Promise<Response|null> {
  if (new URL(request.url).pathname !== '/recover') return null;
  try {
    if (request.method !== 'POST') throw new VaultError('invalid_request',405);
    if (!env.ENROLLMENT_ENABLED) throw new VaultError('recovery_unavailable',503);
    if (!request.headers.get('content-type')?.startsWith('application/json')) throw new VaultError('invalid_request',400);
    const input = workspaceObject(await boundedJson(request,8192),['habiticaUserId','apiToken','descriptor','bootstrapAcknowledged']);
    const account = uuid(input.habiticaUserId);
    if (typeof input.apiToken !== 'string' || !input.apiToken.trim() || input.apiToken.length > 1024) throw new VaultError('invalid_request',400);
    if (input.bootstrapAcknowledged !== undefined && input.bootstrapAcknowledged !== true) throw new VaultError('invalid_request',400);
    const id = await deriveProfileId(account,env.PROFILE_ID_KEY);
    let descriptor:Record<string,unknown>|undefined;
    if (input.descriptor !== undefined) {
      descriptor = workspaceObject(input.descriptor,['version','profileId','connectionId']);
      if (descriptor.version !== recoveryVersion || descriptor.profileId !== id) throw new VaultError('recovery_descriptor_stale',409);
      uuid(descriptor.connectionId);
    } else if (input.bootstrapAcknowledged !== true) throw new VaultError('recovery_acknowledgement_required',403);
    await charge(env,'recovery:profile:'+id,5);
    const credentials = {habiticaUserId:account,apiToken:input.apiToken};
    // Verify account ownership before revealing retained enrollment state.
    await verifyHabitica(credentials,env);
    const profile = await loadProfile(env,id);
    if (!profile) throw new VaultError('existing_connection_missing',404);
    if (profile.revoked_at_utc) throw new VaultError('existing_connection_revoked',403);
    if (profile.consent_version !== consentVersion) throw new VaultError('integration_consent_unsupported',403);
    if (descriptor && descriptor.connectionId !== profile.connection_id) throw new VaultError('recovery_descriptor_stale',409);
    if (!profile.credential_ciphertext || !profile.credential_iv || !profile.credential_key_version || profile.credential_schema_version !== 1 || !profile.management_token_hash) throw new VaultError('credential_repair_required',409);
    const stored = await decryptCredential(id,{ciphertext:profile.credential_ciphertext,iv:profile.credential_iv,keyVersion:profile.credential_key_version,schemaVersion:1},credentialKeys(env));
    if (stored.habiticaUserId !== account || !await tokenMatches(input.apiToken,await tokenHash(stored.apiToken))) throw new VaultError('credential_repair_required',409);
    const token = newManagementToken(), hash = await tokenHash(token), now = new Date().toISOString();
    const expires = new Date(Date.now()+8*60*60*1000).toISOString();
    const results = await env.HABITICA_INTEGRATION_DB.batch([
      env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_recovery_sessions(token_hash,profile_id,connection_id,management_anchor_hash,created_at_utc,expires_at_utc)
        SELECT ?,profile_id,connection_id,management_token_hash,?,? FROM integration_profiles
        WHERE profile_id=? AND connection_id=? AND management_token_hash=? AND consent_version=? AND revoked_at_utc IS NULL AND generation=?`)
        .bind(hash,now,expires,id,profile.connection_id,profile.management_token_hash,consentVersion,profile.generation),
      env.HABITICA_INTEGRATION_DB.prepare(`DELETE FROM integration_recovery_sessions WHERE profile_id=? AND EXISTS(SELECT 1 FROM integration_recovery_sessions WHERE token_hash=?) AND
        (expires_at_utc<=? OR management_anchor_hash<>? OR token_hash NOT IN
          (SELECT token_hash FROM integration_recovery_sessions WHERE profile_id=? ORDER BY created_at_utc DESC,rowid DESC LIMIT 8))`)
        .bind(id,hash,now,profile.management_token_hash,id),
    ]);
    if (!results[0].meta.changes) throw new VaultError('connection_changed',409);
    await activeProfile(env,id,profile.connection_id);
    await managedProfile(new Request(request.url,{headers:{authorization:'Bearer '+token}}),env,id,false);
    return Response.json({profileId:id,managementToken:token,expiresAtUtc:expires,descriptor:recoveryDescriptor(profile),recovered:true},{headers});
  } catch (error) {
    const safe = error instanceof VaultError || error instanceof WorkspaceError ? error : new VaultError('recovery_unavailable',503);
    return trafficFailure(error) ?? Response.json({error:safe.code},{status:safe.status,headers});
  }
}
