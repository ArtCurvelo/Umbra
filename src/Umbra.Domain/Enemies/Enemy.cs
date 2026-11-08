using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;

namespace Umbra.Domain.Enemies;

/// <summary>The hero as enemies see it (its own rules — defense, shields, life — are the hero's, step 5.8).</summary>
public interface IFoeTarget : ICombatant
{
    int X { get; set; }
    int Y { get; set; }
    (int X, int Y) Position => (X, Y);
    int WardBlocked { get; set; }
    double WardGain { get; }
    int GainResource(double amount, string source);
}

/// <summary>Systems enemies call that belong to later steps (the Necromancer's structures and Sentença: 5.8).</summary>
public interface IFoeHooks
{
    IEnumerable<(int X, int Y)> Structures(IMap map) => Array.Empty<(int, int)>();
    bool StrikeStructure(IMap map, Enemy enemy, (int X, int Y) cell, int damage) => false;
    void Sentence(Enemy enemy) { }
}

public sealed class NeutralFoeHooks : IFoeHooks { }

/// <summary>Enemy tables (game/data/umbra/foes.json).</summary>
public static class FoeTables
{
    public static PyDict Root { get; private set; } = new();
    public static IFoeHooks Hooks = new NeutralFoeHooks();

    public static void Load(string dataRoot) =>
        Root = (PyDict)Py.FromJson(JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "foes.json"), Encoding.UTF8)).RootElement)!;

    public static PyDict Types => Root.Sub("ENEMY_TYPES")!;
    public static PyDict Hardcore => Root.Sub("HARDCORE")!;
    public static PyDict Elites => Root.Sub("ELITES")!;
    public static int DormantRange => (int)Root.Num("DORMANT_RANGE");

    /// <summary>boss_catalog.get_boss_type / get_boss_profile.</summary>
    public static string BossType(int floor)
    {
        var order = ItemsOf(Root.Get("BOSS_ORDER"));
        int index = (int)(((Math.Max(1, floor) / 5 - 1) % order.Count + order.Count) % order.Count);
        return order[index];
    }

    private static List<string> ItemsOf(object? list) => ((List<object?>)list!).Cast<string>().ToList();

    public static PyDict BossProfile(int floor)
    {
        var profile = new PyDict(Root.Sub("BOSS_ABILITIES")!.Sub(BossType(floor))!);
        if (floor > 20)
        {
            int cycle = (floor - 1) / 20; double health = 1 + cycle * 0.55, attack = 1 + cycle * 0.32;
            profile["health"] = (long)(Py.Num(profile["health"]) * health); profile["attack"] = (long)(Py.Num(profile["attack"]) * attack);
            profile["defense"] = (long)Py.Num(profile["defense"]) + cycle * 3;
            profile["reward_xp"] = (long)(Py.Num(profile["reward_xp"]) * health); profile["reward_gold"] = (long)(Py.Num(profile["reward_gold"]) * health);
        }
        return profile;
    }

    /// <summary>elements.enemy_element.</summary>
    public static string Element(Enemy e)
    {
        if (e.EnemyType is "cultista" or "bruxa" or "acólito" or "espectro" || e.Name.Contains("Oráculo") || e.Name.Contains("Sem Sol")) return "shadow";
        if (e.EnemyType == "parasita" || e.Name.Contains("Vésper")) return "poison";
        if (e.EnemyType == "sentinela_runica") return "lightning";
        if (e.Name.Contains("Dragão")) return "fire";
        return "physical";
    }
}

