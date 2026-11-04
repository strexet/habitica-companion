"""Publish only the reviewed callback function to the selected existing Netlify project."""
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
LABEL = sys.argv[2] if len(sys.argv)>2 else 'notification-release-2026-11-02'
if LABEL not in {'notification-release-2026-11-02','notification-release-2026-11-02-final'}:
    raise RuntimeError('unqualified_run_directory')
PRIVATE = ROOT / '.skill-tree/artifacts' / LABEL
EVIDENCE = ROOT / '.skill-tree/history/validation' / LABEL
SITE = '04629462-d0c6-4a72-bf02-6170cb1c39f8'

def command(arguments, label, timeout):
    cli = (PRIVATE / 'netlify-cli-path.txt').read_text()
    start = time.monotonic()
    process = subprocess.Popen(['node', cli, *arguments], cwd=PRIVATE / 'netlify-site', stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, start_new_session=True)
    try:
        stdout, stderr = process.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGTERM)
        try:
            stdout, stderr = process.communicate(timeout=5)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            stdout, stderr = process.communicate(timeout=5)
        raise RuntimeError('provider_command_deadline_' + label) from None
    # Provider output can contain environment values; retain raw output only in the ignored private directory.
    fd = os.open(PRIVATE / (label + '-private.log'), os.O_CREAT | os.O_WRONLY | os.O_TRUNC, 0o600)
    with os.fdopen(fd, 'w') as output:
        output.write(stdout + stderr)
    receipt = {'operation': label, 'classification': 'executed-control', 'exit_code': process.returncode,
        'tool_wall_seconds': round(time.monotonic() - start, 3), 'deadline_seconds': timeout, 'cleanup_reserve_seconds': 10}
    (EVIDENCE / (label + '-operation.json')).write_text(json.dumps(receipt, indent=2) + '\n')
    if process.returncode:
        raise RuntimeError('provider_command_failed_' + label)
    try:
        return json.loads(stdout)
    except ValueError:
        return None

def deploy(enabled, release=False):
    baseline = json.loads((PRIVATE / 'netlify-site-baseline.json').read_text())
    if baseline['id'] != SITE or baseline['name'] != 'kaleidoscopic-rolypoly-416a42' or baseline['account_slug'] != 'strexet':
        raise RuntimeError('provider_target_identity_changed')
    manifest = json.loads((EVIDENCE / 'gateway-source-manifest.json').read_text())
    code = (PRIVATE / 'netlify-site/functions/callback-egress.mjs').read_bytes()
    if hashlib.sha256(code).hexdigest() != manifest['functionSha256']:
        raise RuntimeError('reviewed_bundle_changed')
    session = json.loads((PRIVATE / 'session.json').read_text())
    state = 'release-enabled' if release else 'enabled' if enabled else 'disabled'
    linked = PRIVATE / 'netlify-site/.netlify/state.json'
    if not linked.exists() or json.loads(linked.read_text()).get('siteId') != SITE:
        raise RuntimeError('isolated_cli_project_link_required')
    changed = command(['env:set', 'CALLBACK_EGRESS_ENABLED', str(enabled).lower(), '--context', 'production', '--site', SITE, '--force', '--json'], 'netlify-gate-' + state, 35)
    if changed is None:
        raise RuntimeError('provider_flag_setter_returned_no_receipt')
    result = command(['deploy', '--prod', '--no-build', '--site', SITE, '--dir', 'public', '--functions', 'functions', '--json', '--timeout', '90', '--message', 'Synthetic callback qualification ' + session['runId'] + ' ' + state], 'netlify-deploy-' + state, 140)
    current = command(['api', 'getSite', '--data', json.dumps({'site_id': SITE})], 'netlify-verify-' + state, 25)
    deployed = current['published_deploy']
    if current['id'] != SITE or deployed['state'] != 'ready' or result['deploy_id'] != deployed['id']:
        raise RuntimeError('provider_publication_identity_unverified')
    receipt = {'siteId': SITE, 'deployId': deployed['id'], 'state': deployed['state'], 'callbackEgressEnabled': enabled,
        'sourceManifest': str((EVIDENCE/'gateway-source-manifest.json').relative_to(ROOT)),
        'bundleSha256': manifest['functionSha256'], 'previousDeployId': baseline['published_deploy']['id'], 'cliVersion': '26.0.1'}
    (EVIDENCE / ('netlify-' + state + '.json')).write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps({'stage': 'gateway_' + state, 'deployId': deployed['id']}))

if __name__ == '__main__':
    try:
        deploy({'enable': True, 'disable': False, 'activate': True}[sys.argv[1]],sys.argv[1]=='activate')
    except Exception as error:
        print(json.dumps({'stage': 'controller_stopped', 'error': str(error) if isinstance(error, RuntimeError) else type(error).__name__}))
        sys.exit(1)
