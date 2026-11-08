using Umbra.Domain.Combat;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Combat.V16;

/// <summary>
/// The per-tick part of game/combat_system/v16/world.py: zones (pulses, turrets, mines), delayed hits, effects with an
/// end, auras, channels, charges, deaths of the tick (reaper, contagion, deathburst, carnage) and the hero states that act
/// every tick (trail, seed, martyr, chill). Run once per tick for player 1 (UMBRA's lifecycle step).
/// </summary>
public static class SkillWorldTick
{
    private static Ctx Copy(Ctx old, SkillScene scene)
    {
        var ctx = new Ctx(old.Player, old.Spec, scene.Enemies, scene.Map);
        ctx.State.SetCenter(old.State.HasCenter ? old.State.Center : null); return ctx;
    }

    public static void Tick(SkillScene scene)
    {
        var p = scene.Player; var h = SkillEnv.Hooks;
        h.PassiveTick(p);
        Deaths(scene);
        HeroStates(scene);
        var world = p.Skills.World;
        if (world == null) return;
        foreach (var zone in world.Zones.ToList())
        {
            zone.Ticks--; zone.Timer--; bool spent = false;
            h.NecroZoneTick(zone);
            if (zone.Turret != null)
            {
                zone.ShotTimer--;
                if (zone.Follows) zone.Center = p.Position;
                if (zone.ShotTimer <= 0) { zone.ShotTimer = zone.Interval; Shoot(zone, Copy(zone.Ctx, scene)); }
            }
            if (zone.Timer <= 0 || zone.Trigger != null)
            {
                if (zone.Timer <= 0) zone.Timer = SkillWorld.Pulse;
                spent = PulseZone(zone, scene);
            }
            if (spent || zone.Ticks <= 0) { world.Zones.Remove(zone); h.NecroZoneEnd(zone); }
        }
        foreach (var e in world.Events.ToList())
        {
            e.Ticks--;
            if (e.Ticks > 0) continue;
            world.Events.Remove(e); var ctx = Copy(e.Ctx, scene);
            if (e.Kind == "echo")
            {
                foreach (var enemy in e.Targets!.ToList()) if (enemy.Alive) ctx.Hit(enemy, e.Factor, riders: false);
            }
            else if (e.Kind == "recall")
            {
                if (Geo.Walkable(ctx.Map, e.Origin)) (p.X, p.Y) = e.Origin;
                if (e.Heal != 0) Riders.Heal(p, p.MaxHealth * e.Heal);
            }
            else
            {
                ctx.State.SetCenter(e.Center);
                foreach (var enemy in ctx.Near(e.Center, ctx.Spec.Num("radius"))) ctx.Hit(enemy);
                Riders.Run(ctx, "impact");
            }
        }
        foreach (var effect in world.Effects.ToList())
        {
            effect.Ticks--;
            if (effect.Ticks <= 0) { world.Effects.Remove(effect); EndEffect(effect); }
        }
        foreach (var (id, row) in world.Auras.ToList())
            if (!TickAura(row, scene)) world.Auras.RemoveAll(a => a.Id == id);
        if (world.Channel != null) TickChannel(world, scene);
        TickCharges(p);
    }

    // ------------------------------------------------------------------ zones
    private static void Shoot(Zone zone, Ctx ctx)
    {
        var turret = zone.Turret!; var center = zone.Center; var p = ctx.Player;
        for (int s = 0; s < Py.Int(turret.Num("shots", 1)); s++)
        {
            var pool = ctx.Near(center, turret.Num("range", 6));
            if (pool.Count == 0) return;
            var chain = new List<ITarget> { Py.MinBy(pool, e => Geo.Steps(e.Position, center)) };
            for (int i = 0; i < Py.Int(turret.Num("chain", 1)) - 1; i++)
            {
                var last = chain[^1];
                var next = ctx.Near(last.Position, 3).Where(e => !chain.Contains(e)).ToList();
                if (next.Count == 0) break;
                chain.Add(Py.MinBy(next, e => Geo.Steps(e.Position, last.Position)));
            }
            foreach (var enemy in chain)
            {
                string? element = turret.StrOrNull("element") is { Length: > 0 } te ? te : zone.Element;
                int dealt = ctx.Damage(enemy, Math.Max(1, PyMath.Round(Riders.BaseDamage(p) * turret.Num("factor"))), element);
                Riders.Run(ctx, "pulse", enemy, dealt);
            }
        }
    }

