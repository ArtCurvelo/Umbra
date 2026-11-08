r"""Parity cases for the 0.16 skill engine (UMBRA game/combat_system/v16 + game/skills/v16/tuning.py), step 5.4.

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\parity\skills_parity.py

Operation sequences (casts with any rank and options, basic strikes, world ticks, moves, holding a channel...) run
through the REAL Python engine and record, after every operation, its return value, the resolved skill, the full state
of the hero (life, resource, position, states, cooldowns, charges, v16 bookkeeping, zones/events/effects/auras/channel)
and of every enemy. tests/Umbra.Domain.Tests/ParitySkillsTests.cs replays them on the C# port.

Coverage: every skill of the five classes without summons (Bárbaro, Mago, Ladino, Paladino, Druida). Class-specific
systems that are not ported yet are hooks in C#; here they are WRAPPED and every call must return the neutral value
(and leave the hero untouched) with this fixture, so the cases measure the engine alone:
class passives (5.5), tags, Grimório and item affixes (5.6), Warlock and Necromancer rules (5.8).
Python's global `random` is seeded per case; C# replays it with a port of Python's Mersenne Twister.
Fixture rules shared by both sides: the hero's attack damage is `attack` (x1.1 for skills), spend/gain resource are
plain arithmetic, and a 'tick' op runs world.tick, then status.tick on every entity, then cooldowns -1.
Output: tests/parity/skills_cases.json
"""
import copy
import json
import math
import os
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parents[2]
UMBRA = HERE.parent / 'umbra-ashes-below'
os.environ.setdefault('SDL_VIDEODRIVER', 'dummy'); os.environ.setdefault('SDL_AUDIODRIVER', 'dummy')
os.environ.setdefault('UMBRA_DATA_HOME', str(HERE / '.export-home'))
sys.path.insert(0, str(UMBRA))

from game.combat_system import status, elements, necro3, warlock               # noqa: E402
from game.combat_system.v16 import engine as v16, world as v16world             # noqa: E402
from game.skills import catalog as old_catalog, tags, grimoire                  # noqa: E402
from game.skills.v16 import catalog, tuning, passives                           # noqa: E402
from game.equipment import necro_affixes                                        # noqa: E402
import game.sound.api as sound_api                                              # noqa: E402

sound_api.play_sfx = lambda *a, **k: None
import pygame  # noqa: E402
pygame.time.get_ticks = lambda: CLOCK[0] * 1000 // 60          # Convergência Arcana reads the clock: one tick = 1/60 s
elements.EVENTS.clear()
old_catalog.V16_ACTIVE = True                       # the game turns it on at bootstrap (activate_v16)
CLASSES = ('barbaro', 'mago', 'ladino', 'paladino', 'druida')
ROWS = {s['id']: s for c in CLASSES for s in catalog.SKILLS[c]}


# ------------------------------------------------------------------ neutral hooks (asserted)
PASSIVES_ON = [False]
CLOCK = [0]


class HookUsed(AssertionError):
    pass


HERO = []


def light(entity):
    d = entity.__dict__
    return repr(sorted((k, repr(v)) for k, v in d.items() if k not in ('skill_tree',)))


def neutral(module, name, expected=None, check=None, void=False):
    original = getattr(module, name)

    def wrapper(*args, **kwargs):
        if module is passives and PASSIVES_ON[0]: return original(*args, **kwargs)     # 5.5: real class passives
        before = light(HERO[0]) if HERO else None
        result = original(*args, **kwargs)
        if void and HERO and light(HERO[0]) != before: raise HookUsed(f'{module.__name__}.{name} changed the hero')
        if check is not None:
            if not check(result, *args): raise HookUsed(f'{module.__name__}.{name} -> {result!r}')
        elif not void and result != expected: raise HookUsed(f'{module.__name__}.{name} -> {result!r}')
        return result
    setattr(module, name, wrapper)


for n in ('damage_factor', 'duration_factor', 'dot_factor', 'heal_factor', 'barrier_factor', 'health_cost_factor', 'incoming_factor'):
    neutral(passives, n, 1.)
