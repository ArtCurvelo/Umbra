using System.Numerics;
using Umbra.Domain.Gameplay;

namespace Umbra.Domain.Play;

/// <summary>A simple autopilot for the benchmark, the screenshots and --autopilot: walks to the nearest living enemy,
/// swings when next to it, uses Ruptura with enough Fúria and Investida to close the gap; then walks over the drops.</summary>
public static class SessionPilot
{
    public static InputFrame Next(GameSession s)
    {
        var hero = s.Hero; var here = s.HeroWorld;
        var target = s.Enemies.Where(e => e.Alive).OrderBy(e => Vector2.Distance(GameSession.ToWorld(e.Position), here)).FirstOrDefault();
        if (target == null)
        {
            var item = s.Items.OrderBy(i => Vector2.Distance(GameSession.ToWorld(i.Cell), here)).FirstOrDefault();
            if (item == null) return InputFrame.None(here);
            var to = GameSession.ToWorld(item.Cell) - here;
            return new InputFrame(to.LengthSquared() > 1e-4f ? Vector2.Normalize(to) : Vector2.Zero, null, here, false, false, false);
        }
        var at = GameSession.ToWorld(target.Position);
        int steps = Math.Max(Math.Abs(target.X - hero.X), Math.Abs(target.Y - hero.Y));
        bool near = steps <= 1;
        bool skill1 = near && hero.Resource >= 28 && s.Tick % 7 == 0;
        bool skill2 = !near && steps <= 4 && hero.Skills.Cooldowns.GetValueOrDefault("barb2_investida") == 0 && s.Tick % 11 == 0;
        return new InputFrame(near ? Vector2.Zero : Vector2.Normalize(at - here), null, at, near, skill1, skill2);
    }
}
