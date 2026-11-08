using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;

namespace Umbra.Domain.Enemies;

/// <summary>Paths on the tile map — world/navigation.shared_path_step (a distance field plus a bounded local search),
/// world/dungeon.find_path_step (plain breadth-first search, used by bosses) and sanctuary.evacuate.</summary>
public static class Navigation
{
    private static readonly (int X, int Y)[] Four = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    public static Dictionary<(int, int), int> DistanceField(IMap map, (int X, int Y) goal)
    {
        var distances = new Dictionary<(int, int), int>();
        if (map.Sanctuary(goal.X, goal.Y)) return distances;
        var queue = new Queue<(int X, int Y)>(); distances[goal] = 0; queue.Enqueue(goal);
        bool big = map.Width * map.Height > 12000;
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (dx, dy) in Four)
            {
                var p = (X: x + dx, Y: y + dy);
                if (distances.ContainsKey(p)) continue;
                if (big && Math.Max(Math.Abs(p.X - goal.X), Math.Abs(p.Y - goal.Y)) > 24) continue;
                if (0 <= p.X && p.X < map.Width && 0 <= p.Y && p.Y < map.Height && map.Cell(p.X, p.Y) == 0 && !map.Sanctuary(p.X, p.Y))
                {
                    distances[p] = distances[(x, y)] + 1; queue.Enqueue(p);
                }
            }
        }
        return distances;
    }

    public static (int X, int Y)? SharedStep(IMap map, (int X, int Y) start, (int X, int Y) goal, IEnumerable<(int, int)> blocked)
    {
        if (start == goal) return null;
        var distances = DistanceField(map, goal);
        var obstacles = new HashSet<(int, int)>(blocked); obstacles.Remove(start); obstacles.Remove(goal);
        double D((int, int) p) => distances.TryGetValue(p, out var d) ? d : double.PositiveInfinity;
        foreach (var (dx, dy) in Four.OrderBy(d => D((start.X + d.X, start.Y + d.Y))))
        {
            var p = (start.X + dx, start.Y + dy);
            if (!obstacles.Contains(p) && D(p) < D(start)) return p;
        }
        var queue = new Queue<((int X, int Y) Pos, (int X, int Y)? First)>(); queue.Enqueue((start, null));
        var visited = new HashSet<(int, int)> { start };
        while (queue.Count > 0 && visited.Count <= 160)
        {
            var ((x, y), first) = queue.Dequeue();
            foreach (var (dx, dy) in Four)
            {
                var p = (X: x + dx, Y: y + dy);
                if (visited.Contains(p) || obstacles.Contains(p) || !distances.ContainsKey(p)) continue;
                var step = first ?? p;
                if (p == goal) return step;
                visited.Add(p); queue.Enqueue((p, step));
            }
        }
        return null;
    }

    public static (int X, int Y)? BfsStep(IMap map, (int X, int Y) start, (int X, int Y) goal, IEnumerable<(int, int)> blocked)
    {
        if (start == goal) return null;
        var obstacles = new HashSet<(int, int)>(blocked); obstacles.Remove(start); obstacles.Remove(goal);
        var queue = new Queue<((int X, int Y) Pos, (int X, int Y)? First)>(); queue.Enqueue((start, null));
        var visited = new HashSet<(int, int)> { start };
        while (queue.Count > 0)
        {
            var ((x, y), first) = queue.Dequeue();
            foreach (var (dx, dy) in Four)
            {
                var p = (X: x + dx, Y: y + dy);
                if (visited.Contains(p) || obstacles.Contains(p)) continue;
                if (!(0 <= p.X && p.X < map.Width && 0 <= p.Y && p.Y < map.Height) || map.Cell(p.X, p.Y) != 0) continue;
                var step = first ?? p;
                if (p == goal) return step;
                visited.Add(p); queue.Enqueue((p, step));
            }
        }
        return null;
    }

    /// <summary>sanctuary.evacuate: an enemy that ends up in a city is moved to the nearest floor tile outside it.</summary>
    public static void Evacuate(IMap map, Enemy enemy)
    {
        if (!enemy.Alive || !map.Sanctuary(enemy.X, enemy.Y)) return;
        var candidates = new List<(int X, int Y)>();
        for (int b = 0; b < map.Height; b++)
            for (int a = 0; a < map.Width; a++)
                if (map.Cell(a, b) == 0 && !map.Sanctuary(a, b) && !map.Gate(a, b)) candidates.Add((a, b));
        if (candidates.Count > 0)
        {
            int x = enemy.X, y = enemy.Y;
            (enemy.X, enemy.Y) = Umbra.Domain.Skills.Py.MinBy(candidates, p => Math.Abs(p.X - x) + Math.Abs(p.Y - y));
        }
        enemy.Windup = 0; enemy.CurrentAttack = null; enemy.LastKnownPlayer = null;
    }
}

/// <summary>Perception, chase and investigation of ordinary enemies — port of game/combat_system/ai.py.</summary>
public static class EnemyAI
{
    public const int MemoryFrames = 180;
    private static readonly string[] Ranged = { "cultista", "arqueiro", "bruxa" };

