r"""Parity cases for the skill tree, ranks, loadout and class-passive values (step 5.5).

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\parity\tree_parity.py

Runs on REAL heroes of UMBRA's composed engine (import game.core.engine installs every layer, as the game does):
player.unlock_skill, respec.refund/choose_passive, loadout.equip/unequip, points and level. After every operation it
records the return value and the whole tree (points, unlocked, investments, ranks, passive slots), the five slots, every
effect total (SkillTree.get_effect, as composed), the options each learned skill resolves with, and every class-passive
value and factor (skills/v16/passives.py). tests/Umbra.Domain.Tests/ParityTreeTests.cs replays them in C#.
Output: tests/parity/tree_cases.json
"""
import json
import os
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parents[2]
UMBRA = HERE.parent / 'umbra-ashes-below'
os.environ.setdefault('SDL_VIDEODRIVER', 'dummy'); os.environ.setdefault('SDL_AUDIODRIVER', 'dummy')
os.environ.setdefault('UMBRA_DATA_HOME', str(HERE / '.export-home'))
sys.path.insert(0, str(UMBRA))

import game.core.engine  # noqa: E402,F401  (composes the classes)
from game.entities.player import Player                      # noqa: E402
from game.skills import respec, loadout, tree as tree_module  # noqa: E402
from game.skills.v16 import passives, tuning, catalog        # noqa: E402
from game.combat_system import status                         # noqa: E402
from game.skills.catalog import get_skill, get_basic_skill     # noqa: E402

CLASSES = ('barbaro', 'mago', 'ladino', 'paladino', 'necromante', 'bruxo', 'druida')
EFFECTS = sorted(set(tree_module.EFFECT_CAPS) | {'regeneration', 'attack', 'defense'})
PASSIVE_EFFECTS = sorted({row['effect'] for rows in catalog.PASSIVES.values() for row in rows})


def plain(value):
    if isinstance(value, dict): return {str(k): plain(v) for k, v in value.items()}
    if isinstance(value, (list, tuple, set)): return [plain(v) for v in value]
    return value


def snap(p):
    t = p.skill_tree
    learned = [n['skill_id'] for k, n in t.nodes.items() if t.has(k) and n.get('kind') == 'root']
    return {
        'points': t.points, 'unlocked': sorted(t.unlocked), 'investments': sorted([k, v] for k, v in t.investments.items()),
        'ranks': sorted([k, v] for k, v in t.ranks.items()), 'slots': [[k, v] for k, v in t.passive_slots.items()],
        'basic': p.basic_skill_id, 'main': list(p.equipped_main_skills), 'unlocked_main': sorted(p.unlocked_main_skills),
        'active_slot': p.active_main_slot, 'active_skills': t.get_active_skills(),
        'effects': [t.get_effect(e) for e in EFFECTS],
        'chosen': [[sid, [row['options'].index(o) for o in tuning.chosen(p, row)], respec.equipped(p, 'learn:' + sid)]
                   for sid in learned for row in [get_skill(sid) or get_basic_skill(sid)]],
        'passives': [passives.value(p, e) for e in PASSIVE_EFFECTS] + [passives.owned(p, e) for e in PASSIVE_EFFECTS],
        'factors': [passives.duration_factor(p, k) for k in ('slow', 'root', 'poison', 'stun')]
                   + [passives.dot_factor(p, k) for k in ('burn', 'decay', 'bleed')]
                   + [passives.heal_factor(p), passives.barrier_factor(p), passives.health_cost_factor(p), passives.resource_gain_factor(p),
                      passives.regen_factor(p), passives.incoming_factor(p, 'physical'), passives.incoming_factor(p, 'fire'),
                      *passives.chain_bonus(p)],
    }


def run(op, p):
    kind = op['op']
    if kind == 'learn': return p.unlock_skill(op['key'])
    if kind == 'refund': return list(respec.refund(p, op['key']))
    if kind == 'choose': return respec.choose_passive(p, op['key'])
    if kind == 'equip': return list(loadout.equip(p, op['skill'], op['index']))
    if kind == 'unequip': loadout.unequip(p, op['index']); return None
    if kind == 'points': p.skill_tree.add_point(op['n']); return None
    if kind == 'level': p.level = op['n']; return None
    if kind == 'health': p.health = max(1, round(p.max_health * op['ratio'])); return None
    if kind == 'state': status.apply(p, op['kind'], 600, op['mag']); return None
    raise ValueError(kind)


def record(name, cls, ops):
    p = Player((5, 5), cls)
    steps = []
    for op in ops:
        result = run(op, p)
        steps.append({'op': op, 'result': plain(result), 'state': snap(p),
                      'health': [p.health, p.max_health]})
    return {'name': name, 'class': cls, 'steps': steps}


def keys(cls):
    return list(Player((5, 5), cls).skill_tree.nodes)


