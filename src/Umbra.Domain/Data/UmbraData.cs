using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Umbra.Domain.Data;

public sealed record ClassDef(string Id, string Label, int Health, int Mana, int Attack, int Defense, string Resource, string ResourceLabel,
                              int ResourceMax, double ResourceRegen, string Description);

public sealed record EnemyDef(string Id, string Name, int Health, int Attack, int Defense, int MoveCooldown, int Detection, int Windup);

public sealed record SkillOption(string Branch, string Name, string Description, JsonElement Ops);

/// <summary>A 0.16 catalog skill (game/skills/v16). Numbers as the catalog stores them: multiplier fraction, tiles,
/// ticks (60 Hz). Everything not typed here stays in <see cref="Raw"/> (riders, bonus, shape parameters).</summary>
public sealed record SkillData(string Id, string Name, string Description, string Category, string Tab, string Element, string Shape,
                               int UnlockLevel, int Cost, int Cooldown, int ResourceGain, double Multiplier, double Range, double Radius,
                               IReadOnlyList<SkillOption> Options, JsonElement Raw);

public sealed record PassiveData(string Id, string Name, string Levels, string Description, string Effect, IReadOnlyList<double> Values, int UnlockLevel);

public sealed record ClassSkills(IReadOnlyList<SkillData> Skills, IReadOnlyList<PassiveData> Passives, IReadOnlyList<SkillData> Extra);

/// <summary>
/// UMBRA's data, exported from the Python repository by tools/export_umbra_data.py (game/data/umbra). Read-only; nothing
/// here is retyped by hand. <see cref="Rules"/> keeps the raw rule tables (status, elements, rarity, palette).
/// </summary>
public sealed class UmbraData
{
    public IReadOnlyDictionary<string, ClassDef> Classes { get; }
    public IReadOnlyDictionary<string, EnemyDef> Enemies { get; }
    public IReadOnlyDictionary<string, ClassSkills> Skills { get; }
    public JsonElement Rules { get; }
    public JsonElement Bosses { get; }
    public JsonElement Items { get; }
    public JsonElement Manifest { get; }
    public string Root { get; }

    private UmbraData(string root)
    {
        Root = root;
        JsonElement Read(string name) => JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name), Encoding.UTF8)).RootElement;
        var classes = Read("classes.json").GetProperty("classes");
        Classes = classes.EnumerateObject().ToDictionary(p => p.Name, p => new ClassDef(
            p.Name, Str(p.Value, "label"), Int(p.Value, "health"), Int(p.Value, "mana"), Int(p.Value, "attack"), Int(p.Value, "defense"),
            Str(p.Value, "resource"), Str(p.Value, "resource_label"), Int(p.Value, "resource_max"), Num(p.Value, "resource_regen"), Str(p.Value, "description")));
        var enemies = Read("enemies.json").GetProperty("enemies");
        Enemies = enemies.EnumerateObject().ToDictionary(p => p.Name, p => new EnemyDef(
            p.Name, Str(p.Value, "name"), Int(p.Value, "health"), Int(p.Value, "attack"), Int(p.Value, "defense"),
            Int(p.Value, "move_cooldown"), Int(p.Value, "detection"), Int(p.Value, "windup")));
        var skills = Read("skills.json");
        Skills = skills.EnumerateObject().ToDictionary(p => p.Name, p => new ClassSkills(
            p.Value.GetProperty("skills").EnumerateArray().Select(Skill).ToList(),
            p.Value.GetProperty("passives").EnumerateArray().Select(Passive).ToList(),
            p.Value.GetProperty("extra").EnumerateArray().Select(Skill).ToList()));
        Rules = Read("rules.json");
        Bosses = Read("bosses.json");
        Items = Read("items.json");
        Manifest = Read("manifest.json");
    }

    public static UmbraData Load(string root) => new(root);

    /// <summary>Finds game/data/umbra walking up from <paramref name="start"/> (tests run from bin/…).</summary>
    public static string FindRoot(string? start = null)
    {
        var dir = new DirectoryInfo(start ?? AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "game", "data", "umbra");
            if (File.Exists(Path.Combine(candidate, "manifest.json"))) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("game/data/umbra não encontrado (rode tools/export_umbra_data.py)");
    }

    public SkillData Skill(string id) => Skills.Values.SelectMany(c => c.Skills.Concat(c.Extra)).First(s => s.Id == id);

    /// <summary>Files whose SHA-256 no longer matches the manifest (edited by hand after the export).</summary>
    public IEnumerable<string> Tampered()
    {
        foreach (var file in Manifest.GetProperty("files").EnumerateObject())
        {
            var text = File.ReadAllText(Path.Combine(Root, file.Name), Encoding.UTF8).TrimEnd('\n');
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            if (hash != file.Value.GetString()) yield return file.Name;
        }
    }

    private static SkillData Skill(JsonElement e) => new(
        Str(e, "id"), Str(e, "name"), Str(e, "description"), Str(e, "category"), Str(e, "tab"), Str(e, "element"), Str(e, "shape"),
        Int(e, "unlock_level"), Int(e, "cost"), Int(e, "cooldown"), Int(e, "resource_gain"), Num(e, "multiplier"), Num(e, "range"), Num(e, "radius"),
        e.TryGetProperty("options", out var o) ? o.EnumerateArray().Select(x => new SkillOption(Str(x, "branch"), Str(x, "name"), Str(x, "description"), x.GetProperty("ops").Clone())).ToList()
                                               : new List<SkillOption>(),
        e.Clone());

    private static PassiveData Passive(JsonElement e) => new(
        Str(e, "id"), Str(e, "name"), Str(e, "levels"), Str(e, "description"), Str(e, "effect"),
        e.GetProperty("values").EnumerateArray().Select(v => v.GetDouble()).ToList(), Int(e, "unlock_level"));

    private static string Str(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
    private static double Num(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
    private static int Int(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? (int)Math.Round(v.GetDouble()) : 0;
}
