using System.Numerics;
using Umbra.Domain.World;

namespace Umbra.Domain.Tests;

public class WorldTests
{
    [Theory]
    [InlineData(1UL)] [InlineData(7UL)] [InlineData(42UL)] [InlineData(123456789UL)]
    public void CryptIsDeterministicConnectedAndHasEnemiesAndTorches(ulong seed)
    {
        var a = CryptGenerator.Generate(seed); var b = CryptGenerator.Generate(seed);
        Assert.Equal(a.Width, b.Width); Assert.Equal(a.Height, b.Height);
        Assert.Equal(a.Floors(), b.Floors());
        Assert.Equal(a.EnemySpawns, b.EnemySpawns);
        Assert.Equal(3, a.Rooms.Count);
        var start = ((int)a.Start.X, (int)a.Start.Y);
        Assert.False(a.IsWall(start.Item1, start.Item2));
        Assert.True(a.AllFloorsConnected(start));
        Assert.Equal(CryptGenerator.EnemyCount, a.EnemySpawns.Count);
        Assert.All(a.EnemySpawns, s => Assert.False(Collision.Blocked(a, s, .45f)));
        Assert.NotEmpty(a.Torches);
        Assert.All(a.Torches, t => Assert.True(a.IsWall(t.X, t.Y)));
    }

    [Fact]
    public void DifferentSeedsGiveDifferentCrypts()
    {
        var shapes = Enumerable.Range(1, 12).Select(s => CryptGenerator.Generate((ulong)s))
                               .Select(m => (m.Width, m.Height, string.Join(';', m.EnemySpawns))).Distinct().Count();
        Assert.True(shapes > 1);
    }

    [Fact]
    public void MovementSlidesAlongWallsAndNeverEntersThem()
    {
        var map = CryptGenerator.Generate(3);
        var room = map.Rooms[0];
        var p = new Vector2(room.X + 1f, room.Y + room.Height / 2f);
        for (int i = 0; i < 200; i++) p = Collision.Move(map, p, new Vector2(-.08f, .02f), .35f);   // push into the west wall
        Assert.False(Collision.Blocked(map, p, .35f));
        Assert.InRange(p.X, room.X + .35f - 1e-3f, room.X + .5f);       // stopped at the wall
        Assert.True(p.Y > room.Y + room.Height / 2f + .5f);              // but kept sliding south
    }

    [Fact]
    public void FastMoveDoesNotTunnelThroughWalls()
    {
        var map = CryptGenerator.Generate(3);
        var room = map.Rooms[0];
        var p = new Vector2(room.X + 1f, room.Center.Y);
        var after = Collision.Move(map, p, new Vector2(-6f, 0), .35f);
        Assert.True(after.X >= room.X + .35f - 1e-3f);
    }

    [Fact]
    public void LineOfSightIsBlockedByWalls()
    {
        var map = CryptGenerator.Generate(3);
        var hall = map.Rooms[0]; var chamber = map.Rooms[2];
        Assert.True(Collision.LineOfSight(map, hall.Center, hall.Center + new Vector2(1, 1)));
        var cornerOfHall = new Vector2(hall.X + .5f, hall.Y + .5f);
        var cornerOfChamber = new Vector2(chamber.X + .5f, chamber.Y + chamber.Height - .5f);
        Assert.False(Collision.LineOfSight(map, cornerOfHall, cornerOfChamber));
    }
}
