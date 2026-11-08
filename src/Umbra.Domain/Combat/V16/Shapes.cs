using Umbra.Domain.Combat;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Combat.V16;

/// <summary>
/// The shapes of the 0.16 skills — port of the shape_* functions of game/combat_system/v16/engine.py. Each one chooses
/// targets, then calls commit() (which pays; nothing is spent when a shape finds no valid target), then runs the hits.
/// summon, corpse and cage belong to the Necromancer/Warlock step (5.8) and arrive through ISkillHooks.Shape.
/// </summary>
public static class Shapes
{
    public static readonly string[] Names = { "projectile", "line", "cone", "nova", "blast", "chain", "dash", "teleport", "zone", "curse",
                                              "self", "buff", "barrier", "aura", "channel", "summon", "corpse", "cage" };

    /// <summary>Runs the skill's shape; null when the shape is unknown (melee basics go through Strike).</summary>
    public static Outcome? Run(Ctx ctx, Func<bool> commit, string? shape = null) => (shape ?? ctx.Spec.Str("shape")) switch
    {
        "projectile" => Done(Projectile(ctx, commit)), "line" => Done(Line(ctx, commit)), "cone" => Done(Cone(ctx, commit)),
        "nova" => Done(Nova(ctx, commit)), "blast" => Done(Blast(ctx, commit)), "chain" => Done(Chain(ctx, commit)),
        "dash" => Done(Dash(ctx, commit)), "teleport" => Teleport(ctx, commit), "zone" => Done(ZoneShape(ctx, commit)),
        "curse" => Done(Curse(ctx, commit)), "self" => Done(Self(ctx, commit)), "buff" => Done(Buff(ctx, commit)),
        "barrier" => Done(Barrier(ctx, commit)), "aura" => Aura(ctx, commit), "channel" => Done(Channel(ctx, commit)),
        "summon" or "corpse" or "cage" => SkillEnv.Hooks.Shape(ctx, commit),
        _ => null,
    };

    private static Outcome Done(bool ok) => ok ? Outcome.Done : Outcome.Fail;

    private static (int X, int Y) Facing(Ctx ctx, ITarget? target) =>
        target != null ? Geo.Direction(ctx.Player.Position, target.Position) : ctx.Player.AttackDirection;

    private static void Impact(Ctx ctx, (int X, int Y) center) { ctx.State.SetCenter(center); Riders.Run(ctx, "impact"); }

    private static void BlastAt(Ctx ctx, (int X, int Y) center, double radius, double factor = 1, ITarget? exclude = null)
    {
        foreach (var enemy in ctx.Near(center, radius, exclude)) ctx.Hit(enemy, factor);
    }

    private static ITarget? At(Ctx ctx, (int X, int Y) cell) => ctx.Enemies.FirstOrDefault(e => e.Alive && e.Position == cell);

    private static bool Projectile(Ctx ctx, Func<bool> commit)
    {
        var p = ctx.Player; var spec = ctx.Spec; double range = spec.Num("range"); var first = ctx.Primary(range);
        if (first == null) return false;
        var facing = Facing(ctx, first); int count = Py.Int(spec.Num("count", 1));
        var others = ctx.Near(p.Position, range, first).OrderBy(e => Geo.Steps(e.Position, p.Position))
                        .Where(e => spec.T("spread_targets") || Geo.InCone(p.Position, facing, range, e.Position, .3)).ToList();
        var volleys = new List<ITarget> { first }; volleys.AddRange(Py.Head(others, count - 1));
        if (!spec.T("spread_targets")) volleys.AddRange(Enumerable.Repeat(first, Math.Max(0, count - volleys.Count)));
        if (!commit()) return false;
        foreach (var target in volleys)
        {
            var heading = Geo.Direction(p.Position, target.Position); int passed = 0; var end = p.Position;
            foreach (var cell in Geo.LineCells(p.Position, heading, range))
            {
                if (!Geo.Walkable(ctx.Map, cell)) break;
                end = cell;
                var enemy = At(ctx, cell);
                if (enemy == null) continue;
                ctx.Hit(enemy, (passed != 0 ? spec.Num("pierce_factor", 1) : 1) * (1 + spec.Num("pierce_ramp", 0) * passed)); passed++;
                if (spec.T("radius")) { BlastAt(ctx, enemy.Position, spec.Num("radius"), spec.Num("blast_factor", 1), enemy); Impact(ctx, enemy.Position); }
                else if (!spec.T("end_blast")) Impact(ctx, enemy.Position);
                if (passed > spec.Num("pierce", 0)) break;
            }
            if (spec.T("end_blast")) { BlastAt(ctx, end, spec.Num("end_blast")); Impact(ctx, end); }
        }
        return true;
    }

