using System.Numerics;
using Umbra.Domain.Core;

namespace Umbra.Domain.Gameplay;

/// <summary>Rarities as in UMBRA's game/equipment/rarity.py (the droppable ones). The tag goes next to the colour,
/// so colour is never the only signal.</summary>
public enum Rarity { Comum, Magico, Raro, Lendario }

public enum Stat { Health, Attack, Defense }

public sealed record Affix(Stat Stat, int Value);

public sealed record Item(string Name, string Slot, Rarity Rarity)
{
    public string RarityName => Loot.Names[(int)Rarity];
    public string Tag => Loot.Tags[(int)Rarity];
    public Stat MainStat { get; init; } = ItemRules.MainStat(Slot);
    public int Power { get; init; }
    public IReadOnlyList<Affix> Affixes { get; init; } = Array.Empty<Affix>();

    /// <summary>Everything this item adds to one stat (main stat + affixes).</summary>
    public int Total(Stat stat) => (MainStat == stat ? Power : 0) + Affixes.Where(a => a.Stat == stat).Sum(a => a.Value);
}

public sealed class Drop
{
    public int Id { get; }
    public Item Item { get; }
    public Vector2 Position { get; }
    public Drop(int id, Item item, Vector2 position) { Id = id; Item = item; Position = position; }
}

/// <summary>
/// Item numbers of the slice. PROVISÓRIO: invented for Phase 4 so equipping has a real effect; they wait for the
/// Game Director's balance pass (and, in Phase 5, for UMBRA's real item system). Rarity multipliers are UMBRA's
/// (rarity.MULTIPLIER); affix counts follow UMBRA's tier (Comum 0, Mágico 1, Raro 2, Lendário 4 -> capped at 3 here).
/// </summary>
public static class ItemRules
{
    public static readonly double[] RarityMultiplier = { 1.00, 1.12, 1.26, 1.62 };
    public static readonly int[] AffixCount = { 0, 1, 2, 3 };

    public static readonly Dictionary<string, (Stat Stat, int Base)> Slots = new()
    {
        ["Arma"] = (Stat.Attack, 4), ["Mão secundária"] = (Stat.Defense, 2), ["Elmo"] = (Stat.Defense, 2), ["Peitoral"] = (Stat.Defense, 4),
        ["Luvas"] = (Stat.Defense, 1), ["Botas"] = (Stat.Defense, 1), ["Anel"] = (Stat.Health, 8), ["Amuleto"] = (Stat.Health, 12),
    };

    public static readonly Dictionary<Stat, (int Min, int Max)> AffixRange = new()
    {
        [Stat.Health] = (5, 12), [Stat.Attack] = (1, 3), [Stat.Defense] = (1, 3),
    };

    public static Stat MainStat(string slot) => Slots.TryGetValue(slot, out var s) ? s.Stat : Stat.Health;

    public static Item Make(string name, string slot, Rarity rarity, Rng rng)
    {
        var (stat, power) = Slots[slot];
        int value = (int)Math.Round(power * RarityMultiplier[(int)rarity] * (1 + rng.NextDouble() * .25));
        var affixes = new List<Affix>();
        for (int i = 0; i < AffixCount[(int)rarity]; i++)
        {
            var kind = (Stat)rng.Range(0, 2);
            var (min, max) = AffixRange[kind];
            affixes.Add(new Affix(kind, rng.Range(min, max)));
        }
        return new Item(name, slot, rarity) { MainStat = stat, Power = Math.Max(1, value), Affixes = affixes };
    }
}

public static class Loot
{
    public static readonly string[] Names = { "Comum", "Mágico", "Raro", "Lendário" };
    public static readonly string[] Tags = { "C", "M", "R", "L" };
    /// <summary>Slice weights (PROVISÓRIO): 60 / 28 / 10 / 2.</summary>
    public static readonly int[] Weights = { 60, 28, 10, 2 };
    public const double DropChance = .6;

    private static readonly (string Name, string Slot)[] Bases =
    {
        ("Machado Gasto", "Arma"), ("Elmo de Ferro Batido", "Elmo"), ("Cota Remendada", "Peitoral"),
        ("Luvas de Couro Cru", "Luvas"), ("Anel de Cinza", "Anel"), ("Botas de Sola Gasta", "Botas"),
        ("Escudo Rachado", "Mão secundária"), ("Amuleto de Osso", "Amuleto"),
    };

    public static Item Roll(Rng rng)
    {
        var (name, slot) = Bases[rng.Range(0, Bases.Length - 1)];
        int pick = rng.Range(1, Weights.Sum()), rarity = 0;
        while (pick > Weights[rarity]) { pick -= Weights[rarity]; rarity++; }
        return ItemRules.Make(name, slot, (Rarity)rarity, rng);
    }
}
