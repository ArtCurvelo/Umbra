using System.Numerics;
using Umbra.Domain.Data;
using Umbra.Domain.Enemies;
using Umbra.Domain.Core;
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
        Assert.Equal(new[] { "barb2_golpe_brutal", "barb2_ruptura", "barb2_investida" }, s.Kit);     // the slice's Bárbaro, unchanged
        Assert.Equal("barb2_golpe_brutal", s.Hero.BasicSkillId);
        Assert.Equal(new string?[] { "barb2_ruptura", "barb2_investida", null, null }, s.Hero.EquippedMainSkills);
        Assert.Equal(s.Hero.MaxHealth, s.Hero.Health);
    }

    /// <summary>The crypt is populated by UMBRA's dungeon rule (crypt family, packs, cap of 10 on floor 1, hero's level,
    /// elites) plus the boss at the far end of the burial chamber.</summary>
    [Fact]
    public void TheCryptIsPopulatedLikeUmbra()
    {
        int elites = 0, total = 0, seeds = 60;
        var sizes = new HashSet<int>();
        for (ulong seed = 1; seed <= (ulong)seeds; seed++)
        {
            var s = new GameSession(seed);
            var common = s.Enemies.Where(e => !e.Boss).ToList();
            Assert.Single(s.Enemies, e => e.Boss);
            Assert.Same(s.Boss, s.Enemies.Single(e => e.Boss));
            Assert.Equal("Guardião das Cinzas", s.Boss.Name);
            Assert.InRange(common.Count, 0, 10);
            Assert.All(common, e => Assert.Contains(e.EnemyType, GameSession.CryptPool));
            Assert.All(s.Enemies, e => Assert.False(s.Map.IsWall(e.X, e.Y)));
            Assert.Equal(s.Enemies.Count, s.Enemies.Select(e => e.Position).Distinct().Count());
            Assert.DoesNotContain(s.Enemies, e => e.Position == s.Hero.Position);
            var chamber = s.Map.Rooms[^1];
            Assert.Equal(chamber.X + chamber.Width - 2, s.Boss.X);
            // the hero's level: a Sombra at level 6 is stronger than at level 1
            var levelOne = new Enemy((0, 0), 1, false, "sombra");
            Assert.All(common.Where(e => e.EnemyType == "sombra" && e.Rank != "elite"), e => Assert.True(e.MaxHealth > levelOne.MaxHealth));
            elites += common.Count(e => e.Rank == "elite"); total += common.Count;
            sizes.Add(common.Count);
        }
        Assert.True(sizes.Count > 3, "o tamanho da população devia variar com a semente");
        Assert.InRange(elites / (double)total, 0.0, 0.15);                // elites.CHANCE = 5%
        var a = new GameSession(9); var b = new GameSession(9);
        Assert.Equal(a.Enemies.Select(e => (e.EnemyType, e.X, e.Y, e.Rank)), b.Enemies.Select(e => (e.EnemyType, e.X, e.Y, e.Rank)));
    }

    /// <summary>process_enemy_deaths, boss branch: Fúria, the profile's experience and gold, and its drops — among them its
    /// own relic (RELIC_CATALOG: the Guardião das Cinzas leaves the Brasa do Primeiro Carrasco).</summary>
    [Fact]
    public void TheBossLeavesItsRewards()
    {
        var s = new GameSession(3);
        foreach (var e in s.Enemies.Where(e => !e.Boss)) e.IsAlive = false;
        long gold = s.Hero.Gold, xp = s.Hero.Xp; int level = s.Hero.Level;
        s.Boss.Health = 1;
        s.Boss.Damage(50);
        s.Step(InputFrame.None(s.HeroWorld));
        Assert.True(s.Hero.Gold > gold);
        Assert.True(s.Hero.Xp != xp || s.Hero.Level > level);
        var drops = s.Items.Select(i => i.Kind).ToList();
        foreach (var kind in new[] { "potion", "gold", "weapon", "accessory", "resource_orb", "relic" }) Assert.Contains(kind, drops);
        var relic = s.Items.Single(i => i.Kind == "relic").Data;
        Assert.Equal("Brasa do Primeiro Carrasco", relic.Str("name"));
        Assert.Equal("Lendário", relic.Str("rarity"));
    }

    /// <summary>What the red tiles show: the cell an enemy is aimed at while it winds up, then nothing once it struck.</summary>
    [Fact]
    public void DangerShowsTheMarkedCellDuringTheWindup()
    {
        var s = new GameSession(4);
        var target = GameSession.ToWorld(s.Enemies.First(e => !e.Boss).Position);
        for (int i = 0; i < 4000 && s.Hero.Alive; i++)
        {
            bool walking = Vector2.Distance(s.HeroWorld, target) > 2.5f;
            s.Step(new InputFrame(walking ? Vector2.Normalize(target - s.HeroWorld) : Vector2.Zero, null, target, false, false, false));
            var winding = s.Enemies.FirstOrDefault(e => !e.Boss && e.Alive && e.Windup > 0 && e.AttackTarget is not null);
            if (winding == null) continue;
            Assert.Contains(s.Danger(), d => d.Cell == winding.AttackTarget!.Value);
            var t = s.Threat(winding)!.Value;
            Assert.Equal(winding.WindupDuration, t.Elapsed + t.Left);
            return;
        }
        Assert.Fail("nenhum inimigo preparou golpe");
    }

    /// <summary>player.use_potion on the potion button: half the life back, one potion fewer, only on the press.</summary>
    [Fact]
    public void ThePotionButtonDrinksOnce()
    {
        var s = new GameSession(4);
        s.Hero.Inventory.Potion = 2; s.Hero.Health = 10;
        var press = InputFrame.None(s.HeroWorld) with { Potion = true };
        s.Step(press);
        Assert.Equal(1, s.Hero.Inventory.Potion);
        Assert.Equal(Math.Min(s.Hero.MaxHealth, 10 + (int)(s.Hero.MaxHealth * .5)), s.Hero.Health);
        Assert.Contains(s.Events, e => e is PotionEvent { Left: 1 });
        s.Step(press);                                              // held: no second potion
        Assert.Equal(1, s.Hero.Inventory.Potion);
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
        Assert.Contains(events, e => e is CastEvent c && c.SkillId == s.Kit[1]);
        Assert.Contains(events, e => e is DeathEvent);
        Assert.Contains(events, e => e is ItemDroppedEvent);
    }

    /// <summary>A hero who stands near the enemies is reached and struck: they wind up and land.</summary>
    [Fact]
    public void EnemiesHurtAHeroWhoStandsStill()
    {
        var s = new GameSession(4); var events = new List<PlayEvent>();
        var target = GameSession.ToWorld(s.Enemies.First(e => !e.Boss).Position);
        for (int i = 0; i < 4000 && s.Hero.Alive; i++)
        {
            bool walking = Vector2.Distance(s.HeroWorld, target) > 2.5f;
            s.Step(new InputFrame(walking ? Vector2.Normalize(target - s.HeroWorld) : Vector2.Zero, null, target, false, false, false));
            events.AddRange(s.Events);
        }
        Assert.Contains(events, e => e is WindupEvent);
        Assert.Contains(events, e => e is DamageEvent { OnHero: true });
    }

    [Fact]
    public void CryptRngIsDeterministic()
    {
        var a = new Rng(5); var b = new Rng(5);
        Assert.Equal(Enumerable.Range(0, 20).Select(_ => a.NextULong()), Enumerable.Range(0, 20).Select(_ => b.NextULong()));
        var r = new Rng(9);
        Assert.All(Enumerable.Range(0, 1000).Select(_ => r.Range(2, 5)), v => Assert.InRange(v, 2, 5));
    }

    [Fact]
    public void ClickToMoveWalksToTheDestinationAndStops()
    {
        var s = new GameSession(4);
        foreach (var e in s.Enemies) e.IsAlive = false;
        var goal = GameSession.ToWorld((s.Hero.X + 2, s.Hero.Y));
        Assert.Equal(0, s.Grid.Cell(s.Hero.X + 2, s.Hero.Y));
        s.Step(new InputFrame(Vector2.Zero, goal, goal, false, false, false));
        for (int i = 0; i < 120; i++) s.Step(new InputFrame(Vector2.Zero, null, goal, false, false, false));
        Assert.True(Vector2.Distance(s.HeroWorld, goal) < .15f, $"{s.HeroWorld} -> {goal}");
        Assert.Null(s.Destination);
    }

    [Fact]
    public void DiscardDropsAtTheFeetAndStepsBackPickItUp()
    {
        var s = new GameSession(4);
        s.Hero.Inventory.Items.Add(Umbra.Domain.Items.Loot.Drop("armor", 1, s.Hero.Level, "barbaro", new Umbra.Domain.Util.PyRandom(3)));
        var item = s.Discard(0)!;
        Assert.Empty(s.Hero.Inventory.Items);
        Assert.Equal(s.Hero.Position, item.Cell);
        Assert.Contains(item, s.Items);
    }

    /// <summary>Step 5.8d1: every class without summons plays the slice. The kit follows one rule over the class's 0.16
    /// catalog, is learned and equipped, and the autopilot uses all three slots against the crypt.</summary>
    [Theory]
    [InlineData("barbaro", "barb2_golpe_brutal", "barb2_ruptura", "barb2_investida")]
    [InlineData("mago", "mage2_projetil_arcano", "mage2_bola_fogo", "mage2_teleporte")]
    [InlineData("ladino", "rogue2_punhalada", "rogue2_rajada", "rogue2_passo_sombrio")]
    [InlineData("paladino", "pala2_golpe_sagrado", "pala2_julgamento", "pala2_investida")]
    [InlineData("druida", "druid2_espinho", "druid2_tempestade", "druid2_salto")]
    public void EveryClassWithoutSummonsPlaysTheSlice(string cls, string basic, string main, string mobility)
    {
        var s = new GameSession(2, cls);
        Assert.Equal(new[] { basic, main, mobility }, s.Kit);
        Assert.Equal(cls, s.Hero.PlayerClass);
        Assert.Equal(basic, s.Hero.BasicSkillId);
        Assert.Equal(new string?[] { main, mobility, null, null }, s.Hero.EquippedMainSkills);
        var events = new List<PlayEvent>();
        for (int i = 0; i < 6000 && s.Hero.Alive && !(s.Cleared && s.Items.Count == 0); i++) { s.Step(Bot(s)); events.AddRange(s.Events); }
        Assert.True(s.Kills > 0, "nenhum inimigo morreu");
        Assert.Contains(events, e => e is CastEvent c && c.SkillId == basic);
        Assert.Contains(events, e => e is CastEvent c && c.SkillId == main);
        Assert.Contains(events, e => e is DamageEvent { OnHero: false });
    }
}
