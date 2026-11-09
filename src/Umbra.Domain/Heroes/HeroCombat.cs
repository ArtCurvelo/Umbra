using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Enemies;
using Umbra.Domain.Items;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Heroes;

/// <summary>The marked tile of a basic attack, locked during the wind-up (resolver.pending_attack).</summary>
public sealed record PendingAttack((int X, int Y) Origin, (int X, int Y) Target, (int X, int Y) Direction, int Reach, int Total, int ComboIndex)
{
    public int Timer { get; set; } = Total;
}

public sealed record CombatResult(bool Hit, int Damage, (int X, int Y)? Target, string Reason);

/// <summary>
/// The hero's basic attack and slot skills — port of game/combat_system/resolver.py (0.16 path), combat_system/skills.use_skill,
/// skills/loadout.attack_slot/basic_from_slot and the composed entry points (Player.attack_enemy and Player.use_skill with the
/// equipment, sanctuary, feedback and legendary layers). The pre-0.16 basic path is unreachable with the 0.16 catalogs on.
/// </summary>
public sealed class HeroCombat : IHeroHooks
{
    public const int WindupFrames = 5, RecoveryFrames = 14, BufferWindow = 5, ComboWindowFrames = 38;
    public static readonly double[] ComboMultipliers = { 1.00, 1.07, 1.15 };

    public static (int X, int Y) Normalize((int X, int Y)? direction)
    {
        if (direction is not { } d) return (0, 1);
        int dx = Math.Max(-1, Math.Min(1, d.X)), dy = Math.Max(-1, Math.Min(1, d.Y));
        return (dx, dy) != (0, 0) ? (dx, dy) : (0, 1);
    }

    private static bool LineIsClear(IMap? map, (int X, int Y) origin, (int X, int Y) target)
    {
        if (map == null) return true;
        if (!(0 <= target.X && target.X < map.Width && 0 <= target.Y && target.Y < map.Height)) return false;
        if (map.Cell(target.X, target.Y) != 0) return false;
        return Geo.LineOfSight(map, origin, target);
    }

    // ------------------------------------------------------------------ IHeroHooks
    public void CancelAttack(Hero hero, string reason)
    {
        var pending = hero.Pending;
        if (pending == null) return;
        hero.Pending = null; hero.AttackBuffered = false;
        hero.AttackCooldown = Math.Max(0, Math.Min(hero.AttackCooldown, 7));
        hero.LastResult = new CombatResult(false, 0, pending.Target, reason);
    }

    public int Redirect(Hero hero, int damage) => damage;

    /// <summary>update_player_combat after its timers: the wind-up resolves, then a buffered press starts the next swing.</summary>
    public void UpdateCombat(Hero hero)
    {
        var pending = hero.Pending;
        if (pending != null)
        {
            pending.Timer = Math.Max(0, pending.Timer - 1);
            if (pending.Timer == 0) { Resolve(hero, pending); hero.Pending = null; }
        }
        if (hero.AttackBuffered && hero.Pending == null && hero.AttackCooldown <= 0)
            Begin(hero, hero.CombatEnemies, hero.ActiveMap, hero.BufferDirection);
    }

    // ------------------------------------------------------------------ the attack button
    /// <summary>Player.attack_enemy as composed: the weapon of the basic skill, the sanctuary guard, then the wind-up.</summary>
    public static bool AttackEnemy(Hero hero, IReadOnlyList<Enemy> enemies)
    {
        hero.ActiveWeaponSkill = hero.BasicSkillId;
        if (Gear.Ensure(hero).Values.Any(Py.Truthy)) hero.RecalculateStats();
        if (hero.ActiveMap != null && hero.ActiveMap.Sanctuary(hero.X, hero.Y)) return false;
        return Begin(hero, enemies, hero.ActiveMap, hero.AttackDirection);
    }

