r"""Parity cases for combat states and typed damage (UMBRA game/combat_system/status.py and elements.py).

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\parity\status_parity.py

Runs operation sequences through the REAL Python modules of UMBRA and records, after every operation, its return value
and the full state of every test entity. tests/Umbra.Domain.Tests/ParityStatusTests.cs replays the same sequences on the
C# port and must reach the same numbers. Hand-written cases cover each rule; seeded random sequences cover the
interactions. The test entities are plain fixtures (identical on both sides): take_damage subtracts, nothing else.
Output: tests/parity/status_cases.json
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

from game.combat_system import status, elements      # noqa: E402
import game.sound.api as sound_api                     # noqa: E402

sound_api.play_sfx = lambda *a, **k: None              # sound is not a rule
elements.EVENTS.clear()


class Fake:
    """Test entity. Attributes are only set when given, so getattr defaults behave as with real entities."""

    def __init__(self, name, health, boss=False, enemy_type=None, resistances=None, gear=None, windup=False, thorns_element=None):
        self.name = name
        self.health = health
        self.max_health = health
        self.boss = boss
        if enemy_type: self.enemy_type = enemy_type
        if resistances: self.resistances = dict(resistances)
        if gear: self.equipment = {f'slot{i}': g for i, g in enumerate(gear)}
        if windup: self.attack_windup = 25
        if thorns_element: self.thorns_element = thorns_element

    @property
    def alive(self):
        return self.health > 0

    def take_damage(self, amount):
        self.health = max(0, self.health - int(amount))


ENTITY_SPECS = {
    'heroi': dict(health=400, windup=True, gear=[{'resistances': {'fire': .2}, 'element_bonus': {'fire': .3, 'physical': .1}}]),
    'espectro': dict(health=300, enemy_type='espectro', windup=True, thorns_element='shadow'),
    'chefe': dict(health=900, boss=True, resistances={'cold': .4, 'physical': -.1}),
}


def make_world():
    return {name: Fake(name, **spec) for name, spec in ENTITY_SPECS.items()}


def snap(entity):
    rows = getattr(entity, 'statuses', None) or {}
    return {
        'health': entity.health,
        'pressure': int(getattr(entity, 'cc_pressure', 0)), 'pressure_timer': int(getattr(entity, 'cc_pressure_timer', 0)),
        'absorbed': float(getattr(entity, 'absorbed_total', 0)), 'windup': getattr(entity, 'attack_windup', None),
        'statuses': [[kind, [[r['ticks'], float(r['magnitude']), r.get('timer'), float(r.get('carry', 0.)),
                              getattr(r.get('source'), 'name', None)] for r in rows[kind]]] for kind in rows],
    }


def run(op, world):
    t = world[op['t']]
    src = world.get(op.get('src')) if op.get('src') else None
    kind = op['op']
    if kind == 'apply': return status.apply(t, op['kind'], op['ticks'], op['mag'], src)
    if kind == 'frost': return status.add_frost(t, op['amount'])
    if kind == 'tick': return sum(status.tick(t) for _ in range(op['n']))
    if kind == 'absorb': return status.absorb(t, op['amount'])
    if kind == 'deal': return elements.deal(t, op['amount'], op['kind'], src, op.get('id', ''))
    if kind == 'remove': status.remove(t, op.get('kind')); return None
    if kind == 'cleanse': status.cleanse(t); return None
    if kind == 'snapshot':
        data = status.snapshot(t); status.restore(t, data); return data
    if kind == 'query':
        return [status.value(t, op['kind']), status.has(t, op['kind']), status.move_factor(t), status.harmful(t),
                elements.resistance(t, op['kind'] if op['kind'] in elements.NAMES else 'physical'),
                elements.outgoing(t, 'holy'), elements.vulnerability(t)]
    raise ValueError(kind)


def record(name, ops):
    world = make_world()
    steps = []
    for op in ops:
        result = run(op, world)
        if isinstance(result, float) and not math.isfinite(result): raise ValueError('non-finite')
        steps.append({'op': op, 'result': result, 'state': {n: snap(e) for n, e in world.items()}})
    return {'name': name, 'steps': steps}


def handwritten():
    cases = []
    A = lambda t, k, ticks, mag=0., src=None: dict(op='apply', t=t, kind=k, ticks=ticks, mag=mag, src=src)
    T = lambda t, n: dict(op='tick', t=t, n=n)
    cases.append(record('caps e acúmulo de estados iguais', [A('espectro', 'slow', 120, .9), A('espectro', 'slow', 60, .3), T('espectro', 70),
                                                              A('espectro', 'vulnerable', 50, 1.7), dict(op='query', t='espectro', kind='slow')]))
    cases.append(record('dano ao longo do tempo: 5 acúmulos, troca do mais fraco, resto acumulado', [
        A('espectro', 'burn', 300, 7, 'heroi'), A('espectro', 'burn', 200, 3, 'heroi'), A('espectro', 'burn', 100, 11, 'heroi'),
        A('espectro', 'bleed', 90, 5.5, 'heroi'), A('espectro', 'burn', 40, 1, 'heroi'), A('espectro', 'burn', 400, 2, 'heroi'),
        A('espectro', 'burn', 10, .5, 'heroi'), A('espectro', 'burn', 500, 9, 'heroi'), T('espectro', 31), T('espectro', 200)]))
    cases.append(record('chefe: controle pela metade, pressão crescente, imunidade e decaimento', [
        A('chefe', 'stun', 120), A('chefe', 'root', 120), A('chefe', 'slow', 120, .5), A('chefe', 'stun', 120), A('chefe', 'stun', 120),
        A('chefe', 'freeze', 60), A('chefe', 'fear', 60), T('chefe', 181), A('chefe', 'stun', 100), T('chefe', 600), A('chefe', 'stun', 100)]))
    cases.append(record('imune a controle; desimpedido só contra lentidão e prender', [
        A('heroi', 'unstoppable', 100), A('heroi', 'stun', 50), T('heroi', 101), A('heroi', 'unhindered', 100), A('heroi', 'slow', 50, .4),
        A('heroi', 'root', 50), A('heroi', 'stun', 30), dict(op='query', t='heroi', kind='stun')]))
    cases.append(record('atordoar interrompe a preparação do golpe', [A('espectro', 'stun', 10)]))
    cases.append(record('barreira em camadas: a que expira antes absorve primeiro', [
        A('heroi', 'barrier', 300, 40), A('heroi', 'barrier', 100, 25), dict(op='absorb', t='heroi', amount=30), dict(op='absorb', t='heroi', amount=50),
        dict(op='absorb', t='heroi', amount=10), T('heroi', 1)]))
    cases.append(record('gelidez: lentidão, congelamento a 100, chefe nunca congela', [
        dict(op='frost', t='espectro', amount=40), dict(op='query', t='espectro', kind='frost'), dict(op='frost', t='espectro', amount=70),
        dict(op='query', t='espectro', kind='freeze'), dict(op='frost', t='chefe', amount=150), dict(op='query', t='chefe', kind='frost'), T('chefe', 241)]))
    cases.append(record('dano tipado: resistência por perfil, por item, bônus de elemento, vulnerável, poder e enfraquecido', [
        dict(op='deal', t='espectro', amount=50, kind='shadow', src='heroi'), dict(op='deal', t='espectro', amount=50, kind='holy', src='heroi'),
        dict(op='deal', t='heroi', amount=50, kind='fire', src='espectro'), dict(op='deal', t='chefe', amount=50, kind='cold', src='heroi'),
        dict(op='deal', t='chefe', amount=50, kind='physical', src='heroi'), A('chefe', 'vulnerable', 100, .3), dict(op='deal', t='chefe', amount=50, kind='fire', src='heroi'),
        A('heroi', 'power', 100, .5), A('heroi', 'weaken', 100, .2), dict(op='deal', t='espectro', amount=33, kind='physical', src='heroi'),
        A('heroi', 'holy_power', 100, .25), dict(op='deal', t='espectro', amount=40, kind='holy', src='heroi'), dict(op='deal', t='espectro', amount=40, kind='weird', src=None)]))
    cases.append(record('espinhos devolvem dano; o retorno não volta', [
        A('espectro', 'thorns', 100, .5), dict(op='deal', t='espectro', amount=60, kind='physical', src='heroi'),
        dict(op='deal', t='espectro', amount=10, kind='physical', src='heroi', id='thorns')]))
    cases.append(record('regeneração e resist', [
        dict(op='deal', t='heroi', amount=200, kind='physical', src=None), A('heroi', 'regen', 120, .05), T('heroi', 120),
        A('heroi', 'resist', 100, .9), dict(op='deal', t='heroi', amount=100, kind='physical', src=None)]))
    cases.append(record('salvar e restaurar estados', [A('espectro', 'burn', 200, 4, 'heroi'), A('espectro', 'mark', 90), A('espectro', 'taunt', 90, 0, 'heroi'),
                                                        A('espectro', 'weaken', 90, .3), dict(op='snapshot', t='espectro'), T('espectro', 60)]))
    cases.append(record('limpar e remover', [A('heroi', 'slow', 90, .3), A('heroi', 'poison', 90, 4), A('heroi', 'power', 90, .2), dict(op='cleanse', t='heroi'),
                                             dict(op='query', t='heroi', kind='power'), dict(op='remove', t='heroi', kind='power'), dict(op='remove', t='heroi')]))
    cases.append(record('morte por dano ao longo do tempo; mortos não recebem estados', [
        A('espectro', 'poison', 6000, 900, 'heroi'), T('espectro', 60), A('espectro', 'slow', 100, .3), T('espectro', 31)]))
    return cases


def randomized(count, length, seed):
    rng = random.Random(seed)
    kinds = list(status.KINDS); entities = list(ENTITY_SPECS); elems = list(elements.NAMES) + ['weird']
    cases = []
    for c in range(count):
        ops = []
        for _ in range(length):
            t = rng.choice(entities); r = rng.random()
            src = rng.choice(entities + [None])
            if r < .40:
                k = rng.choice(kinds) if rng.random() < .6 else rng.choice(['burn', 'bleed', 'poison', 'slow', 'stun', 'barrier', 'vulnerable', 'regen', 'power', 'weaken'])
                ops.append(dict(op='apply', t=t, kind=k, ticks=rng.choice([-5, 0, 1, 15, 30, 31, 60, 90, 240, 7000]),
                                mag=rng.choice([0, .1, .3, .75, 1.5, 3, 7.5, 40, -1]), src=src))
            elif r < .62: ops.append(dict(op='tick', t=t, n=rng.choice([1, 2, 29, 30, 31, 60, 180, 181])))
            elif r < .74: ops.append(dict(op='deal', t=t, amount=rng.choice([1, 7, 33, 50.5, 120]), kind=rng.choice(elems), src=src))
            elif r < .80: ops.append(dict(op='absorb', t=t, amount=rng.choice([0, 5, 12.5, 60])))
            elif r < .85: ops.append(dict(op='frost', t=t, amount=rng.choice([0, 10, 35, 60, 120])))
            elif r < .90: ops.append(dict(op='query', t=t, kind=rng.choice(kinds)))
            elif r < .93: ops.append(dict(op='cleanse', t=t))
            elif r < .96: ops.append(dict(op='snapshot', t=t))
            else: ops.append(dict(op='remove', t=t, kind=rng.choice(kinds + [None])))
        cases.append(record(f'aleatório {seed}-{c}', ops))
    return cases


def main():
    cases = handwritten() + randomized(160, 28, 2026)
    out = HERE / 'tests' / 'parity' / 'status_cases.json'
    out.parent.mkdir(parents=True, exist_ok=True)
    payload = {'source': 'umbra-ashes-below game/combat_system/status.py + elements.py', 'entities': ENTITY_SPECS, 'cases': cases}
    out.write_bytes(json.dumps(payload, ensure_ascii=False, separators=(',', ':')).encode('utf-8'))
    steps = sum(len(c['steps']) for c in cases)
    print(f'{len(cases)} casos, {steps} operações -> {out} ({out.stat().st_size // 1024} KB)')


if __name__ == '__main__':
    main()