/// <summary>
/// An enemy — port of game/entities/enemies.Enemy as UMBRA's engine composes it: Update runs the shared-states layer
/// (combat_system/necro_status: states tick, stun/freeze/prison hold it, slow and Gelidez lengthen its steps, taunt picks
/// the target), then the sanctuary guard (world/sanctuary: never ends a step inside a city), then the enemy's own update
/// (boss controller or combat_system/ai). Sound and the floating damage numbers are presentation.
/// </summary>
public sealed class Enemy : ITarget
{
    public string Name { get; }
    public string EnemyType { get; }
    public bool Boss { get; }
    public bool Summoned { get; }
    public bool SummonRequested;
    public BossController? Controller;
    public int X { get; set; }
    public int Y { get; set; }
    public (int X, int Y) Position => (X, Y);
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int BaseMoveCooldown, DetectionRange, WindupDuration, AttackRange;
    public bool IsAlive = true;
    public bool Alive => IsAlive;
    public int MoveCooldown, AttackCooldown, Windup, MemoryFrames, StaggerFrames, StuckFrames, LastDamageAmount;
    public string? CurrentAttack, LastAttackKind;
    public (int X, int Y)? AttackTarget, LastKnownPlayer, LastAttackTargetPosition;
    public (int X, int Y) AttackDirection { get; set; } = (0, 1);
    public (int X, int Y) AttackOrigin, PreviousPosition;
    public string AiState = "idle";
    public IMap? AttackMap;
    public string Rank = "normal";
    public List<string> EliteAffixes = new();
    public int NecroSlowTicks, NecroFrozenTicks, NecroVulnerableTicks, NecroPrisonTicks;
    public bool V16Dead { get; set; }

    // ICombatant
    public StatusMap? Statuses { get; set; }
    public int CcPressure { get; set; }
    public int CcPressureTimer { get; set; }
    public double AbsorbedTotal { get; set; }
    public int? AttackWindup { get => Windup; set => Windup = value ?? 0; }
    public IReadOnlyDictionary<string, double>? Resistances => null;
    string? ICombatant.EnemyType => EnemyType;
    public IReadOnlyList<GearRecord> Gear => Array.Empty<GearRecord>();
    int ICombatant.NecroVulnerableTicks => NecroVulnerableTicks;
    int ITarget.NecroFrozenTicks => NecroFrozenTicks;
    int ITarget.NecroSlowTicks => NecroSlowTicks;
    int ITarget.NecroPrisonTicks => NecroPrisonTicks;
    public string ThornsElement => "physical";
    public void TakeDamage(int amount) => Damage(amount);
    public void Heal(int amount) => Health += amount;

    public Enemy((int X, int Y) position, int floor = 1, bool boss = false, string? enemyType = null, bool summoned = false)
    {
        (X, Y) = position; Boss = boss; Summoned = summoned;
        EnemyType = boss ? "boss" : enemyType ?? "sombra";
        long health, attack;
        if (boss)
        {
            var profile = FoeTables.BossProfile(floor);
            Name = profile.Str("name"); health = (long)profile.Num("health"); attack = (long)profile.Num("attack"); Defense = (int)profile.Num("defense");
            BaseMoveCooldown = 20; DetectionRange = 12; WindupDuration = 35; AttackRange = 1;
        }
        else
        {
            var stats = FoeTables.Types.Sub(EnemyType)!;
            Name = stats.Str("name"); health = (long)stats.Num("health"); attack = (long)stats.Num("attack"); Defense = (int)stats.Num("defense");
            BaseMoveCooldown = (int)stats.Num("move_cooldown"); DetectionRange = (int)stats.Num("detection"); WindupDuration = (int)stats.Num("windup");
            AttackRange = (int)stats.Num("attack_range", 1);
        }
        var h = FoeTables.Hardcore;
        double healthMultiplier = h.Num("HARDCORE_ENEMY_HEALTH_BASE") + Math.Max(0, floor - 1) * h.Num("HARDCORE_ENEMY_HEALTH_PER_FLOOR");
        double damageMultiplier = h.Num("HARDCORE_ENEMY_DAMAGE_BASE") + Math.Max(0, floor - 1) * h.Num("HARDCORE_ENEMY_DAMAGE_PER_FLOOR");
        if (!boss)
        {
            health = (long)(health * healthMultiplier);
            attack = Math.Max(2, (long)(attack * damageMultiplier));
            Defense += Math.Max(0, (floor - 1) / 5);
        }
        if (summoned) { health = Math.Max(10, (long)(health * 0.65)); attack = Math.Max(2, (long)(attack * 0.75)); }
        MaxHealth = (int)health; Health = MaxHealth; Attack = Math.Max(1, (int)attack);
        AttackOrigin = Position; PreviousPosition = Position;
        if (boss) Controller = new BossController(this, floor);
    }

    public int Phase => Controller?.Phase ?? 1;

