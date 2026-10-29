#!/usr/bin/env python3
"""Stage synthetic Razor fixtures beside published assets; never reads app profiles."""
import argparse,json,shutil,hashlib,re
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--fixtures',type=Path,required=True);p.add_argument('--published',type=Path,required=True);p.add_argument("--changed-route",action="append",default=[]);p.add_argument("--serve-seconds",type=int,default=0);p.add_argument("--port",type=int,default=5118);args=p.parse_args()
root=args.fixtures.resolve();published=args.published.resolve();source=Path(__file__).resolve().parent;repository=source.parents[1]
if not (root/'coverage.json').exists():p.error('Generate Razor fixtures with HABITICA_BROWSER_EVIDENCE before staging.')
for name in ['css/app.css','js/colorSchemes.js']:
 if (published/name).read_bytes()!=(repository/'src/Habitica.WebApp/wwwroot'/name).read_bytes():p.error('Published asset is stale; publish the app before staging: '+name)

for name in ['qualification-core.mjs','qualification-frame.mjs','qualification-session.mjs','matrix.html']:shutil.copyfile(source/name,root/name)
for original,target in [('css/app.css','app.css'),('_content/MudBlazor/MudBlazor.min.css','mud.css'),('js/colorSchemes.js','colorSchemes.js')]:shutil.copyfile(published/original,root/target)
print(json.dumps({'fixtures':len(json.loads((root/'coverage.json').read_text())),'directory':str(root),'entrypoint':'matrix.html'}))



coverage=json.loads((root/'coverage.json').read_text())
repository=source.parents[1]
routes=sorted(set(route for page in (repository/'src/Habitica.WebApp').rglob('*.razor') for route in re.findall(r'@page "([^"]+)"',page.read_text())))
inventory={route:{'fixtures':[name for name,item in coverage.items() if item['route']==route], 'qualification':'captured synthetic states only' if any(item['route']==route for item in coverage.values()) else 'not captured'} for route in routes}
surfaces={surface:sorted(name for name,item in coverage.items() if surface in item['surfaces']) for surface in ['form','navigation','confirmation','tables','charts','logs','disabled-controls','error','loading']}
(root/'route-inventory.json').write_text(json.dumps({'routes':inventory,'surfaces':surfaces,'omitted':['Modal overlay lifecycle is not represented by inline confirmation fixtures.','SVG graphics, decorative images and pseudo-elements require separate visual review.','CSS sampling covers supported solid, generated PNG and vertical linear backgrounds; unsupported paint is explicitly unqualified.','Physical devices, assistive technologies, hosted login and actual background execution remain unqualified.'],'primaryTargetRule':'44 pixels for marked primary actions, default-size filled controls and navigation links; auxiliary small controls remain separately reviewed.'},indent=2)+'\n')
for route in args.changed_route:
 if route not in inventory or not inventory[route]['fixtures']:p.error('Changed route has no captured fixture: '+route)

(root/'asset-fingerprints.json').write_text(json.dumps({path.name:hashlib.sha256(path.read_bytes()).hexdigest() for path in root.iterdir() if path.is_file() and path.name!='asset-fingerprints.json' and path.suffix in ['.html','.mjs','.css','.js','.json']},indent=2)+'\n')
if not 0<=args.serve_seconds<=900:p.error('Serving deadline must be between zero and 900 seconds.')
if args.serve_seconds:
 import http.server,functools,time
 class QuietHandler(http.server.SimpleHTTPRequestHandler):
  def log_message(self,format,*args):pass
 server=http.server.ThreadingHTTPServer(('127.0.0.1',args.port),functools.partial(QuietHandler,directory=str(root)))
 server.timeout=1;deadline=time.monotonic()+args.serve_seconds
 print(f"http://127.0.0.1:{args.port}/matrix.html",flush=True)
 try:
  while time.monotonic()<deadline:server.handle_request()
 finally:server.server_close()
