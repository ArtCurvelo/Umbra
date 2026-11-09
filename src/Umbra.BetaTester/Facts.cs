using Umbra.Domain.Combat.V16;
using Umbra.Domain.Data;
using Umbra.Domain.Enemies;
using Umbra.Domain.Heroes;
using Umbra.Domain.Play;
using Umbra.Domain.Skills;

namespace Umbra.BetaTester;

/// <summary>
/// The game's own numbers, read from the real tables and a fresh session (never typed here), for the analyst's evidence:
/// the hero at the start of the slice, the crypt family at the hero's level and the three skills of the slice.
/// </summary>
public static class Facts
{
    public const string Golpe = "barb2_golpe_brutal", Ruptura = "barb2_ruptura", Investida = "barb2_investida";
    public const int TicksPerSecond = 60;

    private static readonly object Gate = new();
    private static bool _loaded;

    /// <summary>Loads UMBRA's exported tables once (game/data/umbra).</summary>
    public static void Load()
    {
        lock (Gate)
        {
            if (_loaded) return;
            string root;
            try { root = UmbraData.FindRoot(); }
            catch (DirectoryNotFoundException) { root = UmbraData.FindRoot(Directory.GetCurrentDirectory()); }   // run from outside the repo's bin
            HeroTables.Load(root); FoeTables.Load(root);
            _loaded = true;
        }
    }

    private static GameSession Fresh() { Load(); return new GameSession(1); }

    public static (int MaxHealth, int Attack, int Defense, int MaxResource, int Level, int Points) Hero()
    {
        var h = Fresh().Hero;
        return (h.MaxHealth, h.Attack, h.Defense, h.MaxResource, h.Level, h.Tree.Points);
    }

    /// <summary>The crypt family's common enemies as the session creates them (the hero's level, no elite rank).</summary>
    public static List<(string Name, int MaxHealth, int Attack, int Defense, int Windup)> Foes()
    {
        var s = Fresh(); int level = Math.Max(GameSession.Floor, s.Hero.Level);
        return GameSession.CryptPool.Select(type => new Umbra.Domain.Enemies.Enemy((0, 0), level, false, type))
                                    .Select(e => (e.Name, e.MaxHealth, e.Attack, e.Defense, e.WindupDuration)).ToList();
    }

    /// <summary>The common enemy with the most life (what the rank table is measured against).</summary>
    public static (string Name, int MaxHealth, int Attack, int Defense, int Windup) Toughest() => Foes().MaxBy(f => f.MaxHealth);

    /// <summary>"Sombra 20, Espectro Velado 17, Cultista 28" (wind-up in ticks).</summary>
    public static string Windups() => string.Join(", ", Foes().Select(f => $"{f.Name} {f.Windup}"));

    public static string Name(string id) => (HeroTables.SkillRow(id) ?? HeroTables.BasicRow(id))?.Str("name", id) ?? id;

    /// <summary>A slot skill tuned by the hero's tree (rank 1 at the start of the slice).</summary>
    public static PyDict Row(string id)
    {
        var s = Fresh();
        var row = HeroTables.BasicRow(id) ?? HeroTables.SkillRow(id)!;
        return TreeRules.Resolve(s.Hero.Tree, row);
    }

    public static int Cost(string id) => (int)Row(id).Num("cost", 0);
    public static int Gain(string id) => (int)Row(id).Num("resource_gain", 0);

    /// <summary>Damage of one hit on a full-life enemy at a rank, without crits, combo, conditions or gear:
    /// player.attack_damage (the Básica multiplier inside it, or the tuned skill multiplier on top) minus the enemy's
    /// defense (Enemy.take_damage, at least 1). The Bárbaro of the slice has no tree effects that change it.</summary>
    public static int Damage(string id, int rank, int defense)
    {
        var s = Fresh(); var hero = s.Hero; string key = "learn:" + id;
        hero.Tree.Unlocked.Add(key); hero.Tree.Ranks[key] = rank;
        bool basic = TreeRules.IsBasic(hero, id);
        double raw = basic ? hero.AttackDamage(null, null, null, false)
                           : hero.AttackDamage(null, null, null, true) * TreeRules.Resolve(hero.Tree, HeroTables.SkillRow(id)!).Num("multiplier", 1);
        return Math.Max(1, (int)raw - defense);
    }

    /// <summary>Hits to defeat the toughest common enemy of the crypt, per rank.</summary>
    public static IEnumerable<(int Rank, int Damage, int Hits)> HitsToKill(string id)
    {
        var foe = Toughest();
        for (int rank = 1; rank <= TreeCatalog.MaxRank; rank++)
        {
            int damage = Damage(id, rank, foe.Defense);
            yield return (rank, damage, (foe.MaxHealth + damage - 1) / damage);
        }
    }
}
