using System.Numerics;

namespace Umbra.Domain.World;

/// <summary>Circle against the tile grid. Movement is resolved one axis at a time, so actors slide along walls.</summary>
public static class Collision
{
    public static bool Blocked(DungeonMap map, Vector2 p, float radius)
    {
        int x0 = (int)MathF.Floor(p.X - radius), x1 = (int)MathF.Floor(p.X + radius);
        int y0 = (int)MathF.Floor(p.Y - radius), y1 = (int)MathF.Floor(p.Y + radius);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!map.IsWall(x, y)) continue;
                float cx = Math.Clamp(p.X, x, x + 1), cy = Math.Clamp(p.Y, y, y + 1);
                float dx = p.X - cx, dy = p.Y - cy;
                if (dx * dx + dy * dy < radius * radius) return true;
            }
        return false;
    }

    /// <summary>Moves a circle by <paramref name="delta"/>; returns the reached position (never inside a wall).</summary>
    public static Vector2 Move(DungeonMap map, Vector2 from, Vector2 delta, float radius)
    {
        // Sub-steps keep fast moves (the dash) from tunnelling through a wall.
        int steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / (radius * .5f)));
        var step = delta / steps; var p = from;
        for (int i = 0; i < steps; i++)
        {
            var tryX = new Vector2(p.X + step.X, p.Y);
            if (!Blocked(map, tryX, radius)) p = tryX;
            var tryY = new Vector2(p.X, p.Y + step.Y);
            if (!Blocked(map, tryY, radius)) p = tryY;
        }
        return p;
    }

    /// <summary>Clear straight line between two points (sampled every quarter tile), for enemy sight.</summary>
    public static bool LineOfSight(DungeonMap map, Vector2 a, Vector2 b)
    {
        float length = Vector2.Distance(a, b);
        int samples = Math.Max(1, (int)(length * 4));
        for (int i = 1; i < samples; i++)
        {
            var p = Vector2.Lerp(a, b, i / (float)samples);
            if (map.IsWall((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y))) return false;
        }
        return true;
    }
}