    private static List<ITarget> LineTargets(Ctx ctx, (int X, int Y) origin, (int X, int Y) facing)
    {
        var cells = new HashSet<(int, int)>(Geo.LineCells(origin, facing, ctx.Spec.Num("range"), ctx.Spec.Num("width", 1)));
        return ctx.Enemies.Where(e => cells.Contains(e.Position) && Geo.Clear(ctx.Map, origin, e.Position)).ToList();
    }

    private static bool Line(Ctx ctx, Func<bool> commit)
    {
        var p = ctx.Player; var spec = ctx.Spec; var first = ctx.Primary(spec.Num("range"));
        if (first == null) return false;
        var facing = Facing(ctx, first); var headings = new List<(int X, int Y)> { facing };
        if (spec.Num("lines", 1) > 1)                                     // Mais Lâminas: a second blade 45 degrees apart
        {
            int index = Array.IndexOf(Geo.Eight, facing);
            if (index < 0) throw new ArgumentException("direction not in EIGHT");
            headings.Add(Geo.Eight[(index + 1) % 8]);
        }
        var targets = new List<ITarget>();
        foreach (var heading in headings) targets.AddRange(LineTargets(ctx, p.Position, heading).Where(e => !targets.Contains(e)).ToList());
        targets = (targets.Count > 0 ? targets : new List<ITarget> { first }).OrderBy(e => Geo.Steps(e.Position, p.Position)).ToList();
        if (!commit()) return false;
        for (int index = 0; index < targets.Count; index++)
        {
            ctx.State.Last = index == targets.Count - 1;
            for (int i = 0; i < Py.Int(spec.Num("cuts", 1)); i++) ctx.Hit(targets[index]);       // Arremetida Pálida: several cuts
        }
        ctx.State.Last = false;
        if (!ctx.State.HasCenter) ctx.State.SetCenter(targets[^1].Position);
        if (spec.T("return_factor"))
        {
            SkillWorld.Later(ctx, .4, spec.Num("return_factor"), targets.ToList());
            if (spec.T("return_cooldowns"))
                Riders.Act(ctx, new PyDict { ["do"] = "cooldowns", ["seconds"] = spec.Num("return_cooldowns") * targets.Count }, null, 0);
        }
        return true;
    }

    private static bool Cone(Ctx ctx, Func<bool> commit)
    {
        var p = ctx.Player; var spec = ctx.Spec; double range = spec.Num("range"); var first = ctx.Primary(range);
        if (first == null) return false;
        var facing = Facing(ctx, first);
        var targets = ctx.Near(p.Position, range).Where(e => Geo.InCone(p.Position, facing, range, e.Position, spec.Num("arc", .55))).ToList();
        if (targets.Count == 0) targets.Add(first);
        if (!commit()) return false;
        foreach (var enemy in targets) ctx.Hit(enemy);
        return true;
    }

    public static bool Nova(Ctx ctx, Func<bool> commit)
    {
        var p = ctx.Player; var targets = ctx.Near(p.Position, ctx.Spec.Num("radius"));
        if (targets.Count == 0 && Py.Truthy(ctx.Spec.TryGetValue("needs_target", out var need) ? need : true)) return false;
        ctx.State.SetCenter(p.Position);
        if (!commit()) return false;
        foreach (var enemy in targets) ctx.Hit(enemy);
        Impact(ctx, p.Position);
        return true;
    }

