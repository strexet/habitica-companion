#!/usr/bin/env python3
"""Bounded current-build UI qualification for one exact Skill Tree task key."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import time
import uuid
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[2]

def task_plan(task_key):
    matches=[]
    text=(ROOT/'.skill-tree/documents/future.md').read_text()
    for block in re.findall(r'```json\n(.*?)\n```',text,re.S):
        for check in json.loads(block).get('checks',[]):
            if check.get('recipe_id')=='ux-ui-rendered-browser' and check.get('parameters',{}).get('task_key')==task_key:
                matches.append(check['parameters'])
    # Finished task revalidation uses the immutable accepted check contract.
    if not matches:
        for line in (ROOT/'.skill-tree/history/task-outcomes.jsonl').read_text().splitlines():
            outcome=json.loads(line)
            if outcome.get('disposition')!='completed':
                continue
            matches.extend(c['parameters'] for c in outcome.get('validation_plan',[]) if c.get('recipe_id')=='ux-ui-rendered-browser' and c.get('parameters',{}).get('task_key')==task_key)
    if len(matches)!=1:
        raise ValueError('Exactly one queued or immutable completed check must match task_key')
    return matches[0]

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('task_key')
    parser.add_argument('report_directory',type=Path)
    args=parser.parse_args()
    if not re.fullmatch('[a-z][a-z0-9-]*',args.task_key):
        parser.error('Exact lowercase task key required')
    report=(ROOT/args.report_directory).resolve()
    if not report.is_relative_to(ROOT/'.skill-tree/history/validation'):
        parser.error('Report directory must reside inside owned validation history')
    plan=task_plan(args.task_key)
    if (ROOT/plan['report_directory']).resolve()!=report:
        parser.error('Report directory differs from the selected check contract')
    adapter=json.loads((ROOT/'tests/browser-qualification/ux-ui-adapters.json').read_text())['adapters'].get(args.task_key)
    if not adapter:
        parser.error('The selected task must supply a reviewed journey adapter before execution')
    if not re.fullmatch(r'\./[a-z0-9-]+\.mjs',adapter['journey']):
        parser.error('Journey adapter must be a local reviewed module')
    run=ROOT/'.skill-tree/artifacts/ux-ui'/args.task_key/str(time.time_ns())
    run.mkdir(parents=True);report.mkdir(parents=True,exist_ok=True)
    # The all-built-in contract has24 palettes instead of the normal3. It
    # retains one profile per engine and its own bounded operation envelope.
    large_matrix=plan['matrix']['schemes']=='all built-in schemes from ColorSchemeCatalog; explicitly enumerate resolved IDs in report'
    browser_seconds=1200 if large_matrix else 560
    controller_seconds=1500 if large_matrix else 900
    started=time.monotonic();deadline=started+controller_seconds
    session=str(uuid.uuid4())
    config={'plan':plan,'adapter':adapter,'projectRoot':str(ROOT),'session':session,'runDirectory':str(run),'operationLimits':{'browserSeconds':browser_seconds,'controllerSeconds':controller_seconds,'cleanupSeconds':30}}
    (run/'request.json').write_text(json.dumps(config,indent=2)+'\n')
    env={**os.environ,'HABITICA_BROWSER_EVIDENCE':str(run/'fixtures'),'HABITICA_BROWSER_ALL_SCHEMES':'1'}
    steps=[
        (['dotnet','publish','src/Habitica.WebApp/Habitica.WebApp.csproj','-c','Release','-o',str(run/'published'),'-m:1','-nodeReuse:false'],240),
        (['dotnet','test','tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj','-m:1','-nodeReuse:false','--filter',adapter['test_filter'],'--logger','trx;LogFileName=capture.trx','--results-directory',str(run)],180),
        (['python3','tests/browser-qualification/prepare.py','--fixtures',str(run/'fixtures'),'--published',str(run/'published/wwwroot'),*[v for route in plan['routes'] for v in ['--changed-route',route]]],30),
        (['node','tests/browser-qualification/ux-ui.mjs',str(run/'request.json')],browser_seconds+40),
    ]
    receipts=[]
    try:
        for i,(argv,limit) in enumerate(steps):
            remaining=min(limit,deadline-time.monotonic()-30)
            if remaining<=0:raise TimeoutError('Qualification deadline reserves30seconds for cleanup')
            with (run/f'step-{i}.log').open('w') as log:
                process=subprocess.Popen(argv,cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
                try:code=process.wait(timeout=remaining)
                except subprocess.TimeoutExpired:
                    import signal
                    os.killpg(process.pid,signal.SIGTERM)
                    try:process.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        os.killpg(process.pid,signal.SIGKILL);process.wait(timeout=10)
                    raise
            receipts.append({'argv':argv,'exitCode':code,'log':str((run/f'step-{i}.log').relative_to(ROOT))})
            if code:raise RuntimeError(f'Qualification step{i} failed; inspect the original log')
        tree=ET.parse(run/'capture.trx')
        counters=tree.find('.//{*}Counters')
        capture={k:int(counters.attrib[k]) for k in ['total','executed','passed','failed','notExecuted']}
        if capture['total']<1 or capture['failed'] or capture['notExecuted']:
            raise RuntimeError('Current Razor capture must discover nonzero tests without failures or skips')
        result=json.loads((run/'report.json').read_text())
        result.update(capture=capture,steps=receipts,artifactPath=str((run/'report.json').relative_to(ROOT)))
        (report/f'{session}.json').write_text(json.dumps(result,indent=2)+'\n')
        print(json.dumps({'report':str((report/f'{session}.json').relative_to(ROOT)),'counts':result['counts'],'artifacts':str(run.relative_to(ROOT))}),flush=True)
    except BaseException as error:
        failure={'session':session,'error':str(error),'steps':receipts,'artifacts':str(run.relative_to(ROOT)),'browserCleanup':'Inspect browser failure report when browser execution began; a forced controller timeout never proves child cleanup.'}
        (report/f'{session}-failure.json').write_text(json.dumps(failure,indent=2)+'\n')
        raise

if __name__=='__main__':main()