    /// <summary>Enemy.take_damage: defense, then (bosses) mitigation by phase, a cap per hit and the bone armor.</summary>
    public int Damage(int damage)
    {
        if (!IsAlive) return 0;
        int before = Health; int final = Math.Max(1, damage - Defense);
        if (Controller != null)
        {
            int phase = Controller.Phase; double mitigation = phase == 1 ? 0.12 : 0.08;
            final = Math.Max(1, (int)(final * (1 - mitigation)));
            int cap = Math.Max(1, (int)(MaxHealth * (phase == 1 ? 0.22 : 0.26)));
            final = Math.Min(final, cap);
            final = Controller.AbsorbDamage(final);
        }
        Health = Math.Max(0, Health - final);
        LastDamageAmount = before - Health;
        if (Health > 0 && LastDamageAmount > 0)
        {
            StaggerFrames = Math.Max(StaggerFrames, Boss ? 2 : 4);
            if (!Boss) { Windup = 0; CurrentAttack = null; AttackTarget = null; AiState = "staggered"; }
        }
        Controller?.CheckPhase();
        if (Health <= 0) { IsAlive = false; AiState = "dead"; Windup = 0; CurrentAttack = null; }
        return LastDamageAmount;
    }

    public int DistanceTo(IFoeTarget p) => Math.Abs(p.X - X) + Math.Abs(p.Y - Y);

    // ------------------------------------------------------------------ update (composed)
    public void Update(IFoeTarget player, IMap map, IRandom rng)
    {
        if (!IsAlive) return;
        // necro_status layer
        var states = Statuses;
        if (states is { Count: > 0 } || CcPressureTimer != 0)
        {
            Status.TickOnce(this);
            if (!IsAlive) return;
        }
        bool stunned = states is { Count: > 0 } && (Status.Has(this, "stun") || Status.Has(this, "freeze"));
        bool blocked = NecroFrozenTicks > 0 || NecroPrisonTicks > 0 || stunned;
        bool slow = NecroSlowTicks > 0;
        NecroSlowTicks = Math.Max(0, NecroSlowTicks - 1); NecroFrozenTicks = Math.Max(0, NecroFrozenTicks - 1);
        NecroVulnerableTicks = Math.Max(0, NecroVulnerableTicks - 1); NecroPrisonTicks = Math.Max(0, NecroPrisonTicks - 1);
        if (blocked) { Windup = 0; AiState = NecroFrozenTicks > 0 ? "frozen" : !stunned ? "bone_prison" : "stunned"; return; }
        int @base = BaseMoveCooldown;
        if (slow) BaseMoveCooldown = (int)Math.Max(1, PyMath.Round(@base / .70));
        if (states is { Count: > 0 } && (Status.Has(this, "slow") || Status.Has(this, "frost")))
            BaseMoveCooldown = (int)Math.Max(1, PyMath.Round(BaseMoveCooldown * Status.MoveFactor(this)));
        IFoeTarget target = player;
        if (states is { Count: > 0 } && Status.Has(this, "taunt") && Status.TauntTarget(this) is IFoeTarget taunted) target = taunted;
        try { SanctuaryUpdate(target, map, rng); }
        finally { BaseMoveCooldown = @base; }
    }

    private void SanctuaryUpdate(IFoeTarget player, IMap map, IRandom rng)
    {
        var prior = Position;
        OwnUpdate(player, map, rng);
        if (map.Sanctuary(X, Y))
        {
            if (!map.Sanctuary(prior.X, prior.Y)) (X, Y) = prior;
            else Navigation.Evacuate(map, this);
        }
    }

    private void OwnUpdate(IFoeTarget player, IMap map, IRandom rng)
    {
        if (!IsAlive) return;
        AttackMap = map;
        if (Controller != null)
        {
            if (StaggerFrames > 0) { StaggerFrames--; AiState = "staggered"; return; }
            Controller.Update(player, map, rng);
            SummonRequested = Controller.SummonRequested;
            return;
        }
        EnemyAI.Update(this, player, map);
    }

    /// <summary>prepare_attack with the sanctuary guard: nobody attacks a hero standing in a city.</summary>
    public bool PrepareAttack(string kind, IFoeTarget? player = null)
    {
        if (player != null && AttackMap != null && AttackMap.Sanctuary(player.X, player.Y)) { Windup = 0; CurrentAttack = null; return false; }
        CurrentAttack = kind; Windup = WindupDuration;
        AttackTarget = player?.Position; AttackOrigin = Position; LastAttackKind = kind;
        if (player != null)
        {
            int dx = player.X - X, dy = player.Y - Y;
            if (Math.Abs(dx) > Math.Abs(dy) && dx != 0) AttackDirection = (dx > 0 ? 1 : -1, 0);
            else if (dy != 0) AttackDirection = (0, dy > 0 ? 1 : -1);
            else if (dx != 0) AttackDirection = (dx > 0 ? 1 : -1, 0);
        }
        AiState = "windup";
        return true;
    }

