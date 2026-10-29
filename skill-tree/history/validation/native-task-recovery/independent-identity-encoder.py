import json,hashlib
from pathlib import Path

def digest(s):return hashlib.sha256(s.encode()).hexdigest()
def encode(fields):return ''.join('-1:' if v is None else str(len(v.encode()))+':'+v for v in fields)
def base(c,operation=None):return [c['id'],c['workspaceId'],str(c['expectedRevision']),operation or c['operation'],c['taskId'],'1',None,None,'0',None,None,'0',None,'1',None,'0']
cases=[]
for name,operation,patch in [('unarchive','unarchiveTask',{'unarchive':{'archivedRevision':9}}),('repair-archived-project','unarchiveTask',{'unarchive':{'archivedRevision':9,'planning':{'clearProject':True}}}),('correct-completion','correctOccurrence',{'correctOccurrence':{'occurrenceId':'00000000-0000-4000-8000-000000000004','occurrenceRevision':9,'targetCommandId':'00000000-0000-4000-8000-000000000005'}})]:
 c={'id':'00000000-0000-4000-8000-000000000001','workspaceId':'00000000-0000-4000-8000-000000000002','expectedRevision':10,'operation':operation,'taskId':'00000000-0000-4000-8000-000000000003','patch':patch}
 previous=digest('companion-command-v1\n'+encode(base(c)));u=patch.get('unarchive');o=patch.get('correctOccurrence');plan=None
 if u and u.get('planning'):
  fields=base(c,'planTask')+['0']+['0','0',None,None,None,None,None,None]+[None,'0',None,'0',None,'1',None,'0',None]
  plan=digest('companion-command-v4\n'+encode(fields))
 fields=[str(u['archivedRevision']) if u else None,plan,None,o['occurrenceId'] if o else None,str(o['occurrenceRevision']) if o else None,o['targetCommandId'] if o else None]
 cases.append({'name':name,'command':c,'payloadHash':digest('companion-command-v11\n64:'+previous+encode(fields))})
Path('tests/fixtures/workspace/task-recovery-command-v11.json').write_text(json.dumps({'format':1,'identity':'companion-command-v11','cases':cases},indent=2)+'\n')