neutral(passives, 'value', 0.)
neutral(passives, 'chain_bonus', (0, 0.))
for n in ('on_hit', 'on_cast', 'on_kills', 'on_dodge', 'tick'): neutral(passives, n, void=True)
neutral(tags, 'damage_factor', 1.)
neutral(grimoire, 'bonus', 0.)
neutral(necro_affixes, 'share', 0.)
neutral(warlock, 'damage_factor', 1.)
neutral(warlock, 'allowed', True)
for n in ('broke', 'on_hit', 'on_cast', 'on_kills'): neutral(warlock, n, void=True)
neutral(necro3, 'damage_factor', 1.)
neutral(necro3, 'before_cast', True)
neutral(necro3, 'condition', None)
neutral(necro3, 'zone_targets', check=lambda result, zone, inside: result == inside)
for n in ('after_cast', 'on_hit', 'on_kills', 'zone_tick', 'zone_end'): neutral(necro3, n, void=True)
neutral(necro3, 'act', check=lambda *a: False)


# ------------------------------------------------------------------ fixture
W, H = 26, 20
WALLS = {(x, y) for x in range(W) for y in range(H) if x in (0, W - 1) or y in (0, H - 1)}
WALLS |= {(12, y) for y in range(3, 8)} | {(x, 13) for x in range(4, 9)} | {(15, 11), (16, 11), (16, 12)}
CITY = (20, 14, 25, 19)                              # sanctuary: no combat inside


class Dungeon:
    def __init__(self):
        self.width, self.height = W, H
        self.map = [[1 if (x, y) in WALLS else 0 for x in range(W)] for y in range(H)]
        self.city_zones = [CITY]


class Tree:
    def __init__(self): self.unlocked, self.ranks, self.nodes = set(), {}, {}
    def has(self, key): return key in self.unlocked
    def get_effect(self, name): return 0.


class Hero:
    def __init__(self, x, y, shield):
        self.name = 'heroi'; self.health = self.max_health = 500; self.boss = False
        self.x, self.y = x, y; self.resource, self.max_resource = 60, 100; self.attack = 40
        self.attack_direction = (1, 0); self.aim_target = None
        self.skill_cooldowns = {}; self.skill_cooldown = 0
        self.player_class = 'barbaro'; self.skill_tree = Tree()
        self.dash_cooldown = 0; self.ward_blocked = 0; self.active_main_slot = 0
        self.loadout = {'shield': {'name': 'Escudo'}} if shield else {}
        self.recalcs = 0

    @property
    def alive(self): return self.health > 0
    @property
    def position(self): return (self.x, self.y)
    def take_damage(self, amount): self.health = max(0, self.health - int(amount))

    def spend_resource(self, amount):
        amount = max(0, int(amount))
        if self.resource < amount: return False
        self.resource -= amount; return True

    def gain_resource(self, amount, source='combat'):
        gained = int(round(amount)); self.resource = min(self.max_resource, self.resource + gained); return gained

    def get_attack_damage(self, enemy=None, skill=False): return float(self.attack) * (1.1 if skill else 1.)
    def recalculate_stats(self): self.recalcs += 1


class Enemy:
    def __init__(self, name, x, y, health, boss=False, facing=(0, 0), enemy_type=None):
        self.name = name; self.x, self.y = x, y; self.health = self.max_health = health; self.boss = boss
        self.attack_direction = facing
        if enemy_type: self.enemy_type = enemy_type

    @property
    def alive(self): return self.health > 0
    @property
    def position(self): return (self.x, self.y)
    def take_damage(self, amount): self.health = max(0, self.health - int(amount))


ENEMIES = [('lobo', 9, 8, 180, False, (-1, 0), None), ('espectro', 11, 9, 260, False, (0, 1), 'espectro'),
           ('bruto', 7, 10, 320, False, (1, -1), None), ('chefe', 14, 8, 1500, True, (-1, 0), None),
           ('sombra', 10, 5, 140, False, (0, 0), 'sombra'), ('parasita', 6, 7, 120, False, (1, 0), 'parasita'),
           ('longe', 21, 15, 200, False, (0, 0), None)]          # 'longe' stands in the sanctuary


class Engine:
    def __init__(self, shield):
        self.dungeon = Dungeon(); self.player = Hero(8, 8, shield)
        self.enemies = [Enemy(*row) for row in ENEMIES]


