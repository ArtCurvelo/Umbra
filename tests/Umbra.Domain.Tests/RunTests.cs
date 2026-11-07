using System.Numerics;
using Umbra.Domain.Core;
using Umbra.Domain.Gameplay;

namespace Umbra.Domain.Tests;

public class RunTests
{
    [Fact]
    public void RngIsDeterministic()
    {
        var a = new Rng(5); var b = new Rng(5);
        Assert.Equal(Enumerable.Range(0, 20).Select(_ => a.NextULong()), Enumerable.Range(0, 20).Select(_ => b.NextULong()));
        var r = new Rng(9);
        Assert.All(Enumerable.Range(0, 1000).Select(_ => r.Range(2, 5)), v => Assert.InRange(v, 2, 5));
    }

    [Fact]
    public void LootRarityDistributionFollowsTheWeights()
    {
        var rng = new Rng(1); var counts = new int[4];
        for (int i = 0; i < 20_000; i++) counts[(int)Loot.Roll(rng).Rarity]++;
        for (int i = 0; i < 4; i++) Assert.InRange(counts[i] / 20_000.0, Loot.Weights[i] / 100.0 - .015, Loot.Weights[i] / 100.0 + .015);
    }

    [Fact]
    public void ItemShowsRarityNameAndTag()
    {
        var item = new Item("Anel de Cinza", "Anel", Rarity.Lendario);
        Assert.Equal("Lendário", item.RarityName);
        Assert.Equal("L", item.Tag);
    }

    [Theory]
    [InlineData(1UL)] [InlineData(2UL)] [InlineData(3UL)] [InlineData(11UL)] [InlineData(99UL)]
    public void AutopilotClearsTheCryptAndCollectsTheLoot(ulong seed)
    {
        var sim = new Simulation(seed);
        int ticks = Autopilot.Run(sim, 120 * 60);
        Assert.True(sim.Hero.Alive, $"hero died at tick {sim.Tick}");
        Assert.True(sim.Cleared, $"{sim.EnemiesRemaining} enemies left after {ticks} ticks");
        Assert.Empty(sim.Drops);
        Assert.NotEmpty(sim.Inventory);                            // the last kill always drops when nothing did before
        Assert.True(sim.Hero.Health < sim.Hero.MaxHealth);   // the brutes fight back
    }

    [Fact]
    public void SameSeedAndInputsReplayTheSameRun()
    {
        string Trace(ulong seed)
        {
            var sim = new Simulation(seed); var parts = new List<string>();
            for (int i = 0; i < 3000; i++)
            {
                sim.Step(Autopilot.Next(sim));
                if (i % 100 == 0) parts.Add($"{sim.Hero.Position.X:F4},{sim.Hero.Position.Y:F4},{sim.Hero.Health},{sim.EnemiesRemaining}");
            }
            parts.Add(string.Join('|', sim.Inventory.Select(x => x.Name + x.Tag)));
            return string.Join(';', parts);
        }
        Assert.Equal(Trace(21), Trace(21));
        Assert.NotEqual(Trace(21), Trace(22));
    }

    [Fact]
    public void ClickToMoveWalksToTheDestinationAndStops()
    {
        var sim = new Simulation(4);
        var goal = sim.Hero.Position + new Vector2(2f, 1f);
        sim.Step(new InputFrame(Vector2.Zero, goal, goal, false, false, false));
        for (int i = 0; i < 120; i++) sim.Step(new InputFrame(Vector2.Zero, null, goal, false, false, false));
        Assert.True(Vector2.Distance(sim.Hero.Position, goal) < .1f);
        Assert.Null(sim.Hero.Destination);
    }

    [Fact]
    public void HeroWalksAtTheDataSpeed()
    {
        var sim = new Simulation(4);
        foreach (var e in sim.Enemies) e.Health = 0;
        var start = sim.Hero.Position;
        for (int i = 0; i < 30; i++) sim.Step(new InputFrame(new Vector2(0, 1), null, start, false, false, false));
        Assert.InRange(sim.Hero.Position.Y - start.Y, 2.4f - .01f, 2.4f + .01f);   // 4,8 tiles/s for 0,5 s (room is tall enough)
    }
}
