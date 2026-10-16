import fs from 'node:fs';
import {pathToFileURL} from 'node:url';
function object(value){return value!==null&&typeof value==='object'&&!Array.isArray(value);}
function publicOnly(value){
 if(Array.isArray(value)){for(const item of value)publicOnly(item);return;}
 if(!object(value))return;
 for(const [key,item] of Object.entries(value)){
  if(/token|secret|password|credential|api.?key/i.test(key)||['__proto__','constructor','prototype'].includes(key))throw new Error('Frontend configuration contains a forbidden field.');
  publicOnly(item);
 }
}
function merge(base,extra){const result=structuredClone(base);for(const [key,value] of Object.entries(extra))result[key]=object(value)&&object(result[key])?merge(result[key],value):structuredClone(value);return result;}
export function frontendSettings(base,overrides={},header){
 if(!object(base)||!object(overrides))throw new Error('Frontend settings must be JSON objects.');
 publicOnly(overrides);
 if(Object.keys(overrides).some(key=>!['Habitica','Features','ChatGptIntegration','CompanionWorkspace'].includes(key)))throw new Error('Unknown frontend configuration section.');
 const result=merge(base,overrides);
 if(header!==undefined){if(typeof header!=='string'||!header.trim()||header.length>256||/[\r\n]/.test(header))throw new Error('Invalid public creator header.');result.Habitica={...result.Habitica,XClientHeader:header};}
 for(const [section,key] of [['ChatGptIntegration','AuthBaseUrl'],['CompanionWorkspace','BaseUrl']]){
  const value=result[section]?.[key];
  if(value){let url;try{url=new URL(value);}catch{throw new Error('Invalid integration URL.');}
   if(url.protocol!=='https:'||url.username||url.password||url.search||url.hash||url.pathname!=='/'||url.hostname.endsWith('.invalid'))throw new Error('Integration URL must be a reachable HTTPS origin.');}
 }
 if(result.ChatGptIntegration?.Enabled===true&&!result.ChatGptIntegration.AuthBaseUrl)throw new Error('Enabled linking requires an Auth origin.');
 return result;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 try{const file=process.argv[2];if(!file)throw new Error('A settings file is required.');const extra=process.env.HABITICA_FRONTEND_SETTINGS_FILE?JSON.parse(fs.readFileSync(process.env.HABITICA_FRONTEND_SETTINGS_FILE,'utf8')):{};fs.writeFileSync(file,JSON.stringify(frontendSettings(JSON.parse(fs.readFileSync(file,'utf8')),extra,process.env.HABITICA_X_CLIENT_HEADER),null,2)+'\n');}
 catch(error){process.stderr.write(error instanceof SyntaxError?'Invalid settings JSON.\n':error.message+'\n');process.exitCode=1;}
}