    private static bool PulseZone(Zone zone, SkillScene scene)
    {
        var ctx = Copy(zone.Ctx, scene); var p = ctx.Player; var h = SkillEnv.Hooks;
        if (zone.Follows) zone.Center = p.Position;
        else if (zone.Wander)
        {
            var step = SkillEnv.Rng.Choice(Geo.Eight); var next = (zone.Center.X + step.X, zone.Center.Y + step.Y);
            if (Geo.Walkable(ctx.Map, next)) zone.Center = next;
        }
        ctx.State.SetCenter(zone.Center);
        var inside = ctx.Near(zone.Center, zone.Radius);
        if (zone.Trigger != null)
        {
            if (inside.Count == 0) return false;
            foreach (var enemy in ctx.Near(zone.Center, zone.Trigger.Num("radius", zone.Radius)))
                ctx.Damage(enemy, PyMath.Round(Riders.BaseDamage(p) * zone.Trigger.Num("factor")), zone.Element);
            return true;                                                   // a mine is spent
        }
        inside = h.NecroZoneTargets(zone, inside);                         // Olho da Tormenta: only the edge
        if (Py.Truthy(zone.MaxTargets)) inside = inside.OrderBy(e => Geo.Steps(e.Position, zone.Center)).Take(Py.Int(zone.MaxTargets)).ToList();
        long amount = zone.Dps != 0 ? PyMath.Round(Riders.BaseDamage(p) * zone.Dps * SkillWorld.Pulse / 60) : 0;
        foreach (var enemy in inside)
        {
            int dealt = amount != 0 ? ctx.Damage(enemy, Math.Max(1, PyMath.Round(amount * h.PassiveDamage(ctx, enemy)))) : 0;
            foreach (var row in zone.States)
            {
                if ((row.T("once") || row.T("first")) && (zone.Touched.Contains(enemy) || (row.T("first") && zone.Touched.Count > 0))) continue;
                Riders.ApplyState(enemy, row, p, p);
            }
            zone.Touched.Add(enemy);
            if (zone.Skill) Riders.Run(ctx, "pulse", enemy, dealt);
        }
        if (Geo.Steps(p.Position, zone.Center) <= zone.Radius)
        {
            if (zone.Heal != 0) Riders.Heal(p, p.MaxHealth * zone.Heal * SkillWorld.Pulse / 60);
            foreach (var pair in zone.SelfStatesRaw.Cast<List<object?>>())
                Status.Apply(p, (string)pair[0]!, SkillWorld.Pulse + 10, Py.Num(pair[1]));
            if (zone.ResourcePs != 0) p.GainResource(Math.Max(1, PyMath.Round(zone.ResourcePs * SkillWorld.Pulse / 60)), "v16_zone");
        }
        return false;
    }

    private static void EndEffect(SkillEffect effect)
    {
        var ctx = effect.Ctx; var p = ctx.Player;
        double absorbed = Math.Max(0, p.AbsorbedTotal - effect.Absorbed); ctx.State.SetCenter(p.Position);
        foreach (var rider in ctx.Spec.Rows("riders").ToList())
        {
            if (rider.StrOrNull("on") != "end" || !Riders.Allowed(ctx, rider, null)) continue;
            string action = rider.Str("do");
            if (action is "explode_absorbed" or "nova" or "counter")
            {
                long amount;
                if (action == "explode_absorbed") amount = PyMath.Round(absorbed * rider.Num("factor"));
                else if (action == "counter")
                {
                    int blocked = p.WardBlocked - (ctx.State.BlockedBefore ?? 0);
                    amount = PyMath.Round(Riders.BaseDamage(p) * (rider.Num("factor") + rider.Num("per_block", 0) * blocked));
                }
                else amount = PyMath.Round(Riders.BaseDamage(p) * rider.Num("factor"));
                if (amount > 0)
                    foreach (var enemy in ctx.Near(p.Position, rider.Num("radius", 2))) ctx.Damage(enemy, amount, rider.StrOrNull("element"));
            }
            else if (action == "heal_barrier")
            {
                long left = PyMath.Round(Status.Value(p, "barrier"));
                if (left != 0) Riders.Heal(p, left);
            }
            else Riders.Act(ctx, rider, null, 0);
        }
    }