    public bool PerformAttack(IFoeTarget player)
    {
        if (!player.Alive) { CurrentAttack = null; return false; }
        FoeTables.Hooks.Sentence(this);
        var origin = AttackOrigin; var target = AttackTarget; var kind = CurrentAttack; var map = AttackMap;
        LastAttackKind = kind; LastAttackTargetPosition = target;
        bool hit;
        if (target is not { } t) hit = false;
        else
        {
            int distance = Math.Abs(t.X - origin.X) + Math.Abs(t.Y - origin.Y);
            bool marked = player.Position == t, clear = map != null && Geo.LineOfSight(map, origin, t);
            hit = kind == "ranged" ? marked && 2 <= distance && distance <= AttackRange && clear
                : kind == "normal" && marked && distance == 1 && clear;
        }
        if (hit && Statuses is { Count: > 0 } && Status.Has(this, "blind")) hit = false;
        if (hit && kind == "ranged" && player.Statuses?.Get("ward") is { Count: > 0 } ward)
        {
            hit = false;
            long reflected = PyMath.Round(Attack * ward.Max(r => r.Magnitude));
            if (reflected > 0) Elements.Deal(this, reflected, "holy", player, "ward_reflect");
            player.WardBlocked++;
            if (player.WardGain != 0) player.GainResource(player.WardGain, "ward");
        }
        if (hit)
        {
            Elements.Deal(player, Attack, FoeTables.Element(this), this, "enemy_attack");
            if (kind == "normal" && EnemyType == "parasita") Health = Math.Min(MaxHealth, Health + 3);
        }
        AttackCooldown = kind == "ranged" ? 65 : 45;
        CurrentAttack = null; Windup = 0; MoveCooldown = 15;
        AiState = LastKnownPlayer != null ? "chase" : "idle";
        return hit;
    }

    /// <summary>engine.dormant: a far, idle enemy with nothing running on it skips its AI until the hero nears.</summary>
    public static bool Dormant(Enemy e, int px, int py)
    {
        if (e.Boss || Math.Max(Math.Abs(e.X - px), Math.Abs(e.Y - py)) <= FoeTables.DormantRange) return false;
        if (e.AiState is not "idle" || e.Windup != 0 || e.Statuses is { Count: > 0 }) return false;
        return !(e.NecroSlowTicks != 0 || e.NecroFrozenTicks != 0 || e.NecroVulnerableTicks != 0 || e.NecroPrisonTicks != 0);
    }
}

/// <summary>Enemy ranks — game/combat_system/elites.py (numbers PROVISÓRIOS in UMBRA).</summary>
public static class Elites
{
    public static string Rank(Enemy e) => e.Boss ? "boss" : e.Rank == "elite" ? "elite" : "normal";

    public static Enemy Promote(Enemy e)
    {
        if (e.Boss || e.Summoned || Rank(e) == "elite") return e;
        var t = FoeTables.Elites;
        e.Rank = "elite"; e.EliteAffixes = new List<string>();
        e.MaxHealth = (int)Math.Max(1, PyMath.Round(e.MaxHealth * t.Num("HEALTH"))); e.Health = e.MaxHealth;
        e.Attack = (int)Math.Max(1, PyMath.Round(e.Attack * t.Num("DAMAGE")));
        return e;
    }

    public static int Roll(IEnumerable<Enemy> enemies, IRandom rng)
    {
        int count = 0;
        foreach (var e in enemies)
            if (rng.Random() < FoeTables.Elites.Num("CHANCE") && !e.Boss) { Promote(e); count++; }
        return count;
    }

    public static long Xp(Enemy e, long amount) => Rank(e) == "elite" ? PyMath.Round(amount * FoeTables.Elites.Num("XP")) : amount;
    public static long Gold(Enemy e, long amount) => Rank(e) == "elite" ? PyMath.Round(amount * FoeTables.Elites.Num("GOLD")) : amount;
}
