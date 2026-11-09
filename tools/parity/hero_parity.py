r"""Parity cases for the hero core (step 5.8a): stats, levels, resource, damage taken, potions, movement, dash, ticks.

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\parity\hero_parity.py

REAL heroes of UMBRA's composed engine (Player((x, y), cls) with all five __init__ layers; recalculate_stats through
the equipment, compatibility and balance layers plus class passives; take_damage through the legendary aspects, the
sanctuary guard and core.balance; gain_xp, level_up, get_attack_damage, use_potion, dash and update as composed).
A 'tick' is the hero's part of the engine tick: player.update(), then status.tick(player) and resources.tick(player)
(the lifecycle steps on the hero; the Warlock's and Necromancer's own ticks come with those classes). The basic attack
(resolver) is step 5.8b: no attack is started here, so only its timers run. Python's global `random` is seeded per case.
Output: tests/parity/hero_cases.json
"""
import copy
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

import game.core.engine  # noqa: E402,F401
import game.sound.api as sound_api  # noqa: E402
sound_api.play_sfx = lambda *a, **k: None
import game.core.balance as balance  # noqa: E402
balance.play_sfx = lambda *a, **k: None
from game.entities.player import Player  # noqa: E402
from game.entities.items import Item  # noqa: E402
from game.equipment import transactions, itemdata, loot  # noqa: E402
from game.combat_system import status, resources, elements  # noqa: E402
from game.inventory import pages  # noqa: E402
elements.EVENTS.clear()

CLASSES = ('barbaro', 'mago', 'ladino', 'paladino', 'necromante', 'bruxo', 'druida')
MAP = ["####################",
       "#..................#",
       "#....#.............#",
       "#....#....###......#",
       "#.................##",
       "#.........#........#",
       "#..................#",
       "####################"]


class Dungeon:
    def __init__(self):
        self.map = [[1 if c == '#' else 0 for c in row] for row in MAP]; self.height, self.width = len(MAP), len(MAP[0])
        self._active_enemies = []


class Target:
    """Enemy fixture for get_attack_damage (health ratio and position only)."""
    def __init__(self, x, y, health, max_health): self.x, self.y, self.health, self.max_health = x, y, health, max_health
    @property
    def position(self): return (self.x, self.y)


def plain(v):
    if isinstance(v, dict): return {str(k): plain(x) for k, x in v.items()}
    if isinstance(v, (list, tuple, set)): return [plain(x) for x in v]
    return v


def rows(entity):
    data = getattr(entity, 'statuses', None) or {}
    return [[k, [[r['ticks'], float(r['magnitude']), r.get('timer'), float(r.get('carry', 0.))] for r in data[k]]] for k in data]


def snap(p):
    d = p.__dict__
    return {
        'x': p.x, 'y': p.y, 'previous': list(p.previous_position), 'alive': p.alive, 'level': p.level, 'xp': p.xp, 'xp_to_next': p.xp_to_next,
        'base_attack': p.base_attack, 'base_defense': p.base_defense, 'attack': p.attack, 'defense': p.defense,
        'health': p.health, 'max_health': p.max_health, 'mana': float(p.mana), 'max_mana': p.max_mana, 'resource': p.resource, 'max_resource': p.max_resource,
        'regen_acc': float(p.resource_regen_accumulator), 'regeneration_timer': p.regeneration_timer, 'skill_cooldown': p.skill_cooldown,
        'dash_cooldown': p.dash_cooldown, 'invulnerability': p.invulnerability, 'power': [float(p.temporary_power), p.temporary_power_timer],
        'shield': p.shield_points, 'attack_cooldown': p.attack_cooldown, 'combo': [p.combo_index, p.combo_window], 'statuses': rows(p),
        'fortify': [float(d.get('fortify', 0.)), int(d.get('fortify_timer', 0)), float(d.get('fortify_carry', 0.))],
        'potion': p.inventory.get('potion', 0), 'capacity': p.inventory.capacity, 'points': p.skill_tree.points, 'gold': p.gold,
        'relics': list(p.relics), 'ira_carry': float(d.get('warlock_ira_carry', 0.)), 'name': getattr(p, 'name', None),
        'cooldowns': sorted([k, v] for k, v in p.skill_cooldowns.items()), 'items': len(p.inventory.items),
    }