# ------------------------------------------------------------------ state
def rows_of(entity):
    rows = getattr(entity, 'statuses', None) or {}
    return [[k, [[r['ticks'], float(r['magnitude']), r.get('timer'), float(r.get('carry', 0.)), getattr(r.get('source'), 'name', None)]
                 for r in rows[k]]] for k in rows]


def pos(value): return None if value is None else [value[0], value[1]]


def hero_state(p):
    d = p.__dict__; world = d.get('v16_world')
    imbue = d.get('v16_imbue'); ramp = d.get('v16_ramp')
    state = {
        'health': p.health, 'resource': p.resource, 'x': p.x, 'y': p.y, 'statuses': rows_of(p),
        'cooldowns': sorted([k, v] for k, v in p.skill_cooldowns.items()), 'cooldown': p.skill_cooldown,
        'charges': sorted([k, v] for k, v in d.get('skill_charges', {}).items()),
        'stacks': sorted([k, c, t] for k, (c, t) in (d.get('v16_stacks') or {}).items()),
        'return': None if not d.get('v16_return') else [pos(d['v16_return'][0]), d['v16_return'][1]],
        'imbue': None if not imbue else [imbue[0]['id'], imbue[1]],
        'ramp': None if ramp is None else [next((e.name for e in ENGINE[0].enemies if id(e) == ramp[0]), None), ramp[1]],
        'thorns': getattr(p, 'thorns_element', None), 'ward_gain': float(d.get('ward_gain', 0)),
        'absorbed': float(getattr(p, 'absorbed_total', 0)), 'last': d.get('last_skill_id'), 'anim': d.get('last_skill_anim'),
        'targets': [pos(t) for t in d.get('skill_target_positions', [])], 'direction': pos(d.get('skill_direction')),
        'hold': bool(d.get('v16_hold', False)), 'recalcs': p.recalcs, 'last_dash': d.get('v16_last_dash', 0),
        'pbuffs': [[k, r[0], float(r[1])] for k, r in (d.get('v16_pbuffs') or {}).items()],
        'streak': [d.get('v16_streak', 0), d.get('v16_streak_ticks', 0)], 'family': d.get('v16_last_family'),
        'elements': [[e, t] for e, t in d.get('v16_elements', [])], 'bastion': d.get('v16_bastion_ticks', 0),
    }
    if world:
        state['world'] = {
            'zones': [[z['skill_id'], pos(z['center']), z['radius'], z['ticks'], z['timer'], z['shot_timer'], z['interval'], len(z['touched']),
                       float(z['dps']), z['element']] for z in world['zones']],
            'events': [[e['kind'], e['ticks'], pos(e.get('center')) or pos(e.get('origin'))] for e in world['events']],
            'effects': [[e['ctx'].spec['id'], e['ticks'], float(e['absorbed'])] for e in world['effects']],
            'auras': [[k, r['timer'], r['upkeep_timer']] for k, r in world['auras'].items()],
            'channel': None if not world.get('channel') else [world['channel']['ctx'].spec['id'], world['channel']['timer'],
                                                              world['channel']['upkeep_timer'], world['channel']['action']],
            'charges': sorted([k, m, c] for k, (m, c) in world['charges'].items()),
            'trail_timer': world.get('trail_timer', 0),
        }
    return state


def enemy_state(e):
    return {'health': e.health, 'x': e.x, 'y': e.y, 'statuses': rows_of(e), 'dead': bool(e.__dict__.get('v16_dead', False)),
            'pressure': int(getattr(e, 'cc_pressure', 0)), 'absorbed': float(getattr(e, 'absorbed_total', 0))}


def plain(value):
    if isinstance(value, dict): return {str(k): plain(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)): return [plain(v) for v in value]
    return value


# ------------------------------------------------------------------ operations
ENGINE = []
SPECS = {}


