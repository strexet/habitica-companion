#!/usr/bin/env python3
"""Select the current task journey fixtures from fresh synthetic Razor captures."""
import argparse
import json
import shutil
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--captured', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
repository = Path(__file__).resolve().parents[2]
pack = json.loads(Path(__file__).with_suffix('.json').read_text())
coverage = json.loads((args.captured / 'coverage.json').read_text())
names = list(dict.fromkeys(name for journey in pack['journeys'] for name in journey['fixtures']))
if args.output.exists():
    parser.error('Output must be a new directory; captured fixtures are never overwritten.')
missing = [name for name in names if name not in coverage]
if missing:
    parser.error('Missing fresh captures: ' + ', '.join(missing))
for name in names:
    item = coverage[name]
    if not item.get('interactionTest') or not (args.captured / item['file']).is_file():
        parser.error('Missing interaction provenance or markup: ' + name)
    route = item['route']
    if not any('@page "' + route + '"' in page.read_text() for page in (repository / 'src/Habitica.WebApp/Pages').glob('*.razor')):
        parser.error('Fixture route is absent from current production pages: ' + route)
args.output.mkdir(parents=True)
for name in names:
    shutil.copyfile(args.captured / coverage[name]['file'], args.output / coverage[name]['file'])
shutil.copyfile(args.captured / 'schemes.json', args.output / 'schemes.json')
(args.output / 'coverage.json').write_text(json.dumps({name: coverage[name] for name in names}, indent=2) + '\n')
(args.output / 'journey-pack.json').write_text(json.dumps(pack, indent=2) + '\n')
print(json.dumps({'journeys': len(pack['journeys']), 'fixtures': len(names), 'output': str(args.output)}))
