using Umbra.Domain.Combat;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Combat.V16;

/// <summary>
/// Effects attached to a 0.16 skill — port of game/combat_system/v16/riders.py. A rider is {on, do, ...}; moments: hit
/// (default), cast, after, impact, kill, pulse, end, imbue. Optional on every rider: chance, when, every (+per_target),
/// limit (per cast). Actions are listed in the Python docstring; the Necromancer's arrive through ISkillHooks.NecroAct and
/// the ally ones (swap, ally_intangible) through ISkillHooks.Allies.
/// </summary>
public static class Riders
{
    public const double LeechCap = .12;

    public static double BaseDamage(ISkillUser p) => Math.Max(1, p.Attack);

    public static bool Allowed(Ctx ctx, PyDict rider, ITarget? enemy)
    {
        if (rider.Num("chance", 1) < 1 && SkillEnv.Rng.Random() >= rider.Num("chance")) return false;
        if (rider.T("when") && !Conditions.Check(ctx, enemy, rider.Str("when"))) return false;
        if (rider.T("every"))
        {
            var counters = ctx.Player.Skills.Counters;
            var key = (ctx.Spec.Str("id"), rider.StrOrNull("do"), rider.StrOrNull("kind"), rider.Num("every"), rider.T("per_target") ? (object?)enemy : null);
            counters[key] = counters.GetValueOrDefault(key) + 1;
            if (counters.Count > 256) counters.Clear();
            if (counters[key] % Py.Int(rider.Num("every")) != 0) return false;   // KeyNotFound after a clear, as in Python
        }
        if (rider.T("limit"))
        {
            var used = ctx.State.Limits ??= new Dictionary<PyDict, int>(ReferenceEqualityComparer.Instance);
            if (used.GetValueOrDefault(rider) >= rider.Num("limit")) return false;
            used[rider] = used.GetValueOrDefault(rider) + 1;
        }
        return true;
    }

    public static void Run(Ctx ctx, string moment, ITarget? enemy = null, int dealt = 0)
    {
        foreach (var rider in ctx.Spec.Rows("riders").ToList())
            if (rider.Str("on", "hit") == moment && Allowed(ctx, rider, enemy)) Act(ctx, rider, enemy, dealt);
        if (moment == "hit" && enemy != null)
            foreach (var rider in ctx.Spec.Rows("states").ToList())
                if (rider.Str("on", "hit") == "hit" && !rider.T("first") && Allowed(ctx, rider, enemy)) Act(ctx, rider, enemy, dealt);
    }

    public static int ApplyState(ICombatant target, PyDict rider, ISkillUser p, ICombatant? source = null, double scale = 1)
    {
        var h = SkillEnv.Hooks; string kind = rider.Str("kind");
        double seconds = rider.Num("seconds", 3) * scale * h.DurationFactor(p, kind);
        if (Status.Control.Contains(kind)) seconds *= 1 + h.Affix(p, "control");
        long ticks = PyMath.Round(seconds * 60);
        double magnitude;
        if (rider.ContainsKey("dps")) magnitude = rider.Num("dps") * BaseDamage(p);
        else if (rider.ContainsKey("total")) magnitude = rider.Num("total") * BaseDamage(p) / Math.Max(.5, rider.Num("seconds", 3));
        else magnitude = rider.Num("value", 0);
        if (Status.Dot.ContainsKey(kind)) magnitude *= h.DotFactor(p, kind) * (1 + h.Affix(p, "dot_damage"));
        return Status.Apply(target, kind, ticks, magnitude, source);
    }

    /// <summary>Heals the hero; returns the overheal.</summary>
    public static int Heal(ISkillUser p, double amount)
    {
        int whole = (int)Math.Max(1, PyMath.Round(amount * SkillEnv.Hooks.HealFactor(p)));
        int room = p.MaxHealth - p.Health; p.Health = Math.Min(p.MaxHealth, p.Health + whole);
        return whole > room ? whole - room : 0;
    }

