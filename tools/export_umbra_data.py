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


def trees():
    """The skill tree of each class exactly as the game builds it: a real hero of the composed engine (game.core.engine
    installs every layer and turns the 0.16 catalogs on), in node order. Each node also carries the level the real
    requirements.required_level asks for. Runs last: importing the engine composes the classes."""
    import game.core.engine  # noqa: F401
    from game.entities.player import Player
    from game.skills import tree as tree_module, ranks, requirements, loadout
    from game.skills.catalog import get_basic_skills, get_main_skills
    out = {'EFFECT_CAPS': tree_module.EFFECT_CAPS, 'MAX_RANK': ranks.MAX_RANK, 'PASSIVE_RANK': ranks.PASSIVE_RANK,
           'MAX_LEVEL': ranks.MAX_LEVEL, 'SLOTS': loadout.COUNT, 'classes': {}}
    for cls in ('barbaro', 'mago', 'ladino', 'paladino', 'necromante', 'bruxo', 'druida'):
        hero = Player((0, 0), cls); tree = hero.skill_tree
        nodes = [[key, {**node, 'required_level': requirements.required_level(node)}] for key, node in tree.nodes.items()]
        skills = {row['id']: {'category': row.get('category'), 'basic': True} for row in get_basic_skills(cls)}
        skills.update({row['id']: {'category': row.get('category'), 'basic': False} for row in get_main_skills(cls)})
        out['classes'][cls] = {'points': tree.points, 'nodes': nodes, 'skills': skills,
                               'start': {'basic_skill_id': hero.basic_skill_id, 'equipped_main_skills': hero.equipped_main_skills,
                                         'unlocked': sorted(tree.unlocked)}}
    return out


def equipment():
    """Item tables (step 5.6), read after the engine is composed: core.balance sets the live rarity multipliers and
    legendary.install fills the forge's aspect names. Only tables; the rules are ported in C#."""
    import game.core.engine  # noqa: F401
    from game.entities import items
    from game.equipment import (loot, slots, legendary, necro_affixes, itemdata, gems, temper, enchant, recycle, materials,
                                codex, jeweler, forge)
    from game.inventory import pages, lost
    from game.skills.catalog import RELIC_CATALOG
    take = lambda module, *names: {n: getattr(module, n) for n in names}
    return {
        'items': {'RARITIES': items.RARITIES, 'WEAPONS': items.WEAPONS, 'ARMORS': items.ARMORS, 'WEAPON_BASE': items.WEAPON_BASE,
                  'ARMOR_BASE': items.ARMOR_BASE, 'ACCESSORIES': items.ACCESSORIES, 'RELICS': list(RELIC_CATALOG.values())},
        'loot': take(loot, 'WEAPONS', 'ARMORS', 'SUFFIXES'),
        'slots': take(slots, 'ARMOR', 'JEWELRY', 'CLASS_WEAPONS', 'LABELS', 'SUPPORT_SLOTS'),
        'legendary': {'CATALOG': legendary.CATALOG},
        'necro_affixes': take(necro_affixes, 'AFFIXES', 'PERCENT', 'CAPS', 'TAG'),
        'itemdata': take(itemdata, 'EQUIPMENT', 'DURABILITY_MAX', 'DEATH_LOSS', 'QUALITY_MAX', 'QUALITY_STEP', 'QUALITY_SPECIAL',
                         'MAXIMIZE_BONUS', 'SPECIAL_MATERIAL', 'SOCKET_LIMITS'),
        'gems': take(gems, 'GRADES', 'FAMILIES', 'STAT_NAMES'),
        'temper': take(temper, 'RECIPES', 'STARTING', 'RESETS', 'DEFAULT_RESETS', 'COST', 'GOLD', 'STAT_NAMES'),
        'enchant': take(enchant, 'KEYS', 'NAMES', 'BASE_GOLD', 'GROWTH'),
        'recycle': take(recycle, 'VALUABLE', 'MODES', 'QUESTION'),
        'materials': take(materials, 'CATALOG', 'RECYCLE_YIELD'),
        'codex': take(codex, 'CATEGORIES', 'ASPECT_CATEGORY', 'SLOT_CATEGORIES', 'ENGRAVE_RARITIES', 'ENGRAVE_GOLD'),
        'jeweler': take(jeweler, 'CREATE_COUNT', 'SOCKET_COST'),
        'forge': {'ASPECTS': forge.ASPECTS},
        'pages': take(pages, 'CAPACITY', 'PAGES', 'NAMES', 'ATTR', 'EQUIPMENT', 'POTION_CAP', 'STACK_MAX'),
        'lost': {'MAX': lost.MAX},
    }


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    hashes = {}
    for name in ('classes', 'enemies', 'bosses', 'items'):
        hashes[name + '.json'] = write(name + '.json', json.loads((UMBRA / 'data' / f'{name}.json').read_text(encoding='utf-8')))

    from game.skills.v16 import catalog
    skills = {cls: {'skills': catalog.SKILLS[cls], 'passives': catalog.PASSIVES[cls], 'extra': catalog.EXTRA.get(cls, [])}
              for cls in catalog.MODULES}
    hashes['skills.json'] = write('skills.json', skills)

    from game.combat_system import status, elements, resources
    from game.equipment import rarity
    from game.accessibility import palette
    rules = {
        'status': {name: getattr(status, name) for name in (
            'DOT', 'CONTROL', 'BUFFS', 'TAGS', 'KINDS', 'BOSS_IMMUNE', 'FROST_SLOW', 'FROST_BOSS_SLOW', 'FREEZE_TICKS',
            'CORRUPTION_HEAL', 'CAPS', 'HARMFUL', 'TICK', 'MAX_STACKS', 'MAX_TICKS', 'BOSS_FACTOR', 'PRESSURE_STEP',
            'IMMUNE_AT', 'PRESSURE_DECAY', 'SAVED')},
        'elements': {'NAMES': elements.NAMES, 'COLORS': elements.COLORS, 'CLASS_POOL': elements.CLASS_POOL, 'PROFILES': elements.PROFILES},
        'resources': {name: getattr(resources, name) for name in (
            'FORTIFY_HEAL', 'FORTIFY_SECONDS', 'ORB_SECONDS', 'ORB_HEAL', 'ORB_RESOURCE', 'ORB_LIMIT', 'STACKS')},
        'rarity': {name: getattr(rarity, name) for name in ('ORDER', 'LEGACY', 'DROPPABLE', 'TIER', 'TAG', 'FRAME', 'MULTIPLIER', 'SUPERIOR')},
        'palette': {'DEFAULT': palette.DEFAULT},
    }
    hashes['rules.json'] = write('rules.json', rules)
    hashes['trees.json'] = write('trees.json', trees())
    hashes['equipment.json'] = write('equipment.json', equipment())

    commit = subprocess.run(['git', '-C', str(UMBRA), 'rev-parse', 'HEAD'], capture_output=True, text=True).stdout.strip()
    branch = subprocess.run(['git', '-C', str(UMBRA), 'rev-parse', '--abbrev-ref', 'HEAD'], capture_output=True, text=True).stdout.strip()
    (OUT / 'manifest.json').write_text(json.dumps({'umbra_commit': commit, 'umbra_branch': branch, 'files': hashes}, indent=1) + '\n', encoding='utf-8')
    counts = {cls: len(v['skills']) for cls, v in skills.items()}
    print(f'exportado de {branch} {commit[:7]}: {", ".join(hashes)}; habilidades por classe {counts}')


if __name__ == '__main__':
    main()
