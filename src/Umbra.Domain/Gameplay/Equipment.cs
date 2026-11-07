namespace Umbra.Domain.Gameplay;

/// <summary>Paper-doll slots (Phase 4 mockup, approved). Two rings.</summary>
public enum EquipSlot { Elmo, Amuleto, Peitoral, Luvas, Arma, MaoSecundaria, Anel1, Anel2, Botas }

public sealed class Equipment
{
    private readonly Dictionary<EquipSlot, Item> _items = new();

    public Item? this[EquipSlot slot] => _items.TryGetValue(slot, out var item) ? item : null;
    public IEnumerable<(EquipSlot Slot, Item Item)> All => _items.Select(kv => (kv.Key, kv.Value));

    public static EquipSlot[] SlotsFor(string itemSlot) => itemSlot switch
    {
        "Elmo" => new[] { EquipSlot.Elmo }, "Amuleto" => new[] { EquipSlot.Amuleto }, "Peitoral" => new[] { EquipSlot.Peitoral },
        "Luvas" => new[] { EquipSlot.Luvas }, "Arma" => new[] { EquipSlot.Arma }, "Mão secundária" => new[] { EquipSlot.MaoSecundaria },
        "Anel" => new[] { EquipSlot.Anel1, EquipSlot.Anel2 }, "Botas" => new[] { EquipSlot.Botas }, _ => Array.Empty<EquipSlot>(),
    };

    public static string Label(EquipSlot slot) => slot switch
    {
        EquipSlot.MaoSecundaria => "Mão sec.", EquipSlot.Anel1 or EquipSlot.Anel2 => "Anel", _ => slot.ToString(),
    };

    /// <summary>Where an item would go: the first empty slot it fits, else the first slot (swap).</summary>
    public EquipSlot? TargetFor(Item item)
    {
        var slots = SlotsFor(item.Slot);
        if (slots.Length == 0) return null;
        return slots.FirstOrDefault(s => this[s] == null, slots[0]);
    }

    /// <summary>Puts the item in its slot and returns what was there (or null).</summary>
    public Item? Put(EquipSlot slot, Item item)
    {
        var old = this[slot];
        _items[slot] = item;
        return old;
    }

    public Item? Take(EquipSlot slot)
    {
        var old = this[slot];
        _items.Remove(slot);
        return old;
    }

    public int Total(Stat stat) => _items.Values.Sum(i => i.Total(stat));
}

/// <summary>
/// Skill grades of the slice. PROVISÓRIO: each grade above the first adds 10% to the skill's multiplier and the hero
/// starts with 5 points (the slice has no experience yet). Only the three skills that exist in the slice's combat can be
/// learned; the rest of the Bárbaro catalog arrives with Phase 5.
/// </summary>
public sealed class SkillRanks
{
    public const int MaxRank = 5;
    public const double PerRank = .10;
    public const int StartingPoints = 5;

    private readonly Dictionary<string, int> _ranks = new();
    public int Points { get; private set; } = StartingPoints;

    public SkillRanks(IEnumerable<string> skills) { foreach (var s in skills) _ranks[s] = 1; }

    public bool Learnable(string skill) => _ranks.ContainsKey(skill);
    public int Rank(string skill) => _ranks.TryGetValue(skill, out var r) ? r : 0;
    public float Factor(string skill) => (float)(1 + PerRank * Math.Max(0, Rank(skill) - 1));

    public bool Raise(string skill)
    {
        if (!_ranks.TryGetValue(skill, out var r) || r >= MaxRank || Points <= 0) return false;
        _ranks[skill] = r + 1; Points--;
        return true;
    }

    /// <summary>Gives a point back (never below grade 1: the slice's skills are always known).</summary>
    public bool Lower(string skill)
    {
        if (!_ranks.TryGetValue(skill, out var r) || r <= 1) return false;
        _ranks[skill] = r - 1; Points++;
        return true;
    }
}
