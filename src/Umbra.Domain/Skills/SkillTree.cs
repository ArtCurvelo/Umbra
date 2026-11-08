using System.Text;
using System.Text.Json;

namespace Umbra.Domain.Skills;

/// <summary>The hero as the tree sees it (level gates, the five skill slots, stats refreshed after a change).</summary>
public interface ITreeOwner
{
    int Level { get; }
    string PlayerClass { get; }
    /// <summary>Slot 0 (the attack button); the name is the saved field's.</summary>
    string? BasicSkillId { get; set; }
    /// <summary>Slots 1..4.</summary>
    List<string?> EquippedMainSkills { get; }
    HashSet<string> UnlockedMainSkills { get; }
    int ActiveMainSlot { get; set; }
    SkillTree Tree { get; }
    /// <summary>respec.refresh_stats: recalculate and clamp life/resource (the hero's rule, step 5.8).</summary>
    void RefreshStats();
    /// <summary>resolver.cancel_player_attack + attack_started_at = None (the basic attack wind-up, step 5.8).</summary>
    void CancelAttack(string reason);
}

/// <summary>The tree of one class exactly as UMBRA builds it (game/data/umbra/trees.json, exported from a real hero).</summary>
public sealed class TreeCatalog
{
    public IReadOnlyList<string> Order { get; }
    public IReadOnlyDictionary<string, PyDict> Nodes { get; }
    public int StartPoints { get; }
    /// <summary>The class skills: category and whether it is a Básica (catalog.get_basic_skill).</summary>
    public IReadOnlyDictionary<string, (string Category, bool Basic)> Skills { get; }

    private TreeCatalog(JsonElement row)
    {
        var order = new List<string>(); var nodes = new Dictionary<string, PyDict>();
        foreach (var pair in row.GetProperty("nodes").EnumerateArray())
        {
            string key = pair[0].GetString()!; order.Add(key); nodes[key] = (PyDict)Py.FromJson(pair[1])!;
        }
        Order = order; Nodes = nodes; StartPoints = row.GetProperty("points").GetInt32();
        Skills = row.GetProperty("skills").EnumerateObject().ToDictionary(p => p.Name,
            p => (p.Value.GetProperty("category").GetString() ?? "", p.Value.GetProperty("basic").GetBoolean()));
    }

    public static IReadOnlyDictionary<string, double> EffectCaps { get; private set; } = new Dictionary<string, double>();
    public static int MaxRank { get; private set; } = 15;
    public static int PassiveRank { get; private set; } = 3;
    public static int SlotCount { get; private set; } = 5;
    private static Dictionary<string, TreeCatalog>? _classes;

    public static void Load(string dataRoot)
    {
        var root = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "trees.json"), Encoding.UTF8)).RootElement;
        EffectCaps = root.GetProperty("EFFECT_CAPS").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble());
        MaxRank = root.GetProperty("MAX_RANK").GetInt32(); PassiveRank = root.GetProperty("PASSIVE_RANK").GetInt32();
        SlotCount = root.GetProperty("SLOTS").GetInt32();
        _classes = root.GetProperty("classes").EnumerateObject().ToDictionary(p => p.Name, p => new TreeCatalog(p.Value));
    }

    public static TreeCatalog For(string playerClass) =>
        (_classes ?? throw new InvalidOperationException("TreeCatalog.Load first"))[playerClass];
}

/// <summary>
/// One hero's skill tree — port of game/skills/tree.py (SkillTree, v16 part), ranks.py, requirements.level_blocked and
/// respec.py (equipped/active/side_key/choose_passive/refund). Nodes: legacy 0.1 nodes (never bought again, still
/// counted by GetEffect for old saves), roots `learn:&lt;id&gt;` (ranks 1..15), modifiers `:mod:power|reach|flow`
/// (exclusive per skill), passives `:passive:0..3` (exclusive per side pair) and class passives `class:&lt;id&gt;` (3 ranks).
/// </summary>
public sealed class SkillTree
{
    public TreeCatalog Catalog { get; }
    public IReadOnlyDictionary<string, PyDict> Nodes => Catalog.Nodes;
    public int Points;
    public Dictionary<string, int> Investments { get; } = new();
    public Dictionary<string, int> Ranks { get; } = new();
    /// <summary>side key -> node; insertion-ordered like the Python dict.</summary>
    public List<KeyValuePair<string, string>> PassiveSlots { get; private set; } = new();
    public HashSet<string> Unlocked { get; } = new();
    public ITreeOwner? Owner;

