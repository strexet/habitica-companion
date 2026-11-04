#!/usr/bin/env python3
"""Stage a verified public plugin archive with the exact Pages build revision."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import shutil
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def build(published, revision, endpoint):
    published = Path(published).resolve()
    settings = json.loads((published / 'appsettings.json').read_text())
    if not settings.get('ChatGptIntegration', {}).get('Enabled'):
        return {'skipped': True, 'reason': 'Optional integration is disabled in the published frontend.'}
    if not endpoint:
        raise ValueError('An enabled frontend requires the reviewed public HABITICA_PLUGIN_MCP_ENDPOINT.')
    spec = importlib.util.spec_from_file_location('plugin_packager', ROOT / 'deploy/package-plugin.py')
    package = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(package)
    with tempfile.TemporaryDirectory(prefix='habitica-public-plugin-') as temporary:
        target = Path(temporary) / 'package'
        receipt = package.build(ROOT / 'plugin', target, revision,
                    json.loads((ROOT / 'deploy/plugin-requirements.json').read_text()), endpoint)
        package.verify_archive(target.with_suffix('.zip'))
        name = 'habitica-companion-' + receipt['packageVersion']
        output = published / 'downloads'
        output.mkdir(exist_ok=True)
        shutil.copyfile(target.with_suffix('.zip'), output / (name + '.zip'))
        shutil.copyfile(target.with_suffix('.zip.identity.json'), output / (name + '.identity.json'))
    return {'skipped': False, 'archive': 'downloads/' + name + '.zip', **receipt}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--published', required=True)
    parser.add_argument('--source-revision', required=True)
    parser.add_argument('--endpoint', default=os.environ.get('HABITICA_PLUGIN_MCP_ENDPOINT', ''))
    args = parser.parse_args()
    print(json.dumps(build(args.published, args.source_revision, args.endpoint)))
