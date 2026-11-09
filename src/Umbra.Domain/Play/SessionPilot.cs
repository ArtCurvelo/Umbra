using System.Numerics;
using Umbra.Domain.Enemies;

namespace Umbra.Domain.Play;

/// <summary>A simple autopilot for the benchmark, the screenshots, the self-test and --autopilot: walks UMBRA's path
/// (Navigation.shared_step) to the nearest living enemy, swings when next to it, uses Ruptura with enough Fúria and
/// Investida to close the gap, drinks a potion below a third of its life; then walks over the drops it can take.</summary>
public static class SessionPilot
{
    public static InputFrame Next(GameSession s)
    {
        var hero = s.Hero; var here = s.HeroWorld;
        if (hero.Inventory.Potion > 0 && hero.Health < hero.MaxHealth / 3 && s.Tick % 20 == 0) return InputFrame.None(here) with { Potion = true };
        var target = s.Enemies.Where(e => e.Alive).OrderBy(e => Vector2.Distance(GameSession.ToWorld(e.Position), here)).FirstOrDefault();
        if (target == null)
        {
            var item = s.Items.Where(i => i.Cell != hero.Position).OrderBy(i => Vector2.Distance(GameSession.ToWorld(i.Cell), here)).FirstOrDefault();
            return item == null ? InputFrame.None(here) : new InputFrame(Walk(s, item.Cell), null, here, false, false, false);
        }
        var at = GameSession.ToWorld(target.Position);
        int steps = Math.Max(Math.Abs(target.X - hero.X), Math.Abs(target.Y - hero.Y));
        bool near = steps <= 1;
        bool skill1 = near && hero.Resource >= 28 && s.Tick % 7 == 0;
        bool skill2 = !near && steps <= 4 && hero.Skills.Cooldowns.GetValueOrDefault("barb2_investida") == 0 && s.Tick % 11 == 0;
        return new InputFrame(near ? Vector2.Zero : Walk(s, target.Position), null, at, near, skill1, skill2);
    }

    /// <summary>Toward the centre of the next cell of UMBRA's path, around the other enemies.</summary>
    public static Vector2 Walk(GameSession s, (int X, int Y) goal)
    {
        var bodies = s.Enemies.Where(e => e.Alive && e.Position != goal).Select(e => (e.X, e.Y));
        var next = Navigation.SharedStep(s.Grid, s.Hero.Position, goal, bodies) ?? goal;
        var to = GameSession.ToWorld(next) - s.HeroWorld;
        return to.LengthSquared() > 1e-4f ? Vector2.Normalize(to) : Vector2.Zero;
    }
}
