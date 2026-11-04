#!/usr/bin/env python3
"""Explicit, recoverable publication of committed Companion source to existing production."""
import argparse
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
PRIVATE = ROOT / '.skill-tree/artifacts/integrate-production/release'
EVIDENCE = ROOT / '.skill-tree/history/releases/integration-main-2026-11-05'
ORIGIN = 'https://habitica-companion.pages.dev'
MCP = 'https://habitica-companion-mcp.strexet.workers.dev/mcp'
NAMES = {'chatgpt-auth': 'habitica-companion-auth', 'companion-workspace': 'habitica-companion-workspace', 'chatgpt-mcp': 'habitica-companion-mcp'}
DATABASES = {'chatgpt-auth': 'ada5184c-865d-4c6a-864a-88618f19c06f', 'companion-workspace': '383e0502-7be4-42f9-b818-ee9710f3fa4d'}
args_before = sys.argv[:]
sys.argv = sys.argv[:1]
spec = importlib.util.spec_from_file_location('previous_release', ROOT / 'deploy/notification-release.py')
provider = importlib.util.module_from_spec(spec)
spec.loader.exec_module(provider)
sys.argv = args_before
api = provider.api


def save(path, value, private=False):
    provider.save(path, value, private)


def load(name):
    return json.loads((PRIVATE / name).read_text())


def current(name):
    row = api('/workers/scripts/' + name + '/deployments')['deployments'][0]
    if len(row['versions']) != 1 or row['versions'][0]['percentage'] != 100:
        raise RuntimeError('Split deployment requires review')
    return row['versions'][0]['version_id']


def query(database, sql):
    result = api('/d1/database/' + database + '/query', 'POST', {'sql': sql})
    if not all(row.get('success') for row in result):
        raise RuntimeError('Database preflight refused')
    return result[0].get('results', [])


def command(label, argv, timeout=300, cwd=ROOT, env=None):
    PRIVATE.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    log = PRIVATE / (label + '.log')
    with log.open('w') as stream:
        result = subprocess.run(argv, cwd=cwd, env={**os.environ, 'CI': '1', **(env or {})}, stdout=stream,
                                stderr=subprocess.STDOUT, timeout=timeout)
    receipt = dict(argv=argv, exit_code=result.returncode, seconds=round(time.monotonic() - started, 3), log=str(log.relative_to(ROOT)))
    save(EVIDENCE / (label + '.json'), receipt)
    if result.returncode:
        raise RuntimeError('Command failed: ' + receipt['log'])
    return receipt


def preflight():
    PRIVATE.mkdir(parents=True, exist_ok=True)
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    if (PRIVATE / 'baseline.json').exists():
        return load('baseline.json')
    baseline = dict(workers={}, migrations={}, bookmarks={})
    for part, name in NAMES.items():
        version = current(name)
        detail = api('/workers/scripts/' + name + '/versions/' + version)
        settings = api('/workers/scripts/' + name + '/settings')
        settings.update(detail['resources']['script_runtime'])
        settings['bindings'] = detail['resources']['bindings']
        baseline['workers'][part] = dict(version=version, settings=settings)
    for part, database in DATABASES.items():
        applied = {row['name'] for row in query(database, 'SELECT name FROM d1_migrations ORDER BY id')}
        pending = sorted(path.name for path in (ROOT / 'workers' / part / 'migrations').glob('*.sql') if path.name not in applied)
        baseline['migrations'][part] = dict(applied_count=len(applied), pending=pending)
        baseline['bookmarks'][part] = api('/d1/database/' + database + '/time_travel/bookmark')
    baseline['pages'] = api('/pages/projects/habitica-companion')
    baseline['executor_counts'] = query(DATABASES['companion-workspace'], 'SELECT executor_enabled,COUNT(*) AS count FROM workspaces GROUP BY executor_enabled')
    save(PRIVATE / 'baseline.json', baseline, True)
    save(EVIDENCE / 'baseline.json', dict(worker_versions={p: v['version'] for p, v in baseline['workers'].items()},
          pages=baseline['pages']['canonical_deployment']['id'], migrations=baseline['migrations'],
          bookmarks=baseline['bookmarks'], executor_counts=baseline['executor_counts']))
    return baseline