def resolve(p, op):
    row = ROWS[op['skill']]; tree = p.skill_tree
    keys = tuning.option_keys(row['id'])
    base_unlocked, base_ranks = set(tree.unlocked), dict(tree.ranks)
    tree.unlocked = base_unlocked | {'learn:' + row['id']} | {keys[i] for i in op['picks']}
    tree.ranks = {**base_ranks, 'learn:' + row['id']: op['rank']}
    tree.nodes = {k: {'kind': 'option'} for k in keys}
    spec = tuning.resolved(p, row)
    tree.unlocked, tree.ranks, tree.nodes = base_unlocked, base_ranks, {}
    return spec


def tick(engine):
    CLOCK[0] += 1
    v16world.tick(engine)
    for e in [engine.player] + engine.enemies: status.tick(e)
    p = engine.player
    for k in list(p.skill_cooldowns): p.skill_cooldowns[k] = max(0, p.skill_cooldowns[k] - 1)


def run(op, engine):
    p = engine.player; kind = op['op']
    if kind == 'cast':
        spec = resolve(p, op); frozen = plain(copy.deepcopy(spec))
        return v16.cast(p, spec, engine.enemies, engine.dungeon), frozen
    if kind == 'strike':
        spec = resolve(p, op); frozen = plain(copy.deepcopy(spec))
        target = next(e for e in engine.enemies if e.name == op['target'])
        return v16.strike(p, spec, target, engine.enemies, engine.dungeon, op['factor']), frozen
    if kind == 'tick':
        for _ in range(op['n']): tick(engine)
        return None, None
    if kind == 'hold': p.v16_hold = op['on']; return None, None
    if kind == 'move':
        who = p if op['who'] == 'heroi' else next(e for e in engine.enemies if e.name == op['who'])
        who.x, who.y = op['x'], op['y']; return None, None
    if kind == 'aim': p.aim_target = None if op['at'] is None else tuple(op['at']); return None, None
    if kind == 'face': p.attack_direction = tuple(op['d']); return None, None
    if kind == 'hurt': p.health = max(1, p.health - op['amount']); return None, None
    if kind == 'resource': p.resource = op['value']; return None, None
    if kind == 'enemy_state':
        target = next(e for e in engine.enemies if e.name == op['target'])
        return status.apply(target, op['kind'], op['ticks'], op['mag'], p), None
    if kind == 'dodge': p.dash_cooldown = op['value']; return None, None
    raise ValueError(kind)


def record(name, ops, seed, shield=False, cls=None, ranks=None):
    random.seed(seed); CLOCK[0] = 0; PASSIVES_ON[0] = ranks is not None
    engine = Engine(shield); ENGINE[:] = [engine]; HERO[:] = [engine.player]
    if ranks is not None:
        p = engine.player; p.player_class = cls
        p.skill_tree.unlocked = {'class:' + k for k, v in ranks.items() if v}; p.skill_tree.ranks = {'class:' + k: v for k, v in ranks.items() if v}
    steps = []
    for op in ops:
        try:
            result, spec = run(op, engine)
        except HookUsed:
            raise
        except Exception as error:                          # the Python engine itself fails: C# must fail here too
            steps.append({'op': op, 'error': type(error).__name__}); break
        if isinstance(result, float) and not math.isfinite(result): raise ValueError('non-finite')
        step = {'op': op, 'result': result, 'hero': hero_state(engine.player), 'enemies': {e.name: enemy_state(e) for e in engine.enemies}}
        if spec is not None:                                 # resolved skills are stored once (the file stays small)
            key = f"{op['skill']}|{op['rank']}|{','.join(map(str, sorted(op['picks'])))}"
            if SPECS.setdefault(key, spec) != spec: raise ValueError('same options, different resolution: ' + key)
            step['spec'] = key
        steps.append(step)
    PASSIVES_ON[0] = False
    return {'name': name, 'seed': seed, 'shield': shield, 'class': cls, 'ranks': ranks, 'steps': steps}


# ------------------------------------------------------------------ cases
def C(skill, rank=1, picks=()):
    return dict(op='cast', skill=skill, rank=rank, picks=list(picks))


def T(n): return dict(op='tick', n=n)


