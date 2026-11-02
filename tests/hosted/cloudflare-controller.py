"""Bounded provider setup/cleanup for the owner-approved synthetic callback fixture."""
import base64
import datetime
import email
import email.policy
import hashlib
import json
import os
from pathlib import Path
import secrets
import sys
import tomllib
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
LABEL = sys.argv[2] if len(sys.argv)>2 else 'notification-release-2026-11-02'
if LABEL not in {'notification-release-2026-11-02','notification-release-2026-11-02-final'}:
    raise RuntimeError('unqualified_run_directory')
PRIVATE = ROOT / '.skill-tree/artifacts' / LABEL
EVIDENCE = ROOT / '.skill-tree/history/validation' / LABEL
ACCOUNT = '8c962c94f28f5bdd298024365e0dea40'
AUTH = 'habitica-companion-auth'
FIXTURE = 'habitica-companion-callback-qualification'
DATABASE = 'ada5184c-865d-4c6a-864a-88618f19c06f'
BASE = 'https://api.cloudflare.com/client/v4/accounts/' + ACCOUNT

def api(path, method='GET', payload=None, content_type='application/json', binary=False):
    token = tomllib.loads((Path.home() / 'Library/Preferences/.wrangler/config/default.toml').read_text())['oauth_token']
    data = payload if isinstance(payload, bytes) else json.dumps(payload).encode() if payload is not None else None
    request = urllib.request.Request(BASE + path, data=data, method=method,
        headers={'Authorization': 'Bearer ' + token, 'Content-Type': content_type})
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            raw = response.read(15000000)
            if len(raw) == 15000000:
                raise RuntimeError('bounded_provider_response_exceeded')
            if binary:
                return raw, response.headers['content-type']
    except urllib.error.HTTPError as error:
        raise RuntimeError('provider_http_' + str(error.code)) from None
    value = json.loads(raw)
    if not value.get('success'):
        raise RuntimeError('provider_refused_' + '_'.join(str(e.get('code')) for e in value.get('errors', [])))
    return value['result']

def save(path, value, private=False):
    path.parent.mkdir(parents=True, exist_ok=True)
    if private:
        fd = os.open(path, os.O_CREAT | os.O_WRONLY | os.O_TRUNC, 0o600)
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, 'w') as output:
            json.dump(value, output, indent=2)
            output.write('\n')
    else:
        path.write_text(json.dumps(value, indent=2) + '\n')

