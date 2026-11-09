using System.Numerics;
using Umbra.Domain.Data;
using Umbra.Domain.Enemies;
using Umbra.Domain.Gameplay;
using Umbra.Domain.Heroes;
using Umbra.Domain.Play;

namespace Umbra.Domain.Tests;

/// <summary>Step 5.8c: the slice on the real rules. A simple bot fights the crypt; the session must be deterministic and
/// every part of the loop (attack, skills, enemy strikes, deaths, experience, drops, pickups) must happen.</summary>
[Collection("SkillEnv")]
public class GameSessionTests
{
    static GameSessionTests() { HeroTables.Load(UmbraData.FindRoot()); FoeTables.Load(UmbraData.FindRoot()); }

    /// <summary>Walks to the nearest living enemy, uses Ruptura with enough Fúria, Investida when far, attacks when adjacent;
    /// after the fight walks over the drops.</summary>
    public static InputFrame Bot(GameSession s) => SessionPilot.Next(s);
    private static InputFrame OldBot(GameSession s)
    {
        var hero = s.Hero; var here = s.HeroWorld;
        var target = s.Enemies.Where(e => e.Alive).OrderBy(e => Vector2.Distance(GameSession.ToWorld(e.Position), here)).FirstOrDefault();
        if (target == null)
        {
            var item = s.Items.OrderBy(i => Vector2.Distance(GameSession.ToWorld(i.Cell), here)).FirstOrDefault();
            return item == null ? InputFrame.None(here) : new InputFrame(Vector2.Normalize(GameSession.ToWorld(item.Cell) - here + new Vector2(1e-3f, 0)), null, here, false, false, false);
        }
        var at = GameSession.ToWorld(target.Position); var to = at - here;
        int steps = Math.Max(Math.Abs(target.X - hero.X), Math.Abs(target.Y - hero.Y));
        bool near = steps <= 1;
        bool skill1 = near && hero.Resource >= 28 && s.Tick % 7 == 0;
        bool skill2 = !near && steps <= 4 && hero.Skills.Cooldowns.GetValueOrDefault("barb2_investida") == 0 && s.Tick % 11 == 0;
        return new InputFrame(near ? Vector2.Zero : Vector2.Normalize(to), null, at, near, skill1, skill2);
    }

    private static (GameSession Session, List<PlayEvent> Events) Play(ulong seed, int ticks)
    {
        var s = new GameSession(seed); var events = new List<PlayEvent>();
        for (int i = 0; i < ticks && s.Hero.Alive; i++) { s.Step(Bot(s)); events.AddRange(s.Events); }
        return (s, events);
    }

    [Fact]
    public void StartsAsTheSliceBarbarian()
    {
        var s = new GameSession(7);
        Assert.Equal(GameSession.StartLevel, s.Hero.Level);
        Assert.Equal("barb2_golpe_brutal", s.Hero.BasicSkillId);
        Assert.Equal(new string?[] { "barb2_ruptura", "barb2_investida", null, null }, s.Hero.EquippedMainSkills);
        Assert.Equal(s.Hero.MaxHealth, s.Hero.Health);
        Assert.All(s.Enemies, e => Assert.Equal("Bruto Abissal", e.Name));
        Assert.Equal(s.Map.EnemySpawns.Count, s.Enemies.Count);
    }

    [Fact]
    public void SameSeedSameRun()
    {
        var (a, ea) = Play(11, 3000); var (b, eb) = Play(11, 3000);
        Assert.Equal(a.Tick, b.Tick); Assert.Equal(a.Hero.Health, b.Hero.Health); Assert.Equal(a.Hero.Xp, b.Hero.Xp);
        Assert.Equal(a.Enemies.Select(e => (e.X, e.Y, e.Health)), b.Enemies.Select(e => (e.X, e.Y, e.Health)));
        Assert.Equal(ea.Count, eb.Count);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void TheWholeLoopHappens(ulong seed)
    {
        var (s, events) = Play(seed, 6000);
        Assert.True(s.Kills > 0, "nenhum inimigo morreu");
        Assert.True(s.Hero.Xp > 0 || s.Hero.Level > GameSession.StartLevel, "sem experiência");
        Assert.Contains(events, e => e is DamageEvent { OnHero: false });
        Assert.Contains(events, e => e is CastEvent { Anim: "basic" });
        Assert.Contains(events, e => e is CastEvent c && c.SkillId == "barb2_ruptura");
        Assert.Contains(events, e => e is DeathEvent);
        Assert.Contains(events, e => e is ItemDroppedEvent);
    }

    /// <summary>A hero who stands still is reached and struck: the Brutos wind up and land. (A hero who keeps swinging is
    /// almost never hit: in UMBRA every hit on an ordinary enemy cancels its wind-up — see the 5.8c report.)</summary>
    [Fact]
    public void BrutesHurtAHeroWhoStandsStill()
    {
        var s = new GameSession(4); var events = new List<PlayEvent>();
        var target = GameSession.ToWorld(s.Enemies[0].Position);
        for (int i = 0; i < 4000 && s.Hero.Alive; i++)
        {
            bool walking = Vector2.Distance(s.HeroWorld, target) > 2.5f;
            s.Step(new InputFrame(walking ? Vector2.Normalize(target - s.HeroWorld) : Vector2.Zero, null, target, false, false, false));
            events.AddRange(s.Events);
        }
        Assert.Contains(events, e => e is WindupEvent);
        Assert.Contains(events, e => e is DamageEvent { OnHero: true });
    }
}
