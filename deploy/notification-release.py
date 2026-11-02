"""Bounded 0.3.1 release controller for the three existing Companion Workers.

No account/resource creation, database restore, paid upgrade, user consent or message send.
Source upload and pointer changes require an explicit action; snapshot is read-only.
"""
import copy
import importlib.util
import json
import os
from pathlib import Path
import secrets
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('callback_provider', ROOT/'tests/hosted/cloudflare-controller.py')
provider = importlib.util.module_from_spec(spec)
spec.loader.exec_module(provider)
api, save = provider.api, provider.save
PRIVATE = ROOT/'.skill-tree/artifacts/notification-release-2026-11-02-final'
EVIDENCE = ROOT/'.skill-tree/history/releases/2026-11-02-notifications'
NAMES = {'chatgpt-auth':'habitica-companion-auth','companion-workspace':'habitica-companion-workspace','chatgpt-mcp':'habitica-companion-mcp'}
DATABASES = {'chatgpt-auth':'ada5184c-865d-4c6a-864a-88618f19c06f','companion-workspace':'383e0502-7be4-42f9-b818-ee9710f3fa4d'}

def version(name):
    result = api('/workers/scripts/'+name+'/deployments')['deployments'][0]
    if len(result['versions']) != 1 or result['versions'][0]['percentage'] != 100:
        raise RuntimeError('split_deployment_requires_review')
    return result['versions'][0]['version_id']

def snapshot():
    if (PRIVATE/'release-baseline.json').exists():
        raise RuntimeError('recover_existing_release_snapshot')
    state = {'workers':{},'bookmarks':{}}
    for part, name in NAMES.items():
        current = version(name)
        detail = api('/workers/scripts/'+name+'/versions/'+current)
        settings = api('/workers/scripts/'+name+'/settings')
        # /settings describes the latest upload, which can differ from the active version.
        settings['bindings'] = detail['resources']['bindings']
        settings.update(detail['resources']['script_runtime'])
        modules = provider.source(name)
        folder = PRIVATE/'release-baseline-source'/part
        folder.mkdir(parents=True,exist_ok=True)
        for file, kind, data in modules:
            (folder/file).write_bytes(data)
        state['workers'][part] = {'name':name,'version':current,'settings':settings,'source_hashes':provider.hashes(modules)}
    for part, database in DATABASES.items():
        state['bookmarks'][part] = api('/d1/database/'+database+'/time_travel/bookmark')
    save(PRIVATE/'release-baseline.json',state,True)
    save(EVIDENCE/'baseline.json',{'workers':{p:{k:v[k] for k in ['name','version','source_hashes']} for p,v in state['workers'].items()},'bookmarks':state['bookmarks']})
    print(json.dumps({'stage':'snapshot','workers':{p:v['version'] for p,v in state['workers'].items()},'recovery_bookmarks':list(state['bookmarks'])}))