    public SkillTree(string playerClass) { Catalog = TreeCatalog.For(playerClass); Points = Catalog.StartPoints; }

    public void AddPoint(int amount = 1) => Points += Math.Max(0, amount);
    public bool Has(string id) => Unlocked.Contains(id);

    public string? Slot(string side) => PassiveSlots.FirstOrDefault(kv => kv.Key == side).Value;

    public void SetSlot(string side, string node)
    {
        int i = PassiveSlots.FindIndex(kv => kv.Key == side);
        if (i >= 0) PassiveSlots[i] = new(side, node); else PassiveSlots.Add(new(side, node));
    }

    public void KeepSlots(Func<string, bool> keep) => PassiveSlots = PassiveSlots.Where(kv => keep(kv.Value)).ToList();

    private static string Kind(PyDict node) => node.Str("kind");

    // ------------------------------------------------------------------ ranks (ranks.py)
    public int Rank(string key) => Ranks.TryGetValue(key, out var r) ? r : Has(key) ? 1 : 0;
    public static int Top(PyDict node) => Kind(node) == "root" ? TreeCatalog.MaxRank : TreeCatalog.PassiveRank;

    // ------------------------------------------------------------------ requirements
    public int PlayerLevel => Math.Max(1, Owner?.Level ?? 1);
    public bool LevelBlocked(string key) => !Unlocked.Contains(key) && PlayerLevel < (int)Nodes[key].Num("required_level", 1);

    public bool CanUnlock(string id, int? points = null)
    {
        Nodes.TryGetValue(id, out var node); int available = points ?? Points;
        if (node != null && Unlocked.Contains(id) && Kind(node) is "root" or "class_passive")
            return Rank(id) < Top(node) && available >= node.Num("cost");
        if (node == null || Unlocked.Contains(id) || available < node.Num("cost")) return false;
        if (node.T("legacy")) return false;
        if (LevelBlocked(id)) return false;
        var group = node.Get("exclusive");
        if (Py.Truthy(group) && Unlocked.Any(n => Nodes.TryGetValue(n, out var other) && Equals(other.Get("exclusive"), group))) return false;
        var prerequisite = node.StrOrNull("prerequisite");
        return prerequisite == null || Unlocked.Contains(prerequisite);
    }

    public bool Unlock(string id, int? points = null)
    {
        if (!CanUnlock(id, points)) return false;
        var node = Nodes[id]; int cost = Py.Int(node.Num("cost"));
        if (points == null) Points -= cost;
        bool was = Unlocked.Contains(id);
        Unlocked.Add(id);
        Investments[id] = Investments.GetValueOrDefault(id) + (points == null ? cost : 0);
        if (Kind(node) is "root" or "class_passive") Ranks[id] = (Ranks.TryGetValue(id, out var r) ? r : was ? 1 : 0) + 1;
        if (Kind(node) == "passive") SetSlot(SideKey(node), id);
        return true;
    }

    public double GetEffect(string effect)
    {
        double total = 0;
        foreach (var id in Catalog.Order)
        {
            var node = Nodes[id];
            if (Unlocked.Contains(id) && node.Str("effect") == effect && Active(id) && node.Get("value") is double or long) total += Py.Num(node.Get("value"));
        }
        return TreeCatalog.EffectCaps.TryGetValue(effect, out var cap) ? Math.Min(cap, total) : total;
    }

    public List<string> ActiveSkills() =>
        Catalog.Order.Where(id => Unlocked.Contains(id) && Nodes[id].Str("effect") == "active_skill").Select(id => Nodes[id].Get("value") as string ?? "").ToList();

    // ------------------------------------------------------------------ respec.py
    public static string SideKey(PyDict node)
    {
        int ordinal = Py.Int(node.Num("ordinal"));
        return node.Str("skill_id") + (node.T("free") ? $":p{ordinal}" : ":" + (ordinal < 2 ? "left" : "right"));
    }

