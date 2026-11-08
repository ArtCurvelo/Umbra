r"""Parity cases for the shared combat resources (UMBRA game/combat_system/resources.py).

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\parity\resources_parity.py

Same method as status_parity.py: operation sequences run through the REAL Python module (and status.py, which the
layered Barreira shares) and record, after every operation, its return value and the full state of every test entity
plus the Orbes de Sangue on the floor. tests/Umbra.Domain.Tests/ParityResourcesTests.cs replays them on the C# port.

What resources.py reads from systems that are not ported yet is recorded as plain numbers per entity, computed by the
real UMBRA code from the fixture: the necromancer item affixes (necro_affixes.share 'barrier'/'fortify') and the
Grimório sacrifice bonuses (grimoire.bonus 'attack_speed'/'armour'). The C# side receives them through hooks.
gain_resource is the hero's own method; the fixture adds round(amount), capped, and both sides do the same.
Output: tests/parity/resources_cases.json
"""
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

from game.combat_system import resources, status, elements   # noqa: E402
from game.equipment import necro_affixes                      # noqa: E402
from game.skills import grimoire                              # noqa: E402
import game.sound.api as sound_api                            # noqa: E402

sound_api.play_sfx = lambda *a, **k: None
elements.EVENTS.clear()


class Fake:
    """Test hero/enemy. Attributes are only set when given, so getattr defaults behave as with real entities."""

    def __init__(self, name, health, x=0, y=0, resource=0, max_resource=100, necro=None):
        self.name = name
        self.health = health
        self.max_health = health
        self.boss = False
        self.x, self.y = x, y
        self.resource, self.max_resource = resource, max_resource
        if necro:
            self.player_class = 'necromante'
            self.loadout = {f'slot{i}': {'affixes': dict(a)} for i, a in enumerate(necro['affixes'])}
            self.necro_specs = dict(necro['specs'])
            self.grimoire = dict(necro['grimoire'])

    @property
    def alive(self):
        return self.health > 0

    def take_damage(self, amount):
        self.health = max(0, self.health - int(amount))

    def gain_resource(self, amount, source='combat'):
        gained = int(round(amount)); self.resource = min(self.max_resource, self.resource + gained)
        return gained


ENTITY_SPECS = {
    'heroi': dict(health=400, x=5, y=5, resource=20, max_resource=100, necro=dict(
        affixes=[{'barrier': 15}, {'fortify': 12}, {'fortify': 8}],
        specs={'warrior': 'shield', 'mage': 'shadow', 'golem': 'bone'},
        grimoire={'warrior': 'sacrifice', 'mage': 'up1', 'golem': 'sacrifice'})),
    'aliado': dict(health=250, x=9, y=5, resource=90, max_resource=100),
    'espectro': dict(health=300, x=7, y=7),
}
HEROES = ('heroi', 'aliado')


class Seat:
    def __init__(self, player): self.player = player


class Dungeon:
    pass


class Engine:
    def __init__(self, world):
        self.player = world['heroi']; self.party = [Seat(world['aliado'])]; self.dungeon = Dungeon()


def hooks(world):
    """Values of the not-yet-ported systems, computed by the real UMBRA code (fixed for the whole case)."""
    return {n: {'affix_barrier': necro_affixes.share(e, 'barrier'), 'affix_fortify': necro_affixes.share(e, 'fortify'),
                'grimoire_attack_speed': grimoire.bonus(e, 'attack_speed'), 'grimoire_armour': grimoire.bonus(e, 'armour')}
            for n, e in world.items()}


def snap(entity):
    rows = getattr(entity, 'statuses', None) or {}
    d = entity.__dict__
    return {
        'health': entity.health, 'resource': entity.resource, 'x': entity.x, 'y': entity.y,
        'fortify': float(d.get('fortify', 0.)), 'fortify_timer': int(d.get('fortify_timer', 0)), 'fortify_carry': float(d.get('fortify_carry', 0.)),
        'absorbed': float(getattr(entity, 'absorbed_total', 0)),
        'statuses': [[kind, [[r['ticks'], float(r['magnitude']), r.get('timer'), float(r.get('carry', 0.))] for r in rows[kind]]] for kind in rows],
    }


def orb_state(engine):
    return [[list(o['position']), o['ticks'], float(o['heal']), float(o['resource']), float(o['fortify'])]
            for o in engine.dungeon.__dict__.get('blood_orbs', [])]


