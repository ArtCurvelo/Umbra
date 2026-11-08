namespace Umbra.Domain.Combat;

/// <summary>A hero as the shared resources see it: Fortificação reserve, tile position and the class resource.</summary>
public interface IHero : ICombatant
{
    double Fortify { get; set; }
    int FortifyTimer { get; set; }
    double FortifyCarry { get; set; }
    int X { get; }
    int Y { get; }
    /// <summary>The hero's own rule (skill-tree bonus, class passives); returns what was gained.</summary>
    int GainResource(double amount, string source);
}

/// <summary>An Orbe de Sangue on the floor. Kept by the map, so it stays behind on a map change.</summary>
public sealed class BloodOrb
{
    public int X, Y, Ticks;
    public double Heal, Resource, Fortify;
}

/// <summary>
/// Shared combat resources — C# port of UMBRA's game/combat_system/resources.py (NECROMANCER REBORN sec. 4; numbers
/// PROVISIONAL in UMBRA). Parity with Python: tests/Umbra.Domain.Tests/ParityResourcesTests.cs.
/// Barreira em camadas: AddBarrier adds a layer with its own duration; all layers together never pass the max life, and
/// Status.Absorb spends the layer that expires first (Status.Apply("barrier") keeps the 0.16 single merged barrier).
/// Fortificação: a reserve up to the max life that heals FortifyHeal of the max life per second while hurt; lost
/// FortifySeconds after the last gain. Orbes de Sangue: the first living hero within one tile drinks it.
/// Stacks (Ferocidade, Determinação, Golpe Brutal) live in Statuses with magnitude = number of stacks.
/// Systems not ported yet arrive through hooks: the necromancer item affixes (AffixShare, step 5.6) and the Grimório
/// sacrifice bonuses (GrimoireBonus, step 5.8). Drawing the orbs is presentation (Godot).
/// </summary>
public static class Resources
{
    public const double FortifyHeal = .03, OrbSeconds = 10, OrbHeal = .06, OrbResource = 5;
    public const int FortifySeconds = 8, OrbLimit = 24;
    /// <summary>kind: (max stacks, seconds, bonus per stack).</summary>
    public static readonly IReadOnlyDictionary<string, (int Max, int Seconds, double PerStack)> Stacks =
        new Dictionary<string, (int, int, double)> { ["ferocity"] = (4, 4, .05), ["determination"] = (5, 6, .04), ["brutal"] = (4, 4, 0) };

    /// <summary>necro_affixes.share: an item affix as a fraction (12 -> 0.12). Keys used here: "barrier", "fortify".</summary>
    public static Func<ICombatant, string, double>? AffixShare;
    /// <summary>grimoire.bonus: the hero bonus of the active sacrifices. Keys used here: "attack_speed", "armour".</summary>
    public static Func<ICombatant, string, double>? GrimoireBonus;

    private static double Affix(ICombatant e, string key) => AffixShare?.Invoke(e, key) ?? 0;
    private static double Grimoire(ICombatant e, string key) => GrimoireBonus?.Invoke(e, key) ?? 0;

    // ------------------------------------------------------------------ Barreira em camadas
    public static double BarrierTotal(ICombatant e) => e.Statuses?.Get("barrier")?.Sum(r => r.Magnitude) ?? 0;

    /// <summary>A new layer, trimmed so that all layers stay within the max life. Returns the points actually added.</summary>
    public static double AddBarrier(ICombatant e, double seconds, double points)
    {
        points *= 1 + Affix(e, "barrier");
        double room = Math.Max(0, (e.MaxHealth != 0 ? e.MaxHealth : (int)Math.Truncate(points)) - BarrierTotal(e));
        points = Math.Min(Math.Truncate(points), room);
        if (points <= 0 || seconds <= 0) return 0;
        (e.Statuses ??= new StatusMap()).GetOrAdd("barrier").Add(new StatusRow { Ticks = (int)Math.Truncate(seconds * 60), Magnitude = points });
        return points;
    }

    // ------------------------------------------------------------------ Fortificação
    public static double Fortification(IHero hero) => hero.Fortify;

