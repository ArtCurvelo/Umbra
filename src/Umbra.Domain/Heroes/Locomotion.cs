using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;

namespace Umbra.Domain.Heroes;

/// <summary>The hero's continuous position inside its grid cell (world/locomotion.Motion).</summary>
public sealed class Motion
{
    public (double X, double Y) Position, Previous;
    public (int X, int Y) Cell;
    public (double X, double Y) Velocity;
    public double Distance;
    public IMap? Map;
}

/// <summary>
/// Continuous sub-tile movement — port of game/world/locomotion.py. The grid cell stays the combat contract: the hero
/// glides with bounded acceleration and braking, collides as a disc against walls and occupied tiles, and changes cell
/// through Hero.Move (which cancels a pending swing). The input vector is in world tiles (the camera turn is the input
/// layer's business: locomotion.world_vector is only used for UMBRA's isometric screen).
/// </summary>
public static class Locomotion
{
    public const double Speed = 4.8, Radius = .21, Deadzone = .22;

    public static (double X, double Y) StickVector(double x, double y)
    {
        double length = Math.Sqrt(x * x + y * y);
        if (!double.IsFinite(length) || length <= Deadzone) return (0, 0);
        double strength = Math.Min(1, (length - Deadzone) / (1 - Deadzone));
        return (x / length * strength, y / length * strength);
    }

    public static Motion Sync(Hero hero, IMap? map)
    {
        var state = hero.Motion;
        if (state == null || !ReferenceEquals(state.Map, map) || state.Cell != hero.Position)
        {
            var p = ((double)hero.X, (double)hero.Y);
            state = new Motion { Position = p, Previous = p, Cell = hero.Position, Map = map };
            hero.Motion = state;
        }
        return state;
    }

    private static bool Fits(Hero hero, IMap map, (double X, double Y) p)
    {
        for (int ty = (int)Math.Floor(p.Y - Radius + .5); ty <= (int)Math.Floor(p.Y + Radius + .5); ty++)
            for (int tx = (int)Math.Floor(p.X - Radius + .5); tx <= (int)Math.Floor(p.X + Radius + .5); tx++)
            {
                if (hero.CanEnter(tx, ty, map)) continue;
                double nx = Math.Max(tx - .5, Math.Min(p.X, tx + .5)), ny = Math.Max(ty - .5, Math.Min(p.Y, ty + .5));
                if ((p.X - nx) * (p.X - nx) + (p.Y - ny) * (p.Y - ny) < Radius * Radius - 1e-9) return false;
            }
        return true;
    }

    /// <summary>locomotion.speed_factor: swift, the passive move speed, slow and the slower walk of channels.</summary>
    public static double SpeedFactor(Hero hero)
    {
        var channel = SkillWorld.Channeling(hero);
        return (1 + Status.Value(hero, "swift") + Passives.Value(hero, "move_speed")) * (1 - Status.Value(hero, "slow"))
               * (channel != null ? channel.Num("channel_speed", 1.0) : 1.0);
    }

    public static bool Advance(Hero hero, IMap map, (double X, double Y) input, double dt = 1 / 60.0)
    {
        var state = Sync(hero, map); state.Previous = state.Position;
        double strength = Math.Min(1, Math.Sqrt(input.X * input.X + input.Y * input.Y));
        (double X, double Y) desired = (0, 0);
        if (strength != 0)
        {
            double length = Math.Sqrt(input.X * input.X + input.Y * input.Y);
            double factor = SpeedFactor(hero);
            desired = (input.X / length * strength * Speed * factor, input.Y / length * strength * Speed * factor);
        }
        bool any = desired.X != 0 || desired.Y != 0;
        var (vx, vy) = state.Velocity;
        double change = (any ? 48 : 65) * dt;
        double ddx = desired.X - vx, ddy = desired.Y - vy, dlen = Math.Sqrt(ddx * ddx + ddy * ddy);
        if (dlen <= change) (vx, vy) = desired;
        else { vx += ddx / dlen * change; vy += ddy / dlen * change; }
        if (!any && vx == 0 && vy == 0) { state.Velocity = (0, 0); hero.IsMoving = false; return false; }
        if (any)
        {
            static int Axis(double v) => Math.Abs(v) < .18 ? 0 : v > 0 ? 1 : -1;
            hero.AttackDirection = (Axis(desired.X), Axis(desired.Y));
        }
        if (Math.Sqrt(vx * vx + vy * vy) > .02) hero.CancelAttack("movement");
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(vx * vx + vy * vy) * dt / .12));
        var (x, y) = state.Position;
        for (int s = 0; s < steps; s++)
            for (int axis = 0; axis < 2; axis++)
            {
                if ((axis == 0 ? vx : vy) == 0) continue;
                (double X, double Y) target = axis == 0 ? (x + vx * dt / steps, y) : (x, y + vy * dt / steps);
                if (!Fits(hero, map, target)) { if (axis == 0) vx = 0; else vy = 0; continue; }
                var cell = ((int)Math.Floor(target.X + .5), (int)Math.Floor(target.Y + .5));
                if (cell != hero.Position && !hero.Move(cell.Item1 - hero.X, cell.Item2 - hero.Y, map)) { if (axis == 0) vx = 0; else vy = 0; continue; }
                (x, y) = target;
            }
        state.Velocity = (vx, vy); state.Position = (x, y); state.Cell = hero.Position;
        double distance = Math.Sqrt((state.Position.X - state.Previous.X) * (state.Position.X - state.Previous.X) + (state.Position.Y - state.Previous.Y) * (state.Position.Y - state.Previous.Y));
        state.Distance += distance;
        hero.IsMoving = distance > 1e-5;
        return hero.IsMoving;
    }
}