    private static int Distance((int X, int Y) a, (int X, int Y) b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    public static List<(int, int)> Occupied(IMap map, Enemy enemy, IReadOnlyList<Enemy> all) =>
        all.Where(e => !ReferenceEquals(e, enemy) && e.Alive).Select(e => e.Position).Concat(FoeTables.Hooks.Structures(map)).ToList();

    private static (int X, int Y)? RetreatStep(Enemy enemy, IFoeTarget player, IMap map, ICollection<(int, int)> occupied)
    {
        var candidates = new List<(int X, int Y)>();
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var p = (X: enemy.X + dx, Y: enemy.Y + dy);
            if (occupied.Contains(p) || p == player.Position) continue;
            if (!(0 <= p.X && p.X < map.Width && 0 <= p.Y && p.Y < map.Height) || map.Cell(p.X, p.Y) != 0) continue;
            candidates.Add(p);
        }
        if (candidates.Count == 0) return null;
        return Umbra.Domain.Skills.Py.MaxBy(candidates, p => (Distance(p, player.Position), Geo.LineOfSight(map, p, player.Position) ? 1 : 0));
    }

    private static bool Move(Enemy enemy, (int X, int Y)? step)
    {
        if (enemy.Statuses?.Get("root") is { Count: > 0 }) step = null;     // rooted: still turns and attacks, but stays put
        if (step == null || step == enemy.Position) { enemy.StuckFrames++; return false; }
        enemy.PreviousPosition = enemy.Position; (enemy.X, enemy.Y) = step.Value; enemy.StuckFrames = 0;
        return true;
    }

    /// <summary>The enemies of the current map (dungeon._active_enemies), set by the engine before the enemy step.</summary>
    public static IReadOnlyList<Enemy> Active = Array.Empty<Enemy>();

    public static void Update(Enemy enemy, IFoeTarget player, IMap map)
    {
        if (!enemy.Alive || !player.Alive) return;
        enemy.AttackMap = map;
        int stagger = Math.Max(0, enemy.StaggerFrames);
        if (stagger != 0) { enemy.StaggerFrames = stagger - 1; enemy.AiState = "staggered"; return; }
        if (enemy.AttackCooldown > 0) enemy.AttackCooldown--;
        if (enemy.Windup > 0)
        {
            enemy.Windup--; enemy.AiState = "windup";
            if (enemy.Windup == 0) enemy.PerformAttack(player);
            return;
        }
        var states = enemy.Statuses;
        bool blind = states?.Get("blind") is { Count: > 0 };
        if (player.Statuses?.Get("stealth") is { Count: > 0 }) blind = true;      // Furtividade loses the hero too
        bool visible = !blind && Distance(enemy.Position, player.Position) <= enemy.DetectionRange && Geo.LineOfSight(map, enemy.Position, player.Position);
        if (blind) { enemy.LastKnownPlayer = null; enemy.MemoryFrames = 0; }
        if (visible) { enemy.LastKnownPlayer = player.Position; enemy.MemoryFrames = MemoryFrames; enemy.AiState = "chase"; }
        else
        {
            enemy.MemoryFrames = Math.Max(0, enemy.MemoryFrames - 1);
            if (enemy.MemoryFrames <= 0) { enemy.LastKnownPlayer = null; enemy.AiState = "idle"; }
            else enemy.AiState = "investigate";
        }
        if (enemy.MoveCooldown > 0) { enemy.MoveCooldown--; return; }
        int distance = Distance(enemy.Position, player.Position);
        bool ranged = Ranged.Contains(enemy.EnemyType);
        if (states?.Get("fear") is { Count: > 0 })                                 // Medo: runs away and never attacks
        {
            Move(enemy, RetreatStep(enemy, player, map, Occupied(map, enemy, Active)));
            enemy.AiState = "investigate";
            return;
        }
        if (states?.Get("numb") is { Count: > 0 } && ranged) ranged = false;      // Entorpecimento: no ranged attacks
        if (visible && enemy.AttackCooldown <= 0)
        {
            if (ranged && 2 <= distance && distance <= enemy.AttackRange) { enemy.PrepareAttack("ranged", player); enemy.AiState = "windup"; return; }
            if (!ranged && distance == 1) { enemy.PrepareAttack("normal", player); enemy.AiState = "windup"; return; }
        }
        var target = visible ? player.Position : enemy.LastKnownPlayer;
        if (target == null) return;
        var occupied = Occupied(map, enemy, Active);
        (int X, int Y)? step;
        if (ranged && visible && distance < 2) step = RetreatStep(enemy, player, map, occupied);
        else
        {
            step = Navigation.SharedStep(map, enemy.Position, target.Value, occupied);
            if (step == player.Position) step = null;
        }
        if (step == null && enemy.AttackCooldown <= 0 && BreakOut(enemy, target.Value, map)) return;
        Move(enemy, step);
        enemy.MoveCooldown = Math.Max(1, enemy.BaseMoveCooldown);
        if (enemy.AiState == "investigate" && enemy.Position == target.Value) { enemy.LastKnownPlayer = null; enemy.MemoryFrames = 0; enemy.AiState = "idle"; }
    }

    /// <summary>Walled in by a Necromancer structure: strike the segment between the enemy and its target.</summary>
    private static bool BreakOut(Enemy enemy, (int X, int Y) target, IMap map)
    {
        var walls = FoeTables.Hooks.Structures(map).ToHashSet();
        if (walls.Count == 0) return false;
        var cells = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(d => (X: enemy.X + d.Item1, Y: enemy.Y + d.Item2)).Where(c => walls.Contains(c))
                                                          .OrderBy(c => Math.Abs(c.X - target.X) + Math.Abs(c.Y - target.Y)).ToList();
        if (cells.Count == 0 || !FoeTables.Hooks.StrikeStructure(map, enemy, cells[0], enemy.Attack)) return false;
        enemy.AttackCooldown = 60; enemy.AiState = "breaking";
        return true;
    }
}
