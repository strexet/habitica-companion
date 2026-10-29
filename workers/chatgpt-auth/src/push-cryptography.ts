import {encode,decode} from './cryptography';
import {VaultError,credentialKeys} from './vault';
import {workspaceObject} from '../../shared/workspace-http';

// Optional secrets are provisioned only for the separately enabled push capability.
export interface PushEnv extends AuthEnv {
 PUSH_VAULT_KEY_V1?:string;
 PUSH_VAULT_KEY_V2?:string;
 PUSH_VAPID_PRIVATE_JWK?:string;
}
const text=new TextEncoder();
function bytes(value:unknown,length:number):Uint8Array<ArrayBuffer>{
 if(typeof value!=='string'||! /^[A-Za-z0-9_-]+$/.test(value))throw new VaultError('invalid_push_key',422);
 let result;try{result=decode(value);}catch{throw new VaultError('invalid_push_key',422);}
 if(result.length!==length||encode(result)!==value)throw new VaultError('invalid_push_key',422);return result;
}
export function pushKeys(env:PushEnv){return {'1':env.PUSH_VAULT_KEY_V1,'2':env.PUSH_VAULT_KEY_V2};}
export async function pushConfiguration(env:PushEnv):Promise<string>{
 try{
  if(!Boolean(env.PUSH_ENABLED))throw new Error();
  const keys=pushKeys(env),current=keys[env.PUSH_VAULT_CURRENT_KEY_VERSION as keyof typeof keys];bytes(current,32);
  const other=[env.PROFILE_ID_KEY,...Object.values(credentialKeys(env))].filter(Boolean);
  const push=Object.values(keys).filter((v):v is string=>Boolean(v));
  if(new Set(push).size!==push.length||push.some(value=>other.includes(value)))throw new Error();
  for(const key of push)bytes(key,32);
  const publicBytes=bytes(env.PUSH_VAPID_PUBLIC_KEY,65);if(publicBytes[0]!==4)throw new Error();
  const jwk=workspaceObject(JSON.parse(env.PUSH_VAPID_PRIVATE_JWK??''),['kty','crv','x','y','d','ext','key_ops']);
  if(jwk.kty!=='EC'||jwk.crv!=='P-256')throw new Error();const privateBytes=bytes(jwk.d,32);
  if(push.includes(encode(privateBytes))||other.includes(encode(privateBytes)))throw new Error();
  bytes(jwk.x,32);bytes(jwk.y,32);
  const privateKey=await crypto.subtle.importKey('jwk',{kty:'EC',crv:'P-256',x:jwk.x as string,y:jwk.y as string,d:jwk.d as string},{name:'ECDSA',namedCurve:'P-256'},false,['sign']);
  const publicKey=await crypto.subtle.importKey('raw',publicBytes,{name:'ECDSA',namedCurve:'P-256'},false,['verify']);
  const challenge=text.encode('companion-push-configuration-v1'),algorithm={name:'ECDSA',hash:'SHA-256'};
  if(!await crypto.subtle.verify(algorithm,publicKey,await crypto.subtle.sign(algorithm,privateKey,challenge),challenge))throw new Error();
  return env.PUSH_VAPID_PUBLIC_KEY;
 }catch{throw new VaultError('push_configuration_unavailable',503);}
}
export interface PushMaterial {endpoint:string;keys:{p256dh:string;auth:string};expirationTime:number|null}
export async function pushMaterial(value:unknown,allowExpired=false):Promise<PushMaterial>{
 const input=workspaceObject(value,['endpoint','keys','expirationTime']);
 if(typeof input.endpoint!=='string'||input.endpoint.length>2048)throw new VaultError('invalid_push_endpoint',422);
 let url;try{url=new URL(input.endpoint);}catch{throw new VaultError('invalid_push_endpoint',422);}
 const host=url.hostname;
 // The approved provider set is bounded; unfamiliar public destinations remain unsupported.
 const provider=host==='fcm.googleapis.com'||host==='updates.push.services.mozilla.com'||host==='web.push.apple.com'||/^[a-z0-9-]+\.push\.apple\.com$/.test(host);
 if(!provider||url.protocol!=='https:'||url.username||url.password||url.port||url.hash||url.search||url.pathname==='/'||url.href!==input.endpoint)throw new VaultError('invalid_push_endpoint',422);
 const keys=workspaceObject(input.keys,['p256dh','auth']);const point=bytes(keys.p256dh,65);if(point[0]!==4)throw new VaultError('invalid_push_key',422);
 try{await crypto.subtle.importKey('raw',point,{name:'ECDH',namedCurve:'P-256'},false,[]);}catch{throw new VaultError('invalid_push_key',422);}bytes(keys.auth,16);
 const expirationTime=input.expirationTime;
 if(expirationTime!==null&&(typeof expirationTime!=='number'||!Number.isSafeInteger(expirationTime)||expirationTime<0||!allowExpired&&expirationTime<=Date.now()||expirationTime>8640000000000000))throw new VaultError('invalid_push_expiry',422);
 return {endpoint:url.href,keys:{p256dh:keys.p256dh as string,auth:keys.auth as string},expirationTime};
}
export async function pushHash(value:string):Promise<string>{return encode(new Uint8Array(await crypto.subtle.digest('SHA-256',text.encode(value))));}
function aad(profileId:string,deviceId:string,revision:number,keyVersion:string){return text.encode(`companion-push-subscription:1:${profileId}:${deviceId}:${revision}:${keyVersion}`);}
export async function encryptPush(env:PushEnv,profileId:string,deviceId:string,revision:number,material:PushMaterial){
 const keyVersion=env.PUSH_VAULT_CURRENT_KEY_VERSION,key=await crypto.subtle.importKey('raw',bytes(pushKeys(env)[keyVersion as '1'|'2'],32),'AES-GCM',false,['encrypt']);
 const iv=crypto.getRandomValues(new Uint8Array(12));
 const ciphertext=await crypto.subtle.encrypt({name:'AES-GCM',iv,additionalData:aad(profileId,deviceId,revision,keyVersion)},key,text.encode(JSON.stringify(material)));
 return {ciphertext:encode(new Uint8Array(ciphertext)),iv:encode(iv),keyVersion};
}
export async function decryptPush(env:PushEnv,profileId:string,deviceId:string,revision:number,envelope:{ciphertext:string;iv:string;keyVersion:string}):Promise<PushMaterial>{
 const key=await crypto.subtle.importKey('raw',bytes(pushKeys(env)[envelope.keyVersion as '1'|'2'],32),'AES-GCM',false,['decrypt']);
 const clear=await crypto.subtle.decrypt({name:'AES-GCM',iv:bytes(envelope.iv,12),additionalData:aad(profileId,deviceId,revision,envelope.keyVersion)},key,decode(envelope.ciphertext));
 return pushMaterial(JSON.parse(new TextDecoder().decode(clear)));
}
