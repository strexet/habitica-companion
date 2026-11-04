#!/usr/bin/env python3
"""Current-source, bounded release qualification; synthetic local lanes never publish."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def totals(expected, discovered, passed, failed=0, skipped=0):
    result = dict(expected=expected, discovered=discovered, passed=passed, failed=failed, skipped=skipped)
    if discovered < expected or passed != discovered or failed or skipped:
        raise RuntimeError('Required discovery, pass count or skip policy failed: ' + json.dumps(result))
    return result


def combine(rows):
    return {key: sum(row[key] for row in rows) for key in ['expected', 'discovered', 'passed', 'failed', 'skipped']}


class Qualification:
    def __init__(self, lane, report_directory):
        self.lane = lane
        self.report = (ROOT / report_directory).resolve()
        if not self.report.is_relative_to(ROOT / '.skill-tree/history/validation/integrate-production'):
            raise ValueError('Report directory must belong to the selected integration qualification')
        self.run = ROOT / '.skill-tree/artifacts/integration-qualification' / lane / str(uuid.uuid4())
        self.run.mkdir(parents=True)
        self.steps = []
        self.dotnet = os.environ.get('HABITICA_DOTNET') or shutil.which('dotnet')
        self.env = {**os.environ, 'CI': '1'}
        if self.dotnet:
            self.env['PATH'] = str(Path(self.dotnet).parent) + os.pathsep + self.env['PATH']
        self.env['DOTNET_CLI_HOME'] = str(self.run / 'dotnet-home')
        self.started = time.monotonic()

    def command(self, name, argv, timeout, cwd=ROOT, extra_env=None):
        start = time.monotonic()
        log = self.run / (name + '.log')
        with log.open('w') as output:
            try:
                process = subprocess.run(argv, cwd=cwd, env={**self.env, **(extra_env or {})}, stdout=output,
                                         stderr=subprocess.STDOUT, timeout=timeout)
                code = process.returncode
            except subprocess.TimeoutExpired:
                code = 124
        step = dict(name=name, argv=argv, exit_code=code, seconds=round(time.monotonic() - start, 3),
                    deadline_seconds=timeout, log=str(log.relative_to(ROOT)))
        self.steps.append(step)
        if code:
            raise RuntimeError(name + ' failed; original output: ' + step['log'])
        return log

    def tap(self, path, minimum):
        text = path.read_text()
        read = lambda key: int(re.findall(r'^# ' + key + r' (\d+)$', text, re.M)[-1])
        return totals(minimum, read('tests'), read('pass'), read('fail'), read('skipped') + read('cancelled'))

    def dotnet_lane(self):
        if not self.dotnet:
            raise RuntimeError('Pinned .NET SDK is unavailable')
        log = self.command('sdk', [self.dotnet, '--version'], 30)
        if log.read_text().strip() != '8.0.125':
            raise RuntimeError('The release requires SDK 8.0.125')
        self.command('solution', [self.dotnet, 'test', 'Habitica.sln', '-c', 'Release', '-m:1', '-nodeReuse:false',
                     '--logger', 'trx;LogFilePrefix=integrated', '--results-directory', str(self.run / 'trx')], 900)
        count = dict(total=0, passed=0, failed=0, notExecuted=0)
        suites = []
        for path in sorted((self.run / 'trx').glob('*.trx')):
            row = ET.parse(path).find('.//{*}Counters')
            values = {key: int(row.attrib[key]) for key in count}
            if values['total'] < 1:
                raise RuntimeError('Empty test assembly')
            suites.append(dict(path=str(path.relative_to(ROOT)), **values))
            for key in count:
                count[key] += values[key]
        if len(suites) != 6:
            raise RuntimeError('All six .NET test assemblies must report results')
        dotnet = totals(1758, count['total'], count['passed'], count['failed'], count['notExecuted'])
        files = sorted(str(p.relative_to(ROOT)) for p in (ROOT / 'tests/Functions').glob('*.test.mjs'))
        log = self.command('functions', ['node', '--experimental-strip-types', '--test', '--test-concurrency=1',
                         '--test-timeout=60000', *files], 300)
        return combine([dotnet, self.tap(log, 103)]), dict(suites=suites)

    def workers_lane(self):
        self.command('build-identity', ['python3', 'workers/scripts/generate-build-identity.py', '--check'], 30)
        for name in ['chatgpt-auth', 'chatgpt-mcp', 'companion-workspace']:
            self.command(name + '-build', ['npm', 'run', 'build'], 180, ROOT / 'workers' / name)
        rows = []
        for name, minimum in [('chatgpt-auth', 244), ('companion-workspace', 967)]:
            cwd = ROOT / 'workers' / name
            files = sorted(str(p.relative_to(cwd)) for p in (cwd / 'test').glob('*.test.mjs'))
            log = self.command(name, ['node', '--experimental-strip-types', '--test', '--test-concurrency=1',
                                    '--test-timeout=60000', *files], 1800, cwd)
            rows.append(self.tap(log, minimum))
        return combine(rows), dict(synthetic_providers=True, fixture_services_disposed_by_tests=True)

    def browser_lane(self):
        rows = []
        reports = []
        for key, directory in [('ui-qualification', 'ui-qualification'), ('canonical-destinations', 'canonical-destinations')]:
            target = '.skill-tree/history/validation/ux-ui/' + directory + '/browser'
            log = self.command(key, ['python3', 'tests/browser-qualification/ux-ui.py', key, target], 960)
            output = json.loads(log.read_text().splitlines()[-1])
            report = json.loads((ROOT / output['report']).read_text())
            counts = report['counts']
            rows.append(totals(counts['expected'], counts['discovered'], counts['passed'], counts['failed'], counts['skipped']))
            reports.append(output['report'])
        self.command('pwa-publish', [self.dotnet, 'publish', 'src/Habitica.WebApp/Habitica.WebApp.csproj', '-c', 'Release',
                                   '-o', 'output/pwa-shell-redirect', '-m:1', '-nodeReuse:false'], 300)
        log = self.command('pwa-lifecycle', ['node', 'tests/browser/pwa-shell.mjs'], 170)
        text = log.read_text()
        report = json.JSONDecoder().raw_decode(text[text.index('{'):])[0]
        if report.get('ok') is not True or len(report.get('results', [])) != 2:
            raise RuntimeError('Both browser engines must complete PWA lifecycle controls')
        for row in report['results']:
            for key in ['preFixNavigationFailed', 'workerOnlyUpgradeSameManifest', 'freshInstall',
                        'failedIntegrityUpdateRefused', 'missingShellOnlineFallback', 'missingShellOfflineFailure', 'canariesPreserved']:
                if row.get(key) is not True:
                    raise RuntimeError('Missing native PWA assertion: ' + key)
        rows.append(totals(2, 2, 2))
        return combine(rows), dict(reports=reports, pwa=report,
                 limits='Synthetic/local browser evidence; no real account, physical device, permission or publication.')

    def hosted_lane(self):
        import importlib.util
        spec = importlib.util.spec_from_file_location('coordinated_release', ROOT / 'deploy/coordinated-release.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        report = module.postflight()
        return totals(report['checks'], report['checks'], report['checks']), dict(hosted=report)

    def execute(self):
        self.report.mkdir(parents=True, exist_ok=True)
        try:
            counts, detail = getattr(self, self.lane + '_lane')()
            result = dict(pass_result=True, lane=self.lane, counts=counts, details=detail, steps=self.steps,
                          cleanup='Every launched command exited; browser/fixture controllers own isolated resources and record cleanup.',
                          seconds=round(time.monotonic() - self.started, 3))
        except Exception as error:
            result = dict(pass_result=False, lane=self.lane, error=str(error), steps=self.steps,
                          seconds=round(time.monotonic() - self.started, 3), cleanup='The failing command exited or reached its hard deadline; inspect the original resource cleanup receipt.')
        path = self.report / (self.run.name + '.json')
        path.write_text(json.dumps(result, indent=2) + '\n')
        print(json.dumps(dict(report=str(path.relative_to(ROOT)), **result)))
        return 0 if result['pass_result'] else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('lane', choices=['dotnet', 'workers', 'browser', 'hosted'])
    parser.add_argument('report_directory')
    args = parser.parse_args()
    raise SystemExit(Qualification(args.lane, args.report_directory).execute())
