using System.Numerics;
using Umbra.Domain.Core;

namespace Umbra.Domain.Gameplay;

/// <summary>Rarities as in UMBRA's game/equipment/rarity.py (the droppable ones). The tag goes next to the colour,
/// so colour is never the only signal.</summary>
public enum Rarity { Comum, Magico, Raro, Lendario }

public sealed record Item(string Name, string Slot, Rarity Rarity)
{
    public string RarityName => Loot.Names[(int)Rarity];
    public string Tag => Loot.Tags[(int)Rarity];
}

public sealed class Drop
{
    public int Id { get; }
    public Item Item { get; }
    public Vector2 Position { get; }
    public Drop(int id, Item item, Vector2 position) { Id = id; Item = item; Position = position; }
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
        ("Luvas de Couro Cru", "Luvas"), ("Anel de Cinza", "Anel"),
    };

    public static Item Roll(Rng rng)
    {
        var (name, slot) = Bases[rng.Range(0, Bases.Length - 1)];
        int pick = rng.Range(1, Weights.Sum()), rarity = 0;
        while (pick > Weights[rarity]) { pick -= Weights[rarity]; rarity++; }
        return new Item(name, slot, (Rarity)rarity);
    }
}
