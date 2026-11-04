#!/usr/bin/env python3
"""Exercise actual installed recipe fingerprinting against disposable copies."""
import hashlib,importlib.util,json,sys
from pathlib import Path
root=Path(sys.argv[1]).resolve();temporary=Path(sys.argv[2]).resolve();candidates=json.loads(sys.argv[3]);temporary.mkdir(parents=True,exist_ok=True)
profile=json.loads((root/'.skill-tree/installation/profile.json').read_text());recipe=profile['recipes']['ux-ui-rendered-browser']
for rel in set(recipe['input_paths'])|set(candidates):
 source=root/rel;target=temporary/rel
 if not source.is_file():raise RuntimeError('Required actual input unavailable: '+rel)
 target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(source.read_bytes())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
inputs={rel:sha(temporary/rel) for rel in recipe['input_paths']};target_profile=temporary/'.skill-tree/installation/profile.json';target_profile.parent.mkdir(parents=True,exist_ok=True)
def save():target_profile.write_text(json.dumps({'recipes':{'ux-ui-rendered-browser':recipe},'inputs':inputs}))
save();spec=importlib.util.spec_from_file_location('actual_workflow',root/'.agents/skills/skill-tree-implement-all/scripts/workflow-state.py');workflow=importlib.util.module_from_spec(spec);spec.loader.exec_module(workflow);store=workflow.Store(temporary);check={'recipe_id':'ux-ui-rendered-browser','parameters':{'task_key':'owned-control','report_directory':'.skill-tree/history/validation/owned-control'}}
_,initial=workflow.recipe_for(store,check);_,same=workflow.recipe_for(store,check);results=[]
for rel in candidates:
 path=temporary/rel;original=path.read_bytes()
 if path.suffix=='.json':
  data=json.loads(original)
  if isinstance(data,dict):data['ownedSourceIdentityMutation']=True
  else:data.append({'ownedSourceIdentityMutation':True})
  path.write_text(json.dumps(data))
 else:path.write_bytes(original+b"\nthrow new Error('Owned source drift control');\n")
 rejected=False;reason=None
 try:workflow.recipe_for(store,check)
 except workflow.WorkflowError as error:rejected='Profile refresh-required' in str(error);reason=str(error)
 inputs[rel]=sha(path);save();_,changed=workflow.recipe_for(store,check);results.append({'path':rel,'declared':rel in recipe['input_paths'],'stale_rejected':rejected,'refreshed_identity_changed':changed!=initial,'reason':reason})
 path.write_bytes(original)
 if rel in recipe['input_paths']:inputs[rel]=sha(path)
 else:inputs.pop(rel,None)
 save()
_,restored=workflow.recipe_for(store,check)
print(json.dumps({'unchanged_identity':same==initial,'restored_identity':restored==initial,'mutations':results,'cleanup':'All mutations confined to caller-owned disposable directory; original repository and profile never changed.'}))
