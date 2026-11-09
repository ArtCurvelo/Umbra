using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Data;
using Umbra.Domain.Enemies;
using Umbra.Domain.Items;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Heroes;

/// <summary>Hero tables (game/data/umbra/heroes.json): live class stats after core.balance and the class resources.</summary>
public static class HeroTables
{
    public static PyDict Root { get; private set; } = new();
    public static UmbraData? Data { get; private set; }

    public static void Load(string dataRoot)
    {
        Root = (PyDict)Py.FromJson(JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "heroes.json"), Encoding.UTF8)).RootElement)!;
        Data = UmbraData.Load(dataRoot);
        TreeCatalog.Load(dataRoot); ItemTables.Load(dataRoot); Passives.Use(Data);
        var previous = Resources.AffixShare;                     // item affixes of a real hero (chained: other owners keep theirs)
        Resources.AffixShare = (e, key) => e is Hero h ? h.AffixShare(key) : previous?.Invoke(e, key) ?? 0;
    }

    public static PyDict Stats(string cls) => Root.Sub("CLASSES")!.Sub(cls)!;
    public static PyDict Spec(string cls) => Root.Sub("CLASS_SPECS")!.Sub(cls) ?? Root.Sub("CLASS_SPECS")!.Sub("barbaro")!;
    public static double IraPerLife => Root.Num("IRA_PER_LIFE");

    /// <summary>catalog.get_basic_skill: a Básica of any class (the 0.16 catalogs).</summary>
    /// <summary>catalog.get_skill: a non-basic skill of any class (the 0.16 catalogs, Warlock forms included).</summary>
    public static PyDict? SkillRow(string? id)
    {
        if (id == null || Data == null) return null;
        var row = Data.Skills.Values.SelectMany(c => c.Skills.Concat(c.Extra)).FirstOrDefault(s => s.Id == id && s.Category != "Básica");
        return row == null ? null : (PyDict)Py.FromJson(row.Raw)!;
    }

    public static PyDict? BasicRow(string? id)
    {
        if (id == null || Data == null) return null;
        var row = Data.Skills.Values.SelectMany(c => c.Skills).FirstOrDefault(s => s.Id == id && s.Category == "Básica");
        return row == null ? null : (PyDict)Py.FromJson(row.Raw)!;
    }

    /// <summary>world.difficulty.gold factor (the difficulty tables come with the world, 5.9): Normal = 1.</summary>
    public static Func<double> GoldFactor = () => 1.0;
}

/// <summary>Systems the hero calls that belong to other steps.</summary>
public interface IHeroHooks
{
    /// <summary>necro_specializations.redirect: the Iron Colossus takes part of the damage (Necromancer, 5.8).</summary>
    int Redirect(Hero hero, int damage) => damage;
    /// <summary>resolver.cancel_player_attack (the basic attack, 5.8b).</summary>
    void CancelAttack(Hero hero, string reason) { }
    /// <summary>resolver.update_player_combat beyond its timers (5.8b).</summary>
    void UpdateCombat(Hero hero) { }
}

public sealed class NeutralHeroHooks : IHeroHooks { }

/// <summary>The skill-engine hooks a real hero provides: class passives (5.5) and the item affixes (5.6) of its loadout.
/// Grimório, Warlock and Necromancer rules stay neutral until those classes.</summary>
public class HeroSkillHooks : PassiveHooks, ISkillHooks
{
    double ISkillHooks.Affix(ISkillUser p, string key) => p is Hero h ? h.AffixShare(key) : 0;
}

/// <summary>
/// The hero — port of game/entities/player.Player as UMBRA's engine composes it (core.balance replaces most methods; the
/// equipment, compatibility, legendary and sanctuary layers wrap them). One object serves the skill engine (ISkillUser),
/// the items (IGearOwner), the tree (ITreeOwner), the enemies (IFoeTarget) and the resources (IHero).
/// </summary>
public sealed class Hero : ISkillUser, IGearOwner, ITreeOwner, IFoeTarget
{
    public static IHeroHooks Hooks = new HeroCombat();

