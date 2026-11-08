r"""Exports UMBRA's data from the Python repository to JSON for the C# domain (Phase 5, step 1: data).

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\export_umbra_data.py

Nothing is retyped: the catalogs are read from the UMBRA repository itself (data/*.json and the Python catalogs of the
0.16 skills, validated on import) and the rule tables straight from the modules that use them. A manifest records the
UMBRA commit and the SHA-256 of every file, so a C# test can tell when the export is stale.
Output: game/data/umbra/*.json (read by Umbra.Domain.Data).
"""
import hashlib
import json
import os
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parents[1]
UMBRA = HERE.parent / 'umbra-ashes-below'
OUT = HERE / 'game' / 'data' / 'umbra'
os.environ.setdefault('SDL_VIDEODRIVER', 'dummy'); os.environ.setdefault('SDL_AUDIODRIVER', 'dummy')
os.environ.setdefault('UMBRA_DATA_HOME', str(HERE / '.export-home'))
sys.path.insert(0, str(UMBRA))


def plain(value):
    """JSON-safe copy: tuples/sets become lists, everything else that is not JSON becomes its text."""
    if isinstance(value, dict): return {str(k): plain(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)): return [plain(v) for v in value]
    if isinstance(value, (set, frozenset)): return sorted(plain(v) for v in value)
    if isinstance(value, (str, int, float, bool)) or value is None: return value
    return str(value)


def write(name, data):
    text = json.dumps(plain(data), ensure_ascii=False, indent=1, sort_keys=False)
    (OUT / name).write_bytes((text + '\n').encode('utf-8'))      # exact bytes: write_text would turn \n into CRLF on Windows
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    hashes = {}
    for name in ('classes', 'enemies', 'bosses', 'items'):
        hashes[name + '.json'] = write(name + '.json', json.loads((UMBRA / 'data' / f'{name}.json').read_text(encoding='utf-8')))

    from game.skills.v16 import catalog
    skills = {cls: {'skills': catalog.SKILLS[cls], 'passives': catalog.PASSIVES[cls], 'extra': catalog.EXTRA.get(cls, [])}
              for cls in catalog.MODULES}
    hashes['skills.json'] = write('skills.json', skills)

    from game.combat_system import status, elements
    from game.equipment import rarity
    from game.accessibility import palette
    rules = {
        'status': {name: getattr(status, name) for name in (
            'DOT', 'CONTROL', 'BUFFS', 'TAGS', 'KINDS', 'BOSS_IMMUNE', 'FROST_SLOW', 'FROST_BOSS_SLOW', 'FREEZE_TICKS',
            'CORRUPTION_HEAL', 'CAPS', 'HARMFUL', 'TICK', 'MAX_STACKS', 'MAX_TICKS', 'BOSS_FACTOR', 'PRESSURE_STEP',
            'IMMUNE_AT', 'PRESSURE_DECAY', 'SAVED')},
        'elements': {'NAMES': elements.NAMES, 'COLORS': elements.COLORS, 'CLASS_POOL': elements.CLASS_POOL, 'PROFILES': elements.PROFILES},
        'rarity': {name: getattr(rarity, name) for name in ('ORDER', 'LEGACY', 'DROPPABLE', 'TIER', 'TAG', 'FRAME', 'MULTIPLIER', 'SUPERIOR')},
        'palette': {'DEFAULT': palette.DEFAULT},
    }
    hashes['rules.json'] = write('rules.json', rules)

    commit = subprocess.run(['git', '-C', str(UMBRA), 'rev-parse', 'HEAD'], capture_output=True, text=True).stdout.strip()
    branch = subprocess.run(['git', '-C', str(UMBRA), 'rev-parse', '--abbrev-ref', 'HEAD'], capture_output=True, text=True).stdout.strip()
    (OUT / 'manifest.json').write_text(json.dumps({'umbra_commit': commit, 'umbra_branch': branch, 'files': hashes}, indent=1) + '\n', encoding='utf-8')
    counts = {cls: len(v['skills']) for cls, v in skills.items()}
    print(f'exportado de {branch} {commit[:7]}: {", ".join(hashes)}; habilidades por classe {counts}')


if __name__ == '__main__':
    main()