def publish(action):
    part = {'auth-bootstrap':'chatgpt-auth','workspace':'companion-workspace','auth-bind':'chatgpt-auth','mcp':'chatgpt-mcp','activate-auth':'chatgpt-auth','activate-workspace':'companion-workspace','activate-mcp':'chatgpt-mcp'}[action]
    baseline = json.loads((PRIVATE/'release-baseline.json').read_text())
    state_path = PRIVATE/'release-state.json'
    state = json.loads(state_path.read_text()) if state_path.exists() else {'actions':{},'current':{p:v['version'] for p,v in baseline['workers'].items()}}
    name, current = NAMES[part], version(NAMES[part])
    if current != state['current'][part]:
        raise RuntimeError('unowned_version_drift_'+part)
    if action in state['actions']:
        raise RuntimeError('recover_original_release_action_'+action)
    settings = api('/workers/scripts/'+name+'/settings')
    detail = api('/workers/scripts/'+name+'/versions/'+current)
    settings.update(detail['resources']['script_runtime'])
    original = detail['resources']['bindings']
    bindings = [copy.deepcopy(b) for b in original if b['type']!='secret_text']
    for b in bindings:
        if b['type']=='d1':b.pop('database_id',None)
    def binding(value):
        nonlocal bindings
        bindings = [b for b in bindings if b['name']!=value['name']]+[value]
    active = action.startswith('activate-')
    binding({'name':'EVENTS_ENABLED','type':'json','json':active})
    if part=='chatgpt-auth':
        binding({'name':'CALLBACK_VAULT_CURRENT_KEY_VERSION','type':'plain_text','text':'1'})
        binding({'name':'CALLBACK_EGRESS_ENABLED','type':'json','json':active})
        if action!='auth-bootstrap':
            binding({'name':'EVENTS_WORKSPACE','type':'service','service':NAMES['companion-workspace'],'entrypoint':'EventSubscriptionService'})
        if not any(b['name']=='CALLBACK_VAULT_KEY_V1' for b in original):
            key_file=PRIVATE/'callback-vault-key-v1.txt'
            if not key_file.exists():
                fd=os.open(key_file,os.O_CREAT|os.O_EXCL|os.O_WRONLY,0o600)
                with os.fdopen(fd,'w') as output:output.write(secrets.token_urlsafe(32))
            binding({'name':'CALLBACK_VAULT_KEY_V1','type':'secret_text','text':key_file.read_text()})
    if part=='companion-workspace':
        binding({'name':'AUTH_EVENTS','type':'service','service':NAMES['chatgpt-auth'],'entrypoint':'EventDeliveryService'})
    metadata = {k:settings[k] for k in ['compatibility_date','compatibility_flags','usage_model','placement','tags','observability','logpush','tail_consumers','limits'] if settings.get(k) is not None}
    metadata.update({'main_module':'index.js','bindings':bindings,'keep_bindings':['secret_text']})
    folder=ROOT/'workers'/part/'dist'
    modules=[(p.name,'application/javascript+module' if p.name.endswith('.js') else 'application/json',p.read_bytes()) for p in sorted(folder.iterdir()) if p.name in ['index.js','index.js.map']]
    if not any(p[0]=='index.js' for p in modules):raise RuntimeError('missing_reviewed_bundle')
    identity=(ROOT/'workers/shared/build-identity.generated.ts').read_text().split(' = ',1)[1].removesuffix(' as const;\n')
    identity=json.loads(identity)
    if identity['backendRelease']!='0.3.1' or identity['buildHash'] not in dict((n,d.decode()) for n,_,d in modules)['index.js']:
        raise RuntimeError('stale_release_bundle')
    state['actions'][action]={'previous':current,'source_hashes':provider.hashes(modules),'stage':'upload_intent'}
    save(state_path,state,True)
    payload,kind=provider.multipart(metadata,modules)
    uploaded=api('/workers/scripts/'+name+'/versions','POST',payload,kind)
    target=uploaded['id'];state['actions'][action].update(version=target,stage='uploaded');save(state_path,state,True)
    api('/workers/scripts/'+name+'/deployments','POST',{'strategy':'percentage','versions':[{'version_id':target,'percentage':100}],'annotations':{'workers/message':'Notification release0.3.1 '+action}})
    if version(name)!=target:raise RuntimeError('publication_not_active')
    final=api('/workers/scripts/'+name+'/versions/'+target)['resources']['bindings']
    def reduced(values):return {b['name']:b for b in values if b['name'] not in ['EVENTS_ENABLED','CALLBACK_VAULT_CURRENT_KEY_VERSION','CALLBACK_EGRESS_ENABLED','CALLBACK_VAULT_KEY_V1','EVENTS_WORKSPACE','AUTH_EVENTS']}
    if reduced(original)!=reduced(final):raise RuntimeError('unrelated_runtime_binding_changed')
    if provider.hashes(provider.source(name))!=provider.hashes(modules):raise RuntimeError('published_source_changed')
    state['actions'][action]['stage']='active';state['current'][part]=target;save(state_path,state,True)
    receipt={'action':action,'worker':name,'previous_version':current,'version':target,'state':'active','events_enabled':active,'source_hashes':provider.hashes(modules),'backend_release':identity['backendRelease'],'tool_schema':identity['toolSchemaVersion'],'build_hash':identity['buildHash'],'unrelated_bindings_preserved':True,'new_secret_material_logged':False}
    save(EVIDENCE/(action+'.json'),receipt);print(json.dumps(receipt))

def rollback():
    baseline=json.loads((PRIVATE/'release-baseline.json').read_text())
    state=json.loads((PRIVATE/'release-state.json').read_text())
    for part in ['chatgpt-mcp','companion-workspace','chatgpt-auth']:
        name=NAMES[part]
        if version(name)!=state['current'][part]:raise RuntimeError('unowned_version_prevents_rollback')
        target=baseline['workers'][part]['version']
        api('/workers/scripts/'+name+'/deployments','POST',{'strategy':'percentage','versions':[{'version_id':target,'percentage':100}]})
    save(EVIDENCE/'rollback.json',{'restored':{p:v['version'] for p,v in baseline['workers'].items()},'additive_migrations_retained':True})

if __name__=='__main__':
    PRIVATE.mkdir(parents=True,exist_ok=True);EVIDENCE.mkdir(parents=True,exist_ok=True)
    try:
        action=sys.argv[1]
        if action=='snapshot':snapshot()
        elif action=='rollback':rollback()
        else:publish(action)
    except Exception as error:
        print(json.dumps({'stage':'release_stopped','reason':str(error) if isinstance(error,RuntimeError) else type(error).__name__}));sys.exit(1)