    public bool Equipped(string id)
    {
        var owner = Owner!; var node = Nodes[id];
        if (Kind(node) == "root")
        {
            string skill = node.Str("skill_id");
            return skill == owner.BasicSkillId || owner.EquippedMainSkills.Contains(skill);
        }
        if (Kind(node) == "passive") return PassiveSlots.Any(kv => kv.Value == id) && Equipped("learn:" + node.Str("skill_id"));
        return Has(id);
    }

    /// <summary>A skill passive works only when chosen on its side and its skill is equipped; every other node always.</summary>
    public bool Active(string id)
    {
        var node = Nodes[id];
        if (Kind(node) != "passive") return true;
        return PassiveSlots.Any(kv => kv.Value == id) && Owner != null && Equipped("learn:" + node.Str("skill_id"));
    }

    public bool ChoosePassive(string key)
    {
        if (!Nodes.TryGetValue(key, out var node) || Kind(node) != "passive" || !Has(key)) return false;
        string side = SideKey(node);
        if (Catalog.Order.Any(k => k != key && Has(k) && Kind(Nodes[k]) == "passive" && SideKey(Nodes[k]) == side)) return false;
        SetSlot(side, key);
        return true;
    }

    public string? OccupiedSide(string key)
    {
        if (!Nodes.TryGetValue(key, out var node) || Kind(node) != "passive") return null;
        return Catalog.Order.FirstOrDefault(k => k != key && Has(k) && Kind(Nodes[k]) == "passive" && SideKey(Nodes[k]) == SideKey(node));
    }

    public string? RefundReason(string key)
    {
        if (!Has(key)) return "Este ponto ainda não foi investido.";
        if (Kind(Nodes[key]) is "root" or "class_passive" && Ranks.GetValueOrDefault(key, 1) > 1) return null;
        if (Catalog.Order.Any(id => Has(id) && Nodes[id].StrOrNull("prerequisite") == key)) return "Retire primeiro os pontos dos nós que dependem deste.";
        return null;
    }
}

/// <summary>Buying, refunding and equipping — player.unlock_skill, respec.refund and skills/loadout.py.</summary>
public static class TreeRules
{
    /// <summary>player.unlock_skill: buys a node (or a rank) and equips what a first purchase brings.</summary>
    public static bool Learn(ITreeOwner owner, string nodeId)
    {
        var tree = owner.Tree; bool was = tree.Has(nodeId);
        if (!tree.Unlock(nodeId)) return false;
        var node = tree.Nodes[nodeId];
        string? skillId = node.Str("effect") == "active_skill" ? node.Get("value") as string : null;
        if (skillId != null && tree.Catalog.Skills.TryGetValue(skillId, out var row) && !row.Basic)
        {
            owner.UnlockedMainSkills.Add(skillId);
            if (!was && !owner.EquippedMainSkills.Contains(skillId) && owner.EquippedMainSkills.Contains(null))
                owner.EquippedMainSkills[owner.EquippedMainSkills.IndexOf(null)] = skillId;
        }
        if (node.Str("kind") == "root" && node.Str("family") == "basic" && !was && owner.BasicSkillId == null) owner.BasicSkillId = node.Str("skill_id");
        if (node.Str("kind") == "passive")
        {
            string side = SkillTree.SideKey(node);
            if (tree.Slot(side) == null) tree.SetSlot(side, nodeId);
        }
        owner.RefreshStats();
        return true;
    }

    public static (bool Ok, string Message) Refund(ITreeOwner owner, string key)
    {
        var tree = owner.Tree; var reason = tree.RefundReason(key);
        if (reason != null) return (false, reason);
        var node = tree.Nodes[key]; string kind = node.Str("kind");
        if (kind is "root" or "class_passive" && tree.Ranks.GetValueOrDefault(key, 1) > 1)
        {
            int amount = Math.Min(1, tree.Investments.GetValueOrDefault(key)); tree.Investments[key] = Math.Max(0, tree.Investments.GetValueOrDefault(key) - amount);
            tree.Ranks[key]--; tree.Points += amount; owner.RefreshStats();
            return (true, $"Rank reduzido; {amount} ponto devolvido.");
        }
        int back = tree.Investments.Remove(key, out var invested) ? invested : 0; tree.Ranks.Remove(key);
        tree.Unlocked.Remove(key); tree.Points += back;
        tree.KeepSlots(value => value != key);
        if (kind == "root")
        {
            string skill = node.Str("skill_id");
            if (owner.BasicSkillId == skill) owner.BasicSkillId = null;
            for (int i = 0; i < owner.EquippedMainSkills.Count; i++) if (owner.EquippedMainSkills[i] == skill) owner.EquippedMainSkills[i] = null;
            owner.UnlockedMainSkills.Remove(skill);
            owner.CancelAttack("refund");
        }
        owner.RefreshStats();
        return (true, $"{back} ponto(s) devolvido(s)." + (back == 0 ? " Nó gratuito de save antigo." : ""));
    }