def run(op, p, dungeon):
    k = op['op']
    if k == 'xp': p.gain_xp(op['n']); return None
    if k == 'hurt': return p.take_damage(op['n'])
    if k == 'gain': return p.gain_resource(op['n'], source=op.get('source', 'combat'))
    if k == 'spend': return p.spend_resource(op['n'])
    if k == 'tick':
        for _ in range(op['n']):
            p.update(); status.tick(p); resources.tick(p)
        return None
    if k == 'potion': return p.use_potion()
    if k == 'potions': return pages.add_potions(p, op['n'])
    if k == 'move': return p.move(op['dx'], op['dy'], dungeon)
    if k == 'dash': return p.dash(op['dx'], op['dy'], dungeon)
    if k == 'state': return status.apply(p, op['kind'], op['ticks'], op['mag'])
    if k == 'stack': return resources.add_stack(p, op['kind'], op['n'])
    if k == 'fortify': return resources.fortify(p, op['n'])
    if k == 'shield': p.shield_points = op['n']; return None
    if k == 'power': p.temporary_power = op['value']; p.temporary_power_timer = op['ticks']; return None
    if k == 'points': p.skill_tree.add_point(op['n']); return None
    if k == 'learn': return p.unlock_skill(op['key'])
    if k == 'equip': return list(transactions.equip(p, copy.deepcopy(op['record'])))
    if k == 'unequip': return transactions.unequip(p, op['slot'])
    if k == 'damage':
        target = Target(p.x + op['dx'], p.y + op['dy'], op['health'], 100) if op.get('health') is not None else None
        return p.get_attack_damage(target, skill=op['skill'])
    if k == 'collect':
        item = Item((p.x, p.y), op['kind'], op['floor'], player_level=p.level, player_class=p.player_class)
        return [p.collect_item(item), plain(item.data)]
    if k == 'recalc': p.recalculate_stats(); return None
    raise ValueError(k)


def record(name, cls, ops, seed):
    random.seed(seed)
    p = Player((3, 3), cls); dungeon = Dungeon(); p._active_dungeon = dungeon
    start = snap(p); steps = []
    for op in ops:
        result = run(op, p, dungeon)
        steps.append({'op': op, 'result': plain(result), 'state': snap(p)})
    return {'name': name, 'class': cls, 'seed': seed, 'start': start, 'steps': steps}


def gear(cls, rng, legendary_aspect=None):
    """Real records from loot.roll, plus optional aspects on a chest piece (aspects work from any equipped record)."""
    random.seed(rng.randint(0, 10 ** 6))
    rows_ = [loot.roll(kind, rng.randint(1, 30), rng.choice([5, 20, 40]), rng.choice(['Comum', 'Mágico', 'Raro', 'Lendário']), cls)
             for kind in ('weapon', 'armor', 'armor', 'accessory', 'accessory', 'weapon')]
    if legendary_aspect:
        chest = loot.roll('armor', 10, 10, 'Raro', cls); chest['slot'] = 'chest'; chest['aspect'] = legendary_aspect; chest['required_level'] = 1
        rows_.append(chest)
    for r in rows_: r['required_level'] = 1
    return rows_


