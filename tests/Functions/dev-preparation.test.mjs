import test from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';import os from 'node:os';import path from 'node:path';import {execFileSync} from 'node:child_process';
import {frontendSettings} from '../../deploy/frontend-settings.mjs';import {prepareDev,writeDev,deploymentFlags} from '../../deploy/prepare-dev.mjs';
const root=path.resolve(import.meta.dirname,'../..');
const input=()=>({accountId:'a'.repeat(32),pagesProject:'companion-test-dev',pwaOrigin:'https://companion-test-dev.pages.dev',authOrigin:'https://auth.dev.example.org',mcpOrigin:'https://mcp.dev.example.org',workspaceOrigin:'https://workspace.dev.example.org',pagesKvId:'b'.repeat(32),partyDatabaseId:'11111111-2222-4333-8444-555555555555',authKvId:'c'.repeat(32),authDatabaseId:'22222222-2222-4333-8444-555555555555',workspaceDatabaseId:'33333333-2222-4333-8444-555555555555',creatorHeader:'creator-companion-dev'});
test('public overrides retain unrelated settings and reject credential or URL leakage',()=>{
 const base={Habitica:{XClientHeader:'original'},Features:{PartySyncEnabled:true,AdminUserIds:[],FutureOption:7},ChatGptIntegration:{Enabled:false,AuthBaseUrl:''}};
 const result=frontendSettings(base,{ChatGptIntegration:{Enabled:true,AuthBaseUrl:'https://auth.dev.example.org'}},'changed');assert.equal(result.Features.FutureOption,7);assert.equal(result.Habitica.XClientHeader,'changed');assert.equal(base.Habitica.XClientHeader,'original');
 for(const overrides of [{Habitica:{apiToken:'private'}},{ChatGptIntegration:{Enabled:true,AuthBaseUrl:'https://auth.dev.example.org/?token=secret'}},JSON.parse('{"Features":{"__proto__":{"polluted":true}}}'),{Unknown:{enabled:true}}])assert.throws(()=>frontendSettings(base,overrides));
});
test('dev artifacts isolate resource identities and keep all game-effect flags off',()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'companion-dev-'));const target=path.join(dir,'generated');try{
  const result=writeDev(input(),target);assert.equal(result.configs['chatgpt-auth'].vars.ENROLLMENT_ENABLED,true);assert.equal(result.configs['chatgpt-auth'].account_id,input().accountId);assert.equal(Object.hasOwn(result.pages,'account_id'),false);assert.equal(Object.hasOwn(JSON.parse(fs.readFileSync(path.join(target,'pages/wrangler.json'))),'account_id'),false);
  for(const c of Object.values(result.configs)){for(const key of ['CRON_ENABLED','SPELLS_ENABLED','PROJECTION_ENABLED','PARTY_ENABLED','EXECUTOR_ENABLED','KEY_OPERATIONS_ENABLED'])if(key in c.vars)assert.equal(c.vars[key],false);assert.equal(c.vars.MCP_RESOURCE,input().mcpOrigin+'/mcp');assert.ok(!c.env);for(const b of c.services||[])assert.ok(b.service.includes('-dev-'));}
  assert.ok(fs.existsSync(path.join(target,'plugin/package-identity.json')));assert.ok(fs.existsSync(path.join(target,'plugin.zip.identity.json')));
  assert.ok(fs.existsSync(path.join(target,'pages/functions/api')));assert.equal(JSON.parse(fs.readFileSync(path.join(target,'pages/wrangler.json'))).d1_databases[0].database_id,input().partyDatabaseId);assert.equal(result.pages.kv_namespaces[0].id,input().pagesKvId);assert.equal(result.settings.Features.PartySyncEnabled,true);assert.equal(result.settings.Features.OwnerIsolatedTestingEnabled,false);assert.equal(result.settings.CompanionWorkspace.PartyPlanningEnabled,false);assert.equal(JSON.parse(fs.readFileSync(path.join(target,'plugin/mcp.json'))).mcpServers['habitica-companion'].url,input().mcpOrigin+'/mcp');
  const plan=JSON.parse(fs.readFileSync(path.join(target,'deployment-plan.json')));assert.equal(plan.cloudGameEffectsEnabled,false);assert.equal(plan.liveCronEnabled,false);assert.deepEqual(plan.requiredClients,['ChatGPT','Codex desktop']);
  assert.throws(()=>writeDev(input(),target));
 }finally{fs.rmSync(dir,{recursive:true,force:true});}
});
test('unsafe or incomplete dev input is rejected before output is written',()=>{
 for(const patch of [{accountId:'0'.repeat(32)},{pagesProject:'habitica-companion'},{mcpOrigin:'https://mcp.example.invalid'},{pwaOrigin:'https://habitica-companion.pages.dev'},{authOrigin:'https://auth.dev.example.org/path'},{mcpOrigin:input().authOrigin},{pagesKvId:'8f3984e2aa7c4bff8dc245f3552927e5'},{authDatabaseId:input().workspaceDatabaseId},{apiToken:'private'},{liveCron:true}])assert.throws(()=>prepareDev({...input(),...patch},'/tmp/unused-dev-config'));
 const valid=input();delete valid.authKvId;assert.throws(()=>prepareDev(valid,'/tmp/unused-dev-config'));
});
test('explicit existing Pages selection retains only reviewed browser storage and uses production backend names',()=>{
 const oldKv='8f3984e2aa7c4bff8dc245f3552927e5',oldParty='fec1ea40-3502-4b66-9051-c3f566999a03';
 const selected={...input(),pagesProject:'habitica-companion',pwaOrigin:'https://habitica-companion.pages.dev',pagesKvId:oldKv,partyDatabaseId:oldParty};
 selected.existingPages={project:selected.pagesProject,origin:selected.pwaOrigin,kvNamespaceId:oldKv,partyDatabaseId:oldParty,partyDatabaseName:'habitica-companion-party-sync',productionBranch:'main',previousDeploymentId:'11111111-2222-4333-8444-555555555555'};
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'companion-existing-'));try{
  const result=writeDev(selected,path.join(dir,'generated'));assert.equal(result.pages.name,selected.pagesProject);assert.equal(result.settings.Features.OwnerIsolatedTestingEnabled,false);assert.equal(result.settings.Features.PartySyncEnabled,true);assert.equal(result.pages.kv_namespaces[0].id,oldKv);assert.equal(result.pages.d1_databases[0].database_id,oldParty);assert.equal(result.pages.d1_databases[0].database_name,selected.existingPages.partyDatabaseName);
  for(const c of Object.values(result.configs)){assert.ok(c.name.startsWith('habitica-companion-'));assert.ok(!c.name.includes('-dev-'));assert.equal(c.vars.ENVIRONMENT,'production');assert.equal(c.vars.PWA_ORIGIN,selected.pwaOrigin);for(const flag of ['PROJECTION_ENABLED','PARTY_ENABLED','SPELLS_ENABLED','EXECUTOR_ENABLED','KEY_OPERATIONS_ENABLED','CRON_ENABLED'])if(flag in c.vars)assert.equal(c.vars[flag],false);}
  assert.equal(result.configs['chatgpt-auth'].d1_databases[0].database_id,selected.authDatabaseId);assert.equal(result.configs['companion-workspace'].d1_databases[0].database_id,selected.workspaceDatabaseId);
  const plan=JSON.parse(fs.readFileSync(path.join(dir,'generated/deployment-plan.json')));assert.deepEqual(plan.websiteTarget,selected.existingPages);assert.equal(plan.environment,'production');assert.equal(result.names['chatgpt-auth'],'habitica-companion-auth');assert.equal(result.names['companion-workspace'],'habitica-companion-workspace');assert.equal(result.names['chatgpt-mcp'],'habitica-companion-mcp');
  for(const patch of [{existingPages:undefined},{existingPages:true},{existingPages:{...selected.existingPages,project:'other'}},{existingPages:{...selected.existingPages,kvNamespaceId:'d'.repeat(32)}},{existingPages:{...selected.existingPages,partyDatabaseId:input().partyDatabaseId}},{authKvId:oldKv},{authDatabaseId:oldParty},{authOrigin:'https://habitica-auth-preview.example.invalid'}])assert.throws(()=>prepareDev({...selected,...patch},'/tmp/unused-existing-config'));
 }finally{fs.rmSync(dir,{recursive:true,force:true});}
});
test('runtime origins reject partial bindings and stay consistent for all environments',async()=>{
 const {resourceConfiguration}=await import('../../workers/shared/resources.ts');const c=prepareDev(input(),'/tmp/unused-dev-config').configs['chatgpt-auth'];assert.deepEqual(resourceConfiguration('preview',c.vars),{pwaOrigin:input().pwaOrigin,issuer:input().authOrigin,resource:input().mcpOrigin+'/mcp',workspaceOrigin:input().workspaceOrigin});
 assert.throws(()=>resourceConfiguration('preview',{AUTH_ISSUER:input().authOrigin}));assert.throws(()=>resourceConfiguration('preview',{...c.vars,MCP_RESOURCE:'https://mcp.dev.example.org/other'}));assert.throws(()=>resourceConfiguration('preview',{...c.vars,PWA_ORIGIN:input().authOrigin}));assert.equal(resourceConfiguration('local').issuer,'http://localhost:8787');
});
for(const fail of [false,true])test('actual Pages build preserves full settings and restores source on '+(fail?'publish failure':'success'),()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'companion-build-'));try{
  fs.mkdirSync(path.join(dir,'src/Habitica.WebApp/wwwroot'),{recursive:true});fs.mkdirSync(path.join(dir,'deploy'));fs.mkdirSync(path.join(dir,'bin'));fs.mkdirSync(path.join(dir,'dotnet'));
  for(const file of ['build.sh','deploy/frontend-settings.mjs'])fs.copyFileSync(path.join(root,file),path.join(dir,file));
  const source=path.join(dir,'src/Habitica.WebApp/wwwroot/appsettings.json');const original=JSON.stringify({Habitica:{XClientHeader:'old'},Features:{PartySyncEnabled:true,FutureOption:7},ChatGptIntegration:{Enabled:false,AuthBaseUrl:''}});fs.writeFileSync(source,original);
  const settings=path.join(dir,'override.json');fs.writeFileSync(settings,JSON.stringify({ChatGptIntegration:{Enabled:true,AuthBaseUrl:'https://auth.dev.example.org'}}));
  for(const [file,body] of [['bin/curl',"#!/bin/sh\nprintf '#!/bin/sh\\nexit 0\\n'\n"],['bin/npm','#!/bin/sh\nexit 0\n'],['dotnet/dotnet',`#!/bin/sh\nif [ "$1" = "--version" ]; then exit 0; fi\nmkdir -p output/wwwroot\ncp src/Habitica.WebApp/wwwroot/appsettings.json output/wwwroot/appsettings.json\nexit ${fail?42:0}\n`]]){fs.writeFileSync(path.join(dir,file),body);fs.chmodSync(path.join(dir,file),0o755);}
  let status=0;try{execFileSync('sh',['build.sh'],{cwd:dir,timeout:10000,env:{...process.env,PATH:path.join(dir,'bin')+':'+process.env.PATH,HABITICA_X_CLIENT_HEADER:'creator-dev',HABITICA_FRONTEND_SETTINGS_FILE:settings},stdio:'pipe'});}catch(error){status=error.status;}
  assert.equal(status,fail?42:0);assert.equal(fs.readFileSync(source,'utf8'),original);const published=JSON.parse(fs.readFileSync(path.join(dir,'output/wwwroot/appsettings.json')));assert.equal(published.Features.FutureOption,7);assert.equal(published.ChatGptIntegration.Enabled,true);assert.equal(published.Habitica.XClientHeader,'creator-dev');
 }finally{fs.rmSync(dir,{recursive:true,force:true});}
});