def pages_settings():
    baseline = preflight()
    observed = api('/pages/projects/habitica-companion')
    runtime = copy.deepcopy(baseline['pages']['deployment_configs'])
    for value in runtime.values():
        value.pop('wrangler_config_hash', None)
    runtime['production']['env_vars']['HABITICA_PLUGIN_MCP_ENDPOINT'] = dict(type='plain_text', value=MCP)
    runtime['production']['env_vars']['HABITICA_FRONTEND_SETTINGS_FILE'] = dict(type='plain_text', value='../../deploy/production.frontend.json')
    build = {**baseline['pages']['build_config'], 'build_command': './build.sh', 'root_dir': '', 'destination_dir': 'output/wwwroot'}
    payload = dict(build_config=build, deployment_configs=runtime)
    if observed['source'] != baseline['pages']['source']:
        payload['source'] = baseline['pages']['source']
    api('/pages/projects/habitica-companion', 'PATCH', payload)
    final = api('/pages/projects/habitica-companion')
    if final['build_config'] != build or final['source'] != baseline['pages']['source']:
        raise RuntimeError('Pages build/source settings differ')
    for environment, expected in runtime.items():
        actual = copy.deepcopy(final['deployment_configs'][environment])
        actual.pop('wrangler_config_hash', None)
        if actual != expected:
            raise RuntimeError('Pages runtime settings differ: ' + environment)
    save(EVIDENCE / 'pages-settings.json', dict(runtime_bindings_preserved=True, git_build_output='output/wwwroot', public_plugin_endpoint=MCP))


def build(commit):
    actual = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    if actual != commit or len(commit) != 40:
        raise RuntimeError('Select the exact committed release state before build')
    dirty = subprocess.check_output(['git', 'status', '--porcelain', '--', 'src', 'workers', 'functions', 'migrations', 'plugin', 'deploy', 'build.sh'], cwd=ROOT, text=True)
    if dirty.strip():
        raise RuntimeError('Commit scoped product/release changes before building publication artifacts')
    folder = PRIVATE / 'builds' / commit
    if (folder / 'manifest.json').exists():
        return json.loads((folder / 'manifest.json').read_text())
    folder.mkdir(parents=True, exist_ok=True)
    for part in NAMES:
        command('release-' + part + '-build', ['npm', 'run', 'build'], 180, ROOT / 'workers' / part)
        shutil.copytree(ROOT / 'workers' / part / 'dist', folder / part, dirs_exist_ok=True)
    source = ROOT / 'src/Habitica.WebApp/wwwroot/appsettings.json'
    original = source.read_bytes()
    dotnet = os.environ.get('HABITICA_DOTNET', 'dotnet')
    try:
        command('release-frontend-settings', ['node', 'deploy/frontend-settings.mjs', str(source)], 30,
                env={'HABITICA_FRONTEND_SETTINGS_FILE': str(ROOT / 'deploy/production.frontend.json')})
        command('release-frontend-publish', [dotnet, 'publish', 'src/Habitica.WebApp/Habitica.WebApp.csproj', '-c', 'Release', '-o', str(folder / 'frontend'), '-m:1', '-nodeReuse:false'], 600)
    finally:
        source.write_bytes(original)
    command('release-public-package', ['python3', 'deploy/build-plugin-download.py', '--published', str(folder / 'frontend/wwwroot'), '--source-revision', commit, '--endpoint', MCP], 60)
    manifest = dict(commit=commit, wwwroot=str((folder / 'frontend/wwwroot').relative_to(ROOT)), source_settings_restored=source.read_bytes() == original,
                    assets={str(p.relative_to(folder / 'frontend/wwwroot')): hashlib.sha256(p.read_bytes()).hexdigest() for p in (folder / 'frontend/wwwroot').rglob('*') if p.is_file()},
                    build_identity=json.loads((ROOT / 'workers/shared/build-identity.generated.ts').read_text().split(' = ', 1)[1].removesuffix(' as const;\n')))
    save(folder / 'manifest.json', manifest, True)
    save(PRIVATE / 'manifest.json', manifest, True)
    return manifest


