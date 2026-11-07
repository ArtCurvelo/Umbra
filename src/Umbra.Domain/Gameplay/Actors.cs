using System.Numerics;

namespace Umbra.Domain.Gameplay;

public abstract class Actor
{
    public int Id { get; }
    public Stats Stats { get; }
    public Vector2 Position { get; set; }
    public Vector2 Facing { get; set; } = new(1, 0);
    public int Health { get; set; }
    public bool Alive => Health > 0;
    public float Radius => Stats.Radius;

    protected Actor(int id, Stats stats, Vector2 position)
    {
        Id = id; Stats = stats; Position = position; Health = stats.MaxHealth;
    }
}

public sealed class Hero : Actor
{
    public int Fury { get; set; }
    /// <summary>Ticks until the hero may act again (after a swing or a cast).</summary>
    public int Recovery { get; set; }
    public Dictionary<string, int> Cooldowns { get; } = new();
    public Vector2? Destination { get; set; }
    public int DashTicks { get; set; }
    public Vector2 DashVelocity { get; set; }
    public HashSet<int> DashHits { get; } = new();
    /// <summary>Last skill used and the tick it started, for animation.</summary>
    public string? LastSkill { get; set; }
    public int LastSkillTick { get; set; } = -1000;

    public Hero(int id, Vector2 position) : base(id, SliceData.Barbarian, position) { }

    public int Cooldown(string skillId) => Cooldowns.TryGetValue(skillId, out var t) ? t : 0;
}

public enum AiState { Idle, Chase, Windup, Recover }

public sealed class Enemy : Actor
{
    public string Name { get; }
    public AiState State { get; set; } = AiState.Idle;
    public int StateTicks { get; set; }
    public int LastHitTick { get; set; } = -1000;

    public Enemy(int id, Vector2 position) : base(id, SliceData.Brute, position) => Name = SliceData.BruteName;
}