def handwritten():
    cases = []
    for cls in CLASSES:
        nodes = Player((5, 5), cls).skill_tree.nodes
        roots = [k for k, n in nodes.items() if n.get('kind') == 'root']
        klass = [k for k, n in nodes.items() if n.get('kind') == 'class_passive']
        legacy = [k for k, n in nodes.items() if n.get('legacy')]
        basic = next(k for k in roots if nodes[k].get('family') == 'basic')
        main = [k for k in roots if nodes[k].get('family') == 'main']
        ops = [dict(op='learn', key=legacy[0]), dict(op='learn', key=basic), dict(op='learn', key=main[0]),   # no points, then gates
               dict(op='points', n=40), dict(op='learn', key=main[0]), dict(op='learn', key=basic + ':mod:power'),
               dict(op='level', n=3), dict(op='learn', key=main[0]), dict(op='learn', key=basic + ':mod:power'),
               dict(op='learn', key=basic + ':mod:reach'), dict(op='learn', key=basic + ':passive:0'), dict(op='level', n=30),
               dict(op='learn', key=basic + ':passive:0'), dict(op='learn', key=basic + ':passive:1'), dict(op='choose', key=basic + ':passive:1'),
               dict(op='learn', key=basic + ':passive:2'), dict(op='learn', key=basic), dict(op='learn', key=basic),
               dict(op='refund', key=basic), dict(op='refund', key=basic + ':mod:power'), dict(op='refund', key=basic + ':passive:0'),
               dict(op='learn', key=basic + ':passive:1'), dict(op='choose', key=basic + ':passive:1'), dict(op='choose', key=basic + ':passive:0'),
               dict(op='points', n=40)] + [dict(op='learn', key=basic)] * 16 + [dict(op='refund', key=basic)] * 3
        for k in klass: ops += [dict(op='learn', key=k), dict(op='learn', key=k), dict(op='learn', key=k), dict(op='learn', key=k)]
        ops += [dict(op='health', ratio=.4), dict(op='health', ratio=.9), dict(op='state', kind='focus', mag=.3), dict(op='state', kind='martyr', mag=.3)]
        for k in main: ops += [dict(op='learn', key=k)]
        for k in main[:5]: ops += [dict(op='learn', key=k + ':mod:flow'), dict(op='learn', key=k + ':passive:3')]
        ids = [nodes[k]['skill_id'] for k in roots]
        ops += [dict(op='equip', skill=ids[-1], index=0), dict(op='equip', skill=ids[1], index=4), dict(op='equip', skill='nada', index=2),
                dict(op='equip', skill=ids[2], index=7), dict(op='unequip', index=1), dict(op='equip', skill=ids[3], index=1)]
        supremes = [k for k in roots if nodes[k].get('family') == 'main' and loadout.is_supreme(nodes[k]['skill_id'])]
        for k in supremes[:2]: ops += [dict(op='equip', skill=nodes[k]['skill_id'], index=2 + supremes.index(k))]
        ops += [dict(op='refund', key=main[0]), dict(op='refund', key=main[0] + ':mod:flow'), dict(op='refund', key=main[0])]
        for k in klass: ops += [dict(op='refund', key=k)]
        cases.append(record(f'{cls}: compra, graus, exclusivos, níveis, passivas, espaços e devoluções', cls, ops))
    return cases


def randomized(count, length, seed):
    rng = random.Random(seed)
    cases = []
    for c in range(count):
        cls = rng.choice(CLASSES); nodes = Player((5, 5), cls).skill_tree.nodes
        node_keys = [k for k in nodes if not nodes[k].get('legacy')] + [k for k in nodes if nodes[k].get('legacy')][:2]
        skills = [n['skill_id'] for n in nodes.values() if n.get('kind') == 'root']
        ops = [dict(op='points', n=rng.choice([3, 10, 60])), dict(op='level', n=rng.choice([1, 5, 12, 25, 70]))]
        for _ in range(length):
            r = rng.random()
            if r < .55: ops.append(dict(op='learn', key=rng.choice(node_keys)))
            elif r < .68: ops.append(dict(op='refund', key=rng.choice(node_keys)))
            elif r < .74: ops.append(dict(op='choose', key=rng.choice(node_keys)))
            elif r < .86: ops.append(dict(op='equip', skill=rng.choice(skills), index=rng.randint(0, 5)))
            elif r < .89: ops.append(dict(op='unequip', index=rng.randint(0, 4)))
            elif r < .93: ops.append(dict(op='points', n=rng.choice([1, 2, 5])))
            elif r < .96: ops.append(dict(op='level', n=rng.choice([2, 6, 15, 26, 40])))
            else: ops.append(dict(op='health', ratio=rng.choice([.2, .45, .85, 1.])))
        cases.append(record(f'aleatório {seed}-{c} ({cls})', cls, ops))
    return cases


def main():
    cases = handwritten() + randomized(100, 60, 2029)
    out = HERE / 'tests' / 'parity' / 'tree_cases.json'
    payload = {'source': 'umbra-ashes-below game/skills (tree, ranks, respec, loadout, v16/passives) on the composed engine',
               'effects': EFFECTS, 'passive_effects': PASSIVE_EFFECTS, 'cases': cases}
    out.write_bytes(json.dumps(payload, ensure_ascii=False, separators=(',', ':')).encode('utf-8'))
    print(f'{len(cases)} casos, {sum(len(c["steps"]) for c in cases)} operações -> {out} ({out.stat().st_size // 1024} KB)')


if __name__ == '__main__':
    main()
