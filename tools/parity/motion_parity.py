r"""Parity cases for the hero's continuous movement (step 5.8c): world/locomotion.advance.

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\parity\motion_parity.py

A REAL hero of UMBRA's composed engine moves with sub-tile precision (bounded acceleration, braking, a disc against
solid and occupied tiles, sub-steps); its grid cell is the combat contract and changes through player.move (which also
cancels a pending attack). The input vector is already in world space (the camera transform is the input layer's). After
every tick: continuous position, velocity, travelled distance, grid cell, previous cell, facing and is_moving.
Output: tests/parity/motion_cases.json
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

import game.core.engine  # noqa: E402,F401
from game.entities.player import Player  # noqa: E402
from game.world import locomotion  # noqa: E402
from game.combat_system import status  # noqa: E402

MAP = ["######################",
       "#......#.............#",
       "#......#......##.....#",
       "#.............##.....#",
       "#....#...............#",
       "#....#......#........#",
       "#...........#........#",
       "######################"]


class Dungeon:
    def __init__(self):
        self.map = [[1 if c == '#' else 0 for c in row] for row in MAP]; self.height, self.width = len(MAP), len(MAP[0])


class Blocker:
    def __init__(self, x, y): self.x, self.y, self.alive = x, y, True
    @property
    def position(self): return (self.x, self.y)


class Engine:
    def __init__(self, player, dungeon): self.player, self.dungeon, self.vector, self.render_mode = player, dungeon, (0., 0.), 'topdown'
    def read_motion_input(self): return self.vector


def snap(p):
    m = p.motion
    return {'pos': list(m.position), 'vel': list(m.velocity), 'dist': m.distance, 'cell': [p.x, p.y], 'previous': list(p.previous_position),
            'facing': list(p.attack_direction), 'moving': bool(p.is_moving)}


def record(name, ops, blockers, seed):
    random.seed(seed)
    p = Player((2, 2), 'barbaro'); d = Dungeon(); e = Engine(p, d)
    p._active_enemies = [Blocker(*b) for b in blockers]; locomotion.sync(e)
    steps = []
    for op in ops:
        if op['op'] == 'move':
            e.vector = tuple(op['v'])
            for _ in range(op['n']): locomotion.advance(e)
        elif op['op'] == 'state': status.apply(p, op['kind'], op['ticks'], op['mag'])
        elif op['op'] == 'tick':
            for _ in range(op['n']): status.tick(p)
        steps.append({'op': op, 'state': snap(p)})
    return {'name': name, 'blockers': blockers, 'steps': steps}


def main():
    rng = random.Random(2035); cases = []
    M = lambda x, y, n: dict(op='move', v=[x, y], n=n)
    cases.append(record('reta, parada, diagonal, parede e quina', [M(1, 0, 40), M(0, 0, 20), M(.7071, .7071, 50), M(0, -1, 60), M(-1, 0, 90),
                                                                   M(1, 1, 30), M(0, 0, 3), M(0, 1, 40)], [], 1))
    cases.append(record('inimigos bloqueiam a casa', [M(1, 0, 120), M(0, 1, 60), M(1, 0, 60)], [(5, 2), (6, 3)], 2))
    cases.append(record('lentidão, rapidez e analógico fraco', [dict(op='state', kind='slow', ticks=200, mag=.5), M(1, 0, 60),
                                                               dict(op='state', kind='swift', ticks=300, mag=.4), M(0, 1, 40), M(.3, .1, 60), dict(op='tick', n=400), M(-.5, -.5, 80)], [], 3))
    for c in range(60):
        ops = []
        for _ in range(rng.randint(8, 20)):
            r = rng.random()
            if r < .8:
                a = rng.random() * 6.283; s = rng.choice([0, .2, .5, 1, 1])
                import math
                ops.append(M(round(math.cos(a) * s, 4), round(math.sin(a) * s, 4), rng.choice([1, 3, 8, 15, 30, 60])))
            elif r < .9: ops.append(dict(op='state', kind=rng.choice(['slow', 'swift']), ticks=rng.choice([30, 120]), mag=rng.choice([.2, .6])))
            else: ops.append(dict(op='tick', n=rng.choice([10, 60])))
        blockers = [(rng.randint(1, 20), rng.randint(1, 6)) for _ in range(rng.randint(0, 4))]
        cases.append(record(f'aleatório {c}', ops, blockers, 100 + c))
    out = HERE / 'tests' / 'parity' / 'motion_cases.json'
    out.write_bytes(json.dumps({'source': 'umbra-ashes-below world/locomotion.advance on a composed hero', 'map': MAP, 'cases': cases},
                               ensure_ascii=False, separators=(',', ':')).encode('utf-8'))
    print(f'{len(cases)} casos, {sum(len(c["steps"]) for c in cases)} passos -> {out} ({out.stat().st_size // 1024} KB)')


if __name__ == '__main__':
    main()
