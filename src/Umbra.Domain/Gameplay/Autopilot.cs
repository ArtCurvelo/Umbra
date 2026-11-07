using System.Numerics;
using Umbra.Domain.World;

namespace Umbra.Domain.Gameplay;

/// <summary>
/// Scripted player for tests, the benchmark and the integration self-test: walks to the nearest enemy, swings,
/// uses Ruptura on groups and Investida to close distance, then collects drops. Same state, same inputs.
/// </summary>
public static class Autopilot
{
    public static InputFrame Next(Simulation sim)
    {
        var hero = sim.Hero;
        Enemy? target = sim.Enemies.Where(e => e.Alive).OrderBy(e => Vector2.Distance(e.Position, hero.Position)).FirstOrDefault();
        if (target == null)
        {
            var drop = sim.Drops.OrderBy(d => Vector2.Distance(d.Position, hero.Position)).FirstOrDefault();
            return drop == null ? InputFrame.None(hero.Position + hero.Facing) : new InputFrame(Vector2.Zero, drop.Position, drop.Position, false, false, false);
        }

        var aim = target.Position;
        var toward = Vector2.Normalize(aim - hero.Position);
        float distance = Vector2.Distance(hero.Position, aim);
        if (sim.InArc(target, toward, SliceData.GolpeBrutal.Range, SliceData.GolpeBrutal.ArcDegrees))
        {
            int group = sim.Enemies.Count(e => e.Alive && sim.InArc(e, toward, SliceData.Ruptura.Range, SliceData.Ruptura.ArcDegrees));
            bool cone = hero.Fury >= SliceData.Ruptura.Cost && group >= 2;
            return new InputFrame(Vector2.Zero, null, aim, !cone, cone, false);
        }
        bool dash = hero.Cooldown(SliceData.Investida.Id) == 0 && distance is > 2.5f and < 4.5f
                    && Collision.LineOfSight(sim.Map, hero.Position, aim);
        return new InputFrame(dash ? Vector2.Zero : toward, null, aim, false, false, dash);
    }

    /// <summary>Runs the autopilot until the crypt is clear and every drop is collected, the hero dies or the time runs out.</summary>
    public static int Run(Simulation sim, int maxTicks)
    {
        for (int i = 0; i < maxTicks; i++)
        {
            if (!sim.Hero.Alive || (sim.Cleared && sim.Drops.Count == 0)) return i;
            sim.Step(Next(sim));
        }
        return maxTicks;
    }
}
