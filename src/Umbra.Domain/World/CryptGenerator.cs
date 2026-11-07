using System.Numerics;
using Umbra.Domain.Core;

namespace Umbra.Domain.World;

/// <summary>
/// Vertical-slice crypt: an entry hall, a corridor and a burial chamber, laid out west to east.
/// The seed varies the room sizes, the corridor length and where enemies wait; the shape is always the same three parts,
/// so the slice stays readable and testable.
/// </summary>
public static class CryptGenerator
{
    public const int EnemyCount = 6;

    public static DungeonMap Generate(ulong seed)
    {
        var rng = new Rng(seed);
        int hallW = rng.Range(7, 9), hallH = rng.Range(7, 9);
        int corridor = rng.Range(6, 9);
        int chamberW = rng.Range(10, 12), chamberH = rng.Range(9, 11);
        int height = Math.Max(hallH, chamberH) + 2;
        int width = 1 + hallW + corridor + chamberW + 1;
        var map = new DungeonMap(width, height);

        var hall = new Room(1, (height - hallH) / 2, hallW, hallH);
        var chamber = new Room(1 + hallW + corridor, (height - chamberH) / 2, chamberW, chamberH);
        int mid = height / 2;
        var hallway = new Room(hall.X + hall.Width, mid - 1, corridor, 2);
        foreach (var room in new[] { hall, hallway, chamber })
        {
            map.Carve(room.X, room.Y, room.Width, room.Height);
            map.Rooms.Add(room);
        }
        map.Start = hall.Center;

        // Torches: on walls facing a floor, every 4 tiles along the room edges.
        foreach (var room in new[] { hall, chamber })
        {
            for (int x = room.X + 1; x < room.X + room.Width - 1; x += 4)
            {
                map.Torches.Add((x, room.Y - 1));
                map.Torches.Add((x, room.Y + room.Height));
            }
        }
        map.Torches.RemoveAll(t => !map.IsWall(t.X, t.Y));

        // One enemy waits at the corridor's end; the others stand in the chamber, never at the very entrance.
        map.EnemySpawns.Add(new Vector2(hallway.X + hallway.Width - 1.5f, mid));
        var taken = new HashSet<(int, int)>();
        while (map.EnemySpawns.Count < EnemyCount)
        {
            int x = rng.Range(chamber.X + 2, chamber.X + chamber.Width - 2);
            int y = rng.Range(chamber.Y + 1, chamber.Y + chamber.Height - 2);
            if (taken.Add((x, y))) map.EnemySpawns.Add(new Vector2(x + .5f, y + .5f));
        }
        return map;
    }
}