def run(op, world, engine):
    t = world.get(op.get('t'))
    kind = op['op']
    if kind == 'barrier': return resources.add_barrier(t, op['seconds'], op['points'])
    if kind == 'apply': return status.apply(t, op['kind'], op['ticks'], op['mag'])
    if kind == 'absorb': return status.absorb(t, op['amount'])
    if kind == 'stick': return sum(status.tick(t) for _ in range(op['n']))
    if kind == 'fortify': return resources.fortify(t, op['points'])
    if kind == 'rtick':
        for _ in range(op['n']): resources.tick(t)
        return None
    if kind == 'stack': return resources.add_stack(t, op['kind'], op['amount'], op.get('limit'))
    if kind == 'hurt': t.take_damage(op['amount']); return None
    if kind == 'move': t.x, t.y = op['x'], op['y']; return None
    if kind == 'drop':
        resources.drop_orb(engine.dungeon, op['pos'], op['heal'], op['resource'], op['seconds'], op['fortify']); return None
    if kind == 'orbs':
        for _ in range(op['n']): resources.tick_orbs(engine)
        return None
    if kind == 'query':
        return [resources.barrier_total(t), resources.fortification(t), resources.stacks(t, 'ferocity'),
                resources.stacks(t, 'determination'), resources.stacks(t, 'brutal'), resources.attack_speed(t), resources.armour(t)]
    raise ValueError(kind)


def record(name, ops):
    world = {name: Fake(name, **spec) for name, spec in ENTITY_SPECS.items()}
    engine = Engine(world)
    first = hooks(world)
    steps = []
    for op in ops:
        result = run(op, world, engine)
        if isinstance(result, float) and not math.isfinite(result): raise ValueError('non-finite')
        steps.append({'op': op, 'result': result, 'state': {n: snap(e) for n, e in world.items()}, 'orbs': orb_state(engine)})
    if hooks(world) != first: raise ValueError('hook values changed during the case')
    return {'name': name, 'hooks': first, 'steps': steps}


def D(pos, heal=resources.ORB_HEAL, resource=resources.ORB_RESOURCE, seconds=resources.ORB_SECONDS, fortify=0.):
    return dict(op='drop', pos=list(pos), heal=heal, resource=resource, seconds=seconds, fortify=fortify)


def handwritten():
    B = lambda t, s, p: dict(op='barrier', t=t, seconds=s, points=p)
    Q = lambda t: dict(op='query', t=t)
    cases = [
        record('barreira em camadas: afixo, limite na vida máxima, duração própria', [
            B('heroi', 4, 100), B('aliado', 4, 100), B('heroi', 2, 300), B('heroi', 3, 50), B('aliado', 0, 50), B('aliado', 5, -3),
            B('aliado', 1.5, 160.9), Q('heroi'), dict(op='stick', t='aliado', n=91), Q('aliado')]),
        record('barreira em camadas: absorção pela que expira antes e mistura com a barreira única de 0.16', [
            B('heroi', 6, 80), B('heroi', 2, 40), dict(op='apply', t='heroi', kind='barrier', ticks=200, mag=30),
            dict(op='absorb', t='heroi', amount=50), Q('heroi'), dict(op='absorb', t='heroi', amount=100), B('heroi', 1, 500), Q('heroi')]),
        record('fortificação: afixo, teto, cura por segundo, resto e expiração', [
            dict(op='fortify', t='heroi', points=0), dict(op='fortify', t='heroi', points=50), dict(op='rtick', t='heroi', n=60),
            dict(op='hurt', t='heroi', amount=120), dict(op='rtick', t='heroi', n=37), dict(op='fortify', t='heroi', points=900),
            dict(op='rtick', t='heroi', n=200), dict(op='rtick', t='heroi', n=481), Q('heroi')]),
        record('fortificação: herói cheio não gasta; morto perde a reserva', [
            dict(op='fortify', t='aliado', points=30), dict(op='rtick', t='aliado', n=100), Q('aliado'), dict(op='hurt', t='aliado', amount=250),
            dict(op='rtick', t='aliado', n=1), Q('aliado'), dict(op='rtick', t='espectro', n=5)]),
        record('acúmulos: limite, duração renovada, limite próprio e bônus do Grimório', [
            dict(op='stack', t='heroi', kind='ferocity', amount=1), dict(op='stack', t='heroi', kind='ferocity', amount=2),
            dict(op='stack', t='heroi', kind='ferocity', amount=3), dict(op='stack', t='heroi', kind='determination', amount=7),
            dict(op='stack', t='heroi', kind='brutal', amount=1, limit=2), dict(op='stack', t='heroi', kind='brutal', amount=5, limit=2),
            Q('heroi'), dict(op='stick', t='heroi', n=240), Q('heroi'), dict(op='stick', t='heroi', n=120), Q('heroi'),
            dict(op='stack', t='aliado', kind='determination', amount=2), Q('aliado')]),
        record('orbes de sangue: o primeiro herói a um tile bebe; expiram; limite de 24', [
            D((6, 6)), D((9, 7)), D((20, 20), seconds=.5), D((5.9, 4.2), heal=.1, resource=7.5, fortify=.05),
            dict(op='hurt', t='heroi', amount=200), dict(op='hurt', t='aliado', amount=100), dict(op='orbs', n=1),
            dict(op='move', t='aliado', x=9, y=6), dict(op='orbs', n=1), dict(op='orbs', n=30)]
            + [D((40 + i, 40)) for i in range(26)] + [dict(op='orbs', n=599), dict(op='orbs', n=1)]),
        record('orbe com fortificação e herói morto não bebe', [
            dict(op='hurt', t='aliado', amount=250), D((9, 5), fortify=.1), dict(op='orbs', n=1), dict(op='hurt', t='heroi', amount=50),
            D((5, 6), heal=.0001, resource=200, fortify=.2), dict(op='orbs', n=1), Q('heroi')]),
    ]
    return cases


