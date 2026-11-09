using Umbra.BetaTester;
using Umbra.Domain.Items;
using Umbra.Domain.Play;
using Umbra.Domain.Util;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Tests;

/// <summary>The AI beta tester on UMBRA's real rules (since 5.8c): plays deterministically, measures what really happened
/// and flags what it should.</summary>
[Collection("SkillEnv")]
public class BetaTesterTests
{
    static BetaTesterTests() => Facts.Load();

    [Fact]
    public void SamePersonaAndSeedReplayTheSameRun()
    {
        foreach (var persona in Persona.All)
        {
            var a = Session.Play(persona, 7); var b = Session.Play(persona, 7);
            Assert.Null(a.Error);
            Assert.Equal(a.Digest, b.Digest);
            Assert.Equal(a.Ticks, b.Ticks);
        }
    }

    [Theory]
    [InlineData(1UL)] [InlineData(5UL)] [InlineData(12UL)]
    public void VeteranoClearsTheCryptAndTheNumbersAddUp(ulong seed)
    {
        var rec = Session.Play(Persona.Veterano, seed);
        Assert.Equal(Outcome.Cleared, rec.Outcome);
        Assert.Equal(rec.Enemies, rec.Kills);
        Assert.Equal(rec.Kills, rec.Skills.Values.Sum(s => s.Kills));
        Assert.Equal(rec.Kills, rec.KillTicks.Count);
        Assert.Equal(rec.Enemies * Facts.Brute().MaxHealth, rec.Skills.Values.Sum(s => s.Damage));  // life taken = all enemy life
        Assert.Equal(Facts.Hero().Points, rec.PointsSpent);                                          // spends every starting point
    }

    [Fact]
    public void NovatoNeverCastsRupturaNorSpendsPoints()
    {
        var rec = Session.Play(Persona.Novato, 3);
        Assert.Equal(0, rec.Skill(Facts.Ruptura).Casts);
        Assert.Equal(0, rec.Skill(Facts.Investida).Casts);
        Assert.Equal(0, rec.PointsSpent);
        Assert.True(rec.Skill(Facts.Golpe).Casts > 0);
    }

    /// <summary>The rank table is the game's damage: the first swing of a fresh hero on a full-life Bruto (combo ×1,00, the
    /// slice's Bárbaro has no crit) takes exactly the rank-1 value.</summary>
    [Fact]
    public void HitsToKillMatchTheGame()
    {
        var table = Facts.HitsToKill(Facts.Golpe).ToList();
        Assert.Equal(Umbra.Domain.Skills.TreeCatalog.MaxRank, table.Count);
        Assert.True(table.Zip(table.Skip(1)).All(p => p.Second.Damage >= p.First.Damage));
        var s = new GameSession(4);
        for (int i = 0; i < 3000 && s.Hero.Alive; i++)
        {
            s.Step(SessionPilot.Next(s) with { Skill1 = false, Skill2 = false });
            if (s.Events.OfType<DamageEvent>().FirstOrDefault(e => !e.OnHero) is { } hit) { Assert.Equal(table[0].Damage, hit.Amount); return; }
        }
        Assert.Fail("nenhum golpe acertou");
    }

    [Fact]
    public void GearHabitsDiffer()
    {
        var s = new GameSession(1); var hero = s.Hero;
        PyDict Armor(int defense, string name)
        {
            var r = Loot.Drop("armor", 1, hero.Level, "barbaro", new PyRandom(11));
            r["name"] = name; r["slot"] = "helmet"; r["stats"] = new Umbra.Domain.Skills.PyDict { ["defense"] = (long)defense };
            r["affixes"] = new Umbra.Domain.Skills.PyDict(); r["required_level"] = 1L;
            return r;
        }
        var rec = new RunRecord { Persona = "x", Seed = 1 };
        hero.Inventory.Items.Add(Armor(2, "Elmo Bom"));
        Session.Gear(Persona.Novato, s, rec);                  // empty slot: even the Novato equips
        Assert.Equal(1, rec.Equipped);
        hero.Inventory.Items.Add(Armor(6, "Elmo Melhor"));
        Session.Gear(Persona.Novato, s, rec);                  // the Novato does not swap
        Assert.Equal("Elmo Bom", ((Umbra.Domain.Skills.PyDict)Gear.Ensure(hero)["helmet"]!).Str("name"));
        Session.Gear(Persona.Veterano, s, rec);                // the Veterano takes the upgrade
        Assert.Equal("Elmo Melhor", ((Umbra.Domain.Skills.PyDict)Gear.Ensure(hero)["helmet"]!).Str("name"));
    }

    [Fact]
    public void CrashesStuckRunsAndReplayMismatchesAreBugs()
    {
        var crash = new RunRecord { Persona = "novato", Seed = 4, Outcome = Outcome.Crashed, Error = "NullReferenceException: x" };
        var stuck = new RunRecord { Persona = "novato", Seed = 5, Outcome = Outcome.Stuck, LongestStuckTicks = 180 };
        var a = new RunRecord { Persona = "novato", Seed = 6, Digest = "A" }; var b = new RunRecord { Persona = "novato", Seed = 6, Digest = "B" };
        var findings = Analyst.Analyze(new[] { new Summary { Persona = Persona.Novato, Runs = new[] { crash, stuck } } }, new[] { (a, b) });
        Assert.Equal(3, findings.Count(f => f.Severity == Severity.Bug));
        Assert.Equal(Severity.Bug, findings[0].Severity);        // bugs come first
    }

    [Fact]
    public void ReportHasTheSectionsTheGameDirectorReads()
    {
        var summaries = Persona.All.Select(p => new Summary { Persona = p, Runs = new[] { Session.Play(p, 2), Session.Play(p, 3) } }).ToList();
        var findings = Analyst.Analyze(summaries, Array.Empty<(RunRecord, RunRecord)>());
        string md = Report.Markdown(summaries, findings, "2026-11-09", "teste", 2);
        foreach (var section in new[] { "## Resumo", "## O que mudar", "## Números por perfil", "## Habilidades", "## Itens", "## Metas usadas (PROVISÓRIO)", "## Limites deste teste" })
            Assert.Contains(section, md);
        Assert.All(findings.Where(f => f.Severity != Severity.Ok), f => Assert.False(string.IsNullOrWhiteSpace(f.Suggestion)));
        Assert.DoesNotContain("SliceData", md);
    }
}
