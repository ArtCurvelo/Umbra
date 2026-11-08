namespace Umbra.Domain.Combat;

/// <summary>
/// Typed damage and bounded resistances — C# port of UMBRA's game/combat_system/elements.py (8 elements since 0.15.0).
/// Parity with Python: tests/Umbra.Domain.Tests/ParityStatusTests.cs. Left to the systems that own them (later Phase 5
/// steps): class passives in defense_factor (skills.v16.passives.incoming_factor), the kill reward of marked targets
/// (skill_procs.mark_reward), sound and the hit-event list (presentation).
/// </summary>
public static class Elements
{
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        ["physical"] = "Físico", ["fire"] = "Fogo", ["cold"] = "Gelo", ["lightning"] = "Raio", ["poison"] = "Veneno",
        ["shadow"] = "Sombra", ["holy"] = "Sagrado", ["arcane"] = "Arcano",
    };
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Profiles = new Dictionary<string, IReadOnlyDictionary<string, double>>
    {
        ["espectro"] = new Dictionary<string, double> { ["shadow"] = .35, ["holy"] = -.20 },
        ["sombra"] = new Dictionary<string, double> { ["shadow"] = .25, ["holy"] = -.15 },
        ["parasita"] = new Dictionary<string, double> { ["poison"] = .50, ["fire"] = -.20 },
        ["bruxa"] = new Dictionary<string, double> { ["shadow"] = .20 },
        ["sentinela_runica"] = new Dictionary<string, double> { ["lightning"] = .35 },
    };

    /// <summary>Optional hook for class passives (Pele Endurecida, Guardião): multiplier on incoming damage of a kind.</summary>
    public static Func<ICombatant, string, double>? IncomingFactor;

    public static double Resistance(ICombatant e, string kind)
    {
        double @base;
        if (e.Resistances != null && e.Resistances.TryGetValue(kind, out var own)) @base = own;
        else @base = e.EnemyType != null && Profiles.TryGetValue(e.EnemyType, out var profile) && profile.TryGetValue(kind, out var p) ? p : 0;
        double total = @base + e.Gear.Sum(g => g.Resistances.TryGetValue(kind, out var v) ? v : 0);
        return Math.Max(-.5, Math.Min(.75, total));
    }

    public static double Bonus(ICombatant? e, string kind) =>
        e == null ? 0 : Math.Max(0, Math.Min(2, e.Gear.Sum(g => g.ElementBonus.TryGetValue(kind, out var v) ? v : 0)));

    /// <summary>Extra damage taken: necromancer shadow mages (20%) or the shared "vulnerable" state, whichever is larger.</summary>
    public static double Vulnerability(ICombatant target) =>
        Math.Max(target.NecroVulnerableTicks > 0 ? .20 : 0, target.Statuses is { Count: > 0 } ? Status.Value(target, "vulnerable") : 0);

    /// <summary>States on the attacker: "power" adds damage, "weaken" removes it, "holy_power" adds holy damage.</summary>
    public static double Outgoing(ICombatant? source, string kind = "physical")
    {
        if (source?.Statuses is not { Count: > 0 }) return 1;
        double holy = kind == "holy" ? 1 + Status.Value(source, "holy_power") : 1;
        return (1 + Status.Value(source, "power")) * (1 - Status.Value(source, "weaken")) * holy;
    }

    public static double DefenseFactor(ICombatant target, string kind)
    {
        double factor = target.Statuses is { Count: > 0 } ? 1 - Status.Value(target, "resist") : 1;
        if (IncomingFactor != null) factor *= IncomingFactor(target, kind);
        return factor;
    }

    /// <summary>Deals typed damage; returns the health actually lost. Thorns answer once (never thorns on thorns).</summary>
    public static int Deal(ICombatant target, double amount, string kind = "physical", ICombatant? source = null, string identifier = "")
    {
        if (!Names.ContainsKey(kind)) kind = "physical";
        amount = amount * Outgoing(source, kind) * DefenseFactor(target, kind);
        if (!target.Alive) return 0;
        int before = target.Health;
        int incoming = (int)Math.Max(1, PyMath.Round(amount * (1 + Bonus(source, kind)) * (1 - Resistance(target, kind)) * (1 + Vulnerability(target))));
        target.TakeDamage(incoming);
        int actual = Math.Max(0, before - target.Health);
        var thorns = target.Statuses?.Get("thorns");
        if (thorns is { Count: > 0 } && actual > 0 && source != null && !ReferenceEquals(source, target) && source.Alive && !identifier.StartsWith("thorns"))
            Deal(source, Math.Max(1, PyMath.Round(actual * thorns[0].Magnitude)), target.ThornsElement, target, "thorns");
        return actual;
    }
}
