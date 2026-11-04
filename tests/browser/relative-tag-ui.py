"""Bounded local Release build, current Razor capture and owned browser qualification."""
import json, os, pathlib, subprocess, time, sys
root=pathlib.Path(__file__).resolve().parents[2]
run=root/'.skill-tree/artifacts/relative-tag-ui'/str(time.time_ns());run.mkdir(parents=True)
fixtures=run/'fixtures';published=run/'published';env={**os.environ,'HABITICA_BROWSER_EVIDENCE':str(fixtures)}
steps=[(['dotnet','publish','src/Habitica.WebApp/Habitica.WebApp.csproj','-c','Release','-o',str(published),'-m:1','-nodeReuse:false'],240),(['dotnet','test','tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj','-m:1','-nodeReuse:false','--filter','FullyQualifiedName~WorkspaceTagPanel','--logger','trx;LogFileName=capture.trx','--results-directory',str(run)],120),(['python3','tests/browser-qualification/prepare.py','--fixtures',str(fixtures),'--published',str(published/'wwwroot'),'--changed-route','/todo/sync'],30),(['node','--experimental-strip-types','tests/browser/relative-tag-ui.mjs',str(fixtures),str(published/'wwwroot'),str(run),sys.argv[1] if len(sys.argv)>1 else 'all'],240)]
for i,(argv,timeout) in enumerate(steps):
 with (run/f'step-{i}.log').open('w') as log:
  result=subprocess.run(argv,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=timeout)
 if result.returncode:raise RuntimeError(f'Step {i} failed; original log: {run}/step-{i}.log')
report=json.loads((run/'report.json').read_text());report['artifactPath']=str((run/'report.json').relative_to(root));print(json.dumps(report))
