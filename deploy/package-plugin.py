#!/usr/bin/env python3
"""Build a fresh deterministic plugin archive; never install, publish or modify its source."""
import argparse,hashlib,json,re,zipfile
from pathlib import Path
from urllib.parse import urlsplit
IDENTITY='package-identity.json'
def canonical(value):return (json.dumps(value,sort_keys=True,indent=2)+'\n').encode()
def digest(files):
 h=hashlib.sha256()
 for name,value in sorted(files.items()):h.update(name.encode()+b'\0'+hashlib.sha256(value).hexdigest().encode()+b'\0')
 return h.hexdigest()
def payload(root):
 result={}
 for path in sorted(root.rglob('*')):
  if path.is_symlink():raise ValueError('Package symlinks are forbidden.')
  if not path.is_file():continue
  name=path.relative_to(root).as_posix()
  if path.name==IDENTITY:continue
  if not (name in ['plugin.json','mcp.json','README.md'] or name.startswith('skills/') and path.suffix in ['.md','.json']):raise ValueError('Unexpected package file.')
  result[name]=path.read_bytes()
 return result
def descriptor(files,revision,requirements):
 if len(files)>500 or sum(len(value) for value in files.values())>16*1024*1024:raise ValueError('Package size bound exceeded.')
 for name in files:
  if "\\" in name or name!=Path(name).as_posix() or Path(name).is_absolute() or '..' in Path(name).parts or not(name in ['plugin.json','mcp.json','README.md'] or name.startswith('skills/') and Path(name).suffix in ['.md','.json']):raise ValueError('Unexpected payload path.')
 if not isinstance(requirements,dict) or set(requirements)!={'backendRelease','toolSchemaVersion'}:raise ValueError('Invalid package requirements.')
 version=lambda value:isinstance(value,str) and re.fullmatch(r'(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)',value)
 bounds=requirements['backendRelease']
 if not isinstance(bounds,dict) or set(bounds)!={'minimum','maximumExclusive'} or not all(version(value) for value in bounds.values()) or not version(requirements['toolSchemaVersion']) or tuple(map(int,bounds['minimum'].split('.')))>=tuple(map(int,bounds['maximumExclusive'].split('.'))):raise ValueError('Invalid package compatibility bounds.')
 mcp=json.loads(files['mcp.json']);server=mcp['mcpServers']['habitica-companion'];url=urlsplit(server.get('url',''))
 if set(mcp)!={'$schema','mcpServers'} or set(mcp['mcpServers'])!={'habitica-companion'} or set(server)-{'type','url','extensions'} or server.get('type')!='streamable-http' or url.scheme!='https' or not url.hostname or url.username or url.password or url.query or url.fragment or url.path!='/mcp':raise ValueError('Invalid credential-free MCP wiring.')
 if server.get('extensions',{}) not in [{},{'com.openai':{'auth':{'type':'oauth','client':{'mode':'cimd'}}}}]:raise ValueError('Unexpected MCP extension.')
 manifest=json.loads(files['plugin.json'])
 if manifest.get('name')!='habitica-companion' or not re.fullmatch(r'(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)',manifest.get('version','')):raise ValueError('Invalid semantic package version.')
 if not re.fullmatch('[a-f0-9]{40}',revision):raise ValueError('An explicit reviewed source revision is required.')
 folders=sorted({name.split('/')[1] for name in files if name.startswith('skills/')})
 if not folders or any('skills/'+folder+'/SKILL.md' not in files for folder in folders):raise ValueError('Missing skill entrypoint.')
 return {'schemaVersion':1,'name':manifest['name'],'packageVersion':manifest['version'],'sourceRevision':revision,'sourceRevisionEvidence':'operator-supplied; content hashes bind actual packaged files',
   'contentHash':digest(files),'files':{name:hashlib.sha256(value).hexdigest() for name,value in sorted(files.items())},
   'skills':{folder:digest({name:value for name,value in files.items() if name.startswith('skills/'+folder+'/')}) for folder in folders},
   'requirements':requirements,'evidence':'package descriptor; host installation unknown'}
def verify(root):
 root=Path(root);identity=json.loads((root/IDENTITY).read_text());files=payload(root)
 expected=descriptor(files,identity['sourceRevision'],identity['requirements'])
 if identity!=expected:raise ValueError('Package descriptor/content mismatch.')
 for folder in expected['skills']:
  if json.loads((root/'skills'/folder/'references'/IDENTITY).read_text())!=identity:raise ValueError('Skill descriptor mismatch.')
 expected_paths={IDENTITY}|{'skills/'+folder+'/references/'+IDENTITY for folder in identity['skills']}
 if {p.relative_to(root).as_posix() for p in root.rglob(IDENTITY)}!=expected_paths:raise ValueError('Unexpected descriptor location.')
 return identity
