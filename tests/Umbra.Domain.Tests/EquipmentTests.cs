using System.Numerics;
using Umbra.Domain.Core;
using Umbra.Domain.Gameplay;

namespace Umbra.Domain.Tests;

public class EquipmentTests
{
    private static Item Make(string slot, Rarity rarity = Rarity.Comum, int power = 5, params Affix[] affixes) =>
        new("Teste", slot, rarity) { MainStat = ItemRules.MainStat(slot), Power = power, Affixes = affixes };

    [Fact]
    public void RolledItemsFollowTheTable()
    {
        var rng = new Rng(3);
        for (int i = 0; i < 2000; i++)
        {
            var item = Loot.Roll(rng);
            Assert.Equal(ItemRules.AffixCount[(int)item.Rarity], item.Affixes.Count);
            Assert.Equal(ItemRules.Slots[item.Slot].Stat, item.MainStat);
            int @base = ItemRules.Slots[item.Slot].Base;
            Assert.InRange(item.Power, Math.Max(1, (int)Math.Floor(@base * ItemRules.RarityMultiplier[(int)item.Rarity])),
                           (int)Math.Ceiling(@base * ItemRules.RarityMultiplier[(int)item.Rarity] * 1.25));
            Assert.NotEmpty(Equipment.SlotsFor(item.Slot));
        }
    }

    [Fact]
    public void EquippingChangesTheHeroStatsAndCombat()
    {
        var sim = new Simulation(11);
        sim.Inventory.Add(Make("Arma", power: 6, affixes: new Affix(Stat.Health, 10)));
        sim.Inventory.Add(Make("Peitoral", power: 4));
        Assert.True(sim.Equip(0));
        Assert.True(sim.Equip(0));
        Assert.Empty(sim.Inventory);
        Assert.Equal(22 + 6, sim.Hero.Attack);
        Assert.Equal(8 + 4, sim.Hero.Defense);
        Assert.Equal(190 + 10, sim.Hero.MaxHealth);
        Assert.Equal(SliceData.Damage(28, 1.10f, 2), SliceData.Damage(sim.Hero.Attack, SliceData.GolpeBrutal.Multiplier, 2));
    }

    [Fact]
    public void EquippingSwapsWithTheOldItemAndRingsUseBothSlots()
    {
        var sim = new Simulation(11);
        var first = Make("Arma", power: 3); var second = Make("Arma", power: 7);
        sim.Inventory.AddRange(new[] { first, second });
        sim.Equip(0);
        Assert.Same(first, sim.Hero.Equipment[EquipSlot.Arma]);
        sim.Equip(0);                                          // the second weapon is now at index 0
        Assert.Same(second, sim.Hero.Equipment[EquipSlot.Arma]);
        Assert.Same(first, sim.Inventory[0]);                  // the old one took its place in the bag

        sim.Inventory.Clear();
        var a = Make("Anel"); var b = Make("Anel"); var c = Make("Anel");
        sim.Inventory.AddRange(new[] { a, b, c });
        sim.Equip(0); sim.Equip(0);
        Assert.Same(a, sim.Hero.Equipment[EquipSlot.Anel1]);
        Assert.Same(b, sim.Hero.Equipment[EquipSlot.Anel2]);
        sim.Equip(0);                                          // both full: swaps with the first ring
        Assert.Same(c, sim.Hero.Equipment[EquipSlot.Anel1]);
        Assert.Same(a, sim.Inventory[0]);
    }

    [Fact]
    public void UnequippingHealthNeverLeavesHealthAboveTheMaximum()
    {
        var sim = new Simulation(11);
        sim.Inventory.Add(Make("Amuleto", power: 30));
        sim.Equip(0);
        sim.Hero.Health = sim.Hero.MaxHealth;
        Assert.True(sim.Unequip(EquipSlot.Amuleto));
        Assert.Equal(190, sim.Hero.Health);
        Assert.Single(sim.Inventory);
    }

    [Fact]
    public void FullBagLeavesDropsOnTheFloorAndDiscardDropsAtTheFeet()
    {
        var sim = new Simulation(11);
        for (int i = 0; i < Simulation.BagSize; i++) sim.Inventory.Add(Make("Botas"));
        Assert.False(sim.Unequip(EquipSlot.Arma));
        var drop = new Drop(999, Make("Elmo"), sim.Hero.Position);
        sim.Drops.Add(drop);
        sim.Step(InputFrame.None(sim.Hero.Position));
        Assert.Contains(drop, sim.Drops);                       // bag full: it stays on the floor
        Assert.True(sim.Discard(0));
        Assert.Equal(Simulation.BagSize - 1, sim.Inventory.Count);
        Assert.Contains(sim.Events, e => e is DroppedEvent);
        Assert.Equal(2, sim.Drops.Count);
    }

    [Fact]
    public void SkillGradesSpendPointsAndRaiseDamage()
    {
        var sim = new Simulation(11);
        var ranks = sim.Hero.Ranks;
        Assert.Equal(SkillRanks.StartingPoints, ranks.Points);
        Assert.False(ranks.Learnable("barb2_frenesi"));
        Assert.False(ranks.Lower(SliceData.Ruptura.Id));          // grade 1 is the floor
        for (int i = 0; i < 4; i++) Assert.True(ranks.Raise(SliceData.Ruptura.Id));
        Assert.False(ranks.Raise(SliceData.Ruptura.Id));          // grade 5 is the top
        Assert.Equal(1, ranks.Points);
        Assert.Equal(1.4f, ranks.Factor(SliceData.Ruptura.Id), 4);
        Assert.True(ranks.Lower(SliceData.Ruptura.Id));
        Assert.Equal(2, ranks.Points);

        foreach (var e in sim.Enemies) e.Health = 0;
        var foe = sim.Enemies[0]; foe.Health = 65; foe.Position = sim.Hero.Position + new Vector2(1.5f, 0); foe.State = AiState.Recover; foe.StateTicks = 999;
        sim.Hero.Fury = 30;
        sim.Step(new InputFrame(Vector2.Zero, null, sim.Hero.Position + new Vector2(1, 0), false, true, false));
        Assert.Equal(65 - SliceData.Damage(22, 1.45f * 1.3f, 2), foe.Health);
    }
}