    private static bool Blast(Ctx ctx, Func<bool> commit)
    {
        var p = ctx.Player; var spec = ctx.Spec; var first = ctx.Primary(spec.Num("range"));
        if (first == null) return false;
        var center = first.Position; ctx.State.SetCenter(center);
        if (!commit()) return false;
        if (spec.T("leap"))                                               // Martelo do Céu: land next to the target
            foreach (var cell in Geo.LineCells(center, Geo.Direction(center, p.Position), 1))
                if (Geo.Walkable(ctx.Map, cell) && !ctx.Occupied(cell)) (p.X, p.Y) = cell;
        if (spec.T("meteors"))
        {
            for (int i = 0; i < Py.Int(spec.Num("meteors")); i++)
            {
                int x = center.X + SkillEnv.Rng.RandInt(-5, 5), y = center.Y + SkillEnv.Rng.RandInt(-5, 5);
                var spot = Geo.Walkable(ctx.Map, (x, y)) ? (x, y) : center;
                SkillWorld.LaterBlast(ctx, spot, spec.Num("delay", 1) + i * .5);
            }
            return true;
        }
        if (spec.T("delay")) { SkillWorld.LaterBlast(ctx, center, spec.Num("delay")); return true; }
        ctx.Hit(first);
        BlastAt(ctx, center, spec.Num("radius"), 1, first); Impact(ctx, center);
        return true;
    }

    private static bool Chain(Ctx ctx, Func<bool> commit)
    {
        var p = ctx.Player; var spec = ctx.Spec; var first = ctx.Primary(spec.Num("range"));
        if (first == null) return false;
        if (!commit()) return false;
        var (extraJumps, perJump) = SkillEnv.Hooks.ChainBonus(p);
        var chain = new List<ITarget> { first }; double jump = spec.Num("jump", 3), falloff = spec.Num("falloff", .85);
        bool repeat = spec.T("repeat");
        while (chain.Count < spec.Num("targets", 4) + extraJumps)
        {
            var last = chain[^1]; var pool = ctx.Near(last.Position, jump, last);
            if (!repeat) pool = pool.Where(e => !chain.Contains(e)).ToList();
            if (pool.Count == 0) break;
            chain.Add(repeat ? SkillEnv.Rng.Choice(pool) : Py.MinBy(pool, e => Geo.Steps(e.Position, last.Position)));
        }
        for (int index = 0; index < chain.Count; index++)
        {
            ctx.State.Last = index == chain.Count - 1;
            ctx.Hit(chain[index], Math.Pow(falloff, index) * (1 + perJump * index));
        }
        ctx.State.Last = false;
        return true;
    }

    private static HashSet<(int, int)> Blockers(Ctx ctx) => ctx.Enemies.Where(e => e.Alive).Select(e => e.Position).ToHashSet();

    private static bool Dash(Ctx ctx, Func<bool> commit)
    {
        var p = ctx.Player; var spec = ctx.Spec; double distance = spec.Num("distance", 4);
        var target = ctx.Primary(distance + 1); var facing = Facing(ctx, target);
        var walked = Geo.Path(ctx.Map, p.Position, facing, distance, Blockers(ctx));
        if (walked.Count == 0 && target == null) return false;
        ctx.State.Origin = p.Position;
        if (!commit()) return false;
        if (walked.Count > 0) (p.X, p.Y) = walked[^1];
        ctx.State.SetCenter(p.Position);
        var touched = walked.Count > 0 ? walked : new List<(int X, int Y)> { p.Position };
        foreach (var enemy in ctx.Enemies.Where(e => e.Alive && touched.Any(c => Geo.Steps(e.Position, c) <= 1)).ToList()) ctx.Hit(enemy);
        return true;
    }

