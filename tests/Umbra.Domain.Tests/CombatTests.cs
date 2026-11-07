using System.Numerics;
using Umbra.Domain.Gameplay;

namespace Umbra.Domain.Tests;

public class CombatTests
{
    /// <summary>A fresh run with every enemy parked far away except the ones the test places.</summary>
    private static Simulation Arena(params Vector2[] offsets)
    {
        var sim = new Simulation(11);
        var hall = sim.Map.Rooms[0];
        sim.Hero.Position = hall.Center;
        for (int i = 0; i < sim.Enemies.Count; i++)
        {
            var enemy = sim.Enemies[i];
            if (i < offsets.Length) { enemy.Position = hall.Center + offsets[i]; enemy.State = AiState.Idle; }
            else enemy.Health = 0;                      // out of this test
        }
        return sim;
    }

    private static InputFrame Attack(Simulation sim, Vector2 aim) => new(Vector2.Zero, null, aim, true, false, false);

    [Fact]
    public void DamageFollowsUmbraRule()
    {
        Assert.Equal(22, SliceData.Damage(22, 1.10f, 2));          // Golpe Brutal on a Bruto: int(24.2) - 2
        Assert.Equal(29, SliceData.Damage(22, 1.45f, 2));          // Ruptura: int(31.9) - 2
        Assert.Equal(6, SliceData.Damage(14, 1f, 8));              // Bruto on the Bárbaro
        Assert.Equal(1, SliceData.Damage(3, 1f, 50));              // never below 1
    }

    [Fact]
    public void BasicAttackHitsOnlyTheClosestEnemyInFrontAndGivesFury()
    {
        var sim = Arena(new Vector2(1f, 0), new Vector2(1.2f, .3f), new Vector2(-1f, 0));
        sim.Step(Attack(sim, sim.Hero.Position + new Vector2(1, 0)));
        Assert.Equal(65 - 22, sim.Enemies[0].Health);
        Assert.Equal(65, sim.Enemies[1].Health);
        Assert.Equal(65, sim.Enemies[2].Health);                   // behind the hero
        Assert.Equal(9, sim.Hero.Fury);
    }

    [Fact]
    public void SwingRecoveryLimitsAttackRate()
    {
        var sim = Arena(new Vector2(1f, 0));
        var aim = sim.Hero.Position + new Vector2(1, 0);
        for (int i = 0; i < SliceData.GolpeBrutal.RecoveryTicks; i++) sim.Step(Attack(sim, aim));
        Assert.Equal(65 - 22, sim.Enemies[0].Health);              // one swing in its own recovery window
        sim.Step(Attack(sim, aim));
        Assert.Equal(65 - 44, sim.Enemies[0].Health);
    }

    [Fact]
    public void RupturaCostsFuryAndHitsEveryoneInTheCone()
    {
        var sim = Arena(new Vector2(1.5f, 0), new Vector2(2f, .6f), new Vector2(0, 1.5f));
        sim.Hero.Fury = 30;
        sim.Step(new InputFrame(Vector2.Zero, null, sim.Hero.Position + new Vector2(1, 0), false, true, false));
        Assert.Equal(2, sim.Hero.Fury);
        Assert.Equal(65 - 29, sim.Enemies[0].Health);
        Assert.Equal(65 - 29, sim.Enemies[1].Health);
        Assert.Equal(65, sim.Enemies[2].Health);                   // 90° to the side: outside the cone
    }

    [Fact]
    public void RupturaNeedsFury()
    {
        var sim = Arena(new Vector2(1.5f, 0));
        sim.Step(new InputFrame(Vector2.Zero, null, sim.Hero.Position + new Vector2(1, 0), false, true, false));
        Assert.Equal(65, sim.Enemies[0].Health);
        Assert.Equal(0, sim.Hero.Fury);
    }

    [Fact]
    public void InvestidaMovesFourTilesHitsOnceAndGoesOnCooldown()
    {
        var sim = Arena(new Vector2(2f, 0));
        sim.Enemies[0].State = AiState.Recover; sim.Enemies[0].StateTicks = 10_000;   // stands still
        var start = sim.Hero.Position;
        var input = new InputFrame(Vector2.Zero, null, start + new Vector2(1, 0), false, false, true);
        for (int i = 0; i < SliceData.Investida.RecoveryTicks + 1; i++) sim.Step(input);
        Assert.True(sim.Hero.Position.X - start.X > 2.5f);          // the enemy body stops it short of the full 4 tiles
        Assert.Equal(65 - SliceData.Damage(22, .9f, 2), sim.Enemies[0].Health);
        Assert.True(sim.Hero.Cooldown(SliceData.Investida.Id) > SliceData.Investida.CooldownTicks - 20);
        for (int i = 0; i < 60; i++) sim.Step(input);
        Assert.Equal(65 - SliceData.Damage(22, .9f, 2), sim.Enemies[0].Health);       // still on cooldown
    }

    [Fact]
    public void EnemyTelegraphsBeforeStrikingAndTheHeroCanStepOut()
    {
        var sim = Arena(new Vector2(1.2f, 0));
        var idle = InputFrame.None(sim.Hero.Position);
        sim.Enemies[0].State = AiState.Chase;
        sim.Step(idle);
        Assert.Contains(sim.Events, e => e is TelegraphEvent);
        for (int i = 0; i < SliceData.BruteWindupTicks - 2; i++) sim.Step(idle);
        Assert.Equal(190, sim.Hero.Health);                        // nothing lands during the wind-up

        var dodge = Arena(new Vector2(1.2f, 0)); dodge.Enemies[0].State = AiState.Chase;
        dodge.Step(InputFrame.None(dodge.Hero.Position));
        for (int i = 0; i < SliceData.BruteWindupTicks + 2; i++)
            dodge.Step(new InputFrame(new Vector2(-1, 0), null, dodge.Hero.Position, false, false, false));
        Assert.Equal(190, dodge.Hero.Health);                      // walked out of the committed strike

        var stay = Arena(new Vector2(1.2f, 0)); stay.Enemies[0].State = AiState.Chase;
        for (int i = 0; i < SliceData.BruteWindupTicks + 2; i++) stay.Step(InputFrame.None(stay.Hero.Position));
        Assert.Equal(190 - 6, stay.Hero.Health);                   // stood still: hit for max(1, 14 - 8)
    }

    [Fact]
    public void EnemiesDetectTheHeroOnlyWithinRangeAndSight()
    {
        var sim = Arena(new Vector2(3f, 0), new Vector2(0, 9f));
        sim.Step(InputFrame.None(sim.Hero.Position));
        Assert.Equal(AiState.Chase, sim.Enemies[0].State);
        Assert.Equal(AiState.Idle, sim.Enemies[1].State);
    }

    [Fact]
    public void BodiesDoNotOverlap()
    {
        var sim = Arena(new Vector2(.1f, 0), new Vector2(.2f, .1f));
        for (int i = 0; i < 30; i++) sim.Step(InputFrame.None(sim.Hero.Position));
        var alive = sim.Enemies.Where(e => e.Alive).Cast<Actor>().Append(sim.Hero).ToList();
        for (int i = 0; i < alive.Count; i++)
            for (int j = i + 1; j < alive.Count; j++)
                Assert.True(Vector2.Distance(alive[i].Position, alive[j].Position) >= alive[i].Radius + alive[j].Radius - .05f);
    }
}