def handwritten():
    cases = []
    for cls in CLASSES:
        for row in catalog.SKILLS[cls]:
            sid = row['id']; ops = []
            if row['shape'] == 'melee':
                for picks in ([], [0], [1], [2], [0, 3, 5], [2, 4, 6]):
                    ops += [dict(op='strike', skill=sid, rank=3, picks=picks, target='lobo', factor=1.), T(31)]
                ops += [dict(op='strike', skill=sid, rank=1, picks=[1, 3], target='espectro', factor=.8), T(200)]
            else:
                for i, picks in enumerate(([], [0], [1], [2], [0, 3], [0, 4], [1, 5], [2, 6], [0, 3, 6], [1, 4, 5])):
                    ops += [dict(op='resource', value=100), C(sid, 1 + i % 6, picks), dict(op='hold', on=True), T(37),
                            dict(op='hold', on=i % 2 == 0), T(70 + 13 * i)]
                ops += [T(600)]
            cases.append(record(f'{cls}: {row["name"]} ({sid})', ops, 7000 + len(cases), shield=cls == 'paladino'))
    cases.append(record('sem alvo não gasta nada; santuário protege; recurso insuficiente', [
        dict(op='move', who='heroi', x=21, y=16), C('barb2_ruptura'), C('mage2_bola_fogo'),
        dict(op='move', who='heroi', x=3, y=3), C('mage2_bola_fogo'), dict(op='resource', value=0), dict(op='move', who='heroi', x=8, y=8),
        C('mage2_bola_fogo'), dict(op='resource', value=100), C('mage2_bola_fogo'), C('mage2_bola_fogo')], 9001))
    cases.append(record('mira, direção, paredes e linha de visão', [
        dict(op='aim', at=[10, 5]), C('mage2_bola_fogo'), dict(op='aim', at=None), dict(op='face', d=[0, -1]),
        dict(op='move', who='heroi', x=13, y=5), C('mage2_bola_fogo'), T(30), dict(op='move', who='heroi', x=11, y=5), C('barb2_martelo'), T(300)], 9002))
    M = lambda who, x, y: dict(op='move', who=who, x=x, y=y)
    # aimed at lobo (east); espectro (cos .45) and sombra (cos .45) are inside the 0.3 cone, so they get their own bolt;
    # the bolt aimed at espectro flies diagonally and hits bruto on the way
    fan = [M('lobo', 11, 8), M('espectro', 9, 10), M('bruto', 10, 10), M('sombra', 10, 4), M('parasita', 3, 16), M('chefe', 14, 8),
           dict(op='aim', at=[11, 8])]
    cases.append(record('projéteis extras: só os inimigos no cone de 0,3 (cos) recebem disparos próprios', fan + [
        dict(op='resource', value=100), C('rogue2_rajada'), T(200), dict(op='resource', value=100), C('rogue2_rajada', 2, [0]), T(200),
        dict(op='resource', value=100), C('rogue2_rajada', 3, [5]), T(200), dict(op='resource', value=100), C('pala2_julgamento', 1, [5]), T(200)], 9003))
    return cases


def randomized(count, length, seed):
    rng = random.Random(seed)
    skills = list(ROWS); names = [e[0] for e in ENEMIES]
    cases = []
    for c in range(count):
        ops = []
        for _ in range(length):
            r = rng.random()
            if r < .34:
                sid = rng.choice(skills); n = len(ROWS[sid]['options'])
                picks = sorted({rng.choice([0, 1, 2])} | ({rng.choice([3, 4])} if rng.random() < .5 else set()) |
                               ({rng.choice([5, 6])} if rng.random() < .4 else set())) if n and rng.random() < .75 else []
                if ROWS[sid]['shape'] == 'melee':
                    ops.append(dict(op='strike', skill=sid, rank=rng.randint(1, 15), picks=picks, target=rng.choice(names), factor=rng.choice([1., .6, 1.4])))
                else: ops.append(C(sid, rng.randint(1, 15), picks))
            elif r < .60: ops.append(T(rng.choice([1, 2, 15, 30, 31, 60, 121, 300])))
            elif r < .66: ops.append(dict(op='hold', on=rng.random() < .6))
            elif r < .74: ops.append(dict(op='move', who=rng.choice(['heroi'] + names), x=rng.randint(1, W - 2), y=rng.randint(1, H - 2)))
            elif r < .78: ops.append(dict(op='aim', at=rng.choice([None, [rng.randint(1, W - 2), rng.randint(1, H - 2)]])))
            elif r < .81: ops.append(dict(op='face', d=list(rng.choice([(1, 0), (0, 1), (-1, -1), (1, -1)]))))
            elif r < .85: ops.append(dict(op='hurt', amount=rng.choice([10, 60, 200])))
            elif r < .90: ops.append(dict(op='resource', value=rng.choice([0, 5, 30, 100])))
            elif r < .97: ops.append(dict(op='enemy_state', target=rng.choice(names), kind=rng.choice(['stun', 'slow', 'burn', 'mark', 'curse', 'vulnerable', 'bleed']),
                                          ticks=rng.choice([30, 120, 400]), mag=rng.choice([.2, .5, 3.])))
            else: ops.append(dict(op='dodge', value=rng.choice([0, 30, 90])))
        cases.append(record(f'aleatório {seed}-{c}', ops, seed * 1000 + c, shield=rng.random() < .3))
    return cases