def randomized(count, length, seed):
    rng = random.Random(seed)
    entities = list(ENTITY_SPECS)
    cases = []
    for c in range(count):
        ops = []
        for _ in range(length):
            t = rng.choice(entities); r = rng.random()
            if r < .16: ops.append(dict(op='barrier', t=t, seconds=rng.choice([0, .5, 1, 2.5, 4, 8]), points=rng.choice([-5, 0, 3, 12.7, 40, 150, 600])))
            elif r < .24: ops.append(dict(op='apply', t=t, kind=rng.choice(['barrier', 'ferocity', 'determination', 'regen']),
                                          ticks=rng.choice([30, 90, 300]), mag=rng.choice([1, 2.5, 25, 60])))
            elif r < .32: ops.append(dict(op='absorb', t=t, amount=rng.choice([0, 7, 33.3, 90, 400])))
            elif r < .40: ops.append(dict(op='stick', t=t, n=rng.choice([1, 30, 61, 240, 361])))
            elif r < .50: ops.append(dict(op='fortify', t=t, points=rng.choice([-1, 0, 4, 25.5, 80, 500])))
            elif r < .60: ops.append(dict(op='rtick', t=t, n=rng.choice([1, 17, 60, 119, 480, 481])))
            elif r < .70: ops.append(dict(op='stack', t=t, kind=rng.choice(list(resources.STACKS)), amount=rng.choice([1, 1, 2, 9]),
                                          limit=rng.choice([None, None, 1, 3])))
            elif r < .78: ops.append(dict(op='hurt', t=t, amount=rng.choice([1, 15, 60, 140])))
            elif r < .84: ops.append(dict(op='move', t=rng.choice(HEROES), x=rng.randint(3, 11), y=rng.randint(3, 9)))
            elif r < .90: ops.append(D((rng.randint(3, 11) + rng.choice([0, .4]), rng.randint(3, 9)), heal=rng.choice([resources.ORB_HEAL, .25, 0]),
                                       resource=rng.choice([0, resources.ORB_RESOURCE, 12.5]), seconds=rng.choice([.1, 1, resources.ORB_SECONDS]),
                                       fortify=rng.choice([0., 0., .05])))
            elif r < .95: ops.append(dict(op='orbs', n=rng.choice([1, 6, 60, 601])))
            else: ops.append(dict(op='query', t=t))
        cases.append(record(f'aleatório {seed}-{c}', ops))
    return cases


def main():
    cases = handwritten() + randomized(120, 30, 2027)
    out = HERE / 'tests' / 'parity' / 'resources_cases.json'
    out.parent.mkdir(parents=True, exist_ok=True)
    payload = {'source': 'umbra-ashes-below game/combat_system/resources.py (+ status.py)', 'entities': ENTITY_SPECS, 'cases': cases}
    out.write_bytes(json.dumps(payload, ensure_ascii=False, separators=(',', ':')).encode('utf-8'))
    steps = sum(len(c['steps']) for c in cases)
    print(f'{len(cases)} casos, {steps} operações -> {out} ({out.stat().st_size // 1024} KB)')


if __name__ == '__main__':
    main()