def publish_workers(commit):
    baseline = preflight()
    if any(v['pending'] for v in baseline['migrations'].values()):
        raise RuntimeError('Reviewed pending migrations must precede publication')
    manifest = build(commit)
    state = load('state.json') if (PRIVATE / 'state.json').exists() else dict(current={p: v['version'] for p, v in baseline['workers'].items()}, actions={})
    for part, name in NAMES.items():
        version = current(name)
        if version != state['current'][part]:
            raise RuntimeError('Foreign active version: ' + part)
        if part in state['actions']:
            if state['actions'][part]['stage'] != 'active':
                raise RuntimeError('Recover the original prepared upload before retry')
            folder = PRIVATE / 'builds' / commit / part
            modules = [(p.name, 'application/javascript+module' if p.name.endswith('.js') else 'application/source-map', p.read_bytes()) for p in sorted(folder.iterdir()) if p.name in ['index.js', 'index.js.map']]
            if provider.provider.hashes(modules) != state['actions'][part]['source_hashes']:
                raise RuntimeError('Main Worker bytes differ from the qualified published candidate')
            provider.verified_source(name, modules)
            continue
        detail = api('/workers/scripts/' + name + '/versions/' + version)
        original = detail['resources']['bindings']
        settings = api('/workers/scripts/' + name + '/settings')
        settings.update(detail['resources']['script_runtime'])
        bindings = [copy.deepcopy(value) for value in original if value['type'] != 'secret_text']
        for value in bindings:
            if value['type'] == 'd1':
                value.pop('database_id', None)
        metadata = {key: settings[key] for key in ['compatibility_date', 'compatibility_flags', 'usage_model', 'placement', 'tags', 'observability', 'logpush', 'tail_consumers', 'limits'] if settings.get(key) is not None}
        metadata.update(main_module='index.js', bindings=bindings, keep_bindings=['secret_text'])
        folder = PRIVATE / 'builds' / commit / part
        modules = [(p.name, 'application/javascript+module' if p.name.endswith('.js') else 'application/source-map', p.read_bytes()) for p in sorted(folder.iterdir()) if p.name in ['index.js', 'index.js.map']]
        if manifest['build_identity']['buildHash'] not in next(data.decode() for name_, _, data in modules if name_ == 'index.js'):
            raise RuntimeError('Bundle lacks the reviewed build identity')
        state['actions'][part] = dict(previous=version, stage='upload-intent', source_hashes=provider.provider.hashes(modules), commit=commit)
        save(PRIVATE / 'state.json', state, True)
        payload, kind = provider.provider.multipart(metadata, modules)
        uploaded = api('/workers/scripts/' + name + '/versions', 'POST', payload, kind)
        target = uploaded['id']
        state['actions'][part].update(stage='uploaded', version=target)
        save(PRIVATE / 'state.json', state, True)
        if current(name) != version:
            raise RuntimeError('Foreign deployment during upload')
        api('/workers/scripts/' + name + '/deployments', 'POST', dict(strategy='percentage', versions=[dict(version_id=target, percentage=100)], annotations={'workers/message': 'Complete main integration ' + commit[:12]}))
        final = api('/workers/scripts/' + name + '/versions/' + target)['resources']
        if current(name) != target or sorted(final['bindings'], key=lambda x: x['name']) != sorted(original, key=lambda x: x['name']) or final['script_runtime'] != detail['resources']['script_runtime']:
            raise RuntimeError('Published identity or retained runtime differs')
        provider.verified_source(name, modules)
        state['current'][part] = target
        state['actions'][part]['stage'] = 'active'
        save(PRIVATE / 'state.json', state, True)
        save(EVIDENCE / (part + '.json'), dict(version=target, previous=version, source_commit=commit, build_identity=manifest['build_identity'], runtime_and_secrets_preserved=True))


def publish_pages(commit):
    manifest = build(commit)
    state = load('state.json')
    for part, name in NAMES.items():
        if current(name) != state['current'][part] or state['actions'][part]['stage'] != 'active':
            raise RuntimeError('Matching Workers must be active before Pages publication')
    pages_settings()
    before = api('/pages/projects/habitica-companion')['canonical_deployment']['id']
    pages = PRIVATE / 'pages'
    pages.mkdir(exist_ok=True)
    shutil.copytree(ROOT / 'functions', pages / 'functions', dirs_exist_ok=True)
    runtime = preflight()['pages']['deployment_configs']['production']
    config = dict(name='habitica-companion', compatibility_date=runtime['compatibility_date'], compatibility_flags=runtime.get('compatibility_flags', []),
                  pages_build_output_dir=str(ROOT / manifest['wwwroot']), kv_namespaces=[dict(binding=k, id=v['namespace_id']) for k, v in runtime['kv_namespaces'].items()],
                  d1_databases=[dict(binding=k, database_name='habitica-companion-party-sync', database_id=v['id']) for k, v in runtime['d1_databases'].items()])
    save(pages / 'wrangler.json', config, True)
    command('release-pages', [str(ROOT / 'workers/chatgpt-auth/node_modules/.bin/wrangler'), 'pages', 'deploy', str(ROOT / manifest['wwwroot']), '--project-name', 'habitica-companion', '--branch', 'main', '--commit-hash', commit, '--commit-message', 'Complete integrated main release'], 360, pages, {'CLOUDFLARE_ACCOUNT_ID': provider.provider.ACCOUNT})
    pages_settings()
    after = api('/pages/projects/habitica-companion')['canonical_deployment']
    if after['id'] == before or after['latest_stage']['status'] != 'success':
        raise RuntimeError('Pages publication has no confirmed success')
    manifest['pages'] = dict(id=after['id'], previous=before, url=after['url'])
    manifest['state'] = 'published'
    save(PRIVATE / 'manifest.json', manifest, True)
    save(EVIDENCE / 'deployment.json', dict(commit=commit, pages=manifest['pages'], workers=state['current'], build_identity=manifest['build_identity'], public_origin=ORIGIN))


