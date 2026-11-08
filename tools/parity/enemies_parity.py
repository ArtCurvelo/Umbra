r"""Parity cases for enemies, their AI, elites and bosses (step 5.7).

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\parity\enemies_parity.py

REAL enemies of UMBRA's composed engine (`Enemy(position, floor, boss, enemy_type, summoned)` with every layer: the
shared-states layer of necro_status, the sanctuary guards and the sound/feedback layers) run against a fixture hero
on fixture maps. A 'tick' is the engine's enemy step: dungeon._active_enemies = enemies, then every living enemy that
is not dormant (engine.dormant) gets enemy.update(hero, dungeon). After every operation the full state of every enemy
(AI state, timers, attack, memory, statuses, boss controller with phase, cooldowns, pending ability, bone armor,
corruption zones) and of the hero is recorded. Python's global `random` is seeded per case.

The hero is a fixture (its own rules are step 5.8): take_damage subtracts (never below 0), gain_resource adds.
The maps have no city, so the sanctuary layers do nothing here (cities come with the world, 5.9); no necromancer
structures exist (5.8). Output: tests/parity/enemies_cases.json
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
from game.entities.enemies import Enemy, ENEMY_TYPES  # noqa: E402
from game.combat_system import status, elements, elites  # noqa: E402
elements.EVENTS.clear()

MAPS = {
    'sala': ["##########################",
             "#........................#",
             "#........................#",
             "#.....####...............#",
             "#.....#..........#####...#",
             "#.....#..........#.......#",
             "#................#.......#",
             "#...........#............#",
             "#...........#............#",
             "#.....###...#.....###....#",
             "#........................#",
             "#........................#",
             "##########################"],
    'campo': ["#" * 42] + ["#" + "." * 40 + "#" for _ in range(9)] + ["#" * 42],
    'corredor': ["##############################",
                 "#.......#......#.............#",
                 "#.......#......#.............#",
                 "#..............#......#......#",
                 "#.......#.............#......#",
                 "######.###############.#######",
                 "#............................#",
                 "#.....#######......######....#",
                 "#............................#",
                 "##############################"],
}


class Dungeon:
    def __init__(self, rows):
        self.map = [[1 if c == '#' else 0 for c in row] for row in rows]
        self.height, self.width = len(rows), len(rows[0])


class Hero:
    def __init__(self, x, y):
        self.name = 'heroi'; self.x, self.y = x, y; self.health = self.max_health = 2000; self.boss = False
        self.resource = 0; self.ward_blocked = 0

    @property
    def alive(self): return self.health > 0
    @property
    def position(self): return (self.x, self.y)
    def take_damage(self, amount): self.health = max(0, self.health - int(amount))
    def gain_resource(self, amount, source='combat'): self.resource += int(round(amount)); return int(round(amount))


def rows(entity):
    data = getattr(entity, 'statuses', None) or {}
    return [[k, [[r['ticks'], float(r['magnitude']), r.get('timer'), float(r.get('carry', 0.)), getattr(r.get('source'), 'name', None)] for r in data[k]]] for k in data]


def pos(v): return None if v is None else [v[0], v[1]]


def enemy_state(e):
    d = e.__dict__
    row = {
        'name': e.name, 'type': e.enemy_type, 'x': e.x, 'y': e.y, 'health': e.health, 'max_health': e.max_health, 'attack': e.attack,
        'defense': e.defense, 'alive': e.alive, 'ai': e.ai_state, 'move_cd': e.move_cooldown, 'attack_cd': e.attack_cooldown,
        'windup': e.attack_windup, 'current': e.current_attack, 'target': pos(e.attack_target_position), 'direction': pos(e.attack_direction),
        'origin': pos(e.attack_origin_position), 'known': pos(e.last_known_player_position), 'memory': e.memory_frames,
        'stagger': e.stagger_frames, 'stuck': d.get('stuck_frames', 0), 'previous': pos(e.previous_position), 'base_move': e.base_move_cooldown,
        'statuses': rows(e), 'pressure': [int(d.get('cc_pressure', 0)), int(d.get('cc_pressure_timer', 0))],
        'necro': [int(d.get(k, 0)) for k in ('necro_slow_ticks', 'necro_frozen_ticks', 'necro_vulnerable_ticks', 'necro_prison_ticks')],
        'rank': elites.rank(e), 'last_kind': e.last_attack_kind, 'summon': e.summon_requested, 'absorbed': float(d.get('absorbed_total', 0)),
    }
    b = e.boss_controller
    if b:
        row['boss'] = {
            'phase': b.phase, 'triggered': b.phase_two_triggered, 'cooldowns': [[k, v] for k, v in b.ability_cooldowns.items()],
            'action': b.action_timer, 'message': b.message, 'message_timer': b.message_timer, 'pending': b.pending_ability,
            'pending_timer': b.pending_ability_timer, 'pending_total': b.pending_ability_total, 'pending_target': pos(b.pending_ability_target),
            'pending_direction': pos(b.pending_ability_direction), 'last': b.last_ability, 'basic': [b.basic_windup_timer, b.basic_windup_total, pos(b.basic_windup_target)],
            'last_basic_target': pos(b.last_basic_attack_target), 'bone': b.bone_armor, 'zones': [[pos(z['position']), z['timer']] for z in b.corruption_zones],
            'summon': b.summon_requested, 'teleport': b.teleport_requested,
        }
    return row


def hero_state(h):
    return {'x': h.x, 'y': h.y, 'health': h.health, 'statuses': rows(h), 'ward_blocked': h.ward_blocked, 'resource': h.resource}


def tick(world):
    hero, enemies, dungeon = world['hero'], world['enemies'], world['dungeon']
    dungeon._active_enemies = enemies
    px, py = hero.position
    for enemy in enemies:
        if enemy.alive and not core_engine.dormant(enemy, px, py):
            enemy.update(hero, dungeon)


def run(op, world):
    k = op['op']; hero = world['hero']; enemies = world['enemies']
    if k == 'spawn':
        e = Enemy(tuple(op['at']), op['floor'], op.get('boss', False), op.get('type'), op.get('summoned', False))
        if op.get('elite'): elites.promote(e)
        enemies.append(e); return len(enemies)
    if k == 'tick':
        for _ in range(op['n']): tick(world)
        return None
    if k == 'move': hero.x, hero.y = op['at']; return None
    if k == 'hit': return enemies[op['i'] % len(enemies)].take_damage(op['amount'])
    if k == 'state': return status.apply(enemies[op['i'] % len(enemies)], op['kind'], op['ticks'], op['mag'], hero)
    if k == 'hero_state': return status.apply(hero, op['kind'], op['ticks'], op['mag'])
    if k == 'necro':
        e = enemies[op['i'] % len(enemies)]; setattr(e, op['field'], op['ticks']); return None
    if k == 'heal_hero': hero.health = op['value']; return None
    if k == 'elites':
        rng = random.Random(op['seed']); return elites.roll(enemies, rng)
    if k == 'rewards':
        e = enemies[op['i'] % len(enemies)]; return [elites.xp(e, op['xp']), elites.gold(e, op['gold'])]
    raise ValueError(k)


def record(name, layout, hero_at, ops, seed):
    random.seed(seed)
    world = {'hero': Hero(*hero_at), 'enemies': [], 'dungeon': Dungeon(MAPS[layout])}
    steps = []
    for op in ops:
        result = run(op, world)
        steps.append({'op': op, 'result': result, 'hero': hero_state(world['hero']), 'enemies': [enemy_state(e) for e in world['enemies']]})
    return {'name': name, 'map': layout, 'hero': list(hero_at), 'seed': seed, 'steps': steps}


def floor_cells(layout):
    d = Dungeon(MAPS[layout]); return [(x, y) for y in range(d.height) for x in range(d.width) if d.map[y][x] == 0]


def handwritten():
    cases = []
    T = lambda n: dict(op='tick', n=n)
    for i, kind in enumerate(ENEMY_TYPES):
        ops = [dict(op='spawn', at=[14, 6], floor=1 + 4 * i, type=kind), dict(op='spawn', at=[3, 10], floor=7, type=kind, summoned=True), T(1)]
        ops += [T(25), dict(op='move', at=[12, 6]), T(40), dict(op='move', at=[13, 7]), T(90), dict(op='hit', i=0, amount=9), T(30),
                dict(op='move', at=[20, 2]), T(200), dict(op='move', at=[2, 2]), T(250), dict(op='rewards', i=0, xp=40, gold=12)]
        cases.append(record(f'{kind}: percepção, perseguição, ataque, recuo, memória', 'sala', (10, 6), ops, 6000 + i))
    for floor in (5, 10, 15, 20, 25, 30, 35, 40, 45, 65):
        ops = [dict(op='spawn', at=[16, 6], floor=floor, boss=True), dict(op='spawn', at=[18, 8], floor=floor, type='sombra')]
        for _ in range(14):
            ops += [T(37), dict(op='move', at=[12 + (floor + _) % 7, 3 + (_ * 3) % 8]), T(23)]
        ops += [dict(op='hit', i=0, amount=99999), T(60), dict(op='hit', i=0, amount=99999), dict(op='hit', i=0, amount=99999), T(200),
                dict(op='hit', i=0, amount=99999), dict(op='hit', i=0, amount=99999), T(400)]
        cases.append(record(f'chefe do andar {floor}: fases, habilidades, armadura, zonas', 'sala', (12, 6), ops, 6100 + floor))
    cases.append(record('estados: atordoar, congelar, prender, lentidão, provocação, cegueira, medo, entorpecimento, furtividade', 'sala', (10, 6), [
        dict(op='spawn', at=[13, 6], floor=3, type='bruto'), dict(op='spawn', at=[15, 8], floor=3, type='arqueiro'), dict(op='spawn', at=[8, 9], floor=3, type='cultista'),
        dict(op='spawn', at=[16, 6], floor=10, boss=True), T(10),
        dict(op='state', i=0, kind='stun', ticks=60, mag=0), T(30), dict(op='state', i=0, kind='slow', ticks=300, mag=.5), T(60),
        dict(op='state', i=1, kind='root', ticks=120, mag=0), T(60), dict(op='state', i=2, kind='fear', ticks=120, mag=0), T(70),
        dict(op='state', i=1, kind='blind', ticks=100, mag=0), T(60), dict(op='state', i=1, kind='numb', ticks=200, mag=0), T(80),
        dict(op='state', i=0, kind='taunt', ticks=200, mag=0), T(40), dict(op='state', i=2, kind='freeze', ticks=90, mag=0), T(40),
        dict(op='state', i=3, kind='numb', ticks=300, mag=0), dict(op='state', i=3, kind='stun', ticks=200, mag=0), T(120),
        dict(op='necro', i=0, field='necro_frozen_ticks', ticks=50), dict(op='necro', i=1, field='necro_slow_ticks', ticks=200),
        dict(op='necro', i=2, field='necro_prison_ticks', ticks=40), T(90), dict(op='hero_state', kind='stealth', ticks=200, mag=1), T(100),
        dict(op='hero_state', kind='ward', ticks=400, mag=.5), T(200), dict(op='state', i=0, kind='burn', ticks=300, mag=4), T(200)], 6200))
    cases.append(record('elites e recompensas', 'corredor', (3, 6), [dict(op='spawn', at=list(c), floor=12, type='sombra') for c in ((10, 6), (14, 2), (20, 8), (26, 3))]
                        + [dict(op='elites', seed=s) for s in range(30)] + [dict(op='rewards', i=i, xp=33, gold=17) for i in range(4)] + [T(200)], 6300))
    # dormancy boundary: the enemy stops at the last known position exactly 30 tiles from the hero, idle, with a step cooldown
    cases.append(record('dormência: inimigo parado a exatamente 30 e a 31 casas', 'campo', (5, 5), [
        dict(op='spawn', at=[9, 5], floor=1, type='sombra'), dict(op='spawn', at=[9, 7], floor=1, type='bruto'), T(1),
        dict(op='move', at=[35, 5]), T(130), T(270), dict(op='move', at=[36, 5]), T(60), dict(op='move', at=[34, 5]), T(60)], 6400))
    # phase II lands at half life: hits that stop at 53% (Guardião: 450 life, defense 5, cap 99 per hit)
    cases.append(record('fase II exatamente na metade da vida', 'sala', (12, 6), [
        dict(op='spawn', at=[18, 6], floor=5, boss=True), dict(op='hit', i=0, amount=200), dict(op='hit', i=0, amount=200),
        dict(op='hit', i=0, amount=19), T(5), dict(op='hit', i=0, amount=8), T(5), dict(op='hit', i=0, amount=30), T(60)], 6401))
    return cases


def randomized(count, length, seed):
    rng = random.Random(seed); cases = []
    kinds = list(ENEMY_TYPES)
    for c in range(count):
        layout = rng.choice(list(MAPS)); cells = floor_cells(layout)
        hero_at = rng.choice(cells); ops = []
        for _ in range(rng.randint(3, 7)):
            if rng.random() < .15: ops.append(dict(op='spawn', at=list(rng.choice(cells)), floor=rng.choice([5, 10, 15, 25, 40]), boss=True))
            else: ops.append(dict(op='spawn', at=list(rng.choice(cells)), floor=rng.randint(1, 40), type=rng.choice(kinds), summoned=rng.random() < .15, elite=rng.random() < .1))
        for _ in range(length):
            r = rng.random()
            if r < .45: ops.append(dict(op='tick', n=rng.choice([1, 5, 17, 30, 45, 90])))
            elif r < .65: ops.append(dict(op='move', at=list(rng.choice(cells))))
            elif r < .75: ops.append(dict(op='hit', i=rng.randint(0, 9), amount=rng.choice([1, 8, 30, 150, 600])))
            elif r < .85: ops.append(dict(op='state', i=rng.randint(0, 9), kind=rng.choice(['stun', 'slow', 'root', 'fear', 'blind', 'numb', 'taunt', 'freeze', 'frost', 'burn', 'poison']),
                                          ticks=rng.choice([30, 90, 240]), mag=rng.choice([0, .3, .6, 3])))
            elif r < .89: ops.append(dict(op='hero_state', kind=rng.choice(['ward', 'stealth']), ticks=rng.choice([60, 240]), mag=.4))
            elif r < .93: ops.append(dict(op='necro', i=rng.randint(0, 9), field=rng.choice(['necro_slow_ticks', 'necro_frozen_ticks', 'necro_vulnerable_ticks', 'necro_prison_ticks']), ticks=rng.choice([30, 120])))
            elif r < .96: ops.append(dict(op='heal_hero', value=rng.choice([2000, 500, 0])))
            else: ops.append(dict(op='spawn', at=list(rng.choice(cells)), floor=rng.randint(1, 40), type=rng.choice(kinds)))
        cases.append(record(f'aleatório {seed}-{c}', layout, hero_at, ops, seed * 100 + c))
    return cases


def main():
    cases = handwritten() + randomized(110, 40, 2032)
    out = HERE / 'tests' / 'parity' / 'enemies_cases.json'
    payload = {'source': 'umbra-ashes-below entities/enemies + combat_system/ai, bosses, elites on the composed engine', 'maps': MAPS, 'cases': cases}
    out.write_bytes(json.dumps(payload, ensure_ascii=False, separators=(',', ':')).encode('utf-8'))
    print(f'{len(cases)} casos, {sum(len(c["steps"]) for c in cases)} operações -> {out} ({out.stat().st_size // 1024} KB)')


if __name__ == '__main__':
    main()