def with_passives(per_class, length, seed):
    """5.5: each class with its own skills and its 6 class passives at random ranks (real passives.py)."""
    rng = random.Random(seed); names = [e[0] for e in ENEMIES]; cases = []
    for cls in CLASSES:
        skills = [s['id'] for s in catalog.SKILLS[cls]]
        for c in range(per_class):
            ranks = {row['id']: rng.choice([0, 1, 2, 3, 3]) for row in catalog.PASSIVES[cls]}
            ops = [dict(op='hurt', amount=rng.choice([0, 150, 300, 380]))]
            for _ in range(length):
                r = rng.random()
                if r < .42:
                    sid = rng.choice(skills); n = len(ROWS[sid]['options'])
                    picks = sorted({rng.choice([0, 1, 2])} | ({rng.choice([3, 4])} if rng.random() < .5 else set())) if n and rng.random() < .6 else []
                    if ROWS[sid]['shape'] == 'melee': ops.append(dict(op='strike', skill=sid, rank=rng.randint(1, 8), picks=picks, target=rng.choice(names), factor=1.))
                    else: ops.append(C(sid, rng.randint(1, 8), picks))
                elif r < .66: ops.append(T(rng.choice([1, 30, 60, 121, 200])))
                elif r < .72: ops.append(dict(op='hold', on=rng.random() < .6))
                elif r < .79: ops.append(dict(op='move', who=rng.choice(['heroi'] + names), x=rng.randint(4, 14), y=rng.randint(4, 12)))
                elif r < .84: ops.append(dict(op='hurt', amount=rng.choice([20, 90, 250])))
                elif r < .89: ops.append(dict(op='resource', value=rng.choice([10, 100])))
                elif r < .95: ops.append(dict(op='enemy_state', target=rng.choice(names), kind=rng.choice(['stun', 'slow', 'mark', 'curse', 'burn']),
                                              ticks=rng.choice([120, 400]), mag=rng.choice([.3, 2.])))
                else: ops.append(dict(op='dodge', value=rng.choice([0, 40, 90])))
            cases.append(record(f'passivas {cls} {c}', ops, seed * 1000 + len(cases), shield=cls == 'paladino', cls=cls, ranks=ranks))
    return cases


def main():
    cases = handwritten() + randomized(150, 40, 2028) + with_passives(24, 45, 2030)
    out = HERE / 'tests' / 'parity' / 'skills_cases.json'
    out.parent.mkdir(parents=True, exist_ok=True)
    payload = {'source': 'umbra-ashes-below game/combat_system/v16 + game/skills/v16/tuning.py', 'classes': CLASSES,
               'map': {'width': W, 'height': H, 'walls': sorted(WALLS), 'city': CITY},
               'enemies': ENEMIES, 'specs': SPECS, 'cases': cases}
    out.write_bytes(json.dumps(payload, ensure_ascii=False, separators=(',', ':')).encode('utf-8'))
    steps = sum(len(c['steps']) for c in cases)
    errors = sum(1 for c in cases for s in c['steps'] if 'error' in s)
    print(f'{len(cases)} casos, {steps} operações, {errors} erros do próprio Python -> {out} ({out.stat().st_size // 1024} KB)')


if __name__ == '__main__':
    main()
