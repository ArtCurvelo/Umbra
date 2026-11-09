using Umbra.Domain.Data;
using Umbra.Domain.Enemies;
using Umbra.Domain.Heroes;

namespace Umbra.Domain.Tests;

/// <summary>Phase 5, step 1: UMBRA's data read in C#, and the typed export checked against the rules' tables.</summary>
public class DataTests
{
    private static readonly UmbraData Data = UmbraData.Load(UmbraData.FindRoot());

    [Fact]
    public void ExportIsIntactAndComplete()
    {
        Assert.Empty(Data.Tampered());
        Assert.Equal(7, Data.Classes.Count);
        Assert.Equal(new[] { "barbaro", "bruxo", "druida", "ladino", "mago", "necromante", "paladino" }, Data.Skills.Keys.OrderBy(k => k));
        foreach (var (cls, set) in Data.Skills)
        {
            Assert.Equal(cls == "necromante" ? 23 : 9, set.Skills.Count);
            Assert.All(set.Skills, s => Assert.False(string.IsNullOrWhiteSpace(s.Name)));
        }
        // the approved 0.16 rule: 7 options per skill (3 Comportamento, 2 Especialização, 2 Transformação), outside the Reborn
        foreach (var s in Data.Skills.Where(kv => kv.Key != "necromante").SelectMany(kv => kv.Value.Skills))
        {
            Assert.Equal(7, s.Options.Count);
            Assert.Equal(new[] { 3, 2, 2 }, new[] { "Comportamento", "Especialização", "Transformação" }.Select(b => s.Options.Count(o => o.Branch == b)));
        }
    }

    /// <summary>The typed export (UmbraData) and the rules' tables (HeroTables, FoeTables) read the same numbers.</summary>
    [Fact]
    public void TypedExportMatchesTheRules()
    {
        HeroTables.Load(UmbraData.FindRoot()); FoeTables.Load(UmbraData.FindRoot());
        var barbaro = Data.Classes["barbaro"]; var stats = HeroTables.Stats("barbaro");
        Assert.Equal(barbaro.Health, (int)stats.Num("health"));
        Assert.Equal(barbaro.Attack, (int)stats.Num("attack"));
        Assert.Equal(barbaro.Defense, (int)stats.Num("defense"));
        Assert.Equal(barbaro.ResourceMax, (int)HeroTables.Spec("barbaro").Num("resource_max"));
        var bruto = Data.Enemies["bruto"];
        var enemy = new Umbra.Domain.Enemies.Enemy((0, 0), 1, false, "bruto");
        Assert.Equal(bruto.Windup, enemy.WindupDuration);
        Assert.Equal(bruto.Detection, enemy.DetectionRange);
        foreach (var id in new[] { "barb2_golpe_brutal", "barb2_ruptura", "barb2_investida" })
        {
            var typed = Data.Skill(id); var row = HeroTables.BasicRow(id) ?? HeroTables.SkillRow(id)!;
            Assert.Equal(typed.Name, row.Str("name"));
            Assert.Equal(typed.Multiplier, row.Num("multiplier"), 4);
            Assert.Equal(typed.Cost, (int)row.Num("cost"));
        }
    }
}
