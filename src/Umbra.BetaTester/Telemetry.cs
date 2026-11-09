using Umbra.Domain.Gameplay;

namespace Umbra.BetaTester;

public enum Outcome { Cleared, Died, Timeout, Stuck, Crashed }

/// <summary>Use of one skill during a run.</summary>
public sealed class SkillUse
{
    public int Casts { get; set; }
    public int Whiffs { get; set; }       // casts that hit nothing
    public int Hits { get; set; }
    public int Damage { get; set; }
    public int Overkill { get; set; }     // damage past the enemy's remaining life
    public int Kills { get; set; }
}

/// <summary>Everything measured in one run: what the report reads. Ticks are simulation ticks (60 per second).</summary>
public sealed class RunRecord
{
    public required string Persona { get; init; }
    public required ulong Seed { get; init; }
    public Outcome Outcome { get; set; }
    public string? Error { get; set; }
    public int Ticks { get; set; }
    public double Seconds => Ticks / (double)SliceData.TicksPerSecond;
    public int Enemies { get; set; }
    public int Kills { get; set; }

    public int DamageTaken { get; set; }
    public int MaxHealth { get; set; }
    public int LowestHealth { get; set; }
    public int StrikesLanded { get; set; }
    public int StrikesMissed { get; set; }
    public int DodgesStarted { get; set; }
    public int DodgesLocked { get; set; }

    public Dictionary<string, SkillUse> Skills { get; } = new();
    public List<int> KillTicks { get; } = new();          // first hit -> death, per enemy
    public int FirstContactTick { get; set; } = -1;
    public int FuryCappedTicks { get; set; }
    public int IdleTicks { get; set; }                    // alive enemies, no input, nothing happening
    public int LongestStuckTicks { get; set; }

    public int[] DropsByRarity { get; } = new int[4];
    public int PickedUp { get; set; }
    public int BagFullTicks { get; set; }
    public int Equipped { get; set; }
    public int AttackFromGear { get; set; }
    public int DefenseFromGear { get; set; }
    public int HealthFromGear { get; set; }
    public int PointsSpent { get; set; }
    public string Digest { get; set; } = "";

    public SkillUse Skill(string id) => Skills.TryGetValue(id, out var s) ? s : Skills[id] = new SkillUse();
}
