using System.Numerics;

namespace Umbra.Domain.World;

public enum Cell : byte { Wall, Floor }

public readonly record struct Room(int X, int Y, int Width, int Height)
{
    public Vector2 Center => new(X + Width / 2f, Y + Height / 2f);
    public bool Contains(Vector2 p) => p.X >= X && p.X < X + Width && p.Y >= Y && p.Y < Y + Height;
}

/// <summary>Tile grid in world units (1 tile = 1 unit). Tile (x, y) covers [x, x+1) x [y, y+1); world Y is Godot's Z.</summary>
public sealed class DungeonMap
{
    private readonly Cell[] _cells;

    public int Width { get; }
    public int Height { get; }
    public List<Room> Rooms { get; } = new();
    public List<(int X, int Y)> Torches { get; } = new();
    public List<Vector2> EnemySpawns { get; } = new();
    public Vector2 Start { get; set; }

    public DungeonMap(int width, int height)
    {
        Width = width; Height = height;
        _cells = new Cell[width * height];
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public Cell this[int x, int y]
    {
        get => InBounds(x, y) ? _cells[y * Width + x] : Cell.Wall;
        set { if (InBounds(x, y)) _cells[y * Width + x] = value; }
    }
    public bool IsWall(int x, int y) => this[x, y] == Cell.Wall;

    public void Carve(int x, int y, int width, int height)
    {
        for (int j = y; j < y + height; j++)
            for (int i = x; i < x + width; i++) this[i, j] = Cell.Floor;
    }

    /// <summary>Wall tiles that touch at least one floor tile: the only walls worth building as geometry.</summary>
    public IEnumerable<(int X, int Y)> VisibleWalls()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (IsWall(x, y) && Neighbours8(x, y).Any(n => !IsWall(n.X, n.Y))) yield return (x, y);
    }

    public IEnumerable<(int X, int Y)> Floors()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (!IsWall(x, y)) yield return (x, y);
    }

    public static IEnumerable<(int X, int Y)> Neighbours8(int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (dx != 0 || dy != 0) yield return (x + dx, y + dy);
    }

    /// <summary>True when every floor tile can be reached from <paramref name="from"/> (4-connected).</summary>
    public bool AllFloorsConnected((int X, int Y) from)
    {
        var seen = new HashSet<(int, int)> { from };
        var queue = new Queue<(int X, int Y)>(); queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var n in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                if (!IsWall(n.Item1, n.Item2) && seen.Add(n)) queue.Enqueue(n);
        }
        return seen.Count == Floors().Count();
    }
}
