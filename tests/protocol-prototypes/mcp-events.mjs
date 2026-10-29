// Research-only fake-host model. No Worker imports, listeners, fetch or app-server access.
import {createHash,createHmac,randomBytes,timingSafeEqual} from 'node:crypto';
import {isIP} from 'node:net';
export const eventProtocol='2026-07-28';
const uuid=x=>typeof x==='string'&&/^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i.test(x)&&x!=='00000000-0000-0000-0000-000000000000';
const hash=x=>createHash('sha256').update(x).digest('hex');
const same=(a,b)=>typeof a==='string'&&typeof b==='string'&&Buffer.byteLength(a)===Buffer.byteLength(b)&&timingSafeEqual(Buffer.from(a),Buffer.from(b));
const exact=(v,keys)=>{if(!v||typeof v!=='object'||Array.isArray(v)||Object.keys(v).some(k=>!keys.includes(k)))throw Error('schema');return v;};
function filter(value){exact(value,['workspaceId','taskId']);if(!uuid(value.workspaceId)||!uuid(value.taskId))throw Error('filter');return{workspaceId:value.workspaceId.toLowerCase(),taskId:value.taskId.toLowerCase()};}
function callback(value){if(typeof value!=='string'||value.length>2048)throw Error('callback');const u=new URL(value);if(u.protocol!=='https:'||u.username||u.password||u.hash||u.port&&u.port!=='443'||isIP(u.hostname.replace(/^\[|\]$/g,''))||!u.hostname.includes('.'))throw Error('callback');return u.href;}
function key(secret){if(typeof secret!=='string'||!secret.startsWith('whsec_'))throw Error('secret');const text=secret.slice(6),raw=Buffer.from(text,'base64');if(!/^[A-Za-z0-9+/]+={0,2}$/.test(text)||raw.toString('base64')!==text||raw.length<24||raw.length>64)throw Error('secret');return raw;}
export function sign(secret,id,time,body){return'v1,'+createHmac('sha256',key(secret)).update(id+'.'+time+'.'+body).digest('base64');}
export function verify(secret,id,time,body,signature,now){return /^\d+$/.test(String(time))&&Math.abs(now/1000-Number(time))<=300&&same(sign(secret,id,time,body),signature);}
// The fake adapter accepts an explicit small public-address set. This is deliberately not
// a production IP classifier or a claim that a DNS lookup followed by fetch pins a socket.
const fakePublicAddresses=new Set(['93.184.216.34','2606:4700:4700::1111']);
export function createEventsModel({store,authorize,connect,now=Date.now,random=()=>randomBytes(24).toString('base64url')}){
 if(!(store instanceof Map)||typeof authorize!=='function'||typeof connect!=='function')throw Error('explicit fake adapters required');
 const subscriptions=()=>store.get('subscriptions')??new Map(),receipts=()=>store.get('receipts')??new Map();
 store.set('subscriptions',subscriptions());store.set('receipts',receipts());
 async function post(row,body,id){
  const time=String(Math.floor(now()/1000));
  // connect is injected test code: it must resolve at connection time, enforce the exact
  // approved address and original TLS hostname, and prohibit redirects. No default network.
  const session=await connect(new URL(row.url));
  if(!authorize(row.actor,row.args)||row.expires<=now())throw Error('context changed before handoff');
  if(!session||!fakePublicAddresses.has(session.address)||session.tlsHostname!==new URL(row.url).hostname||session.redirects!==false)throw Error('unvalidated connection');
  return session.send({method:'POST',timeoutMs:1000,body,headers:{'Content-Type':'application/json','webhook-id':id,'webhook-timestamp':time,'webhook-signature':sign(row.secret,id,time,body),'X-MCP-Subscription-Id':row.id}});
 }
 function identity(actor,request,secretRequired){
  exact(request,['name','arguments','delivery','cursor','ttlMs']);if(request.name!=='companion.review_due'||request.cursor!=null)throw Error('event');const args=filter(request.arguments);
  exact(request.delivery,secretRequired?['mode','url','secret']:['mode','url']);if(request.delivery.mode!=='webhook')throw Error('delivery');
  const url=callback(request.delivery.url);if(secretRequired)key(request.delivery.secret);
  if(typeof actor!=='string'||!authorize(actor,args))throw Error('owner');
  const id='sub_'+hash(JSON.stringify({actor,url,name:request.name,arguments:args}));return{id,args,url};
 }
 return{
  discover:()=>({resultType:'complete',supportedVersions:[eventProtocol],capabilities:{tools:{},events:{}}}),
  list(actor,args){if(!authorize(actor,filter(args)))throw Error('owner');return{events:[{name:'companion.review_due',delivery:['webhook'],inputSchema:{type:'object',required:['workspaceId','taskId'],additionalProperties:false,properties:{workspaceId:{type:'string',format:'uuid'},taskId:{type:'string',format:'uuid'}}},payloadSchema:{type:'object',required:['workspaceId','taskId','occurrenceId'],additionalProperties:false,properties:{workspaceId:{type:'string',format:'uuid'},taskId:{type:'string',format:'uuid'},occurrenceId:{type:'string',format:'uuid'}}}}]};},
  async subscribe(actor,request){
   const {id,args,url}=identity(actor,request,true);const ttl=request.ttlMs==null?120000:request.ttlMs;if(!Number.isSafeInteger(ttl)||ttl<1000)throw Error('expiry');
   const old=subscriptions().get(id);if(old&&old.secret!==request.delivery.secret)throw Error('research model does not implement key rotation');
   const row={id,actor,args,url,secret:request.delivery.secret,expires:now()+Math.min(ttl,3600000),active:false};
   const challenge=random(),deadline=now()+1000,idVerify='verify_'+random();const response=await post(row,JSON.stringify({type:'verification',challenge}),idVerify);
   if(now()>deadline||response.status<200||response.status>=300||!same(response.body?.challenge,challenge)||!authorize(actor,args))throw Error('challenge');
   row.active=true;subscriptions().set(id,row);return{id,refreshBefore:new Date(row.expires).toISOString(),cursor:null,truncated:false};
  },
  unsubscribe(actor,request){const {id}=identity(actor,request,false);subscriptions().delete(id);return{};},
  async deliver(actor,id,event){
   const row=subscriptions().get(id);if(!row||row.actor!==actor)throw Error('owner');
   if(!authorize(actor,row.args)){subscriptions().delete(id);throw Error('revoked');}
   if(!row.active||row.expires<=now()){subscriptions().delete(id);throw Error('expired');}
   exact(event,['eventId','name','timestamp','data','cursor']);exact(event.data,['workspaceId','taskId','occurrenceId']);
   if(typeof event.eventId!=='string'||!/^[a-zA-Z0-9_-]{1,100}$/.test(event.eventId)||event.name!=='companion.review_due'||event.cursor!==null||!uuid(event.data.occurrenceId)||!Number.isFinite(Date.parse(event.timestamp))||!/(?:Z|[+-]\d\d:\d\d)$/.test(event.timestamp)||JSON.stringify(filter({workspaceId:event.data.workspaceId,taskId:event.data.taskId}))!==JSON.stringify(row.args))throw Error('event');
   const body=JSON.stringify(event);if(Buffer.byteLength(body)>262144)throw Error('size');
   const rid=id+':'+event.eventId,existing=receipts().get(rid),bodyHash=hash(body);
   if(existing){if(existing.bodyHash!==bodyHash)throw Error('event id reused');return{...existing};}
   const receipt={eventId:event.eventId,bodyHash,state:'dispatched',acceptedByHost:false,renderedReply:null};receipts().set(rid,receipt);
   try{const response=await post(row,body,event.eventId);receipt.status=response.status;receipt.acceptedByHost=response.status>=200&&response.status<300;receipt.state=receipt.acceptedByHost?'accepted':response.status===410?'gone':response.status===413?'too-large':'refused';if(response.status===410)subscriptions().delete(id);}
   catch{receipt.state='unknown';}
   return{...receipt};
  }
 };
}