    // ------------------------------------------------------------------ auras and channels
    private static bool TickAura(AuraRow row, SkillScene scene)
    {
        var ctx = Copy(row.Ctx, scene); var p = ctx.Player; var spec = ctx.Spec;
        row.UpkeepTimer--;
        if (row.UpkeepTimer <= 0)
        {
            row.UpkeepTimer = 60;
            if (spec.T("upkeep") && !p.SpendResource(Py.Int(spec.Num("upkeep")))) return false;
        }
        row.Timer--;
        if (row.Timer > 0) return true;
        row.Timer = SkillWorld.Pulse;
        foreach (var pair in spec.List("aura_self").Cast<List<object?>>()) Status.Apply(p, (string)pair[0]!, SkillWorld.Pulse + 10, Py.Num(pair[1]));
        long amount = PyMath.Round(Riders.BaseDamage(p) * spec.Num("multiplier", 0) * SkillWorld.Pulse / 60);
        foreach (var enemy in ctx.Near(p.Position, spec.Num("radius")))
        {
            if (amount != 0) ctx.Damage(enemy, amount, spec.StrOrNull("element"));
            Riders.Run(ctx, "pulse", enemy, (int)amount);
        }
        return true;
    }

    private static void TickChannel(SkillWorld world, SkillScene scene)
    {
        var row = world.Channel!; var ctx = Copy(row.Ctx, scene); var p = ctx.Player; var spec = ctx.Spec;
        if (!p.Skills.Hold || !p.Alive) { SkillWorld.StopChannel(p); return; }
        row.UpkeepTimer--;
        if (row.UpkeepTimer <= 0)
        {
            row.UpkeepTimer = 60;
            if (spec.T("upkeep") && !p.SpendResource(Py.Int(spec.Num("upkeep")))) { SkillWorld.StopChannel(p); return; }
            if (spec.T("health_upkeep")) p.Health = Math.Max(1, p.Health - (int)Math.Max(1, PyMath.Round(p.MaxHealth * spec.Num("health_upkeep"))));
        }
        foreach (var pair in spec.List("channel_self").Cast<List<object?>>()) Status.Apply(p, (string)pair[0]!, 20, Py.Num(pair[1]));
        row.Timer--;
        if (row.Timer > 0) return;
        row.Timer = (int)PyMath.Round(spec.Num("pulse", .5) * 60);
        Shapes.Run(ctx, () => true, spec.Str("pulse_shape", "nova"));
        if (spec.Str("category") == "Básica" && ctx.State.Targets.Count > 0)
            p.GainResource(Math.Max(1, Py.Int(spec.Num("resource_gain", 4)) / 3), "basic_attack");
    }

    // ------------------------------------------------------------------ per tick
    private static void TickCharges(ISkillUser p)
    {
        var s = p.Skills;
        foreach (var (id, (maximum, cooldown)) in SkillWorld.State(p).Charges.ToList())
        {
            int held = s.Charges.TryGetValue(id, out var v) ? v : maximum;
            if (held < maximum && s.Cooldowns.GetValueOrDefault(id) <= 0)
            {
                held++; s.Charges[id] = held;
                if (held < maximum) s.Cooldowns[id] = cooldown;
            }
        }
    }

