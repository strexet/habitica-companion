import {Resolver} from 'node:dns/promises';
import {isIP} from 'node:net';
import tls from 'node:tls';
import https from 'node:https';

const v4Blocks = [
  ['0.0.0.0',8],['10.0.0.0',8],['100.64.0.0',10],['127.0.0.0',8],['169.254.0.0',16],
  ['172.16.0.0',12],['192.0.0.0',24],['192.0.2.0',24],['192.88.99.0',24],['192.168.0.0',16],
  ['198.18.0.0',15],['198.51.100.0',24],['203.0.113.0',24],['224.0.0.0',3],
];
const v4 = value => value.split('.').reduce((n, part) => (n << 8n) + BigInt(part), 0n);
function v6(value) {
  const sides = value.split('::'), left = sides[0] ? sides[0].split(':') : [], right = sides[1] ? sides[1].split(':') : [];
  return [...left, ...Array(8-left.length-right.length).fill('0'), ...right].reduce((n, part) => (n << 16n) + BigInt('0x'+part), 0n);
}
const inside = (value, base, prefix, size) => value >> BigInt(size-prefix) === base >> BigInt(size-prefix);
export function publicAddress(value) {
  if (typeof value !== 'string' || value.includes('%')) return false;
  if (isIP(value) === 4) return !v4Blocks.some(([base,prefix]) => inside(v4(value),v4(base),prefix,32));
  // Fail closed for translation, mapped, tunnel, special-use and future allocations.
  if (isIP(value) !== 6 || value.includes('.')) return false;
  const n = v6(value);
  return inside(n,v6('2000::'),3,128) && ![
    ['2001::',23],['2001:db8::',32],['2002::',16],['3fff::',20],['2620:4f:8000::',48],
  ].some(([base,prefix]) => inside(n,v6(base),prefix,128));
}
function addressIdentity(value) {
  if (isIP(value) === 4) return '4:'+v4(value);
  if (typeof value === 'string' && value.startsWith('::ffff:') && isIP(value.slice(7)) === 4) return '4:'+v4(value.slice(7));
  if (isIP(value) === 6 && !value.includes('.') && !value.includes('%')) return '6:'+v6(value);
  return null;
}
export function callbackUrl(value) {
  if (typeof value !== 'string' || value.length > 2048 || /[\x00-\x20\x7f]/.test(value)) throw Error('destination_refused');
  const url = new URL(value), labels = url.hostname.split('.');
  if (url.protocol !== 'https:' || url.username || url.password || url.hash || url.port || url.href !== value ||
      isIP(url.hostname.replace(/^\[|\]$/g,'')) || labels.length < 2 || url.hostname.length > 253 ||
      labels.some(label => !/^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/.test(label)) ||
      ['localhost','local','internal','invalid','test'].includes(labels.at(-1))) throw Error('destination_refused');
  return url;
}
export async function resolvePublic(host, signal) {
  const resolver = new Resolver({timeout:1000, tries:1});
  const cancel = () => resolver.cancel();
  signal.throwIfAborted(); signal.addEventListener('abort', cancel, {once:true});
  try {
    const query = async family => {
      try { return await resolver[family](host); }
      catch (error) { if (['ENODATA','ENOTFOUND'].includes(error.code)) return []; throw error; }
    };
    const addresses = (await Promise.all([query('resolve4'), query('resolve6')])).flat();
    signal.throwIfAborted();
    return addresses;
  } finally { resolver.cancel(); signal.removeEventListener('abort', cancel); }
}

// Internal primitive: caller must supply an approved address. No second DNS lookup occurs.
// Tests use a local TLS server directly; the deployed entry always calls postPublic below.
export async function sendPinned({url,address,body,headers,signal,maxResponseBytes=8192}, {connect=tls.connect,ca} = {}) {
  let socket, request;
  const destroy = () => { request?.destroy(); socket?.destroy(); };
  signal.throwIfAborted(); signal.addEventListener('abort', destroy, {once:true});
  try {
    await new Promise((resolve,reject) => {
      socket = connect({host:address,port:Number(url.port || 443),servername:url.hostname,
        rejectUnauthorized:true,minVersion:'TLSv1.2',ALPNProtocols:['http/1.1'],...(ca ? {ca} : {}),
        checkServerIdentity:(_host,cert) => tls.checkServerIdentity(url.hostname,cert)});
      const abort = () => reject(Error('deadline'));
      signal.addEventListener('abort',abort,{once:true});
      const cleanup = () => signal.removeEventListener('abort',abort);
      socket.once('error',error => { cleanup(); reject(error); });
      socket.once('secureConnect',() => {
        cleanup();
        if (!socket.authorized || addressIdentity(socket.remoteAddress) !== addressIdentity(address) ||
            tls.checkServerIdentity(url.hostname,socket.getPeerCertificate())) { destroy(); reject(Error('tls_refused')); return; }
        resolve();
      });
    });
    signal.throwIfAborted();
    // Create HTTP only after the TLS and remote-address checks, before writing any payload.
    return await new Promise((resolve,reject) => {
      const agent = new https.Agent({keepAlive:false,maxSockets:1});
      agent.createConnection = () => socket;
      request = https.request({hostname:url.hostname,port:Number(url.port || 443),path:url.pathname+url.search,
        method:'POST',agent,signal,maxHeaderSize:8192,headers:{...headers,host:url.host,
          'content-length':String(body.length),'connection':'close','accept-encoding':'identity'}},response => {
        const chunks=[]; let bytes=0;
        response.on('data',chunk => {
          bytes += chunk.length;
          if (bytes > maxResponseBytes) { response.destroy(Error('response_too_large')); return; }
          chunks.push(chunk);
        });
        response.once('error',reject);
        response.once('aborted',() => reject(Error('response_aborted')));
        response.once('end',() => resolve({status:response.statusCode,body:Buffer.concat(chunks)}));
      });
      request.once('error',reject);
      request.once('close',() => agent.destroy());
      request.end(body);
    });
  } finally { signal.removeEventListener('abort',destroy); destroy(); }
}
export async function postPublic(input, {resolve=resolvePublic,send=sendPinned} = {}) {
  const url=callbackUrl(input.url), answers=await resolve(url.hostname,input.signal);
  input.signal.throwIfAborted();
  if (!Array.isArray(answers) || !answers.length || answers.length > 16 || answers.some(address => !publicAddress(address))) throw Error('destination_refused');
  // Each operation opens one connection; failure never tries another address or follows Location.
  return send({...input,url,address:answers[0]});
}
