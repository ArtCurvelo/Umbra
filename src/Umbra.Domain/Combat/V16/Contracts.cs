using Umbra.Domain.Combat;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;

namespace Umbra.Domain.Combat.V16;

/// <summary>Anything that stands on a tile and can be hit by a skill (enemies; the hero too).</summary>
public interface ITarget : ICombatant
{
    int X { get; set; }
    int Y { get; set; }
    (int X, int Y) Position => (X, Y);
    /// <summary>(0, 0) when it faces nowhere (the "behind" condition is then false).</summary>
    (int X, int Y) AttackDirection { get; }
    bool V16Dead { get; set; }
    int NecroFrozenTicks => 0;
    int NecroSlowTicks => 0;
    int NecroPrisonTicks => 0;
}

/// <summary>The hero as the 0.16 skill engine sees it. Resource rules (spend/gain) and the attack damage are the hero's own
/// (class, tree and items); the engine only calls them.</summary>
public interface ISkillUser : IHero, ITarget
{
    new int Health { get; set; }
    new int X { get; set; }
    new int Y { get; set; }
    int Resource { get; set; }
    bool SpendResource(int amount);
    /// <summary>Base damage of riders and zones (riders.base_damage reads player.attack).</summary>
    double Attack { get; }
    double GetAttackDamage(ITarget? enemy, bool skill);
    (int X, int Y)? AimTarget { get; }
    /// <summary>thorns_element chosen by a skill (null = never set; damage then answers as physical).</summary>
    string? ThornsChoice { get; set; }
    int DashCooldown { get; }
    int WardBlocked { get; }
    bool HasShield { get; }
    int ActiveMainSlot { get; }
    void RecalculateStats();
    SkillState Skills { get; }
    /// <summary>For the class passives (5.5): the class and the tree that holds their ranks (null = no passives).</summary>
    string PlayerClass { get; }
    SkillTree? Tree { get; }
}

/// <summary>Per-hero bookkeeping of the 0.16 engine (player.skill_cooldowns, skill_charges and the v16_* fields).</summary>
public sealed class SkillState
{
    public Dictionary<string, int> Cooldowns { get; } = new();
    public int Cooldown;
    public Dictionary<string, int> Charges { get; } = new();
    public (PyDict Spec, int Charges)? Imbue;
    public (object? Target, int Count) Ramp = (null, 0);
    public bool HasRamp;
    public Dictionary<(string, string?, string?, double, object?), int> Counters { get; } = new();
    public Dictionary<string, (int Count, int Ticks)> Stacks { get; } = new();
    public ((int X, int Y) Origin, int Ticks)? Return;
    public SkillWorld? World;
    public bool Hold;
    public int LastDash;
    public double WardGain;
    public string? LastSkillId, LastSkillAnim;
    public List<(int X, int Y)?> TargetPositions { get; } = new();
    public (int X, int Y)? Direction;
    // class passives (skills/v16/passives.py): temporary bonuses and their counters
    /// <summary>v16_pbuffs: name -> (ticks left, value), insertion-ordered.</summary>
    public List<KeyValuePair<string, (int Ticks, double Value)>> PassiveBuffs { get; } = new();
    public int Streak, StreakTicks, PactTicks, BastionTicks;
    public string? LastFamily;
    public bool HasLastFamily;
    /// <summary>v16_elements: (element, time in ms) of the recent casts (Convergência Arcana).</summary>
    public List<(string? Element, long Time)> Elements { get; set; } = new();
    public string? Affinity;
}

/// <summary>
/// Systems the engine calls that belong to later steps: class passives (5.5), tags, Grimório and item affixes (5.6),
/// Warlock and Necromancer rules (5.8). Every member has the neutral default, which is what UMBRA's code returns for a
/// hero without passives, tags, affixes, Grimório or those classes (asserted by tools/parity/skills_parity.py).
/// </summary>
public interface ISkillHooks
{
    double PassiveDamage(Ctx ctx, ITarget? enemy) => 1;
    double PassiveValue(ISkillUser p, string effect) => 0;
    double DurationFactor(ISkillUser p, string kind) => 1;
    double DotFactor(ISkillUser p, string kind) => 1;
    double HealFactor(ISkillUser p) => 1;
    double BarrierFactor(ISkillUser p) => 1;
    double HealthCostFactor(ISkillUser p) => 1;
    (int Jumps, double PerJump) ChainBonus(ISkillUser p) => (0, 0);
    void PassiveOnHit(Ctx ctx, ITarget enemy, int dealt) { }
    void PassiveOnCast(Ctx ctx) { }
    void PassiveOnKills(ISkillUser p, int count) { }
    void PassiveOnDodge(ISkillUser p) { }
    void PassiveTick(ISkillUser p) { }
    double TagDamage(ISkillUser p, PyDict spec) => 1;
    double Grimoire(ISkillUser p, string key) => 0;
    double Affix(ISkillUser p, string key) => 0;
    bool WarlockAllowed(ISkillUser p, PyDict spec) => true;
    double WarlockDamage(ISkillUser p, PyDict spec, ITarget? enemy) => 1;
    void WarlockBroke(ISkillUser p) { }
    void WarlockOnHit(Ctx ctx, ITarget enemy, int dealt) { }
    void WarlockOnCast(Ctx ctx) { }
    void WarlockOnKills(ISkillUser p, int count) { }
    double NecroDamage(Ctx ctx, ITarget? enemy) => 1;
    bool NecroBeforeCast(Ctx ctx) => true;
    void NecroAfterCast(Ctx ctx) { }
    void NecroOnHit(Ctx ctx, ITarget enemy, int dealt) { }
    void NecroOnKills(ISkillUser p, IReadOnlyList<ITarget> dead) { }
    void NecroZoneTick(Zone zone) { }
    void NecroZoneEnd(Zone zone) { }
    List<ITarget> NecroZoneTargets(Zone zone, List<ITarget> inside) => inside;
    /// <summary>necro3.condition: null when the condition is not one of the Necromancer's.</summary>
    bool? NecroCondition(Ctx ctx, ITarget? enemy, string when) => null;
    /// <summary>necro3.act: false when the action is not one of the Necromancer's.</summary>
    bool NecroAct(Ctx ctx, PyDict rider, ITarget? enemy, int dealt) => false;
    /// <summary>Summons, corpses and the Jaula (shapes summon/corpse/cage; riders swap/ally_intangible): step 5.8.</summary>
    Outcome? Shape(Ctx ctx, Func<bool> commit) => null;
    void Allies(Ctx ctx, PyDict rider) { }
}

public sealed class NeutralHooks : ISkillHooks { }

/// <summary>What a shape answers: failed (nothing spent), done, an aura switched off, or a free second use.</summary>
public enum Outcome { Fail, Done, Off, Free }

/// <summary>Shared services of the engine: the hooks and the random generator (Python's global `random`).</summary>
public static class SkillEnv
{
    public static ISkillHooks Hooks = new NeutralHooks();
    public static IRandom Rng = new PyRandom(0);
    /// <summary>pygame.time.get_ticks (ms): only Convergência Arcana reads the clock.</summary>
    public static Func<long> Clock = () => 0;
}
