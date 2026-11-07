namespace Umbra.Domain.Gameplay;

/// <summary>Base numbers of an actor. Values for the slice come from UMBRA's data/classes.json and data/enemies.json.</summary>
public sealed record Stats(int MaxHealth, int Attack, int Defense, float Speed, float Radius);

public enum SkillKind { Strike, Cone, Dash }

/// <summary>A skill as data. Ticks are simulation ticks (60 per second).</summary>
public sealed record SkillDef(string Id, string Name, SkillKind Kind, float Multiplier, float Range, float ArcDegrees,
                              int Cost, int FuryGain, int CooldownTicks, int RecoveryTicks);

public static class SliceData
{
    public const int TicksPerSecond = 60;

    /// <summary>Bárbaro (data/classes.json): vida 190, ataque 22, defesa 8; 4,8 tiles/s (world/locomotion.SPEED).</summary>
    public static readonly Stats Barbarian = new(190, 22, 8, 4.8f, .35f);
    public const int FuryMax = 100;

    /// <summary>Bruto Abissal (data/enemies.json): vida 65, ataque 14, defesa 2, 32 ticks por tile, detecção 6, preparação 35.</summary>
    public static readonly Stats Brute = new(65, 14, 2, 60f / 32f, .45f);
    public const string BruteName = "Bruto Abissal";
    public const float BruteDetection = 6f;
    public const int BruteWindupTicks = 35;
    public const int BruteRecoverTicks = 45;

    /// <summary>Catalog 0.16 (game/skills/v16/barbaro.py). Arcs and recovery are slice values (PROVISÓRIO).</summary>
    public static readonly SkillDef GolpeBrutal = new("barb2_golpe_brutal", "Golpe Brutal", SkillKind.Strike, 1.10f, 1f, 110f, 0, 9, 0, 24);
    public static readonly SkillDef Ruptura = new("barb2_ruptura", "Ruptura", SkillKind.Cone, 1.45f, 2f, 90f, 28, 0, 0, 30);
    public static readonly SkillDef Investida = new("barb2_investida", "Investida", SkillKind.Dash, .90f, 4f, 0f, 0, 0, 9 * TicksPerSecond, 12);

    /// <summary>Same rule as UMBRA's Enemy.take_damage: max(1, int(damage) - defense).</summary>
    public static int Damage(int attack, float multiplier, int defense) => Math.Max(1, (int)(attack * multiplier) - defense);
}