def handwritten():
    cases = []
    for i, cls in enumerate(CLASSES):
        nodes = Player((0, 0), cls).skill_tree.nodes
        klass = [k for k, n in nodes.items() if n.get('kind') == 'class_passive']
        ops = [dict(op='tick', n=120), dict(op='xp', n=50), dict(op='xp', n=400), dict(op='xp', n=5000), dict(op='xp', n=90000), dict(op='tick', n=30),
               dict(op='hurt', n=10), dict(op='hurt', n=60), dict(op='tick', n=200), dict(op='potion'), dict(op='potions', n=9), dict(op='potion'), dict(op='potion'),
               dict(op='gain', n=7.4), dict(op='gain', n=33, source='regeneration'), dict(op='spend', n=20), dict(op='spend', n=500), dict(op='tick', n=600),
               dict(op='move', dx=1, dy=0), dict(op='move', dx=1, dy=1), dict(op='move', dx=1, dy=-1), dict(op='move', dx=0, dy=-1), dict(op='dash', dx=1, dy=0),
               dict(op='dash', dx=1, dy=0), dict(op='tick', n=181), dict(op='dash', dx=0, dy=1), dict(op='hurt', n=40), dict(op='tick', n=31), dict(op='hurt', n=40),
               dict(op='state', kind='guard', ticks=300, mag=.3), dict(op='hurt', n=80), dict(op='state', kind='barrier', ticks=300, mag=50), dict(op='hurt', n=80),
               dict(op='shield', n=25), dict(op='hurt', n=80), dict(op='stack', kind='determination', n=5), dict(op='hurt', n=80),
               dict(op='state', kind='intangible', ticks=60, mag=0), dict(op='hurt', n=500), dict(op='tick', n=61),
               dict(op='state', kind='focus', ticks=600, mag=.5), dict(op='tick', n=120), dict(op='state', kind='martyr', ticks=200, mag=.2), dict(op='tick', n=100),
               dict(op='fortify', n=200), dict(op='tick', n=240), dict(op='power', value=.25, ticks=100),
               dict(op='damage', skill=False, health=None), dict(op='damage', skill=True, health=None), dict(op='damage', skill=False, health=20, dx=1, dy=0),
               dict(op='damage', skill=True, health=90, dx=2, dy=1), dict(op='tick', n=101), dict(op='damage', skill=False, health=10, dx=1, dy=1)]
        for k in klass: ops += [dict(op='learn', key=k)] * 4
        ops += [dict(op='recalc'), dict(op='tick', n=300), dict(op='hurt', n=120), dict(op='potion')]
        ops += [dict(op='state', kind='laststand', ticks=300, mag=0), dict(op='hurt', n=99999), dict(op='hurt', n=99999), dict(op='tick', n=5)]
        cases.append(record(f'{cls}: níveis, dano, recurso, poções, movimento, investida, estados, passivas', cls, ops, 7100 + i))
    rng = random.Random(77)
    for i, cls in enumerate(CLASSES):
        records = gear(cls, rng)
        ops = [dict(op='xp', n=20000)] + [dict(op='equip', record=r) for r in records] + [dict(op='tick', n=10), dict(op='hurt', n=50)]
        for aspect in ('ancestral_guard', 'oath_guard', 'devastation', 'resonance', 'radiant_verdict', 'bulwark', 'celerity', 'shadow_step'):
            chest = gear(cls, rng, aspect)[-1]
            ops += [dict(op='equip', record=chest), dict(op='hurt', n=50), dict(op='damage', skill=False, health=None), dict(op='damage', skill=True, health=None),
                    dict(op='potions', n=2), dict(op='hurt', n=100), dict(op='potion'), dict(op='dash', dx=1, dy=0), dict(op='tick', n=200)]
        ops += [dict(op='unequip', slot=s) for s in ('chest', 'helmet', 'ring1', 'amulet')] + [dict(op='tick', n=5)]
        for kind in ('power', 'protection', 'gold', 'potion', 'resource_orb', 'relic', 'relic', 'weapon', 'armor', 'accessory'):
            ops.append(dict(op='collect', kind=kind, floor=rng.randint(1, 25)))
        cases.append(record(f'{cls}: equipamento, aspectos lendários e coletas', cls, ops, 7200 + i))
    return cases


