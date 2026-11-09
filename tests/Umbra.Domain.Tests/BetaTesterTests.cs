using Umbra.BetaTester;
using Umbra.Domain.Gameplay;

namespace Umbra.Domain.Tests;

/// <summary>The AI beta tester: plays deterministically, measures what really happened and flags what it should.</summary>
public class BetaTesterTests
{
    [Fact]
    public void SamePersonaAndSeedReplayTheSameRun()
    {
        foreach (var persona in Persona.All)
        {
            var a = Session.Play(persona, 7); var b = Session.Play(persona, 7);
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
        Assert.Equal(rec.Enemies * SliceData.Brute.MaxHealth, rec.Skills.Values.Sum(s => s.Damage));  // useful damage = all enemy life
        Assert.Equal(Session.PointsPlan.Length, rec.PointsSpent);
    }

    [Fact]
    public void DodgingReducesDamageTaken()
    {
        var seeds = Enumerable.Range(1, 12).Select(i => (ulong)i).ToList();
        double vet = seeds.Average(s => Session.Play(Persona.Veterano, s).DamageTaken);
        double aggressive = seeds.Average(s => Session.Play(Persona.Agressivo, s).DamageTaken);
        Assert.True(vet < aggressive, $"veterano {vet}, agressivo {aggressive}");
    }

    [Fact]
    public void NovatoNeverCastsRupturaNorSpendsPoints()
    {
        var rec = Session.Play(Persona.Novato, 3);
        Assert.Equal(0, rec.Skill(SliceData.Ruptura.Id).Casts);
        Assert.Equal(0, rec.Skill(SliceData.Investida.Id).Casts);
        Assert.Equal(0, rec.PointsSpent);
    }

    [Fact]
    public void HitsToKillMatchTheGameFormula()
    {
        var golpe = Analyst.HitsToKill(SliceData.GolpeBrutal).ToList();
        Assert.Equal((1, 22, 3), golpe[0]);                       // 22 * 1.10 = 24 - defesa 2 = 22; 65 de vida -> 3 golpes
        Assert.Equal(SkillRanks.MaxRank, golpe.Count);
    }

    [Fact]
    public void GearHabitsDiffer()
    {
        var sim = new Simulation(1);
        sim.Inventory.Add(new Item("Machado Bom", "Arma", Rarity.Raro) { MainStat = Stat.Attack, Power = 6 });
        var rec = new RunRecord { Persona = "x", Seed = 1 };
        Session.Gear(Persona.Novato, sim, rec);                  // empty slot: even the Novato equips
        Assert.Equal(1, rec.Equipped);
        sim.Inventory.Add(new Item("Machado Melhor", "Arma", Rarity.Lendario) { MainStat = Stat.Attack, Power = 9 });
        Session.Gear(Persona.Novato, sim, rec);                  // the Novato does not swap
        Assert.Equal("Machado Bom", sim.Hero.Equipment[EquipSlot.Arma]!.Name);
        Session.Gear(Persona.Veterano, sim, rec);                // the Veterano takes the upgrade
        Assert.Equal("Machado Melhor", sim.Hero.Equipment[EquipSlot.Arma]!.Name);
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
        string md = Report.Markdown(summaries, findings, "2026-11-08", "teste", 2);
        foreach (var section in new[] { "## Resumo", "## O que mudar", "## Números por perfil", "## Habilidades", "## Itens", "## Metas usadas (PROVISÓRIO)", "## Limites deste teste" })
            Assert.Contains(section, md);
        Assert.All(findings.Where(f => f.Severity != Severity.Ok), f => Assert.False(string.IsNullOrWhiteSpace(f.Suggestion)));
    }
}