def postflight():
    manifest = load('manifest.json')
    if manifest.get('state') != 'published':
        raise RuntimeError('A successful exact-main publication is required')
    expected = subprocess.check_output(['git', 'rev-parse', 'origin/main'], cwd=ROOT, text=True).strip()
    if expected != manifest['commit']:
        raise RuntimeError('Main moved after the selected release build')
    state = load('state.json')
    baseline = preflight()
    deployed = api('/pages/projects/habitica-companion')['canonical_deployment']
    if deployed['latest_stage']['status'] != 'success' or deployed.get('deployment_trigger', {}).get('metadata', {}).get('commit_hash') != expected:
        raise RuntimeError('Current Pages deployment does not identify exact main')
    checks = 1
    result = dict(commit=expected, assets={}, workers={}, migrations={})
    for part, name in NAMES.items():
        if current(name) != state['current'][part]:
            raise RuntimeError('Published Worker ownership changed')
        resources = api('/workers/scripts/' + name + '/versions/' + state['current'][part])['resources']
        previous = baseline['workers'][part]['settings']
        if sorted(resources['bindings'], key=lambda x: x['name']) != sorted(previous['bindings'], key=lambda x: x['name']):
            raise RuntimeError('Runtime binding drift: ' + part)
        folder = PRIVATE / 'builds' / expected / part
        modules = [(p.name, 'application/javascript+module' if p.name.endswith('.js') else 'application/source-map', p.read_bytes()) for p in sorted(folder.iterdir()) if p.name in ['index.js', 'index.js.map']]
        provider.verified_source(name, modules)
        body = subprocess.check_output(['curl', '--fail', '--silent', '--show-error', '--max-time', '25', 'https://' + name + '.strexet.workers.dev/health'])
        result['workers'][part] = dict(version=state['current'][part], health=json.loads(body))
        checks += 1
    version = manifest['build_identity']['backendRelease']
    for path in ['index.html', 'appsettings.json', '_framework/blazor.webassembly.js', '_framework/blazor.boot.json', '_framework/Habitica.WebApp.wasm', 'service-worker.js', 'service-worker-assets.js', 'css/app.css', 'js/field-focus.js', 'vendor/dexie.mjs', 'downloads/habitica-companion-' + version + '.zip']:
        body = subprocess.check_output(['curl', '--fail', '--silent', '--show-error', '--max-time', '25', ORIGIN + '/' + ('' if path == 'index.html' else path)])
        if hashlib.sha256(body).hexdigest() != manifest['assets'][path]:
            raise RuntimeError('Served artifact differs: ' + path)
        result['assets'][path] = dict(sha256=hashlib.sha256(body).hexdigest(), bytes=len(body))
        checks += 1
    for part, database in DATABASES.items():
        names = {row['name'] for row in query(database, 'SELECT name FROM d1_migrations ORDER BY id')}
        missing = [p.name for p in (ROOT / 'workers' / part / 'migrations').glob('*.sql') if p.name not in names]
        if missing:
            raise RuntimeError('Missing current source migrations: ' + part)
        result['migrations'][part] = dict(applied=len(names), missing=[])
        checks += 1
    issuer = 'https://habitica-companion-auth.strexet.workers.dev'
    def public_json(url):
        return json.loads(subprocess.check_output(['curl', '--fail', '--silent', '--show-error', '--max-time', '25', url]))
    oauth = public_json(issuer + '/.well-known/oauth-authorization-server')
    resource = public_json('https://habitica-companion-mcp.strexet.workers.dev/.well-known/oauth-protected-resource/mcp')
    if oauth.get('issuer') != issuer or 'S256' not in oauth.get('code_challenge_methods_supported', []) or resource.get('resource') != MCP or issuer not in resource.get('authorization_servers', []):
        raise RuntimeError('OAuth discovery or PKCE contract failed')
    result['oauth'] = dict(issuer=issuer, pkce_s256=True, resource=MCP)
    checks += 2
    result['checks'] = checks
    save(EVIDENCE / 'postflight.json', result)
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['preflight', 'pages-settings', 'build', 'workers', 'pages', 'postflight'])
    parser.add_argument('--commit')
    args = parser.parse_args()
    try:
        if args.action == 'preflight':
            value = preflight()
            print(json.dumps(dict(stage='preflight', migrations=value['migrations'], worker_versions={p: v['version'] for p, v in value['workers'].items()})))
        elif args.action == 'pages-settings':
            pages_settings()
        elif args.action in ['build', 'workers', 'pages']:
            if not args.commit:
                raise RuntimeError('An explicit exact source commit is required')
            {'build': build, 'workers': publish_workers, 'pages': publish_pages}[args.action](args.commit)
        else:
            print(json.dumps(postflight()))
    except Exception as error:
        print(json.dumps({'stopped': str(error) if isinstance(error, (RuntimeError, ValueError)) else type(error).__name__}))
        raise SystemExit(1)
