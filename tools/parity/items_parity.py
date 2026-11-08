r"""Parity cases for the Reforge items (step 5.6): drops, records, equipment slots, bag pages, crafting.

    ..\umbra-ashes-below\.venv\Scripts\python.exe tools\parity\items_parity.py

Runs on REAL heroes of UMBRA's composed engine. Drops are real `Item((x, y), kind, floor, player_level, player_class)`
objects (create_data rebalanced by core.balance, then loot.roll). Every other operation is the real module function:
transactions, itemdata (quality, durability), forge, recycle/repair, gems/jeweler, temper, enchant, codex/wardrobe,
inventory pages and lost items. After every step the hero's whole item state is recorded: gold, level, materials,
every bag page, the loadout, the legacy aliases (by slot), lost items, the world's unlock registry, active aspects,
gem bonuses and Necromancer affix totals. Python's global `random` is seeded per case.

Not deterministic in Python: loot.roll picks the weapon family from a *set*, whose order follows the process's string
hashing. The order this process sees is recorded per class (`families`) and the C# replay uses it.
The hero's stats (recalculate_stats) are step 5.8; the only part of it that touches items (sync_aliases with the
active weapon skill) is mirrored by the C# fixture.
Output: tests/parity/items_cases.json
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
from game.entities.player import Player                                   # noqa: E402
from game.entities.items import Item                                      # noqa: E402
from game.equipment import (transactions, itemdata, forge, recycle, gems, jeweler, temper, enchant, codex,  # noqa: E402
                            legendary, necro_affixes, slots, selection, materials)
from game.inventory import pages, lost                                    # noqa: E402

CLASSES = ('barbaro', 'mago', 'ladino', 'paladino', 'necromante', 'bruxo', 'druida')
FAMILIES = {c: list(tuple({kind for values in slots.CLASS_WEAPONS[c].values() for kind in values})) for c in CLASSES}
NECRO_KEYS = sorted(necro_affixes.AFFIXES)


def plain(value):
    if isinstance(value, dict): return {str(k): plain(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)): return [plain(v) for v in value]
    return value


def snap(p, unlocks):
    gear = p.loadout or {}
    aliases = {}
    for key, value in p.equipment.items():
        same = next((slot for slot, record in gear.items() if record is not None and record is value), None)
        aliases[key] = ['slot', same] if same else ['value', plain(value)]
    bag = p.inventory
    return {
        'gold': p.gold, 'level': p.level, 'materials': plain(p.__dict__.get('materials', {})),
        'items': plain(bag.items), 'gems': plain(bag.gems), 'consumables': plain(bag.consumables), 'potion': bag.get('potion', 0),
        'capacity': bag.capacity, 'loadout': [[k, plain(v)] for k, v in gear.items()], 'aliases': aliases,
        'lost': plain(p.__dict__.get('lost_items', [])), 'unlocks': plain(unlocks),
        'aspects': sorted(legendary.active(p)), 'active_key': selection.active_key(p),
        'gem_bonuses': [plain(gems.bonuses(r)) if r else None for r in gear.values()],
        'necro': [necro_affixes.total(p, k) for k in NECRO_KEYS], 'slots_free': [pages.free(bag, page) for page in pages.PAGES],
    }


def bag_record(p, index):
    items = p.inventory.items
    return items[index % len(items)] if items else None


def run(op, p, unlocks):
    k = op['op']
    if k == 'drop':
        data = Item((0, 0), op['kind'], op['floor'], player_level=op['level'], player_class=op['cls']).data
        if op.get('keep'): return list(pages.add(p.inventory, data))
        if op.get('lose'): return lost.keep(p, data)
        return plain(data)
    if k == 'equip': return list(transactions.from_inventory(p, op['index'], op.get('slot')))
    if k == 'equip_last': return list(transactions.from_inventory(p, len(p.inventory.items) - 1 - op['back']))
    if k == 'unequip': return transactions.unequip(p, op['slot'])
    if k == 'forge': return list(forge.transact(p, op['action'], op['index']))
    if k == 'wear': return itemdata.wear_equipped(p, op['amount'])
    if k == 'repair_all': return list(recycle.repair_all(p))
    if k == 'repair':
        r = bag_record(p, op['index']); return list(recycle.repair(p, r)) if r else None
    if k == 'add_socket':
        r = bag_record(p, op['index']); return list(jeweler.add_socket(p, r)) if r else None
    if k == 'give_gem': return list(pages.add(p.inventory, gems.make(op['family'], op['grade'], op['count'])))
    if k == 'socket':
        r = bag_record(p, op['index']); return list(jeweler.socket_gem(p, r, op['gid'], op.get('at'))) if r else None
    if k == 'unsocket':
        r = bag_record(p, op['index']); return list(jeweler.unsocket(p, r, op['at'])) if r else None
    if k == 'gem_create': return list(jeweler.create(p, op['family'], op['grade'], op['times']))
    if k == 'gem_recycle': return list(jeweler.recycle(p, op['gid']))
    if k == 'temper':
        r = bag_record(p, op['index']); return list(temper.apply(p, r, op['recipe'], unlocks)) if r else None
    if k == 'unlock_temper': return temper.unlock(unlocks, op['recipe'])
    if k == 'enchant':
        r = bag_record(p, op['index'])
        if not r or not r.get('affixes'): return None
        key = sorted(r['affixes'])[op['pick'] % len(r['affixes'])]
        return list(enchant.enchant(p, r, key))
    if k == 'junk':
        r = bag_record(p, op['index']); return recycle.toggle_junk(r) if r else None
    if k == 'lock':
        r = bag_record(p, op['index'])
        if r: r['locked'] = not r.get('locked')
        return None
    if k == 'recycle':
        plan = recycle.plan(p, op['mode'])
        return [plain({key: v for key, v in plan.items()}), list(recycle.execute(p, plan, op['confirm'], unlocks))]
    if k == 'engrave':
        r = bag_record(p, op['index']); return list(codex.engrave(p, r, op['aspect'], unlocks)) if r else None
    if k == 'look':
        r = bag_record(p, op['index'])
        if not r: return None
        looks = codex.looks_for(unlocks, r)
        key = None if op['pick'] < 0 or not looks else looks[op['pick'] % len(looks)][0]
        return list(codex.apply_look(r, unlocks, key))
    if k == 'put': return list(pages.add(p.inventory, copy.deepcopy(op['record'])))
    if k == 'material': materials.add(p, op['id'], op['n']); return None
    if k == 'gold': p.gold = op['n']; return None
    if k == 'level': p.level = op['n']; return None
    if k == 'recover': return list(lost.recover(p, op['index']))
    if k == 'potions': return pages.add_potions(p, op['n'])
    if k == 'legacy':
        record = copy.deepcopy(op['record']); itemdata.normalize(record); itemdata.normalize(record)
        return plain(record)
    raise ValueError(k)


def record_case(name, cls, ops, seed):
    random.seed(seed)
    p = Player((5, 5), cls); p.gold = 0; unlocks = {'codex': [], 'wardrobe': {}, 'temper': []}
    start = snap(p, unlocks)
    steps = []
    for op in ops:
        result = run(op, p, unlocks)
        steps.append({'op': op, 'result': plain(result), 'state': snap(p, unlocks)})
    return {'name': name, 'class': cls, 'seed': seed, 'start': start, 'start_extra': {'active_weapon_skill': getattr(p, 'active_weapon_skill', None),
            'basic_skill_id': p.basic_skill_id, 'potion_capacity': getattr(p, 'potion_capacity', pages.POTION_CAP)}, 'steps': steps}


LEGACY = [
    {'kind': 'weapon', 'slot': 'weapon', 'name': 'Velha', 'rarity': 'Épico', 'value': 9, 'stats': {'attack': 9}, 'durability': {'current': 140, 'max': 120}, 'quality': 31.7, 'sockets': ['rubi:2', None, 'safira:1'], 'weapon_type': 'sword2h'},
    {'kind': 'armor', 'slot': 'gloves', 'name': 'Luva', 'rarity': 'Incomum', 'value': 3, 'stats': {'defense': 3}, 'durability': {'current': -5}, 'sockets': ['x'], 'appearance': None},
    {'kind': 'accessory', 'slot': 'ring', 'name': 'Anel', 'rarity': 'Misterioso', 'quality': None, 'temper': {'stat': 'mana'}},
    {'kind': 'gem', 'id': 'rubi:1'},
]


def random_ops(rng, cls, length):
    ops = [dict(op='gold', n=rng.choice([0, 500, 5000, 50000])), dict(op='level', n=rng.choice([1, 8, 20, 45, 70]))]
    for _ in range(4):
        ops.append(dict(op='drop', kind=rng.choice(['weapon', 'armor', 'accessory']), floor=rng.randint(1, 30), level=rng.choice([None, 5, 20, 60]), cls=cls, keep=True))
    for _ in range(length):
        r = rng.random()
        if r < .16: ops.append(dict(op='drop', kind=rng.choice(['weapon', 'armor', 'accessory', 'weapon', 'gold', 'potion', 'relic', 'resource_orb', 'chest']),
                                    floor=rng.randint(1, 40), level=rng.choice([None, 1, 12, 30, 70]), cls=rng.choice([cls, cls, None]),
                                    keep=rng.random() < .75, lose=rng.random() < .1))
        elif r < .26: ops.append(dict(op='equip', index=rng.randint(0, 6), slot=rng.choice([None, None, 'ring2', 'dual2', 'off_hand', 'shield', 'focus', 'helmet'])))
        elif r < .31: ops.append(dict(op='unequip', slot=rng.choice(['helmet', 'chest', 'ring1', 'ring2', 'amulet', 'off_hand', 'main_hand', 'dual1', 'slashing', 'staff', 'sword'])))
        elif r < .39: ops.append(dict(op='forge', action=rng.choice(['upgrade', 'upgrade', 'sell', 'potion']), index=rng.randint(0, 6)))
        elif r < .42: ops.append(dict(op='wear', amount=rng.choice([10, 35, 200])))
        elif r < .45: ops.append(dict(op=rng.choice(['repair_all', 'repair']), index=rng.randint(0, 6)))
        elif r < .50: ops.append(dict(op='add_socket', index=rng.randint(0, 6)))
        elif r < .54: ops.append(dict(op='give_gem', family=rng.choice(list(gems.FAMILIES)), grade=rng.randint(1, 6), count=rng.choice([1, 3, 7, 120])))
        elif r < .59: ops.append(dict(op='socket', index=rng.randint(0, 6), gid=f'{rng.choice(list(gems.FAMILIES))}:{rng.randint(1, 3)}', at=rng.choice([None, 0, 1])))
        elif r < .62: ops.append(dict(op='unsocket', index=rng.randint(0, 6), at=rng.randint(0, 1)))
        elif r < .65: ops.append(dict(op='gem_create', family=rng.choice(['rubi', 'safira', 'caveira']), grade=rng.randint(1, 7), times=rng.choice([1, 2, 5])))
        elif r < .67: ops.append(dict(op='gem_recycle', gid=f'{rng.choice(["rubi", "safira"])}:{rng.randint(1, 4)}'))
        elif r < .72: ops.append(dict(op='temper', index=rng.randint(0, 6), recipe=rng.choice(list(temper.RECIPES) + ['nada'])))
        elif r < .74: ops.append(dict(op='unlock_temper', recipe=rng.choice(list(temper.RECIPES))))
        elif r < .79: ops.append(dict(op='enchant', index=rng.randint(0, 6), pick=rng.randint(0, 3)))
        elif r < .82: ops.append(dict(op=rng.choice(['junk', 'lock']), index=rng.randint(0, 6)))
        elif r < .86: ops.append(dict(op='recycle', mode=rng.choice(recycle.MODES), confirm=rng.random() < .6))
        elif r < .89: ops.append(dict(op='engrave', index=rng.randint(0, 6), aspect=rng.choice(list(legendary.CATALOG))))
        elif r < .91: ops.append(dict(op='look', index=rng.randint(0, 6), pick=rng.randint(-1, 3)))
        elif r < .95: ops.append(dict(op='material', id=rng.choice(list(materials.CATALOG) + ['material:nada']), n=rng.choice([1, 3, 10])))
        elif r < .97: ops.append(dict(op='recover', index=rng.randint(0, 2)))
        else: ops.append(dict(op='potions', n=rng.choice([1, 3, 9])))
    return ops


def R(kind, slot, name, rarity='Raro', weapon_type=None, aspect=None, level=1, ilevel=20, value=10, stats=None, affixes=None):
    row = {'kind': kind, 'slot': slot, 'name': name, 'weapon_type': weapon_type, 'value': value, 'base_value': value,
           'stats': stats or ({'attack': value} if kind == 'weapon' else {'defense': value}), 'affixes': affixes or {'attack': 2, 'health': 9},
           'rarity': rarity, 'color': [250, 222, 70], 'required_level': level, 'item_level': ilevel, 'gear_schema': 60, 'forge_rank': 0}
    if aspect: row['aspect'] = aspect
    return itemdata.normalize(row)


def scenarios():
    put = lambda record: dict(op='put', record=record)
    rich = [dict(op='gold', n=10 ** 6), dict(op='level', n=70)]
    cases = []
    cases.append(record_case('códice e guarda-roupa: reciclar Lendário com aspecto, gravar em Raro, aplicar aparência', 'barbaro', rich + [
        put(R('weapon', 'weapon', 'Montante Velho', 'Lendário', 'sword2h', aspect='echo')), put(R('armor', 'helmet', 'Elmo Antigo', 'Lendário', aspect='ancestral_guard')),
        put(R('accessory', 'ring', 'Anel Gasto', 'Mágico')), dict(op='recycle', mode='all', confirm=False), dict(op='recycle', mode='all', confirm=True),
        put(R('weapon', 'weapon', 'Montante Novo', 'Raro', 'sword2h')), put(R('armor', 'helmet', 'Elmo Novo', 'Raro')), put(R('armor', 'boots', 'Bota', 'Comum')),
        dict(op='engrave', index=0, aspect='echo'), dict(op='engrave', index=1, aspect='echo'), dict(op='engrave', index=1, aspect='ancestral_guard'),
        dict(op='engrave', index=2, aspect='ancestral_guard'), dict(op='engrave', index=0, aspect='arcane_conduit'),
        dict(op='look', index=0, pick=0), dict(op='look', index=1, pick=0), dict(op='look', index=2, pick=0), dict(op='look', index=0, pick=-1),
        # an aspect on a Raro is not learned when recycled (only Lendário or better)
        put(R('armor', 'chest', 'Peitoral Gravado', 'Raro', aspect='siphon')), put(R('armor', 'pants', 'Calça Mítica', 'Único Mítico', aspect='celerity')),
        dict(op='recycle', mode='Raro', confirm=True), dict(op='recycle', mode='all', confirm=True)], 4200))
    W = lambda name, family, **kw: R('weapon', 'weapon', name, 'Raro', family, **kw)
    for cls, order in (('paladino', [W('Escudo', 'shield', stats={'defense': 8}), W('Espada', 'sword_long'), W('Montante', 'sword2h'), W('Escudo 2', 'shield'), W('Montante 2', 'sword2h')]),
                       ('bruxo', [W('Foco', 'focus'), W('Espada', 'sword_long'), W('Cajado', 'staff'), W('Foco 2', 'focus'), W('Cajado 2', 'staff')]),
                       ('mago', [W('Varinha', 'wand'), W('Orbe', 'focus'), W('Cajado', 'staff'), W('Orbe 2', 'focus'), W('Varinha 2', 'wand'), W('Orbe 3', 'focus')])):
        ops = rich[:]
        for record in order: ops += [put(record), dict(op='equip_last', back=0)]
        ops += [dict(op='unequip', slot=s) for s in ('off_hand', 'shield', 'focus', 'main_hand', 'greatsword', 'staff')]
        ops += [dict(op='give_gem', family='rubi', grade=2, count=90), dict(op='give_gem', family='rubi', grade=2, count=20), dict(op='give_gem', family='rubi', grade=2, count=95)]
        cases.append(record_case(f'{cls}: conflitos de mãos e pilhas de gemas', cls, ops, 4210 + len(cases)))
    necro = [dict(op='drop', kind=rng_kind, floor=28, level=27, cls='necromante') for rng_kind in ('weapon', 'armor', 'accessory') * 30]
    cases.append(record_case('necromante: afixos próprios perto do nível 60', 'necromante', necro, 4202))
    cases.append(record_case('joalheiro: engastes, gemas, criação e reciclagem', 'paladino', rich + [
        put(R('armor', 'helmet', 'Elmo', 'Raro')), put(R('weapon', 'weapon', 'Espada', 'Raro', 'sword_long')), put(R('accessory', 'ring', 'Anel', 'Raro')),
        dict(op='add_socket', index=0), dict(op='material', id='material:ferragem_engaste', n=5),
        dict(op='add_socket', index=0), dict(op='add_socket', index=0), dict(op='add_socket', index=0), dict(op='add_socket', index=1),
        dict(op='add_socket', index=2), dict(op='give_gem', family='rubi', grade=1, count=10), dict(op='give_gem', family='caveira', grade=3, count=2),
        dict(op='socket', index=0, gid='rubi:1'), dict(op='socket', index=0, gid='caveira:3', at=1), dict(op='socket', index=0, gid='rubi:1'),
        dict(op='socket', index=1, gid='rubi:1'), dict(op='socket', index=2, gid='caveira:3'), dict(op='unsocket', index=0, at=0), dict(op='unsocket', index=0, at=0),
        dict(op='gem_create', family='rubi', grade=2, times=5), dict(op='gem_create', family='rubi', grade=3, times=1), dict(op='gem_recycle', gid='rubi:2'),
        dict(op='gem_recycle', gid='rubi:9'), dict(op='give_gem', family='safira', grade=6, count=99), dict(op='give_gem', family='safira', grade=6, count=5)], 4201))
    for cls, records in (('paladino', [R('weapon', 'weapon', 'Montante', 'Raro', 'sword2h'), R('weapon', 'weapon', 'Espada', 'Raro', 'sword_long'),
                                       R('weapon', 'weapon', 'Escudo', 'Raro', 'shield', stats={'defense': 8})]),
                         ('mago', [R('weapon', 'weapon', 'Cajado', 'Raro', 'staff'), R('weapon', 'weapon', 'Orbe', 'Raro', 'focus'), R('weapon', 'weapon', 'Varinha', 'Raro', 'wand')]),
                         ('barbaro', [R('weapon', 'weapon', 'Adaga', 'Mágico', 'dagger'), R('weapon', 'weapon', 'Machadinha', 'Mágico', 'axe1h'),
                                      R('weapon', 'weapon', 'Malho', 'Raro', 'mace2h'), R('weapon', 'weapon', 'Sem tipo', 'Comum')]),
                         ('bruxo', [R('weapon', 'weapon', 'Cajado', 'Raro', 'staff'), R('weapon', 'weapon', 'Espada', 'Raro', 'sword_long'), R('weapon', 'weapon', 'Foco', 'Raro', 'focus')])):
        armor = [R('armor', s, s, 'Comum') for s in ('helmet', 'chest', 'gloves', 'pants', 'boots')] + [R('accessory', 'ring', 'Anel A'), R('accessory', 'ring', 'Anel B'),
                 R('accessory', 'amulet', 'Amuleto'), R('armor', 'armor', 'Peça antiga'), R('weapon', 'weapon', 'Alta', 'Raro', level=99)]
        ops = rich + [put(r) for r in records + armor]
        ops += [dict(op='equip', index=0) for _ in range(len(records) + len(armor))]
        ops += [dict(op='unequip', slot=sl) for sl in ('helmet', 'ring1', 'off_hand', 'main_hand', 'greatsword', 'sword', 'shield', 'staff', 'focus', 'dual1', 'dual2', 'blunt', 'slashing')]
        ops += [dict(op='equip', index=i, slot=sl) for i, sl in ((0, 'ring2'), (1, 'off_hand'), (2, 'dual2'), (0, 'shield'), (3, 'focus'), (0, None), (0, None))]
        ops += [dict(op='wear', amount=30), dict(op='wear', amount=90), dict(op='repair', index=0), dict(op='repair_all'), dict(op='gold', n=3), dict(op='wear', amount=50), dict(op='repair_all')]
        ops += [put(r) for r in records] + [dict(op='equip_last', back=len(records) - 1 - i) for i in range(len(records))]   # each weapon again, over the others
        ops += [put(r) for r in reversed(records)] + [dict(op='unequip', slot='helmet')] + [dict(op='equip', index=0) for _ in range(len(records) + 2)]
        cases.append(record_case(f'{cls}: equipar todos os espaços, conflitos de mãos, desgaste e reparo', cls, ops, 4300 + len(cases)))
    cases.append(record_case('têmpera, encantamento e itens perdidos', 'ladino', rich + [
        put(R('armor', 'chest', 'Peitoral', 'Raro')), put(R('weapon', 'weapon', 'Arco', 'Raro', 'bow')), put(R('accessory', 'amulet', 'Colar', 'Raro')),
        dict(op='temper', index=0, recipe='vitalidade'), dict(op='material', id='material:poeira_tempera', n=6),
        dict(op='temper', index=0, recipe='vitalidade'), dict(op='temper', index=0, recipe='fortaleza'), dict(op='temper', index=0, recipe='vitalidade'),
        dict(op='temper', index=0, recipe='fortaleza'), dict(op='temper', index=0, recipe='fortaleza'), dict(op='temper', index=1, recipe='furia'),
        dict(op='unlock_temper', recipe='furia'), dict(op='temper', index=1, recipe='furia'), dict(op='temper', index=2, recipe='vitalidade'),
        dict(op='enchant', index=0, pick=0), dict(op='enchant', index=0, pick=1), dict(op='enchant', index=1, pick=0)]
        + [dict(op='drop', kind='armor', floor=5, level=10, cls='ladino', lose=True) for _ in range(12)]
        + [dict(op='recover', index=0), dict(op='recover', index=3), dict(op='recover', index=9)], 4400))
    return cases


def main():
    rng = random.Random(2031); cases = []
    for cls in CLASSES:
        drops = [dict(op='drop', kind=kind, floor=floor, level=level, cls=c)
                 for kind in ('weapon', 'armor', 'accessory', 'gold', 'potion', 'relic', 'chest', 'resource_orb', 'power', 'protection')
                 for floor, level in ((1, None), (9, 4), (20, 22), (35, 70)) for c in (cls, None)]
        cases.append(record_case(f'{cls}: quedas de todos os tipos', cls, drops * 2, 4000 + len(cases)))
    cases.append(record_case('registros antigos normalizados', 'barbaro', [dict(op='legacy', record=r) for r in LEGACY], 4100))
    cases.append(record_case('qualidade até +25 com material especial', 'mago', [
        dict(op='gold', n=10 ** 7), dict(op='level', n=70), dict(op='drop', kind='weapon', floor=30, level=60, cls='mago', keep=True)]
        + [dict(op='forge', action='upgrade', index=0)] * 24 + [dict(op='material', id='material:essencia_primordial', n=1)]
        + [dict(op='forge', action='upgrade', index=0)] * 3 + [dict(op='enchant', index=0, pick=1), dict(op='enchant', index=0, pick=0)], 4101))
    cases += scenarios()
    for c in range(70):
        cls = CLASSES[c % len(CLASSES)]
        cases.append(record_case(f'aleatório {c} ({cls})', cls, random_ops(rng, cls, 50), 5000 + c))
    out = HERE / 'tests' / 'parity' / 'items_cases.json'
    payload = {'source': 'umbra-ashes-below equipment/inventory on the composed engine', 'families': FAMILIES, 'necro_keys': NECRO_KEYS, 'cases': cases}
    out.write_bytes(json.dumps(payload, ensure_ascii=False, separators=(',', ':')).encode('utf-8'))
    print(f'{len(cases)} casos, {sum(len(c["steps"]) for c in cases)} operações -> {out} ({out.stat().st_size // 1024} KB); famílias {FAMILIES}')


if __name__ == '__main__':
    main()