test('normal app source defaults never inherit agent test restrictions',()=>{const settings=JSON.parse(fs.readFileSync(new URL('../../src/Habitica.WebApp/wwwroot/appsettings.json',import.meta.url),'utf8'));assert.equal(settings.Features.OwnerIsolatedTestingEnabled,false);assert.equal(settings.Features.PartySyncEnabled,true);});

test('explicit development profile enables implemented availability while retaining identities and consent boundaries',()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'companion-development-'));try{
  const selected={...input(),featureProfile:'development'},result=writeDev(selected,path.join(dir,'generated'));
  for(const config of Object.values(result.configs))for(const [flag,value] of Object.entries(config.vars))if(flag.endsWith('_ENABLED'))assert.equal(value,!['CALLBACK_EGRESS_ENABLED','PUSH_ENABLED','PUSH_DISPATCH_ENABLED','PUSH_REMINDERS_ENABLED'].includes(flag),flag);
  assert.equal(result.settings.CompanionWorkspace.PartyPlanningEnabled,true);assert.equal(result.settings.Features.OwnerIsolatedTestingEnabled,false);assert.equal(result.settings.Features.PartySyncEnabled,true);
  assert.equal(result.configs['chatgpt-auth'].vars.CREDENTIAL_CURRENT_KEY_VERSION,'1');assert.equal(result.configs['chatgpt-auth'].d1_databases[0].database_id,input().authDatabaseId);
  const plan=JSON.parse(fs.readFileSync(path.join(dir,'generated/deployment-plan.json')));assert.equal(plan.featureProfile,'development');assert.equal(plan.cloudGameEffectsEnabled,true);assert.equal(plan.liveCronEnabled,true);assert.equal(plan.perAccountExecutorConsentRequired,true);
  assert.equal(prepareDev(input(),path.join(dir,'pilot')).featureProfile,'pilot');assert.throws(()=>prepareDev({...input(),featureProfile:'all'},path.join(dir,'invalid')));
 }finally{fs.rmSync(dir,{recursive:true,force:true});}
});
test('new capability flags require explicit mapping and never gain accidental development activation',()=>{
 assert.throws(()=>deploymentFlags({FUTURE_DESTRUCTIVE_ENABLED:false},'development'));
 assert.throws(()=>deploymentFlags({OAUTH_ENABLED:false},'unknown'));
 assert.deepEqual(deploymentFlags({OAUTH_ENABLED:false,EXECUTOR_ENABLED:true},'pilot'),{OAUTH_ENABLED:true,EXECUTOR_ENABLED:false});
 assert.deepEqual(deploymentFlags({OAUTH_ENABLED:false,EXECUTOR_ENABLED:false},'development'),{OAUTH_ENABLED:true,EXECUTOR_ENABLED:true});
});

test('ordinary capability profiles keep separately provisioned Web Push disabled',()=>{
 for(const profile of ['pilot','development']){
  const flags=deploymentFlags({PUSH_ENABLED:true,PUSH_DISPATCH_ENABLED:true,PUSH_REMINDERS_ENABLED:true,OAUTH_ENABLED:false},profile);
  for(const key of ['PUSH_ENABLED','PUSH_DISPATCH_ENABLED','PUSH_REMINDERS_ENABLED'])assert.equal(flags[key],false);assert.equal(flags.OAUTH_ENABLED,true);
 }
});

test('callback egress requires separate operator provisioning in every ordinary profile',()=>{
 for(const profile of ['pilot','development'])assert.equal(deploymentFlags({CALLBACK_EGRESS_ENABLED:true},profile).CALLBACK_EGRESS_ENABLED,false);
});