    private static void Deaths(SkillScene scene)
    {
        var p = scene.Player; var fresh = new List<ITarget>();
        foreach (var enemy in scene.Enemies)
            if (!enemy.Alive && !enemy.V16Dead) { enemy.V16Dead = true; fresh.Add(enemy); }
        if (fresh.Count == 0) return;
        foreach (var enemy in fresh)
        {
            var rows = enemy.Statuses;
            if (rows?.Get("reaper") is { Count: > 0 } reaper) p.GainResource(Math.Truncate(reaper.Max(r => r.Magnitude)), "curse_reaper");
            if (rows?.Get("contagion") is { Count: > 0 })
                Riders.Spread(p, enemy, new[] { "curse", "vulnerable", "weaken", "contagion", "burn", "mark" }, scene.Enemies);
            if (rows?.Get("deathburst") is { Count: > 0 } burst)
            {
                double factor = burst.Max(r => r.Magnitude);
                foreach (var other in scene.Enemies)
                    if (other.Alive && Geo.Steps(other.Position, enemy.Position) <= 1)
                        Elements.Deal(other, Math.Max(1, PyMath.Round(Riders.BaseDamage(p) * factor)), "shadow", p, "v16_detonation");
            }
        }
        if (Status.Has(p, "carnage"))                                    // Carnificina: kills extend the buffs, up to +10 s
        {
            var rows = p.Statuses!; var budget = rows.Get("carnage")![0];
            long extra = Math.Min(fresh.Count * 60, PyMath.Round(budget.Magnitude * 60));
            if (extra > 0)
            {
                budget.Magnitude -= extra / 60.0;
                foreach (var kind in new[] { "power", "haste", "swift", "carnage" })
                    foreach (var row in rows.Get(kind) ?? new List<StatusRow>()) row.Ticks += (int)extra;
            }
        }
        SkillEnv.Hooks.PassiveOnKills(p, fresh.Count);
        SkillEnv.Hooks.WarlockOnKills(p, fresh.Count);
        SkillEnv.Hooks.NecroOnKills(p, fresh);
    }

    private static void HeroStates(SkillScene scene)
    {
        var p = scene.Player; var rows = p.Statuses;
        if (rows?.Get("trail") is { Count: > 0 } trail && scene.Map != null)    // Resíduo: poison where the necromancer walks
        {
            var world = SkillWorld.State(p); world.TrailTimer--;
            if (world.TrailTimer <= 0)
            {
                world.TrailTimer = SkillWorld.Pulse;
                var ctx = new Ctx(p, new PyDict { ["id"] = "v16_trail", ["element"] = "poison", ["category"] = "Defensiva" }, scene.Enemies, scene.Map);
                SkillWorld.AddZone(ctx, p.Position, new PyDict { ["radius"] = 0.0, ["seconds"] = 3.0, ["dps"] = trail[0].Magnitude, ["element"] = "poison" });
            }
        }
        if (rows?.Get("seed") is { Count: > 0 } seed && p.Health < p.MaxHealth * .35)       // Semente
        {
            double ratio = seed[0].Magnitude; rows.Remove("seed"); Riders.Heal(p, p.MaxHealth * ratio);
        }
        if (rows?.Get("martyr") is { Count: > 0 } martyr && p.Health < p.MaxHealth * martyr[0].Magnitude)
        {
            rows.Remove("martyr"); rows.Remove("holy_power");
        }
        if (rows?.Get("chill") is { Count: > 0 } chill)
        {
            double value = chill[0].Magnitude;
            foreach (var enemy in scene.Enemies)
                if (enemy.Alive && Geo.Steps(enemy.Position, p.Position) <= 1) Status.Apply(enemy, "slow", 20, value);
        }
        if (p.Skills.Return is { } back) p.Skills.Return = back.Ticks > 1 ? (back.Origin, back.Ticks - 1) : null;
        int dash = p.DashCooldown;
        if (dash > p.Skills.LastDash) SkillEnv.Hooks.PassiveOnDodge(p);
        p.Skills.LastDash = dash;
    }
}