def multipart(metadata, modules):
    boundary = 'callback-' + uuid.uuid4().hex
    parts = [('metadata', 'metadata.json', 'application/json', json.dumps(metadata).encode())]
    parts += [(name, name, kind, data) for name, kind, data in modules]
    body = bytearray()
    for name, filename, kind, data in parts:
        body.extend((f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"; filename="{filename}"\r\nContent-Type: {kind}\r\n\r\n').encode())
        body.extend(data)
        body.extend(b'\r\n')
    body.extend(f'--{boundary}--\r\n'.encode())
    return bytes(body), 'multipart/form-data; boundary=' + boundary

def source(script):
    raw, kind = api('/workers/scripts/' + script, binary=True)
    message = email.message_from_bytes(('Content-Type: ' + kind + '\r\nMIME-Version: 1.0\r\n\r\n').encode() + raw, policy=email.policy.default)
    if not message.is_multipart():
        raise RuntimeError('unexpected_source_format')
    modules = []
    for part in message.iter_parts():
        name = part.get_filename() or part.get_param('name', header='Content-Disposition')
        if not name or '..' in Path(name).parts:
            raise RuntimeError('unexpected_module_identity')
        modules.append((name, 'application/javascript+module' if name.endswith(('.js', '.mjs')) else part.get_content_type(), part.get_payload(decode=True)))
    return modules

def hashes(modules):
    return {name: hashlib.sha256(data).hexdigest() for name, _, data in modules}

def deployment():
    result = api('/workers/scripts/' + AUTH + '/deployments')['deployments'][0]
    if len(result['versions']) != 1 or result['versions'][0]['percentage'] != 100:
        raise RuntimeError('unqualified_split_deployment')
    return result['versions'][0]['version_id']

def query(sql, parameters=None):
    result = api('/d1/database/' + DATABASE + '/query', 'POST', {'sql': sql, 'params': parameters or []})
    if not all(r.get('success') for r in result):
        raise RuntimeError('fixture_database_refused')
    return result[0].get('results', [])

def setup():
    if (PRIVATE / 'session.json').exists():
        raise RuntimeError('recover_existing_session_before_new_setup')
    try:
        api('/workers/scripts/' + FIXTURE + '/settings')
    except RuntimeError as error:
        if str(error) != 'provider_http_404':
            raise
    else:
        raise RuntimeError('existing_fixture_has_no_current_ownership_receipt')
    if query("SELECT name FROM sqlite_master WHERE type='table' AND name='callback_qualification_observations'"):
        raise RuntimeError('existing_fixture_table_requires_recovery')
    baseline = api('/workers/scripts/' + AUTH + '/settings')
    db = next(b for b in baseline['bindings'] if b['name'] == 'HABITICA_INTEGRATION_DB')
    if db.get('id', db.get('database_id')) != DATABASE:
        raise RuntimeError('database_identity_changed')
    run, webhook, request = (str(uuid.uuid4()) for _ in range(3))
    body = json.dumps({'type': 'verification', 'challenge': 'synthetic-' + run}, separators=(',', ':'))
    session = {'runId': run, 'webhookId': webhook, 'requestId': request, 'expiresAt': int(datetime.datetime.now(datetime.timezone.utc).timestamp() * 1000) + 3600000,
        'signingKey': base64.b64encode(secrets.token_bytes(32)).decode(), 'readKey': secrets.token_urlsafe(32), 'body': body,
        'gateway': 'https://kaleidoscopic-rolypoly-416a42.netlify.app/callback-egress', 'issuer': 'https://habitica-companion-auth.strexet.workers.dev',
        'receiver': 'https://' + FIXTURE + '.strexet.workers.dev/callback-qualification/' + run + '/receive',
        'serviceEnvFile': str(ROOT / '.skill-tree/artifacts/callback-egress/netlify.env'), 'baselineAuthVersion': deployment(), 'baselineSourceHashes': hashes(source(AUTH))}
    save(PRIVATE / 'session.json', session, True)
    save(PRIVATE / 'auth-settings-baseline.json', baseline, True)
    query('CREATE TABLE callback_qualification_observations(run_id TEXT NOT NULL,webhook_id TEXT NOT NULL,deliveries INTEGER NOT NULL,body_hash TEXT NOT NULL,signature_valid INTEGER NOT NULL,PRIMARY KEY(run_id,webhook_id))')
    bindings = [{'name': 'DB', 'type': 'd1', 'id': DATABASE}]
    values = {'QUALIFICATION_RUN': run, 'QUALIFICATION_WEBHOOK_ID': webhook, 'QUALIFICATION_EXPIRES_AT': str(session['expiresAt']),
        'QUALIFICATION_EXPECTED_SHA256': hashlib.sha256(body.encode()).hexdigest(), 'QUALIFICATION_KEY_B64': session['signingKey'], 'QUALIFICATION_READ_KEY': session['readKey']}
    for name, value in values.items():
        bindings.append({'name': name, 'type': 'secret_text' if name in ['QUALIFICATION_KEY_B64', 'QUALIFICATION_READ_KEY'] else 'plain_text', 'text': value})
    code = (ROOT / 'tests/hosted/callback-receiver.mjs').read_bytes()
    metadata = {'main_module': 'receiver.mjs', 'compatibility_date': '2026-10-12', 'compatibility_flags': ['nodejs_compat'], 'bindings': bindings,
        'observability': {'enabled': True, 'logs': {'enabled': True, 'invocation_logs': False}, 'traces': {'enabled': True}}}
    payload, kind = multipart(metadata, [('receiver.mjs', 'application/javascript+module', code)])
    result = api('/workers/scripts/' + FIXTURE, 'PUT', payload, kind)
    api('/workers/scripts/' + FIXTURE + '/subdomain', 'POST', {'enabled': True, 'previews_enabled': False})
    deployed = hashes(source(FIXTURE))
    if deployed.get('receiver.mjs') != hashlib.sha256(code).hexdigest():
        raise RuntimeError('receiver_source_identity_mismatch')
    save(EVIDENCE / 'receiver-deployment.json', {'runId': run, 'worker': FIXTURE, 'accountId': ACCOUNT, 'databaseId': DATABASE,
        'sourceHashes': deployed, 'deploymentId': result.get('deployment_id'), 'versionId': result.get('version_id'),
        'baselineAuthVersion': session['baselineAuthVersion'], 'baselineAuthSourceHashes': session['baselineSourceHashes'],
        'expiryUtc': datetime.datetime.fromtimestamp(session['expiresAt'] / 1000, datetime.timezone.utc).isoformat(), 'syntheticOnly': True})
    print(json.dumps({'stage': 'receiver_ready', 'runId': run, 'worker': FIXTURE}))

def auth_gate(enabled):
    session = json.loads((PRIVATE / 'session.json').read_text())
    current = deployment()
    if enabled:
        if current != session['baselineAuthVersion'] or hashes(source(AUTH)) != session['baselineSourceHashes']:
            raise RuntimeError('auth_baseline_changed')
        settings = json.loads((PRIVATE / 'auth-settings-baseline.json').read_text())
        bindings = [b for b in settings['bindings'] if b['type'] != 'secret_text']
        for binding in bindings:
            if binding['type'] == 'd1':
                binding.pop('database_id', None)
            if binding['name'] == 'CALLBACK_EGRESS_ENABLED':
                if binding.get('json') is not False:
                    raise RuntimeError('auth_gate_was_not_disabled')
                binding['json'] = True
        metadata = {k: settings[k] for k in ['compatibility_date', 'compatibility_flags', 'usage_model', 'placement', 'tags', 'observability', 'logpush', 'tail_consumers'] if settings.get(k) is not None}
        metadata.update({'main_module': 'index.js', 'bindings': bindings, 'keep_bindings': ['secret_text']})
        payload, kind = multipart(metadata, source(AUTH))
        result = api('/workers/scripts/' + AUTH + '/versions', 'POST', payload, kind)
        target = result['id']
        session['enabledAuthVersion'] = target
        save(PRIVATE / 'session.json', session, True)
    else:
        if current not in [session.get('enabledAuthVersion'), session['baselineAuthVersion']]:
            raise RuntimeError('unowned_auth_version_prevents_rollback')
        target = session['baselineAuthVersion']
    if current != target:
        api('/workers/scripts/' + AUTH + '/deployments', 'POST', {'strategy': 'percentage', 'versions': [{'version_id': target, 'percentage': 100}], 'annotations': {'workers/message': 'Bounded synthetic callback qualification' if enabled else 'Restore callback-disabled baseline'}})
    after = api('/workers/scripts/' + AUTH + '/settings')
    active = api('/workers/scripts/' + AUTH + '/versions/' + deployment())
    gate = next(b for b in active['resources']['bindings'] if b['name'] == 'CALLBACK_EGRESS_ENABLED')
    if gate.get('json') is not enabled or hashes(source(AUTH)) != session['baselineSourceHashes']:
        raise RuntimeError('auth_gate_or_source_verification_failed')
    expected = json.loads((PRIVATE / 'auth-settings-baseline.json').read_text())
    comparable = lambda value: {**value, 'bindings': sorted([b for b in value['bindings'] if b['name'] != 'CALLBACK_EGRESS_ENABLED'], key=lambda b: b['name'])}
    if comparable(after) != comparable(expected):
        raise RuntimeError('unrelated_auth_settings_changed')
    save(EVIDENCE / ('auth-enabled.json' if enabled else 'auth-disabled.json'), {'versionId': deployment(), 'callbackEgressEnabled': enabled, 'sourceHashes': session['baselineSourceHashes'], 'unrelatedSettingsUnchanged': True})
    print(json.dumps({'stage': 'auth_enabled' if enabled else 'auth_restored', 'versionId': target}))

def cleanup():
    session = json.loads((PRIVATE / 'session.json').read_text())
    expected = json.loads((EVIDENCE / 'receiver-deployment.json').read_text())
    if hashes(source(FIXTURE)) != expected['sourceHashes']:
        raise RuntimeError('fixture_identity_changed')
    other = query('SELECT COUNT(*) AS n FROM callback_qualification_observations WHERE run_id<>?', [session['runId']])
    if other[0]['n']:
        raise RuntimeError('unowned_fixture_rows')
    api('/workers/scripts/' + FIXTURE, 'DELETE')
    query('DROP TABLE callback_qualification_observations')
    save(EVIDENCE / 'receiver-cleanup.json', {'runId': session['runId'], 'workerRemoved': True, 'syntheticTableRemoved': True, 'authClaimFencesRetained': True})
    print(json.dumps({'stage': 'receiver_removed', 'runId': session['runId']}))

if __name__ == '__main__':
    try:
        {'setup': setup, 'auth-enable': lambda: auth_gate(True), 'auth-disable': lambda: auth_gate(False), 'cleanup': cleanup}[sys.argv[1]]()
    except Exception as error:
        print(json.dumps({'stage': 'controller_stopped', 'error': str(error) if isinstance(error, RuntimeError) else type(error).__name__}))
        sys.exit(1)