    public static void Act(Ctx ctx, PyDict rider, ITarget? enemy, int dealt)
    {
        var p = ctx.Player; var spec = ctx.Spec; string action = rider.Str("do");
        switch (action)
        {
            case "status":
                if (enemy is { Alive: true }) ApplyState(enemy, rider, p, p, spec.Num("state_seconds", 1));
                break;
            case "self":
                if (rider.Str("kind") == "thorns") p.ThornsChoice = rider.Str("element", "physical");
                Status.Apply(p, rider.Str("kind"), PyMath.Round(rider.Num("seconds", 3) * 60), rider.Num("value", 0));
                break;
            case "heal": Heal(p, p.MaxHealth * rider.Num("ratio")); break;
            case "leech":
                if (dealt > 0)
                {
                    long room = Math.Max(0, PyMath.Round(p.MaxHealth * LeechCap) - ctx.State.Healed);
                    long amount = Math.Min(room, Math.Max(1, PyMath.Round(dealt * rider.Num("ratio"))));
                    if (amount != 0) { Heal(p, amount); ctx.State.Healed += (int)amount; }
                }
                break;
            case "resource": p.GainResource(Py.Int(rider.Num("amount")), "v16_" + spec.Str("id")); break;
            case "barrier":
            {
                double points = p.MaxHealth * rider.Num("ratio") * SkillEnv.Hooks.BarrierFactor(p);
                if (rider.T("stack_cap")) points = Math.Min(p.MaxHealth * rider.Num("stack_cap"), Status.Value(p, "barrier") + points);
                var rows = p.Statuses?.Get("barrier");
                if (rows is { Count: > 0 } && rider.T("stack_cap")) { rows[0].Magnitude = PyMath.Round(points); rows[0].Ticks = (int)PyMath.Round(rider.Num("seconds", 5) * 60); }
                else Status.Apply(p, "barrier", PyMath.Round(rider.Num("seconds", 5) * 60), PyMath.Round(points));
                break;
            }
            case "splash":
                if (enemy != null && dealt > 0)
                {
                    var others = ctx.Near(enemy.Position, rider.Num("radius", 1), enemy).OrderBy(e => Geo.Steps(e.Position, enemy.Position)).ToList();
                    foreach (var other in others.Take(Py.Int(rider.Num("max", 8)))) ctx.Damage(other, Math.Max(1, PyMath.Round(dealt * rider.Num("factor"))));
                }
                break;
            case "extra":
                if (enemy is { Alive: true }) ctx.Hit(enemy, rider.Num("factor"), riders: false);
                break;
            case "echo": SkillWorld.Later(ctx, rider.Num("delay", 1), rider.Num("factor"), ctx.State.Targets); break;
            case "pull": case "push":
                if (enemy is { Alive: true }) Shove(ctx, rider, enemy, action == "pull");
                break;
            case "zone":
            {
                var fallback = enemy != null ? enemy.Position : ctx.State.CenterOr(p.Position);
                (int, int)? at = rider.Str("at", "target") switch
                {
                    "self" => p.Position, "center" => ctx.State.CenterOr(p.Position), "target" => fallback,
                    "origin" => ctx.State.Origin ?? p.Position,
                    var other => throw new KeyNotFoundException(other),
                };
                SkillWorld.AddZone(ctx, at!.Value, rider);
                break;
            }
            case "explode":
                if (enemy is { Alive: false })
                    foreach (var other in ctx.Near(enemy.Position, rider.Num("radius", 1), enemy))
                        ctx.Damage(other, Math.Max(1, PyMath.Round(BaseDamage(p) * rider.Num("factor"))), rider.StrOrNull("element"));
                break;
            case "spread":
                if (enemy is { Alive: false }) Spread(p, enemy, rider.List("kinds").Cast<string>().ToList(), ctx.Enemies);
                break;
            case "cleanse": Status.Cleanse(p); break;
            case "cooldowns":
            {
                long cut = PyMath.Round(rider.Num("seconds") * 60);
                foreach (var key in p.Skills.Cooldowns.Keys.ToList())
                    if (key != spec.Str("id")) p.Skills.Cooldowns[key] = (int)Math.Max(0, p.Skills.Cooldowns[key] - cut);
                break;
            }
            case "stack":
            {
                string id = spec.Str("id");
                int count = (int)Math.Min(spec.Num("stack_max", 5), Conditions.Stacks(p, id) + 1);
                int ticks = (int)PyMath.Round(spec.Num("stack_seconds", 3) * 60);
                p.Skills.Stacks[id] = (count, ticks);
                string kind = spec.Str("stack_kind", "haste");
                Status.Remove(p, kind); Status.Apply(p, kind, ticks, count * spec.Num("stack_per", .05));
                break;
            }
            case "burst":
                foreach (var other in ctx.Near(p.Position, rider.Num("radius", 1)))
                {
                    if (rider.T("factor")) ctx.Damage(other, Math.Max(1, PyMath.Round(BaseDamage(p) * rider.Num("factor"))), rider.StrOrNull("element"));
                    foreach (var row in rider.Rows("states")) ApplyState(other, row, p, p);
                }
                break;
            case "recall": SkillWorld.Recall(ctx, rider.Num("delay", 3), rider.Num("heal", 0)); break;
            case "gather":
            {
                var center = ctx.State.CenterOr(p.Position);
                foreach (var other in ctx.Near(center!.Value, rider.Num("radius", 5)))
                    Shove(ctx, new PyDict { ["tiles"] = rider.Num("tiles", 2) }, other, true, center);
                break;
            }
            case "hurt": p.Health = Math.Max(1, p.Health - (int)Math.Max(1, PyMath.Round(p.MaxHealth * rider.Num("ratio")))); break;
            case "shards":
            {
                var center = ctx.State.CenterOr(p.Position)!.Value;
                var pool = ctx.Near(center, rider.Num("radius", 3)).OrderBy(e => Geo.Steps(e.Position, center)).ToList();
                for (int i = 0; i < Py.Int(rider.Num("count", 4)); i++)
                    if (pool.Count > 0) ctx.Damage(pool[i % pool.Count], Math.Max(1, PyMath.Round(BaseDamage(p) * rider.Num("factor"))));
                break;
            }
            case "swap": case "ally_intangible": SkillEnv.Hooks.Allies(ctx, rider); break;
            case "explode_absorbed": case "heal_barrier": case "nova": case "counter": break;      // handled by the effect end
            default:
                if (!SkillEnv.Hooks.NecroAct(ctx, rider, enemy, dealt)) throw new ArgumentException("Ação desconhecida: " + action);
                break;
        }
    }