    public static bool Begin(Hero hero, IReadOnlyList<Enemy> enemies, IMap? map, (int X, int Y)? direction)
    {
        if (!hero.Alive) return false;
        if (!hero.SlotAttack) hero.BasicOverride = null;                  // the attack button
        hero.SlotAttack = false;
        if (hero.BasicOverride == null)
        {
            var cast = AttackSlot(hero, enemies, map ?? hero.ActiveMap);
            if (cast != null) return cast.Value;                         // a non-basic skill on the attack button
        }
        if ((hero.BasicOverride ?? hero.BasicSkillId) == null) return false;
        var active = map ?? hero.ActiveMap;
        if (active != null && active.Sanctuary(hero.X, hero.Y)) return false;
        var dir = Normalize(direction ?? hero.AttackDirection);
        hero.CombatEnemies = enemies;
        if (map != null) hero.ActiveMap = map;
        int cooldown = Math.Max(0, hero.AttackCooldown);
        if (hero.Pending != null || cooldown > 0)
        {
            if (cooldown <= BufferWindow) { hero.AttackBuffered = true; hero.BufferDirection = dir; return true; }
            return false;
        }
        var basic = hero.BasicSkill() ?? new PyDict();
        if (basic.T("v16") && basic.Str("shape") == "channel")           // channelled basics
        {
            if (SkillWorld.Channeling(hero) != null) return true;
            return SkillEngine.Cast(hero, basic, enemies, map ?? hero.ActiveMap);
        }
        var origin = hero.Position;
        var weapon = hero.Equipment.Get("weapon") as PyDict ?? new PyDict();
        int reach = (int)Math.Max(1, Math.Min(10, Math.Max(Math.Truncate(weapon.Num("reach", 1)), Math.Truncate(basic.Num("range", 1)))));
        var candidates = new List<(int D2, int Steps, int Index, (int X, int Y) Position, int Dx, int Dy)>();
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (!e.Alive) continue;
            int dx = e.X - origin.X, dy = e.Y - origin.Y, steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps < 1 || steps > Math.Max(8, reach)) continue;
            if (!LineIsClear(map, origin, e.Position)) continue;
            candidates.Add((dx * dx + dy * dy, steps, i, e.Position, dx, dy));
        }
        (int X, int Y) target;
        if (candidates.Count > 0)
        {
            var aim = hero.AimTarget;
            var selected = aim is { } a
                ? Py.MinBy(candidates, r => (r.Position.X - a.X) * (r.Position.X - a.X) + (r.Position.Y - a.Y) * (r.Position.Y - a.Y))
                : Py.MinBy(candidates, r => (r.D2, r.Steps, r.Index));
            dir = (Math.Sign(selected.Dx), Math.Sign(selected.Dy));
            target = selected.Steps <= reach ? selected.Position : (origin.X + dir.X * reach, origin.Y + dir.Y * reach);
        }
        else target = (origin.X + dir.X * reach, origin.Y + dir.Y * reach);
        int combo = hero.ComboWindow <= 0 ? 0 : hero.ComboIndex;
        hero.Pending = new PendingAttack(origin, target, dir, reach, WindupFrames, combo % ComboMultipliers.Length);
        double haste = 1 + Status.Value(hero, "haste") + Resources.AttackSpeed(hero);
        hero.AttackCooldown = Math.Max(6, (int)(RecoveryFrames * basic.Num("recovery", 1.0) / haste));
        hero.AttackBuffered = false; hero.BufferDirection = dir; hero.AttackDirection = dir;
        hero.LastResult = new CombatResult(false, 0, target, "windup");
        return true;
    }

    /// <summary>loadout.attack_slot: a non-basic skill in slot 0 is cast by the attack button (null = slot 0 holds a Básica).</summary>
    public static bool? AttackSlot(Hero hero, IReadOnlyList<Enemy> enemies, IMap? map)
    {
        var id = hero.BasicSkillId;
        if (id == null || HeroTables.BasicRow(id) != null) return null;
        var row = HeroTables.SkillRow(id);
        if (row == null) return null;
        var spec = TreeRules.Resolve(hero.Tree, row);
        if (!spec.T("v16")) return false;
        return SkillEngine.Cast(hero, spec, enemies, map);
    }

    public static CombatResult Resolve(Hero hero, PendingAttack pending)
    {
        var map = hero.ActiveMap; var target = pending.Target;
        CombatResult Done(CombatResult r) { hero.LastResult = r; return r; }
        if (map != null && map.Sanctuary(hero.X, hero.Y)) return Done(new CombatResult(false, 0, target, "safe_city"));
        if (!LineIsClear(map, pending.Origin, target)) return Done(new CombatResult(false, 0, target, "blocked_by_wall"));
        var enemies = hero.CombatEnemies;
        var enemy = enemies.FirstOrDefault(e => e.Alive && e.Position == target);
        if (enemy == null) return Done(new CombatResult(false, 0, target, "empty_or_evaded_tile"));
        int combo = pending.ComboIndex % ComboMultipliers.Length;
        var row = hero.BasicSkill() ?? new PyDict();
        if (!row.T("v16"))
        {
            // The pre-0.16 path, reached in UMBRA only with no Básica at all: a Básica used from a slot leaves its swing
            // pending, the attack button clears basic_override, and slot 0 holds a non-basic skill. With an empty skill row
            // it is plain physical damage (no effect, no proc), then the basic hit, a short stagger and 5 resource.
            if (row.Count > 0) throw new NotSupportedException("pre-0.16 basic skill row");
            int baseDamage = Math.Max(1, hero.AttackDamage(enemy.Health, enemy.MaxHealth, enemy.Position, false));
            int damage = Math.Max(1, (int)(baseDamage * ComboMultipliers[combo]));
            int before = enemy.Health;
            Elements.Deal(enemy, damage, "physical", hero, "");
            int legacyDealt = Math.Max(0, before - enemy.Health);
            if (legacyDealt > 0) BasicHit(hero, enemy, enemies, legacyDealt, map);
            if (enemy.Alive) enemy.StaggerFrames = Math.Max(enemy.StaggerFrames, 4);
            hero.ComboIndex = (combo + 1) % ComboMultipliers.Length; hero.ComboWindow = ComboWindowFrames;
            var legacy = Done(new CombatResult(legacyDealt > 0, legacyDealt, target, legacyDealt > 0 ? "hit" : "armored_or_invulnerable"));
            if (legacyDealt > 0)
            {
                hero.GainResource(5, "basic_attack");
                if (hero.ResourceType == "faith") hero.GainResource(2, "combat_faith");
            }
            return legacy;
        }
        int dealt = SkillEngine.Strike(hero, row, enemy, enemies, map, ComboMultipliers[combo]);
        if (dealt > 0) BasicHit(hero, enemy, enemies, dealt, map);
        hero.ComboIndex = (combo + 1) % ComboMultipliers.Length; hero.ComboWindow = ComboWindowFrames;
        var result = Done(new CombatResult(dealt > 0, dealt, target, dealt > 0 ? "hit" : "armored_or_invulnerable"));
        if (dealt > 0) hero.GainResource((int)Math.Truncate(row.Num("resource_gain", 5)), "basic_attack");
        return result;
    }

    /// <summary>legendary.basic_hit (installed as forge.on_basic_hit): life drain, Foco Rúnico and the echo aspects.</summary>
    public static void BasicHit(Hero hero, Enemy target, IReadOnlyList<Enemy> enemies, int dealt, IMap? map)
    {
        var a = hero.Aspects();
        if (a.Contains("siphon") || a.Contains("soul_feast"))
            hero.Health = Math.Min(hero.MaxHealth, hero.Health + (int)Math.Max(1, PyMath.Round(dealt * (a.Contains("soul_feast") ? .08 : .05))));
        if (a.Contains("focus")) hero.GainResource(3, "combat");
        double ratio = new[] { ("echo", .25), ("quake_echo", .30), ("arcane_conduit", .40), ("stormcaller", .35) }
            .Where(r => a.Contains(r.Item1)).Select(r => r.Item2).DefaultIfEmpty(0).Max();
        if (ratio == 0) return;
        var choices = enemies.Where(e => !ReferenceEquals(e, target) && e.Alive && Math.Max(Math.Abs(e.X - target.X), Math.Abs(e.Y - target.Y)) <= 2
                                         && (map == null || Geo.LineOfSight(map, target.Position, e.Position))).ToList();
        if (choices.Count > 0)
            Py.MinBy(choices, e => (e.X - target.X) * (e.X - target.X) + (e.Y - target.Y) * (e.Y - target.Y)).Damage((int)Math.Max(1, PyMath.Round(dealt * ratio)));
    }

    // ------------------------------------------------------------------ slot skills
    /// <summary>player.get_equipped_main_skill: slots 1..4, tuned (Básicas too).</summary>
    public static PyDict? EquippedSkill(Hero hero, int slot)
    {
        if (slot < 0 || slot >= hero.EquippedMainSkills.Count) return null;
        var id = hero.EquippedMainSkills[slot];
        if (id == null) return null;
        var row = HeroTables.SkillRow(id) ?? HeroTables.BasicRow(id);
        return row == null ? null : TreeRules.Resolve(hero.Tree, row);
    }

    /// <summary>Player.use_skill(enemies, slot) as composed (legendary shield on cast, weapon of the skill, sanctuary,
    /// feedback pre-checks, core.balance, skills.use_skill).</summary>
    public static bool UseSkill(Hero hero, IReadOnlyList<Enemy> enemies, int slot)
    {
        var row = EquippedSkill(hero, slot);
        hero.ActiveWeaponSkill = row?.Str("id");
        if (Gear.Ensure(hero).Values.Any(Py.Truthy)) hero.RecalculateStats();
        bool result = Guarded(hero, enemies, slot);
        if (result && hero.Aspects().Overlaps(new[] { "frost_ward", "living_roots", "grave_guard" }))
            hero.ShieldPoints = (int)Math.Max(hero.ShieldPoints, PyMath.Round(hero.MaxHealth * .08));
        return result;
    }

    private static bool Guarded(Hero hero, IReadOnlyList<Enemy> enemies, int slot)
    {
        if (hero.ActiveMap != null && hero.ActiveMap.Sanctuary(hero.X, hero.Y)) return false;
        var data = EquippedSkill(hero, slot);                                   // feedback.skill_attempt pre-checks
        if (data == null) return false;
        string id = data.Str("id");
        if (data.Num("cooldown", 0) > 0 && hero.Skills.Cooldowns.GetValueOrDefault(id) > 0) return false;
        if (hero.Resource < data.Num("cost")) return false;
        if (!hero.Alive) return false;                                          // skills.use_skill
        if (hero.ActiveMap != null && hero.ActiveMap.Sanctuary(hero.X, hero.Y)) return false;
        var skill = EquippedSkill(hero, slot);
        if (skill == null) return false;
        if (skill.Str("category") == "Básica")
        {
            hero.BasicOverride = skill.Str("id"); hero.SlotAttack = true;       // loadout.basic_from_slot
            return Begin(hero, enemies, hero.ActiveMap, null);
        }
        if (skill.T("v16")) return SkillEngine.Cast(hero, skill, enemies, hero.ActiveMap);
        return false;
    }
}