    private static Outcome Teleport(Ctx ctx, Func<bool> commit)
    {
        var p = ctx.Player; var spec = ctx.Spec; double distance = spec.Num("distance", 5); (int X, int Y)? destination = null; ITarget? target = null;
        var back = p.Skills.Return;
        if (spec.T("return_window") && back != null)                     // Retorno: second use goes back to the mark, free
        {
            p.Skills.Return = null; (p.X, p.Y) = back.Value.Origin; return Outcome.Free;
        }
        if (spec.T("behind_target"))
        {
            target = ctx.Primary(distance);
            if (target == null) return Outcome.Fail;
            var f = Geo.Direction(p.Position, target.Position);
            foreach (var cell in new[] { (target.X + f.X, target.Y + f.Y), (target.X - f.Y, target.Y + f.X), (target.X + f.Y, target.Y - f.X) })
                if (Geo.Walkable(ctx.Map, cell) && !ctx.Occupied(cell)) { destination = cell; break; }
        }
        else
        {
            (int X, int Y) facing;
            if (spec.T("away"))
            {
                var closest = ctx.Enemies.Count > 0 ? Py.MinBy(ctx.Enemies, e => Geo.Steps(e.Position, p.Position)) : null;
                facing = closest != null ? Geo.Direction(closest.Position, p.Position) : p.AttackDirection;
            }
            else facing = p.AimTarget is { } aim ? Geo.Direction(p.Position, aim) : p.AttackDirection;
            var walked = Geo.Path(ctx.Map, p.Position, facing, distance, Blockers(ctx));
            destination = walked.Count > 0 ? walked[^1] : null;
        }
        if (destination == null) return Outcome.Fail;
        ctx.State.Origin = p.Position;
        if (!commit()) return Outcome.Fail;
        (p.X, p.Y) = destination.Value; ctx.State.SetCenter(destination);
        if (spec.T("return_window")) p.Skills.Return = (ctx.State.Origin.Value, (int)PyMath.Round(spec.Num("return_window") * 60));
        if (target != null) ctx.Hit(target);
        return Outcome.Done;
    }

    private static bool ZoneShape(Ctx ctx, Func<bool> commit)
    {
        var spec = ctx.Spec; var p = ctx.Player; bool atSelf = spec.Str("at") == "self";
        var first = atSelf ? null : ctx.Primary(spec.Num("range"));
        if (first == null && !atSelf) return false;
        var center = atSelf ? p.Position : first!.Position; ctx.State.SetCenter(center);
        if (!commit()) return false;
        if (spec.T("impact") && spec.T("multiplier"))                     // Chão Corrompido: the first blow
            foreach (var enemy in ctx.Near(center, spec.Num("radius"))) ctx.Hit(enemy, spec.Num("impact") / spec.Num("multiplier"));
        var spots = new List<(int X, int Y)> { center };
        if (spec.T("line_zone"))                                           // Trilha da Ruína: a line from the hero
        {
            var facing = Geo.Direction(p.Position, center);
            spots = Geo.LineCells(p.Position, facing, Py.Int(spec.Num("line_zone"))).Where(c => Geo.Walkable(ctx.Map, c)).ToList();
            if (spots.Count == 0) spots.Add(center);
            spec["radius"] = 0.0;
        }
        for (int i = 0; i < Py.Int(spec.Num("scatter", 1)) - 1; i++)
        {
            int x = center.X + SkillEnv.Rng.RandInt(-2, 2), y = center.Y + SkillEnv.Rng.RandInt(-2, 2);
            spots.Add(Geo.Walkable(ctx.Map, (x, y)) ? (x, y) : center);
        }
        foreach (var spot in spots)
            SkillWorld.AddZone(ctx, spot, new PyDict
            {
                ["radius"] = spec.Get("radius"), ["seconds"] = spec.TryGetValue("duration_s", out var d) ? d : 5.0,
                ["dps"] = spec.TryGetValue("multiplier", out var m) ? m : 0.0, ["element"] = spec.Get("element"),
                ["states"] = spec.TryGetValue("states", out var st) ? st : new List<object?>(), ["heal"] = spec.TryGetValue("heal", out var h) ? h : 0.0,
                ["follows"] = spec.T("follows") && spots.Count == 1, ["trigger"] = spec.Get("trigger"), ["skill_riders"] = true,
                ["turret"] = spec.Get("turret"), ["self_states"] = spec.TryGetValue("self_states", out var ss) ? ss : new List<object?>(),
                ["resource_ps"] = spec.TryGetValue("resource_ps", out var r) ? r : 0.0, ["max_targets"] = spec.Get("max_targets"),
                ["wander"] = spec.T("wander"), ["bloom"] = spec.T("bloom"), ["max_active"] = spec.Get("max_active"),
                ["turret_interval"] = spec.Get("turret_interval"),
            });
        SkillWorld.TrackEffect(ctx, spec.Num("duration_s", 0));
        return true;
    }

