import {buildIdentity} from './build-identity.generated.ts';
export {buildIdentity};
export type BuildService='auth'|'workspace'|'mcp';
export const identityEnvelope=(service:BuildService)=>({service,...buildIdentity});
const version=(value:unknown):value is string=>typeof value==='string'&&/^\d{1,6}\.\d{1,6}\.\d{1,6}$/.test(value);
const hash=(value:unknown):value is string=>typeof value==='string'&&/^[a-f0-9]{64}$/.test(value);
const compare=(a:string,b:string)=>{const x=a.split('.').map(Number),y=b.split('.').map(Number);for(let i=0;i<3;i++)if(x[i]!==y[i])return x[i]-y[i];return 0;};
export interface ClientBuildReport {packageVersion?:string;packageHash?:string;toolSchemaVersion?:string}
export function clientBuildReport(value:unknown):ClientBuildReport{
 if(!value||typeof value!=='object'||Array.isArray(value))throw Error('invalid_build_report');
 const r=value as Record<string,unknown>;
 if(Object.keys(r).some(key=>!['packageVersion','packageHash','toolSchemaVersion'].includes(key))
   ||r.packageVersion!==undefined&&!version(r.packageVersion)||r.packageHash!==undefined&&!hash(r.packageHash)
   ||r.toolSchemaVersion!==undefined&&!version(r.toolSchemaVersion))throw Error('invalid_build_report');
 return {...r} as ClientBuildReport;
}
export function peerIdentity(value:unknown,service:BuildService){
 if(!value||typeof value!=='object'||Array.isArray(value))return null;
 const r=value as Record<string,unknown>,range=r.supportedPackage as Record<string,unknown>|undefined;
 if(r.service!==service||!version(r.backendRelease)||!version(r.toolSchemaVersion)||!hash(r.buildHash)||!range
    ||!version(range.minimum)||!version(range.maximumExclusive)||compare(range.minimum,range.maximumExclusive)>=0)return null;
 return {service,backendRelease:r.backendRelease,toolSchemaVersion:r.toolSchemaVersion,buildHash:r.buildHash,
   supportedPackage:{minimum:range.minimum,maximumExclusive:range.maximumExclusive}};
}
export function compatibilityDiscovery(report:ClientBuildReport,auth:unknown,workspace:unknown){
 const workers={mcp:identityEnvelope('mcp'),auth:peerIdentity(auth,'auth'),workspace:peerIdentity(workspace,'workspace')};
 const identities=Object.values(workers);
 const state=identities.some(value=>value===null)?'unavailable':identities.every(value=>value!.buildHash===buildIdentity.buildHash
   &&value!.backendRelease===buildIdentity.backendRelease&&value!.toolSchemaVersion===buildIdentity.toolSchemaVersion
   &&value!.supportedPackage.minimum===buildIdentity.supportedPackage.minimum&&value!.supportedPackage.maximumExclusive===buildIdentity.supportedPackage.maximumExclusive)?'coherent':'mixed';
 const packageCompatibility=report.packageVersion===undefined?'unknown':compare(report.packageVersion,buildIdentity.supportedPackage.minimum)>=0&&compare(report.packageVersion,buildIdentity.supportedPackage.maximumExclusive)<0?'compatible':'incompatible';
 const schemaCompatibility=report.toolSchemaVersion===undefined?'unknown':report.toolSchemaVersion.split('.')[0]===buildIdentity.toolSchemaVersion.split('.')[0]?'compatible':'incompatible';
 return {backend:{state,workers},package:{installation:'unknown',reportedByClient:report,packageCompatibility,schemaCompatibility},
   guidance:'Backend identity describes these Workers. Client reports never prove installed skills or grant access. Mixed or unavailable Workers require operator review; discovery never retries account operations.'};
}