    public string Name { get; set; } = "Viajante";
    public string PlayerClass { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public (int X, int Y) Position => (X, Y);
    public (int X, int Y) PreviousPosition;
    public bool IsAlive = true;
    public bool Alive => IsAlive;
    public bool Boss => false;
    public int Level { get; set; } = 1;
    long IGearOwner.Level => Level;
    public long Xp, XpToNext = 100;
    public int BaseAttack, BaseDefense, Attack, Defense;
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public double Mana, ManaRegen;
    public int MaxMana;
    public int Resource { get; set; }
    public int MaxResource;
    public readonly string ResourceType, ResourceLabel;
    public double ResourceRegen, RegenAccumulator;
    public int RegenerationTimer;
    /// <summary>player.skill_cooldown: the global cooldown the 0.16 engine sets and the hero's tick runs down.</summary>
    public int SkillCooldown { get => Skills.Cooldown; set => Skills.Cooldown = value; }
    // basic attack (resolver)
    public PendingAttack? Pending;
    public bool AttackBuffered, SlotAttack;
    public (int X, int Y) BufferDirection = (0, 1);
    public CombatResult? LastResult;
    public IReadOnlyList<Enemy> CombatEnemies = Array.Empty<Enemy>();
    public int DashCooldown { get; set; }
    public int Invulnerability, TemporaryPowerTimer, ShieldPoints, AttackCooldown, ComboIndex, ComboWindow;
    public double TemporaryPower;
    public long Gold { get; set; }
    public List<string> Relics { get; } = new();
    public double IraCarry;
    public (int X, int Y)? MarkedEnemyPosition;
    public double MarkDamageBonus = .22;
    public IMap? ActiveMap;

    // tree and slots
    public SkillTree Tree { get; }
    SkillTree? ISkillUser.Tree => Tree;
    public string? BasicSkillId { get; set; }
    public string? BasicOverride;
    public List<string?> EquippedMainSkills { get; } = new() { null, null, null, null };
    public HashSet<string> UnlockedMainSkills { get; } = new();
    public int ActiveMainSlot { get; set; }
    public SkillState Skills { get; } = new();

    // items
    public Bag Inventory { get; } = new() { Potion = 1, Capacity = 24 };
    public PyDict? Loadout { get; set; }
    public PyDict Equipment { get; } = new() { ["weapon"] = null, ["armor"] = null, ["accessory"] = null };
    public bool CanonicalReady { get; set; }
    public string? ActiveWeaponSkill { get; set; }
    public PyDict Materials { get; } = new();
    public List<PyDict> LostItems { get; } = new();
    public long PotionCapacity => Pages.PotionCap;

    // combat-facing
    public StatusMap? Statuses { get; set; }
    public int CcPressure { get; set; }
    public int CcPressureTimer { get; set; }
    public double AbsorbedTotal { get; set; }
    public int? AttackWindup { get => null; set { } }
    public IReadOnlyDictionary<string, double>? Resistances => null;
    public string? EnemyType => null;
    public int NecroVulnerableTicks => 0;
    public string? ThornsChoice { get; set; }
    public string ThornsElement => ThornsChoice ?? "physical";
    public (int X, int Y) AttackDirection { get; set; } = (0, 1);
    public (int X, int Y)? AimTarget { get; set; }
    public bool V16Dead { get; set; }
    public int WardBlocked { get; set; }
    public double WardGain => Skills.WardGain;
    public double Fortify { get; set; }
    public int FortifyTimer { get; set; }
    public double FortifyCarry { get; set; }
    double ISkillUser.Attack => Attack;
    public bool HasShield => Py.Truthy(Loadout?.Get("shield"));

    public IReadOnlyList<GearRecord> Gear
    {
        get
        {
            var records = Loadout != null && Loadout.Values.Any(Py.Truthy) ? Loadout.Values : Equipment.Values;
            return records.OfType<PyDict>().Select(r => new GearRecord(Floats(r.Sub("resistances")), Floats(r.Sub("element_bonus")))).ToList();
        }
    }

    private static IReadOnlyDictionary<string, double> Floats(PyDict? d) => (d ?? new PyDict()).ToDictionary(kv => kv.Key, kv => Py.Num(kv.Value));

    /// <summary>Player((x, y), cls) with every __init__ layer: base, core.balance, character creation, equipment, hero modes.</summary>
    public Hero((int X, int Y) position, string playerClass)
    {
        (X, Y) = position; PreviousPosition = position; PlayerClass = playerClass;
        var stats = HeroTables.Stats(playerClass); var spec = HeroTables.Spec(playerClass);
        MaxHealth = (int)stats.Num("health"); Health = MaxHealth;
        MaxMana = (int)stats.Num("mana"); Mana = MaxMana; ManaRegen = stats.Num("mana_regen");
        BaseAttack = (int)stats.Num("attack"); BaseDefense = (int)stats.Num("defense"); Attack = BaseAttack; Defense = BaseDefense;
        ResourceType = spec.Str("resource"); ResourceLabel = spec.Str("resource_label");
        MaxResource = (int)spec.Num("resource_max");
        Resource = ResourceType switch { "mana" => MaxResource, "energy" => 35, "essence" => 20, _ => 0 };
        ResourceRegen = spec.Num("resource_regen");
        Tree = new SkillTree(playerClass) { Owner = this };
        RecalculateStats();                                     // composed: no loadout yet, so the base recalculation
        XpToNext = Math.Max(140, XpToNext);                     // core.balance
        Inventory.Capacity = Pages.Capacity;                    // equipment layer: 30 equipment slots
        Loadout = Items.Gear.Empty(this);
    }

    // ------------------------------------------------------------------ stats (composed recalculate_stats)
    public void RecalculateStats()
    {
        if (Loadout == null || !Loadout.Values.Any(Py.Truthy)) CompatRecalculate();
        else EquipmentRecalculate();
        double bonus = Passives.Value(this, "max_health");                         // Raízes Profundas
        if (bonus != 0) MaxHealth = (int)PyMath.Round(MaxHealth * (1 + bonus));
        double penalty = SkillWorld.ResourcePenalty(this);                          // Aura Selada
        if (penalty != 0) MaxResource = (int)Math.Max(10, MaxResource - penalty);
        Health = Math.Min(Health, MaxHealth); Resource = Math.Min(Resource, MaxResource);
    }

    private void CompatRecalculate()
    {
        BalanceRecalculate();
        Attack += (int)Py.Num((Equipment.Get("weapon") as PyDict)?.Get("forge_rank") ?? 0L);
        Defense += (int)Py.Num((Equipment.Get("armor") as PyDict)?.Get("forge_rank") ?? 0L);
    }

    private static long I(PyDict? d, string key, long fallback = 0) => d != null && d.ContainsKey(key) && d[key] != null ? (long)Math.Truncate(Py.Num(d[key])) : fallback;

    private void BalanceRecalculate()
    {
        var weapon = Equipment.Get("weapon") as PyDict; var armor = Equipment.Get("armor") as PyDict; var accessory = Equipment.Get("accessory") as PyDict;
        long weaponLevel = Math.Max(1, I(weapon, "item_level", 1)), armorLevel = Math.Max(1, I(armor, "item_level", 1));
        long weaponBonus = Math.Min(Math.Max(0, I(weapon, "value")), 4 + (long)(weaponLevel * 0.34));
        long armorBonus = Math.Min(Math.Max(0, I(armor, "value")), 2 + (long)(armorLevel * 0.22));
        var acc = accessory?.Sub("stats") ?? new PyDict();
        long flatAttack = (long)Tree.GetEffect("attack") + Math.Min(3, Math.Max(0, I(acc, "attack")));
        double attackPercent = Math.Min(0.12, Tree.GetEffect("attack_percent"));
        Attack = (int)Math.Max(1, (long)((BaseAttack + weaponBonus + flatAttack) * (1 + attackPercent)));
        Defense = (int)Math.Max(0, BaseDefense + armorBonus + (long)Tree.GetEffect("defense") + Math.Min(2, Math.Max(0, I(acc, "defense"))));
        int oldMaxHealth = Math.Max(1, MaxHealth), oldMaxMana = Math.Max(1, MaxMana);
        var stats = HeroTables.Stats(PlayerClass); int levelGain = Math.Max(0, Level - 1);
        double healthBonus = Math.Min(0.12, Tree.GetEffect("health_bonus")), manaBonus = Math.Min(0.12, Tree.GetEffect("mana_bonus"));
        long accHealth = Math.Min(18, Math.Max(0, I(acc, "health"))), accMana = Math.Min(18, Math.Max(0, I(acc, "mana")));
        MaxHealth = (int)((stats.Num("health") + levelGain * 8) * (1 + healthBonus) * (1 + SkillEnv.Hooks.Grimoire(this, "life"))) + (int)accHealth;
        MaxMana = (int)((stats.Num("mana") + levelGain * 3) * (1 + manaBonus)) + (int)accMana;
        ManaRegen = stats.Num("mana_regen");
        var spec = HeroTables.Spec(PlayerClass);
        MaxResource = (int)(spec.Num("resource_max") * (1 + Math.Min(0.30, Tree.GetEffect("resource_bonus"))));
        if (spec.Str("resource") == "mana") { MaxResource = MaxMana; Resource = Math.Min(MaxResource, Math.Max(0, Resource)); Mana = Resource; }
        else { MaxMana = 0; Mana = 0; Resource = Math.Min(MaxResource, Math.Max(0, Resource)); }
        Health = Math.Min(MaxHealth, Health + Math.Max(0, MaxHealth - oldMaxHealth));
        Mana = Math.Min(MaxMana, Mana + Math.Max(0, MaxMana - oldMaxMana));
    }

    /// <summary>equipment/stats.recalculate: the whole loadout on top of the base recalculation.</summary>
    private void EquipmentRecalculate()
    {
        var gear = Items.Gear.Ensure(this);
        int health = Health, oldMaxHealth = MaxHealth, resource = Resource;
        Items.Gear.SyncAliases(this, ActiveWeaponSkill);
        CompatRecalculate();
        string selected = Items.Gear.ActiveKey(this, ActiveWeaponSkill);
        var weapons = Items.Gear.ClassWeapons(PlayerClass);
        var extras = gear.Where(kv => kv.Value is PyDict d && Py.Truthy(d) && kv.Key is not ("chest" or "amulet")
                                      && (!weapons.ContainsKey(kv.Key) || Items.Gear.Support(kv.Key))).Select(kv => (PyDict)kv.Value!).ToList();
        Defense += (int)extras.Sum(r => I(r.Sub("stats"), "defense"));
        long Extra(string key) => extras.Sum(r => I(r.Sub("stats"), key));
        long Affix(string key) => gear.Values.OfType<PyDict>().Where(Py.Truthy).Sum(r => I(r.Sub("affixes"), key) + I(Temper.Bonus(r), key));
        Attack += (int)(Extra("attack") + Affix("attack")); Defense += (int)Affix("defense"); MaxHealth += (int)(Extra("health") + Affix("health"));
        var weapon = gear.Get(selected) as PyDict ?? new PyDict(); var chest = gear.Get("chest") as PyDict ?? new PyDict();
        if (Py.Truthy(weapon) && Equals(weapon.Get("gear_schema"), 60L)) Attack += (int)Math.Max(0, I(weapon, "value") - (4 + (long)(I(weapon, "item_level", 1) * .34)));
        if (Py.Truthy(chest) && Equals(chest.Get("gear_schema"), 60L)) Defense += (int)Math.Max(0, I(chest, "value") - (2 + (long)(I(chest, "item_level", 1) * .22)));
        long mana = Extra("mana") + Affix("mana");
        if (ResourceType == "mana") { MaxMana += (int)mana; MaxResource = MaxMana; } else MaxResource += (int)mana;
        Health = Math.Min(MaxHealth, health + Math.Max(0, MaxHealth - oldMaxHealth));
        Resource = Math.Min(resource, MaxResource);
        if (ResourceType == "mana") Mana = Resource;
    }

    /// <summary>respec.refresh_stats: recalculate, keeping life and resource within the new maximums.</summary>
    public void RefreshStats()
    {
        int health = Health, resource = Resource;
        RecalculateStats();
        Health = Math.Min(health, MaxHealth); Resource = Math.Min(resource, MaxResource);
        if (ResourceType == "mana") Mana = Resource;
    }

    public void CancelAttack(string reason) => Hooks.CancelAttack(this, reason);

    // ------------------------------------------------------------------ resource
    public int GainResource(double amount, string source)
    {
        amount = Math.Max(0, amount);
        double bonus = Math.Min(0.50, Tree.GetEffect("resource_gain"));
        if (source is not ("regeneration" or "failed_summon_refund")) amount *= Passives.ResourceGainFactor(this);
        int gained = (int)PyMath.Round(amount * (1 + bonus));
        Resource = Math.Min(MaxResource, Resource + gained);
        if (ResourceType == "mana") Mana = Resource;
        return gained;
    }

    public bool SpendResource(int amount)
    {
        amount = Math.Max(0, amount);
        if (Resource < amount) return false;
        Resource -= amount;
        if (ResourceType == "mana") Mana = Resource;
        return true;
    }

    // ------------------------------------------------------------------ levels
    public static long NextLevelRequirement(long current, int level)
    {
        level = Math.Max(2, level);
        return 100 + PyMath.Round(42 * (level - 1) + 9 * Math.Pow(level - 1, 1.55));
    }

    /// <summary>core.balance.gain_xp: 58% of the experience, then the original loop.</summary>
    public void GainXp(long amount)
    {
        if (Level >= TreeCatalog.MaxLevel) { Xp = 0; return; }
        long reduced = amount != 0 ? Math.Max(1, (long)(Math.Max(0, amount) * 0.58)) : 0;
        Xp += Math.Max(0, reduced);
        while (Level < TreeCatalog.MaxLevel && Xp >= XpToNext) { Xp -= XpToNext; LevelUp(); }
        if (Level >= TreeCatalog.MaxLevel) Xp = 0;
    }

    public bool LevelUp()
    {
        if (Level >= TreeCatalog.MaxLevel) { Xp = 0; return false; }
        Level++;
        XpToNext = NextLevelRequirement(XpToNext, Level);
        if (Level % 2 == 0) BaseAttack++;
        if (Level % 4 == 0) BaseDefense++;
        RecalculateStats();
        Health = Math.Min(MaxHealth, Health + 12);
        GainResource(12, "level_up");
        Tree.AddPoint(1);
        return true;
    }

    // ------------------------------------------------------------------ damage
    public HashSet<string> Aspects() => Items.Gear.ActiveAspects(this);

    /// <summary>necro_affixes.share: an item affix of the loadout as a fraction (12 -> 0.12).</summary>
    public double AffixShare(string key) => NecroAffixes.Share((Loadout ?? new PyDict()).Values.Cast<PyDict?>(), key);

    /// <summary>get_attack_damage (core.balance + legendary aspects). Always draws one random number (the critical roll).</summary>
    public double GetAttackDamage(ITarget? enemy, bool skill) => AttackDamage(enemy?.Health, enemy?.MaxHealth, enemy?.Position, skill);

    public int AttackDamage(int? enemyHealth, int? enemyMax, (int X, int Y)? enemyPosition, bool skill)
    {
        double damage = Attack * (1 + TemporaryPower);
        if (skill) damage *= 1 + Math.Min(0.18, Tree.GetEffect("skill_damage"));
        else
        {
            var basic = BasicSkill();
            damage *= basic?.Num("multiplier", 1.0) ?? 1.0;
            damage *= 1 + Math.Min(0.12, Tree.GetEffect("basic_damage"));
        }
        bool hasEnemy = enemyHealth != null;
        if (hasEnemy && enemyHealth <= enemyMax * 0.25) damage *= 1 + Math.Min(0.12, Tree.GetEffect("execution"));
        if (hasEnemy && MarkedEnemyPosition == enemyPosition) damage *= 1 + MarkDamageBonus;
        if (hasEnemy && PlayerClass == "ladino")
        {
            var (ex, ey) = enemyPosition!.Value;
            if (Math.Abs(ex - X) + Math.Abs(ey - Y) <= 2) damage *= 1 + Math.Min(0.10, Tree.GetEffect("rogue_mastery"));
        }
        double critical = Tree.GetEffect(skill ? "skill_critical" : "critical");
        if (SkillEnv.Rng.Random() < Math.Min(0.18, critical)) damage *= 1.45 + Math.Min(0.12, Tree.GetEffect("critical_damage"));
        double value = Math.Max(1, (long)damage);
        var a = Aspects();                                                     // legendary aspects (outer layer)
        if (skill) value *= 1 + (a.Contains("resonance") ? .15 : 0) + (a.Contains("radiant_verdict") ? .20 : 0);
        else if (a.Contains("devastation")) value *= 1.15;
        if (hasEnemy && a.Contains("marked_hunt") && enemyPosition == MarkedEnemyPosition) value *= 1.25;
        return (int)Math.Max(1, PyMath.Round(value));
    }

    /// <summary>player.get_basic_skill: the Básica in the attack slot, tuned by its rank and options ({} when none).</summary>
    public PyDict? BasicSkill()
    {
        var row = HeroTables.BasicRow(BasicOverride ?? BasicSkillId);
        return row == null ? null : TreeRules.Resolve(Tree, row);
    }

    /// <summary>take_damage as composed: legendary guards (x0.9), sanctuary (no damage in a city), core.balance (intangible,
    /// Último Fôlego, guard, barrier, shield), the original (damage reduction, x1.38, armour, defense), the Warlock's Ira.</summary>
    public void TakeDamage(int amount) => TakeDamage((double)amount);

    public int? TakeDamage(double amount)
    {
        if (Aspects().Overlaps(new[] { "ancestral_guard", "oath_guard" })) amount = PyMath.Round(amount * .9);
        if (ActiveMap != null && ActiveMap.Sanctuary(X, Y)) return 0;
        if (!IsAlive || Invulnerability > 0) { BaseTakeDamage(amount); return null; }
        if (Status.Has(this, "intangible")) return null;
        if (Status.Has(this, "laststand") && amount >= Health + Status.Value(this, "barrier"))
        {
            Health = 1;
            foreach (var kind in new[] { "laststand", "power", "haste", "swift", "carnage" }) Status.Remove(this, kind);
            return null;
        }
        if (Status.Has(this, "guard")) amount *= 1 - Status.Value(this, "guard");
        int incoming = (int)Math.Max(0, Math.Truncate(Status.Absorb(this, Math.Max(0, (long)Math.Truncate(amount)))));
        int shield = Math.Max(0, ShieldPoints), absorbed = Math.Min(shield, incoming);
        if (absorbed != 0) { ShieldPoints = shield - absorbed; incoming -= absorbed; }
        if (incoming <= 0) return null;
        int before = Health;
        BaseTakeDamage(incoming);
        if (Health < before) WarlockHurt(before - Health);
        return null;
    }

    private void BaseTakeDamage(double damage)
    {
        if (!IsAlive || Invulnerability > 0) return;
        double reduction = Math.Min(0.25, Tree.GetEffect("damage_reduction"));
        long incoming = (long)(Math.Max(0, damage) * 1.38);
        double defense = Defense * (1 + Resources.Armour(this));
        int final = (int)Math.Max(2, (long)(incoming * (1 - reduction)) - (long)(defense * 0.62));
        final = Hooks.Redirect(this, final);
        Health = Math.Max(0, Health - final);
        if (ResourceType == "faith") GainResource(3, "damage_taken");
        CancelAttack("interrupted");
        if (Health <= 0) IsAlive = false;
    }

    /// <summary>warlock.hurt: the Warlock gains Ira from damage taken, and Furtividade ends.</summary>
    private void WarlockHurt(int lost)
    {
        if (lost <= 0 || PlayerClass != "bruxo") return;
        Status.Remove(this, "stealth");
        double carry = IraCarry + lost / (double)Math.Max(1, MaxHealth) * HeroTables.IraPerLife; int whole = (int)Math.Truncate(carry); IraCarry = carry - whole;
        if (whole != 0) GainResource(whole, "warlock_hurt");
    }

    public void Heal(int amount) => Health += amount;

    // ------------------------------------------------------------------ potions, movement, dash
    public bool UsePotion()
    {
        bool result;
        if (Inventory.Potion <= 0 || Health == MaxHealth) result = false;
        else
        {
            Inventory.Potion--; Health = Math.Min(MaxHealth, Health + (int)(MaxHealth * 0.50)); result = true;
        }
        if (result && Aspects().Contains("bulwark")) ShieldPoints = (int)Math.Max(ShieldPoints, PyMath.Round(MaxHealth * .12));
        return result;
    }

    public IReadOnlyList<ITarget> ActiveEnemies = Array.Empty<ITarget>();
    /// <summary>Continuous position inside the cell (world/locomotion); null until the first step.</summary>
    public Motion? Motion;
    public bool IsMoving;

    public bool CanEnter(int x, int y, IMap map)
    {
        if (!(0 <= x && x < map.Width && 0 <= y && y < map.Height)) return false;
        if (map.Cell(x, y) == 1) return false;
        return !ActiveEnemies.Any(e => e.Alive && e.Position == (x, y));
    }

    public bool Move(int dx, int dy, IMap? map)
    {
        if (!IsAlive || map == null) return false;
        dx = Math.Max(-1, Math.Min(1, dx)); dy = Math.Max(-1, Math.Min(1, dy));
        if (dx == 0 && dy == 0) return false;
        int nx = X + dx, ny = Y + dy;
        if (!CanEnter(nx, ny, map)) return false;
        if (dx != 0 && dy != 0 && (!CanEnter(X + dx, Y, map) || !CanEnter(X, Y + dy, map))) return false;
        CancelAttack("movement");
        PreviousPosition = Position; X = nx; Y = ny;
        return true;
    }

    public bool Dash(int dx, int dy, IMap? map)
    {
        bool moved = false;
        if (IsAlive && DashCooldown <= 0 && !(dx == 0 && dy == 0))
        {
            dx = Math.Max(-1, Math.Min(1, dx)); dy = Math.Max(-1, Math.Min(1, dy));
            int distance = 3 + Math.Min(1, (int)Tree.GetEffect("dash_range"));
            for (int i = 0; i < distance; i++) { if (!Move(dx, dy, map)) break; moved = true; }
            if (moved)
            {
                if (ResourceType == "fury") GainResource(3, "dash");
                DashCooldown = 180; Invulnerability = 30;
            }
        }
        if (moved)
        {
            var a = Aspects();
            DashCooldown = (int)PyMath.Round(DashCooldown * (a.Contains("shadow_step") ? .75 : a.Contains("celerity") ? .8 : 1));
        }
        return moved;
    }

    // ------------------------------------------------------------------ per tick
    /// <summary>player.update (core.balance keeps the original): timers, resource regeneration, cooldowns, temporary
    /// power, tree regeneration, then the basic-attack timers (resolver.update_player_combat).</summary>
    public void Update()
    {
        if (SkillCooldown > 0) SkillCooldown--;
        if (DashCooldown > 0) DashCooldown--;
        if (Invulnerability > 0) Invulnerability--;
        RegenAccumulator += ResourceRegen * Passives.RegenFactor(this);
        if (RegenAccumulator >= 1) { int amount = (int)RegenAccumulator; RegenAccumulator -= amount; GainResource(amount, "regeneration"); }
        if (ResourceType == "mana") Mana = Resource;
        foreach (var id in Skills.Cooldowns.Keys.ToList()) Skills.Cooldowns[id] = Math.Max(0, Skills.Cooldowns[id] - 1);
        if (TemporaryPowerTimer > 0) { TemporaryPowerTimer--; if (TemporaryPowerTimer == 0) TemporaryPower = 0; }
        double regeneration = Tree.GetEffect("regeneration");
        if (regeneration > 0)
        {
            RegenerationTimer++;
            if (RegenerationTimer >= 60) { RegenerationTimer = 0; Health = Math.Min(MaxHealth, Health + (int)regeneration); }
        }
        if (AttackCooldown > 0) AttackCooldown--;
        if (ComboWindow > 0) { ComboWindow--; if (ComboWindow == 0) ComboIndex = 0; }
        Hooks.UpdateCombat(this);
    }

    // ------------------------------------------------------------------ collecting
    /// <summary>collect_item: equipment goes to the bag (equipment layer); the rest as the original.</summary>
    public bool Collect(string kind, PyDict data)
    {
        if (kind is "weapon" or "armor" or "accessory") return Inventory.Add(data);
        switch (kind)
        {
            case "potion": return Pages.AddPotions(this, 1) > 0;
            case "power": BaseAttack += (int)Py.Num(data["value"]); Relics.Add(data.Str("name")); RecalculateStats(); return true;
            case "protection": BaseDefense += (int)Py.Num(data["value"]); Relics.Add(data.Str("name")); RecalculateStats(); return true;
            case "gold": Gold += PyMath.Round(Py.Num(data["value"]) * HeroTables.GoldFactor()); return true;
            case "resource_orb": GainResource(data.Num("value", 12), "orb"); return true;
            case "relic":
            {
                string name = data.Str("name", "Relíquia Antiga");
                if (Relics.Contains(name)) { Gold += 35; return true; }
                Relics.Add(name); var stats = data.Sub("stats") ?? new PyDict();
                BaseAttack += (int)Math.Truncate(stats.Num("attack", 2)); BaseDefense += (int)Math.Truncate(stats.Num("defense", 1));
                RecalculateStats(); return true;
            }
        }
        return false;
    }
}