    private static void Shove(Ctx ctx, PyDict rider, ITarget enemy, bool towards, (int X, int Y)? center = null)
    {
        var p = ctx.Player; var c = (center ?? ctx.State.CenterOr(p.Position))!.Value;
        if (enemy.Position == c) c = p.Position;
        var step = Geo.Direction(enemy.Position, c); int sign = towards ? 1 : -1;
        for (int i = 0; i < Py.Int(rider.Num("tiles", 1)); i++)
        {
            var next = (enemy.X + step.X * sign, enemy.Y + step.Y * sign);
            bool blocked = !Geo.Walkable(ctx.Map, next);
            if (next == p.Position || (towards && next == c) || blocked || ctx.Occupied(next, enemy))
            {
                if (blocked && rider.T("wall")) ctx.Damage(enemy, Math.Max(1, PyMath.Round(BaseDamage(p) * rider.Num("wall"))));
                break;
            }
            (enemy.X, enemy.Y) = next;
        }
    }

    public static void Spread(ISkillUser p, ITarget enemy, IReadOnlyList<string> kinds, IEnumerable<ITarget> enemies)
    {
        var others = enemies.Where(e => e.Alive && !ReferenceEquals(e, enemy) && Geo.Steps(e.Position, enemy.Position) <= 3)
                            .OrderBy(e => Geo.Steps(e.Position, enemy.Position)).ToList();
        if (others.Count == 0) return;
        foreach (var kind in kinds)
            foreach (var row in (enemy.Statuses?.Get(kind) ?? new List<StatusRow>()).ToList())
                Status.Apply(others[0], kind, Math.Max(60, row.Ticks), row.Magnitude, p);
    }
}
