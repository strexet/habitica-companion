import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {frontendSettings} from './frontend-settings.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const read=relative=>JSON.parse(fs.readFileSync(path.join(root,relative),'utf8'));
const fields=['accountId','pagesProject','pwaOrigin','authOrigin','mcpOrigin','workspaceOrigin','pagesKvId','partyDatabaseId','authKvId','authDatabaseId','workspaceDatabaseId','creatorHeader'];
function origin(value){let url;try{url=new URL(value);}catch{throw new Error('A dev HTTPS origin is missing or invalid.');}
 if(url.protocol!=='https:'||url.username||url.password||url.search||url.hash||url.pathname!=='/'||!url.hostname.includes('.')||url.hostname.endsWith('.invalid')||url.hostname==='localhost')throw new Error('Dev addresses must be canonical HTTPS origins.');
 if(url.origin!==value)throw new Error('Dev origins must omit trailing slashes and paths.');return value;}
export function prepareDev(input,target){
 if(!input||typeof input!=='object'||Array.isArray(input)||Object.keys(input).some(k=>!fields.includes(k)&&k!=='existingPages')||fields.some(k=>typeof input[k]!=='string'||!input[k]))throw new Error('Use exactly the public fields from dev.example.json; secrets are forbidden.');
 const existing=input.existingPages;
 if(existing!==undefined){
  const keys=['project','origin','kvNamespaceId','partyDatabaseId','partyDatabaseName','productionBranch','previousDeploymentId'];
  if(!existing||typeof existing!=='object'||Array.isArray(existing)||Object.keys(existing).some(k=>!keys.includes(k))||keys.some(k=>typeof existing[k]!=='string'||!existing[k])||existing.project!==input.pagesProject||existing.origin!==input.pwaOrigin||existing.kvNamespaceId!==input.pagesKvId||existing.partyDatabaseId!==input.partyDatabaseId||!/^[a-z0-9][a-z0-9-]{0,63}$/.test(existing.partyDatabaseName)||!/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/.test(existing.previousDeploymentId))throw new Error('Existing Pages selection must exactly identify the reviewed project, origin, retained bindings and rollback deployment.');
 }
 if(!/^[a-f0-9]{32}$/.test(input.accountId)||/^0+$/.test(input.accountId)||!(existing?/^[a-z0-9][a-z0-9-]{0,35}$/:/^[a-z0-9][a-z0-9-]{0,35}-dev$/).test(input.pagesProject))throw new Error('Invalid Cloudflare account or dev Pages project name.');
 const origins=['pwaOrigin','authOrigin','mcpOrigin','workspaceOrigin'].map(k=>origin(input[k]));
 const committedOrigins=fs.readFileSync(path.join(root,'workers/shared/resources.ts'),'utf8').match(/https:\/\/[^\"\s,}]+/g)||[];
 if(origins.some(value=>!(existing&&value===existing.origin)&&committedOrigins.some(committed=>new URL(committed).origin===value)))throw new Error('Committed remote origins cannot be reused for isolated dev.');
 if(new Set(origins).size!==4)throw new Error('Dev service origins must be distinct.');
 if(new URL(input.pwaOrigin).hostname.endsWith('.pages.dev')&&new URL(input.pwaOrigin).hostname!==input.pagesProject+'.pages.dev')throw new Error('Pages origin must match the dedicated dev project.');
 const templates=Object.fromEntries(['chatgpt-auth','chatgpt-mcp','companion-workspace'].map(name=>[name,read('workers/'+name+'/wrangler.jsonc')]));
 const pages=fs.readFileSync(path.join(root,'wrangler.toml'),'utf8');
 const reserved=new Set((pages+JSON.stringify(templates)).match(/[a-f0-9]{32}|[a-f0-9]{8}-[a-f0-9-]{27}/g)||[]);
 const ids=['pagesKvId','partyDatabaseId','authKvId','authDatabaseId','workspaceDatabaseId'].map(k=>input[k]);
 if(new Set(ids).size!==5||ids.some(id=>reserved.has(id)&&!(existing&&[existing.kvNamespaceId,existing.partyDatabaseId].includes(id))))throw new Error('Dev resources must be distinct and cannot reuse committed normal/preview resource IDs.');
 for(const key of ['pagesKvId','authKvId'])if(!/^[a-f0-9]{32}$/.test(input[key])||/^0+$/.test(input[key]))throw new Error('Invalid dev KV namespace ID.');
 for(const key of ['partyDatabaseId','authDatabaseId','workspaceDatabaseId'])if(!/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/.test(input[key])||/^0+-0+-0+-0+-0+$/.test(input[key]))throw new Error('Invalid dev database ID.');
 const configuration={PWA_ORIGIN:input.pwaOrigin,AUTH_ISSUER:input.authOrigin,MCP_RESOURCE:input.mcpOrigin+'/mcp',WORKSPACE_ORIGIN:input.workspaceOrigin};
 const backendPrefix=input.pagesProject;
 const names={'chatgpt-auth':backendPrefix+'-auth','chatgpt-mcp':backendPrefix+'-mcp','companion-workspace':backendPrefix+'-workspace'};
 const configs={};
 for(const [name,template] of Object.entries(templates)){
  const c=structuredClone(template);delete c.env;c.account_id=input.accountId;c.name=names[name];c.main=path.relative(target,path.join(root,'workers',name,'src/index.ts'));c.vars={...c.vars,...configuration};c.vars.ENVIRONMENT=existing?'production':'preview';
  for(const flag of Object.keys(c.vars).filter(k=>k.endsWith('_ENABLED')))c.vars[flag]=['OAUTH_ENABLED','ENROLLMENT_ENABLED','WORKSPACE_ENABLED'].includes(flag);
  if('HABITICA_X_CLIENT_HEADER' in c.vars)c.vars.HABITICA_X_CLIENT_HEADER=input.creatorHeader;
  const serviceOrigin=input[name==='chatgpt-auth'?'authOrigin':name==='chatgpt-mcp'?'mcpOrigin':'workspaceOrigin'];const host=new URL(serviceOrigin).hostname;
  c.workers_dev=host.endsWith('.workers.dev');c.preview_urls=false;
  if(c.workers_dev){if(host.split('.')[0]!==c.name||host.split('.').length!==4)throw new Error('workers.dev hostname must match the generated Worker name.');delete c.routes;}
  else c.routes=[{pattern:host,custom_domain:true}];
  if(c.kv_namespaces)c.kv_namespaces=c.kv_namespaces.map(v=>({...v,id:input.authKvId}));
  if(c.d1_databases)c.d1_databases=c.d1_databases.map(v=>({...v,database_name:backendPrefix+(name==='chatgpt-auth'?'-auth':'-workspace'),database_id:input[name==='chatgpt-auth'?'authDatabaseId':'workspaceDatabaseId'],migrations_dir:path.relative(target,path.join(root,'workers',name,'migrations'))}));
  if(c.services)c.services=c.services.map(v=>({...v,service:v.service.includes('companion-workspace')?names['companion-workspace']:names['chatgpt-auth']}));
  configs[name]=c;
 }
 const settings=frontendSettings(read('src/Habitica.WebApp/wwwroot/appsettings.json'),{Features:{PartySyncEnabled:true,OwnerIsolatedTestingEnabled:false},ChatGptIntegration:{Enabled:true,AuthBaseUrl:input.authOrigin},CompanionWorkspace:{BaseUrl:input.workspaceOrigin,PartyPlanningEnabled:false}},input.creatorHeader);
 const pagesConfig={name:input.pagesProject,compatibility_date:'2026-10-12',pages_build_output_dir:path.relative(target,path.join(root,'output/wwwroot')),kv_namespaces:[{binding:'HABITICA_SYNC_KV',id:input.pagesKvId}],d1_databases:[{binding:'HABITICA_PARTY_DB',database_name:existing?.partyDatabaseName??input.pagesProject+'-party',database_id:input.partyDatabaseId,migrations_dir:path.relative(target,path.join(root,'migrations'))}]};
 return {configs,pages:pagesConfig,settings,names,configuration};
}
export function writeDev(input,target){
 const result=prepareDev(input,target);if(fs.existsSync(target))throw new Error('Dev output already exists; select a fresh directory.');
 fs.mkdirSync(target,{recursive:true});
 for(const [name,config] of Object.entries(result.configs))fs.writeFileSync(path.join(target,name+'.json'),JSON.stringify(config,null,2)+'\n');
 const pagesRoot=path.join(target,'pages');fs.mkdirSync(pagesRoot);
 const pages={...result.pages,pages_build_output_dir:path.relative(pagesRoot,path.join(root,'output/wwwroot')),d1_databases:result.pages.d1_databases.map(v=>({...v,migrations_dir:path.relative(pagesRoot,path.join(root,'migrations'))}))};
 fs.writeFileSync(path.join(pagesRoot,'wrangler.json'),JSON.stringify(pages,null,2)+'\n');fs.cpSync(path.join(root,'functions'),path.join(pagesRoot,'functions'),{recursive:true});
 fs.writeFileSync(path.join(target,'appsettings.json'),JSON.stringify(result.settings,null,2)+'\n');
 fs.cpSync(path.join(root,'plugin'),path.join(target,'plugin'),{recursive:true});const mcp=read('plugin/mcp.json');mcp.mcpServers['habitica-companion'].url=input.mcpOrigin+'/mcp';mcp.mcpServers['habitica-companion'].extensions={'com.openai':{auth:{type:'oauth',client:{mode:'cimd'}}}};fs.writeFileSync(path.join(target,'plugin/mcp.json'),JSON.stringify(mcp,null,2)+'\n');
 fs.writeFileSync(path.join(target,'deployment-plan.json'),JSON.stringify({environment:input.existingPages?'production':'dev',websiteTarget:input.existingPages??{project:input.pagesProject,productionBranch:'develop',storage:'new-dev'},cloudGameEffectsEnabled:false,liveCronEnabled:false,names:result.names,origins:result.configuration,resourceIds:{pagesKv:input.pagesKvId,partyD1:input.partyDatabaseId,oauthKv:input.authKvId,authD1:input.authDatabaseId,workspaceD1:input.workspaceDatabaseId},secretsRequired:['PROFILE_ID_KEY','CREDENTIAL_KEY_V1','CREDENTIAL_KEY_V2'],requiredClients:['ChatGPT','Codex desktop']},null,2)+'\n');return result;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 try{if(!process.argv[2])throw new Error('Usage: node deploy/prepare-dev.mjs <public-config.json> [fresh-output-directory]');const input=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));const target=path.resolve(process.argv[3]||path.join(root,input.existingPages?'deploy/.production':'deploy/.dev'));writeDev(input,target);process.stdout.write((input.existingPages?'Production':'Dev')+' configuration prepared. Review deployment-plan.json before remote operations. No resources were changed.\n');}
 catch(error){process.stderr.write(error instanceof SyntaxError?'Invalid public dev configuration JSON.\n':error.message+'\n');process.exitCode=1;}
}