    private static bool Curse(Ctx ctx, Func<bool> commit)
    {
        var spec = ctx.Spec; var first = ctx.Primary(spec.Num("range"));
        if (first == null) return false;
        var targets = new List<ITarget> { first };
        if (!spec.T("single")) targets.AddRange(ctx.Near(first.Position, spec.Num("radius"), first));
        ctx.State.SetCenter(first.Position);
        if (!commit()) return false;
        foreach (var enemy in targets) ctx.Hit(enemy);
        return true;
    }

    private static bool SelfArea(Ctx ctx)
    {
        if (ctx.Spec.T("radius"))
        {
            ctx.State.SetCenter(ctx.Player.Position);
            foreach (var enemy in ctx.Near(ctx.Player.Position, ctx.Spec.Num("radius"))) ctx.Hit(enemy);
        }
        return true;
    }

    private static bool Self(Ctx ctx, Func<bool> commit)
    {
        ctx.State.SetCenter(ctx.Player.Position);
        if (!commit()) return false;
        return SelfArea(ctx);
    }

    private static bool Buff(Ctx ctx, Func<bool> commit)
    {
        var p = ctx.Player; var spec = ctx.Spec; ctx.State.SetCenter(p.Position);
        if (!commit()) return false;
        foreach (var row in spec.List("buffs").Cast<List<object?>>())
            Status.Apply(p, (string)row[0]!, PyMath.Round(Py.Num(row[1]) * 60), Py.Num(row[2]));
        double duration = spec.Num("duration_s", 0);
        if (spec.T("ward_s"))                                              // Muralha de Fé
        {
            duration = spec.Num("ward_s"); Status.Apply(p, "ward", PyMath.Round(duration * 60), spec.Num("ward_reflect", 0));
            if (spec.T("ward_guard")) Status.Apply(p, "guard", PyMath.Round(duration * 60), spec.Num("ward_guard"));
            p.Skills.WardGain = spec.Num("ward_gain", 0); ctx.State.BlockedBefore = p.WardBlocked;
        }
        if (spec.T("imbue_charges")) p.Skills.Imbue = (spec, Py.Int(spec.Num("imbue_charges")));
        if (spec.T("follow_zone"))
        {
            var zone = spec.Sub("follow_zone")!;
            SkillWorld.AddZone(ctx, p.Position, new PyDict
            {
                ["radius"] = zone.Get("radius"), ["seconds"] = duration != 0 ? duration : 10.0, ["dps"] = zone.Get("dps"),
                ["element"] = spec.Get("element"), ["follows"] = true,
                ["states"] = zone.TryGetValue("states", out var st) ? st : new List<object?>(),
            });
        }
        SkillWorld.TrackEffect(ctx, duration);
        return SelfArea(ctx);
    }

    private static bool Barrier(Ctx ctx, Func<bool> commit)
    {
        ctx.State.SetCenter(ctx.Player.Position);
        if (!commit()) return false;
        var p = ctx.Player; double seconds = ctx.Spec.Num("duration_s", 5);
        Status.Apply(p, "barrier", PyMath.Round(seconds * 60), PyMath.Round(p.MaxHealth * ctx.Spec.Num("ratio") * SkillEnv.Hooks.BarrierFactor(p)));
        SkillWorld.TrackEffect(ctx, seconds);
        return SelfArea(ctx);
    }

    private static Outcome Aura(Ctx ctx, Func<bool> commit)
    {
        if (SkillWorld.AuraActive(ctx.Player, ctx.Spec.Str("id"))) { SkillWorld.StopAura(ctx.Player, ctx.Spec.Str("id")); return Outcome.Off; }
        if (!commit()) return Outcome.Fail;
        SkillWorld.StartAura(ctx); return Outcome.Done;
    }

    private static bool Channel(Ctx ctx, Func<bool> commit)
    {
        if (!commit()) return false;
        SkillWorld.StartChannel(ctx); return true;
    }
}
