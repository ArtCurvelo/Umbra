using Umbra.Domain.Data;
using Umbra.Domain.Gameplay;

namespace Umbra.Domain.Tests;

/// <summary>Phase 5, step 1: UMBRA's data read in C#, and the slice's typed numbers checked against it.</summary>
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

    [Fact]
    public void SliceNumbersMatchUmbra()
    {
        var barbaro = Data.Classes["barbaro"];
        Assert.Equal(SliceData.Barbarian.MaxHealth, barbaro.Health);
        Assert.Equal(SliceData.Barbarian.Attack, barbaro.Attack);
        Assert.Equal(SliceData.Barbarian.Defense, barbaro.Defense);
        Assert.Equal(SliceData.FuryMax, barbaro.ResourceMax);
        var bruto = Data.Enemies["bruto"];
        Assert.Equal(SliceData.Brute.MaxHealth, bruto.Health);
        Assert.Equal(SliceData.Brute.Attack, bruto.Attack);
        Assert.Equal(SliceData.Brute.Defense, bruto.Defense);
        Assert.Equal(SliceData.BruteWindupTicks, bruto.Windup);
        Assert.Equal(60f / bruto.MoveCooldown, SliceData.Brute.Speed, 4);
        Assert.Equal((int)SliceData.BruteDetection, bruto.Detection);
        foreach (var slice in new[] { SliceData.GolpeBrutal, SliceData.Ruptura, SliceData.Investida })
        {
            var s = Data.Skill(slice.Id);
            Assert.Equal(s.Name, slice.Name);
            Assert.Equal(s.Multiplier, slice.Multiplier, 4);
            Assert.Equal(s.Cost, slice.Cost);
            Assert.Equal(s.ResourceGain, slice.FuryGain);
            Assert.Equal(s.Cooldown, slice.CooldownTicks);
        }
        Assert.Equal(2, Data.Skill(SliceData.Ruptura.Id).Range);
    }

    [Fact]
    public void RarityMultipliersMatchUmbra()
    {
        var multipliers = Data.Rules.GetProperty("rarity").GetProperty("MULTIPLIER");
        string[] names = { "Comum", "Mágico", "Raro", "Lendário" };
        for (int i = 0; i < names.Length; i++) Assert.Equal(multipliers.GetProperty(names[i]).GetDouble(), ItemRules.RarityMultiplier[i], 6);
        var tags = Data.Rules.GetProperty("rarity").GetProperty("TAG");
        for (int i = 0; i < names.Length; i++) Assert.Equal(tags.GetProperty(names[i]).GetString(), Loot.Tags[i]);
    }
}