    public static double Fortify(IHero hero, double points)
    {
        if (points <= 0) return 0;
        points *= 1 + Affix(hero, "fortify");
        double before = hero.Fortify;
        hero.Fortify = Math.Min(hero.MaxHealth, before + points); hero.FortifyTimer = FortifySeconds * 60;
        return hero.Fortify - before;
    }

    /// <summary>Hero step (every hero, co-op guests included): Fortificação heals and expires.</summary>
    public static void Tick(IHero hero)
    {
        double reserve = hero.Fortify;
        if (reserve <= 0) return;
        hero.FortifyTimer--;
        if (hero.FortifyTimer <= 0 || !hero.Alive) { hero.Fortify = 0; return; }
        if (hero.Health < hero.MaxHealth)
        {
            double heal = Math.Min(reserve, hero.MaxHealth * FortifyHeal / 60);
            double carry = hero.FortifyCarry + heal; int whole = (int)Math.Truncate(carry); hero.FortifyCarry = carry - whole;
            hero.Heal(Math.Min(hero.MaxHealth, hero.Health + whole) - hero.Health); hero.Fortify = reserve - heal;
        }
    }

    // ------------------------------------------------------------------ stacks
    public static int AddStack(ICombatant e, string kind, int amount = 1, int? limit = null)
    {
        var (max, seconds, _) = Stacks[kind]; int top = limit ?? max;
        var rows = (e.Statuses ??= new StatusMap()).GetOrAdd(kind);
        if (rows.Count > 0) { var row = rows[0]; row.Magnitude = Math.Min(top, row.Magnitude + amount); row.Ticks = seconds * 60; }
        else rows.Add(new StatusRow { Ticks = seconds * 60, Magnitude = Math.Min(top, amount) });
        return (int)Math.Truncate(rows[0].Magnitude);
    }

    public static int StackCount(ICombatant e, string kind) => (int)Math.Truncate(Status.Value(e, kind));

    public static double AttackSpeed(ICombatant e) => StackCount(e, "ferocity") * Stacks["ferocity"].PerStack + Grimoire(e, "attack_speed");

    public static double Armour(ICombatant e) => StackCount(e, "determination") * Stacks["determination"].PerStack + Grimoire(e, "armour");

    // ------------------------------------------------------------------ Orbes de Sangue
    public static void DropOrb(List<BloodOrb>? orbs, double x, double y, double heal = OrbHeal, double resource = OrbResource,
                               double seconds = OrbSeconds, double fortify = 0)
    {
        if (orbs == null) return;
        orbs.Add(new BloodOrb { X = (int)Math.Truncate(x), Y = (int)Math.Truncate(y), Ticks = (int)Math.Truncate(seconds * 60), Heal = heal, Resource = resource, Fortify = fortify });
        if (orbs.Count > OrbLimit) orbs.RemoveRange(0, orbs.Count - OrbLimit);
    }

    /// <summary>One step of the orbs on a map; <paramref name="heroes"/> is every hero, player 1 first.</summary>
    public static void TickOrbs(List<BloodOrb>? orbs, IEnumerable<IHero> heroes)
    {
        if (orbs is not { Count: > 0 }) return;
        var living = heroes.Where(h => h.Alive).ToList();
        foreach (var orb in orbs.ToList())
        {
            orb.Ticks--;
            var drinker = living.FirstOrDefault(h => Math.Max(Math.Abs(h.X - orb.X), Math.Abs(h.Y - orb.Y)) <= 1);
            if (drinker != null)
            {
                drinker.Heal(Math.Min(drinker.MaxHealth, drinker.Health + (int)Math.Max(1, PyMath.Round(drinker.MaxHealth * orb.Heal))) - drinker.Health);
                drinker.GainResource(orb.Resource, "blood_orb"); orbs.Remove(orb);
                if (orb.Fortify != 0) Fortify(drinker, drinker.MaxHealth * orb.Fortify);
            }
            else if (orb.Ticks <= 0) orbs.Remove(orb);
        }
    }
}
