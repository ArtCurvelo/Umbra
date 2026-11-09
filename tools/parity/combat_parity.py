r"""Parity cases for the hero's basic attack and slot skills (step 5.8b), in a full combat loop.

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\parity\combat_parity.py

REAL heroes and REAL enemies of UMBRA's composed engine fight on a fixture map. The hero learns skills in the real tree
(player.unlock_skill), puts them in the five slots (loadout.equip) and uses them through the composed entry points:
player.attack_enemy (attack button: begin_player_attack wind-up, buffer, combo; a non-basic skill in slot 0 is cast)
and player.use_skill(enemies, slot) (a Básica in slots 2..5 attacks like the attack button). A 'tick' is the engine's
order: player.update() (resolver timers resolve the marked tile with the 0.16 strike and the legendary basic hit), each
living non-dormant enemy's update, then the hero's lifecycle steps (status.tick, resources.tick) and the 0.16 world tick.
Classes without summons (their Warlock/Necromancer rules are step 5.8d). Python's global `random` is seeded per case.
Output: tests/parity/combat_cases.json
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

import game.core.engine as core_engine  # noqa: E402
import game.sound.api as sound_api  # noqa: E402
sound_api.play_sfx = lambda *a, **k: None
import game.core.balance as balance  # noqa: E402
balance.play_sfx = lambda *a, **k: None
import pygame  # noqa: E402
CLOCK = [0]
pygame.time.get_ticks = lambda: CLOCK[0] * 1000 // 60
from game.entities.player import Player  # noqa: E402
from game.entities.enemies import Enemy  # noqa: E402
from game.skills import loadout  # noqa: E402
from game.skills.v16 import catalog  # noqa: E402
from game.combat_system import status, resources, elements  # noqa: E402
from game.combat_system.v16 import world as v16world  # noqa: E402
from game.equipment import transactions, loot  # noqa: E402
elements.EVENTS.clear()

CLASSES = ('barbaro', 'mago', 'ladino', 'paladino', 'druida')
MAP = ["############################",
       "#..........................#",
       "#..........................#",
       "#.....####.................#",
       "#.....#..........######....#",
       "#.....#..........#.........#",
       "#................#.........#",
       "#...........#..............#",
       "#...........#..............#",
       "#.....###...#.......###....#",
       "#..........................#",
       "############################"]


class Dungeon:
    def __init__(self):
        self.map = [[1 if c == '#' else 0 for c in row] for row in MAP]; self.height, self.width = len(MAP), len(MAP[0])
        self._active_enemies = []


class Engine:
    def __init__(self, player, enemies, dungeon): self.player, self.enemies, self.dungeon = player, enemies, dungeon


def pos(v): return None if v is None else [v[0], v[1]]


def rows(entity):
    data = getattr(entity, 'statuses', None) or {}
    return [[k, [[r['ticks'], float(r['magnitude'])] for r in data[k]]] for k in data]


def snap(p, enemies):
    d = p.__dict__; pending = p.pending_attack; last = p.last_combat_result; world = d.get('v16_world') or {}
    hero = {
        'x': p.x, 'y': p.y, 'health': p.health, 'resource': p.resource, 'alive': p.alive, 'shield': p.shield_points,
        'pending': None if not pending else [pos(pending['origin']), pos(pending['target']), pos(pending['direction']), pending['reach'], pending['timer'], pending['combo_index']],
        'attack_cooldown': p.attack_cooldown, 'buffered': bool(p.attack_buffered), 'buffer_direction': pos(p.attack_buffer_direction),
        'direction': pos(p.attack_direction), 'combo': [p.combo_index, p.combo_window],
        'last': None if last is None else [bool(last.hit), last.damage, pos(last.target), last.reason],
        'basic': p.basic_skill_id, 'slots': list(p.equipped_main_skills), 'active_slot': p.active_main_slot, 'weapon_skill': d.get('active_weapon_skill'),
        'cooldowns': sorted([k, v] for k, v in p.skill_cooldowns.items()), 'statuses': rows(p), 'last_skill': d.get('last_skill_id'),
        'zones': len(world.get('zones', [])), 'events': len(world.get('events', [])), 'channel': bool(world.get('channel')), 'hold': bool(d.get('v16_hold')),
        'stacks': sorted([k, c, t] for k, (c, t) in (d.get('v16_stacks') or {}).items()),
    }
    return {'hero': hero, 'enemies': [[e.x, e.y, e.health, e.alive, e.ai_state, e.attack_windup, rows(e)] for e in enemies]}


def tick(engine):
    CLOCK[0] += 1
    p, enemies, dungeon = engine.player, engine.enemies, engine.dungeon
    p._active_enemies = enemies; dungeon._active_enemies = enemies; p._combat_enemies = enemies; p._active_dungeon = dungeon
    p.update()
    px, py = p.position
    for e in enemies:
        if e.alive and not core_engine.dormant(e, px, py): e.update(p, dungeon)
    status.tick(p); resources.tick(p)
    v16world.tick(engine)


def run(op, engine):
    p = engine.player; k = op['op']
    if k == 'learn': return p.unlock_skill(op['key'])
    if k == 'equip': return list(loadout.equip(p, op['skill'], op['index']))
    if k == 'attack':
        p._active_dungeon = engine.dungeon
        return p.attack_enemy(engine.enemies)
    if k == 'skill':
        p._active_dungeon = engine.dungeon
        return p.use_skill(engine.enemies, slot=op['slot'])
    if k == 'tick':
        for _ in range(op['n']): tick(engine)
        return None
    if k == 'face': p.attack_direction = tuple(op['d']); return None
    if k == 'aim': p.aim_target = None if op['at'] is None else tuple(op['at']); return None
    if k == 'move': return p.move(op['dx'], op['dy'], engine.dungeon)
    if k == 'resource': p.resource = op['n']; return None
    if k == 'hold': p.v16_hold = op['on']; return None
    if k == 'spawn':
        e = Enemy(tuple(op['at']), op['floor'], False, op['type']); engine.enemies.append(e); return len(engine.enemies)
    if k == 'gear': return list(transactions.equip(p, op['record']))
    raise ValueError(k)


def record(name, cls, hero_at, enemies, ops, seed):
    random.seed(seed); CLOCK[0] = 0
    p = Player(tuple(hero_at), cls); dungeon = Dungeon()
    p.skill_tree.add_point(200); p.level = 40; p.recalculate_stats(); p.health = p.max_health
    engine = Engine(p, [Enemy(tuple(at), floor, False, kind) for at, floor, kind in enemies], dungeon)
    steps = []
    for op in ops:
        result = run(op, engine)
        steps.append({'op': op, 'result': result, 'state': snap(p, engine.enemies)})
    return {'name': name, 'class': cls, 'hero': list(hero_at), 'enemies': enemies, 'seed': seed, 'steps': steps}


def learn_all(cls, rng, options=True):
    ops = []
    for row in catalog.SKILLS[cls]:
        ops.append(dict(op='learn', key='learn:' + row['id']))
        for _ in range(rng.choice([0, 2, 5])): ops.append(dict(op='learn', key='learn:' + row['id']))
        if options and row['options']:
            ops.append(dict(op='learn', key=f"learn:{row['id']}:mod:{rng.choice(['power', 'reach', 'flow'])}"))
            if rng.random() < .6: ops.append(dict(op='learn', key=f"learn:{row['id']}:passive:{rng.choice([0, 1])}"))
            if rng.random() < .4: ops.append(dict(op='learn', key=f"learn:{row['id']}:passive:{rng.choice([2, 3])}"))
    for k in [k for k, n in Player((0, 0), cls).skill_tree.nodes.items() if n.get('kind') == 'class_passive']:
        ops += [dict(op='learn', key=k)] * rng.choice([0, 1, 3])
    return ops


PACK = [((8, 6), 6, 'bruto'), ((10, 5), 4, 'sombra'), ((9, 8), 5, 'cultista'), ((14, 6), 8, 'guardiao'), ((15, 2), 3, 'rastejante'), ((20, 7), 9, 'arqueiro')]


def handwritten():
    cases = []; rng = random.Random(9)
    for i, cls in enumerate(CLASSES):
        skills = [r['id'] for r in catalog.SKILLS[cls]]
        basics = [r['id'] for r in catalog.SKILLS[cls] if r['category'] == 'Básica']
        mains = [s for s in skills if s not in basics]
        ops = learn_all(cls, rng)
        ops += [dict(op='equip', skill=basics[0], index=0), dict(op='equip', skill=mains[0], index=1), dict(op='equip', skill=basics[1], index=2),
                dict(op='equip', skill=mains[1], index=3), dict(op='equip', skill=mains[-1], index=4)]
        T = lambda n: dict(op='tick', n=n)
        ops += [dict(op='attack'), T(2), dict(op='attack'), T(3), dict(op='attack'), T(10), dict(op='attack'), T(20), dict(op='attack'), T(4), dict(op='attack'), T(40)]
        ops += [dict(op='resource', n=100), dict(op='skill', slot=0), T(30), dict(op='skill', slot=1), T(5), dict(op='skill', slot=2), T(10), dict(op='skill', slot=3), T(60)]
        ops += [dict(op='move', dx=1, dy=0), dict(op='attack'), dict(op='move', dx=0, dy=1), T(30), dict(op='face', d=[-1, 0]), dict(op='attack'), T(30)]
        ops += [dict(op='equip', skill=mains[2], index=0), dict(op='attack'), T(40), dict(op='attack'), T(200), dict(op='aim', at=[9, 8]), dict(op='equip', skill=basics[1], index=0)]
        for _ in range(12): ops += [dict(op='attack'), T(rng.choice([1, 6, 15, 25]))]
        ops += [dict(op='hold', on=True), dict(op='resource', n=100)] + [dict(op='skill', slot=s) for s in (0, 1, 2, 3)] + [T(120), dict(op='hold', on=False), T(400)]
        cases.append(record(f'{cls}: ataque básico, combo, buffer, espaços, habilidades em combate', cls, (6, 6), PACK, ops, 8100 + i))
    for i, (cls, aspect) in enumerate((('mago', 'frost_ward'), ('druida', 'living_roots'), ('barbaro', 'quake_echo'), ('paladino', 'echo'), ('ladino', 'focus'))):
        random.seed(8300 + i)
        chest = loot.roll('armor', 10, 10, 'Raro', cls); chest['slot'] = 'chest'; chest['aspect'] = aspect; chest['required_level'] = 1
        siphon = loot.roll('accessory', 10, 10, 'Raro', cls); siphon['slot'] = 'amulet'; siphon['aspect'] = 'siphon'; siphon['required_level'] = 1
        skills = [r['id'] for r in catalog.SKILLS[cls]]
        ops = learn_all(cls, rng, options=False) + [dict(op='gear', record=chest), dict(op='gear', record=siphon)]
        ops += [dict(op='equip', skill=s, index=n) for n, s in enumerate(skills[:5])]
        for _ in range(10):
            ops += [dict(op='resource', n=100), dict(op='attack'), dict(op='tick', n=8)] + [dict(op='skill', slot=s) for s in (0, 1, 2, 3)] + [dict(op='tick', n=50)]
        cases.append(record(f'{cls}: aspectos lendários no golpe básico e ao conjurar ({aspect})', cls, (6, 6), PACK, ops, 8300 + i))
    return cases


def randomized(count, length, seed):
    rng = random.Random(seed); cases = []
    kinds = ['sombra', 'bruto', 'rastejante', 'cultista', 'espectro', 'arqueiro', 'parasita', 'guardiao', 'bruxa', 'espreitador']
    cells = [(x, y) for y in range(len(MAP)) for x in range(len(MAP[0])) if MAP[y][x] == '.']
    for c in range(count):
        cls = CLASSES[c % len(CLASSES)]
        skills = [r['id'] for r in catalog.SKILLS[cls]]
        hero_at = rng.choice(cells)
        enemies = [(list(rng.choice([q for q in cells if abs(q[0] - hero_at[0]) + abs(q[1] - hero_at[1]) <= 7])), rng.randint(1, 15), rng.choice(kinds))
                   for _ in range(rng.randint(2, 6))]
        enemies = [e for e in enemies if tuple(e[0]) != hero_at]
        ops = learn_all(cls, rng, options=rng.random() < .8)
        order = skills[:]; rng.shuffle(order)
        ops += [dict(op='equip', skill=s, index=i) for i, s in enumerate(order[:5])]
        for _ in range(length):
            r = rng.random()
            if r < .28: ops.append(dict(op='attack'))
            elif r < .45: ops.append(dict(op='skill', slot=rng.randint(0, 3)))
            elif r < .75: ops.append(dict(op='tick', n=rng.choice([1, 2, 4, 6, 15, 30, 60])))
            elif r < .82: ops.append(dict(op='move', dx=rng.choice([-1, 0, 1]), dy=rng.choice([-1, 0, 1])))
            elif r < .86: ops.append(dict(op='face', d=list(rng.choice([(1, 0), (0, 1), (-1, 0), (0, -1)]))))
            elif r < .89: ops.append(dict(op='aim', at=rng.choice([None, list(rng.choice(cells))])))
            elif r < .93: ops.append(dict(op='resource', n=rng.choice([0, 30, 100])))
            elif r < .96: ops.append(dict(op='hold', on=rng.random() < .6))
            elif r < .98: ops.append(dict(op='equip', skill=rng.choice(skills), index=rng.randint(0, 4)))
            else: ops.append(dict(op='spawn', at=list(rng.choice(cells)), floor=rng.randint(1, 10), type=rng.choice(kinds)))
        cases.append(record(f'aleatório {seed}-{c} ({cls})', cls, hero_at, enemies, ops, seed * 100 + c))
    return cases


def main():
    cases = handwritten() + randomized(95, 70, 2034)
    out = HERE / 'tests' / 'parity' / 'combat_cases.json'
    payload = {'source': 'umbra-ashes-below resolver + skills.use_skill + loadout + composed Player/Enemy', 'map': MAP, 'cases': cases}
    out.write_bytes(json.dumps(payload, ensure_ascii=False, separators=(',', ':')).encode('utf-8'))
    print(f'{len(cases)} casos, {sum(len(c["steps"]) for c in cases)} operações -> {out} ({out.stat().st_size // 1024} KB)')


if __name__ == '__main__':
    main()