def verify_archive(archive):
 archive=Path(archive);receipt=json.loads(archive.with_suffix('.zip.identity.json').read_text())
 if receipt.get('archiveSha256')!=hashlib.sha256(archive.read_bytes()).hexdigest():raise ValueError('Archive hash mismatch.')
 with zipfile.ZipFile(archive) as source:
  names=source.namelist()
  if len(names)>500 or len(names)!=len(set(names)) or any(name.startswith('/') or '..' in Path(name).parts for name in names) or sum(info.file_size for info in source.infolist())>16*1024*1024:raise ValueError('Invalid archive entries.')
  all_files={name:source.read(name) for name in names}
 identity=json.loads(all_files[IDENTITY]);files={name:value for name,value in all_files.items() if Path(name).name!=IDENTITY}
 if descriptor(files,identity['sourceRevision'],identity['requirements'])!=identity:raise ValueError('Archived descriptor mismatch.')
 expected={IDENTITY}|{'skills/'+folder+'/references/'+IDENTITY for folder in identity['skills']}
 if {name for name in names if Path(name).name==IDENTITY}!=expected or any(json.loads(all_files[name])!=identity for name in expected):raise ValueError('Archived skill descriptor mismatch.')
 if receipt!={'packageVersion':identity['packageVersion'],'contentHash':identity['contentHash'],'archiveSha256':hashlib.sha256(archive.read_bytes()).hexdigest()}:raise ValueError('Archive receipt mismatch.')
 return receipt
def build(source,target,revision,requirements,endpoint=None):
 source=Path(source).resolve();target=Path(target).resolve()
 if target.exists() or source==target or source in target.parents:raise ValueError('A fresh output directory outside source is required.')
 files=payload(source)
 if endpoint is not None:
  url=urlsplit(endpoint)
  if url.scheme!='https' or not url.hostname or url.username or url.password or url.query or url.fragment or url.path!='/mcp':raise ValueError('A credential-free HTTPS MCP endpoint is required.')
  mcp=json.loads(files['mcp.json']);server=mcp['mcpServers']['habitica-companion'];server['url']=endpoint;files['mcp.json']=canonical(mcp)
 mcp=json.loads(files['mcp.json']);server=mcp['mcpServers']['habitica-companion']
 if set(mcp)!= {'$schema','mcpServers'} or set(mcp['mcpServers'])!={'habitica-companion'} or set(server)-{'type','url','extensions'} or server.get('type')!='streamable-http':raise ValueError('Unexpected MCP wiring or credential headers.')
 identity=descriptor(files,revision,requirements);encoded=canonical(identity)
 archive=target.with_suffix('.zip')
 if archive.exists() or archive.with_suffix('.zip.identity.json').exists():raise ValueError('Archive output already exists.')
 target.mkdir(parents=True)
 for name,value in files.items():
  path=target/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(value)
 (target/IDENTITY).write_bytes(encoded)
 for folder in identity['skills']:
  path=target/'skills'/folder/'references'/IDENTITY;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(encoded)
 verify(target)
 archive=target.with_suffix('.zip')
 if archive.exists():raise ValueError('Archive already exists.')
 with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_STORED) as output:
  for path in sorted(target.rglob('*')):
   if path.is_file():
    info=zipfile.ZipInfo(path.relative_to(target).as_posix(),(1980,1,1,0,0,0));info.create_system=3;info.external_attr=0o100644<<16;output.writestr(info,path.read_bytes())
 receipt={'packageVersion':identity['packageVersion'],'contentHash':identity['contentHash'],'archiveSha256':hashlib.sha256(archive.read_bytes()).hexdigest()}
 with archive.with_suffix('.zip.identity.json').open('xb') as output:output.write(canonical(receipt))
 return receipt
if __name__=='__main__':
 parser=argparse.ArgumentParser();sub=parser.add_subparsers(dest='mode',required=True)
 b=sub.add_parser('build');b.add_argument('--source',type=Path,required=True);b.add_argument('--target',type=Path,required=True);b.add_argument('--source-revision',required=True);b.add_argument('--requirements',type=Path,required=True);b.add_argument('--endpoint')
 v=sub.add_parser('verify');v.add_argument('root',type=Path)
 a=sub.add_parser('verify-archive');a.add_argument('archive',type=Path)
 args=parser.parse_args()
 try:
  result=verify_archive(args.archive) if args.mode=='verify-archive' else verify(args.root) if args.mode=='verify' else build(args.source,args.target,args.source_revision,json.loads(args.requirements.read_text()),args.endpoint)
  print(json.dumps(result,sort_keys=True))
 except (ValueError,KeyError,OSError) as error:parser.exit(1,'Package verification failed: '+str(error)+'\n')