def randomized(count, length, seed):
    rng = random.Random(seed); cases = []
    for c in range(count):
        cls = CLASSES[c % len(CLASSES)]
        nodes = Player((0, 0), cls).skill_tree.nodes
        klass = [k for k, n in nodes.items() if n.get('kind') == 'class_passive']
        records = gear(cls, rng, rng.choice([None, 'ancestral_guard', 'devastation', 'bulwark']))
        ops = [dict(op='xp', n=rng.choice([0, 300, 3000, 40000]))]
        for _ in range(length):
            r = rng.random()
            if r < .2: ops.append(dict(op='tick', n=rng.choice([1, 30, 60, 181, 600])))
            elif r < .32: ops.append(dict(op='hurt', n=rng.choice([1, 15, 45, 120, 400])))
            elif r < .38: ops.append(dict(op='xp', n=rng.choice([10, 150, 900])))
            elif r < .44: ops.append(dict(op=rng.choice(['gain', 'spend']), n=rng.choice([1, 6.5, 25, 80])))
            elif r < .50: ops.append(dict(op=rng.choice(['potion', 'potions']), n=rng.choice([1, 3])))
            elif r < .58: ops.append(dict(op=rng.choice(['move', 'dash', 'move']), dx=rng.choice([-1, 0, 1]), dy=rng.choice([-1, 0, 1])))
            elif r < .66: ops.append(dict(op='state', kind=rng.choice(['guard', 'barrier', 'intangible', 'focus', 'martyr', 'laststand', 'power']), ticks=rng.choice([60, 300]), mag=rng.choice([.2, .5, 40])))
            elif r < .70: ops.append(dict(op='stack', kind=rng.choice(['determination', 'ferocity']), n=rng.choice([1, 3])))
            elif r < .73: ops.append(dict(op='fortify', n=rng.choice([20, 150])))
            elif r < .76: ops.append(dict(op='shield', n=rng.choice([0, 15, 60])))
            elif r < .80: ops.append(dict(op='power', value=rng.choice([0, .2, .5]), ticks=rng.choice([1, 50, 200])))
            elif r < .84: ops.append(dict(op='learn', key=rng.choice(klass)))
            elif r < .86: ops.append(dict(op='points', n=rng.choice([1, 5])))
            elif r < .92: ops.append(dict(op='equip', record=rng.choice(records)))
            elif r < .94: ops.append(dict(op='unequip', slot=rng.choice(['chest', 'helmet', 'ring1', 'amulet', 'boots'])))
            elif r < .98: ops.append(dict(op='damage', skill=rng.random() < .5, health=rng.choice([None, 10, 60]), dx=rng.choice([1, 2]), dy=rng.choice([0, 1])))
            else: ops.append(dict(op='collect', kind=rng.choice(['power', 'protection', 'gold', 'potion', 'resource_orb', 'relic']), floor=rng.randint(1, 30)))
        cases.append(record(f'aleatório {seed}-{c} ({cls})', cls, ops, seed * 100 + c))
    return cases


def main():
    cases = handwritten() + randomized(105, 60, 2033)
    out = HERE / 'tests' / 'parity' / 'hero_cases.json'
    from game.equipment import slots
    families = {c: list(tuple({kind for values in slots.CLASS_WEAPONS[c].values() for kind in values})) for c in CLASSES}   # set order of this process
    payload = {'source': 'umbra-ashes-below entities/player + core.balance + equipment + legendary on the composed engine', 'map': MAP,
               'families': families, 'cases': cases}
    out.write_bytes(json.dumps(payload, ensure_ascii=False, separators=(',', ':')).encode('utf-8'))
    print(f'{len(cases)} casos, {sum(len(c["steps"]) for c in cases)} operações -> {out} ({out.stat().st_size // 1024} KB)')


if __name__ == '__main__':
    main()
