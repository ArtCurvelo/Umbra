using Umbra.Domain.Combat;

namespace Umbra.Domain.Combat.V16;

/// <summary>The tile map as the skill engine sees it (UMBRA dungeon: map[y][x], 0 = floor, 1 = wall).</summary>
public interface IMap
{
    int Width { get; }
    int Height { get; }
    int Cell(int x, int y);
    /// <summary>world.sanctuary.inside: city tiles where nobody fights.</summary>
    bool Sanctuary(int x, int y);
}

/// <summary>Tile geometry of the 0.16 skill shapes — port of game/combat_system/v16/geometry.py (and the line of sight of
/// game/world/dungeon.py).</summary>
public static class Geo
{
    public static readonly (int X, int Y)[] Eight = { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };
    public const int Wall = 1;

    public static int Sign(int value) => value == 0 ? 0 : value > 0 ? 1 : -1;

    public static (int X, int Y) Direction((int X, int Y) origin, (int X, int Y) target, (int X, int Y)? fallback = null)
    {
        int dx = target.X - origin.X, dy = target.Y - origin.Y;
        if (dx == 0 && dy == 0) return fallback ?? (0, 1);
        double angle = Math.Atan2(dy, dx);
        long octant = ((PyMath.Round(angle / (Math.PI / 4)) % 8) + 8) % 8;
        return Eight[octant];
    }

    public static int Steps((int X, int Y) a, (int X, int Y) b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    public static int Manhattan((int X, int Y) a, (int X, int Y) b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    public static bool Walkable(IMap? map, (int X, int Y) p) =>
        map == null || (0 <= p.X && p.X < map.Width && 0 <= p.Y && p.Y < map.Height && map.Cell(p.X, p.Y) == 0);

    public static bool Clear(IMap? map, (int X, int Y) a, (int X, int Y) b) => map == null || LineOfSight(map, a, b);

    /// <summary>dungeon.has_line_of_sight: Bresenham; walls block, and so does a diagonal squeezed between two walls.</summary>
    public static bool LineOfSight(IMap map, (int X, int Y) start, (int X, int Y) end)
    {
        int x0 = start.X, y0 = start.Y, x1 = end.X, y1 = end.Y;
        if (!(0 <= x0 && x0 < map.Width && 0 <= y0 && y0 < map.Height)) return false;
        if (!(0 <= x1 && x1 < map.Width && 0 <= y1 && y1 < map.Height)) return false;
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, error = dx - dy;
        while ((x0, y0) != (x1, y1))
        {
            int px = x0, py = y0, twice = 2 * error;
            if (twice > -dy) { error -= dy; x0 += sx; }
            if (twice < dx) { error += dx; y0 += sy; }
            if (x0 != px && y0 != py && map.Cell(x0, py) == Wall && map.Cell(px, y0) == Wall) return false;
            if ((x0, y0) == (x1, y1)) return true;
            if (!(0 <= x0 && x0 < map.Width && 0 <= y0 && y0 < map.Height)) return false;
            if (map.Cell(x0, y0) == Wall) return false;
        }
        return true;
    }

    /// <summary>Cells of a straight beam `length` tiles long; width 3 adds one lane on each side.</summary>
    public static List<(int X, int Y)> LineCells((int X, int Y) origin, (int X, int Y) facing, double length, double width = 1)
    {
        var (dx, dy) = facing; var side = (-dy, dx); int half = Math.Max(0, ((int)Math.Truncate(width) - 1) / 2);
        var cells = new List<(int, int)>();
        for (int i = 1; i <= (int)Math.Truncate(length); i++)
            for (int lane = -half; lane <= half; lane++)
                cells.Add((origin.X + dx * i + side.Item1 * lane, origin.Y + dy * i + side.Item2 * lane));
        return cells;
    }

    /// <summary>Inside a cone (cos &gt;= spread) up to `length` tiles.</summary>
    public static bool InCone((int X, int Y) origin, (int X, int Y) facing, double length, (int X, int Y) position, double spread = .55)
    {
        int vx = position.X - origin.X, vy = position.Y - origin.Y;
        if ((vx == 0 && vy == 0) || Steps(origin, position) > length) return false;
        double norm = Hypot(vx, vy) * Hypot(facing.X, facing.Y);
        return (vx * facing.X + vy * facing.Y) / norm >= spread;
    }

    private static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);

    /// <summary>True when the attacker stands on the side the enemy is not facing.</summary>
    public static bool Behind(ITarget enemy, ITarget attacker)
    {
        var (fx, fy) = enemy.AttackDirection;
        if (fx == 0 && fy == 0) return false;
        return (attacker.X - enemy.X) * fx + (attacker.Y - enemy.Y) * fy < 0;
    }

    /// <summary>Cells walked by a dash/teleport, stopping before walls and occupied cells.</summary>
    public static List<(int X, int Y)> Path(IMap? map, (int X, int Y) origin, (int X, int Y) facing, double distance, ISet<(int, int)> blockers)
    {
        var cells = new List<(int, int)>(); var (x, y) = origin;
        for (int i = 0; i < (int)Math.Truncate(distance); i++)
        {
            var next = (x + facing.X, y + facing.Y);
            if (!Walkable(map, next) || blockers.Contains(next)) break;
            cells.Add(next); (x, y) = next;
        }
        return cells;
    }
}