    // ------------------------------------------------------------------ loadout.py (five free slots)
    public static bool Known(ITreeOwner owner, string? skillId) => skillId != null && owner.Tree.Catalog.Skills.ContainsKey(skillId);
    public static bool IsBasic(ITreeOwner owner, string? skillId) => skillId != null && owner.Tree.Catalog.Skills.TryGetValue(skillId, out var r) && r.Basic;
    public static bool IsSupreme(ITreeOwner owner, string? skillId) => skillId != null && owner.Tree.Catalog.Skills.TryGetValue(skillId, out var r) && r.Category == "Suprema";
    public static List<string?> Slots(ITreeOwner owner) => new List<string?> { owner.BasicSkillId }.Concat(owner.EquippedMainSkills).ToList();
    public static bool Learned(ITreeOwner owner, string? skillId) => skillId != null && owner.Tree.Has("learn:" + skillId);

    private static void Set(ITreeOwner owner, int index, string? skillId)
    {
        if (index == 0)
        {
            if (owner.BasicSkillId != skillId) owner.CancelAttack("equip");
            owner.BasicSkillId = skillId;
        }
        else
        {
            owner.EquippedMainSkills[index - 1] = skillId;
            if (skillId != null && !IsBasic(owner, skillId)) owner.UnlockedMainSkills.Add(skillId);
        }
    }

    public static (bool Ok, string Message) Equip(ITreeOwner owner, string skillId, int index)
    {
        if (!(0 <= index && index < TreeCatalog.SlotCount)) return (false, "Espaço inválido.");
        if (!Known(owner, skillId) || !Learned(owner, skillId)) return (false, "Aprenda a habilidade antes de equipar.");
        string message = $"Equipada no espaço {index + 1}.";
        var slots = Slots(owner);
        for (int other = 0; other < slots.Count; other++)
        {
            var current = slots[other];
            if (other == index || current == null) continue;
            if (current == skillId) Set(owner, other, null);
            else if (IsSupreme(owner, skillId) && IsSupreme(owner, current))
            {
                Set(owner, other, null); message = $"Só uma Suprema: a outra saiu do espaço {other + 1}.";
            }
        }
        Set(owner, index, skillId);
        if (index >= 1) owner.ActiveMainSlot = index - 1;
        owner.RefreshStats();
        return (true, message);
    }

    public static void Unequip(ITreeOwner owner, int index) { Set(owner, index, null); owner.RefreshStats(); }

    public static int? IndexOf(ITreeOwner owner, string skillId)
    {
        var slots = Slots(owner);
        for (int i = 0; i < slots.Count; i++) if (slots[i] != null && slots[i] == skillId) return i;
        return null;
    }

    /// <summary>tuning.chosen: indices of the options of a skill that are bought and active, in branch order.</summary>
    public static List<int> Chosen(SkillTree tree, string skillId)
    {
        var keys = Tuning.OptionKeys(skillId); var picks = new List<int>();
        for (int i = 0; i < keys.Count; i++)
            if (tree.Unlocked.Contains(keys[i]) && tree.Nodes.ContainsKey(keys[i]) && tree.Active(keys[i])) picks.Add(i);
        return picks;
    }

    /// <summary>tuning.resolved for a hero: rank and options read from the tree.</summary>
    public static PyDict Resolve(SkillTree tree, PyDict row)
    {
        string id = row.Str("id");
        var options = row.List("options");
        return Tuning.Resolve(row, tree.Rank("learn:" + id), Chosen(tree, id).Where(i => i < options.Count));
    }
}
