using Umbra.Domain.Gameplay;

namespace Umbra.BetaTester;

/// <summary>All runs of one persona, reduced to the numbers the analyst and the report use.</summary>
public sealed class Summary
{
    public required Persona Persona { get; init; }
    public required IReadOnlyList<RunRecord> Runs { get; init; }

    public int Count => Runs.Count;
    public double Rate(Outcome o) => Count == 0 ? 0 : Runs.Count(r => r.Outcome == o) / (double)Count;
    public IEnumerable<RunRecord> Finished => Runs.Where(r => r.Outcome is Outcome.Cleared or Outcome.Died);

    public double MedianClearSeconds => Median(Runs.Where(r => r.Outcome == Outcome.Cleared).Select(r => r.Seconds));
    public double MeanLowestHealthShare => Mean(Finished.Select(r => r.LowestHealth / (double)Math.Max(1, r.MaxHealth)));
    public double MeanDamageTaken => Mean(Finished.Select(r => (double)r.DamageTaken));
    public int Strikes => Runs.Sum(r => r.StrikesLanded + r.StrikesMissed);
    public double StrikeLandedShare => Strikes == 0 ? 0 : Runs.Sum(r => r.StrikesLanded) / (double)Strikes;
    public int DodgesWanted => Runs.Sum(r => r.DodgesStarted + r.DodgesLocked);
    public double DodgeLockedShare => DodgesWanted == 0 ? 0 : Runs.Sum(r => r.DodgesLocked) / (double)DodgesWanted;
    public double MedianKillSeconds => Median(Runs.SelectMany(r => r.KillTicks).Select(t => t / (double)SliceData.TicksPerSecond));
    public double FuryCappedShare => Share(r => r.FuryCappedTicks, r => r.Ticks);
    public double IdleShare => Share(r => r.IdleTicks, r => r.Ticks);
    public double DropsPerRun => Count == 0 ? 0 : Runs.Sum(r => r.DropsByRarity.Sum()) / (double)Count;
    public int Drops(Rarity rarity) => Runs.Sum(r => r.DropsByRarity[(int)rarity]);
    public double MeanAttackFromGear => Mean(Finished.Select(r => (double)r.AttackFromGear));
    public double MeanDefenseFromGear => Mean(Finished.Select(r => (double)r.DefenseFromGear));
    public double MeanHealthFromGear => Mean(Finished.Select(r => (double)r.HealthFromGear));
    public int BagFullRuns => Runs.Count(r => r.BagFullTicks > 0);

    public IEnumerable<string> SkillIds => Runs.SelectMany(r => r.Skills.Keys).Distinct().OrderBy(SkillOrder);
    public int TotalDamage => Runs.Sum(r => r.Skills.Values.Sum(s => s.Damage));
    public SkillUse Skill(string id)
    {
        var all = new SkillUse();
        foreach (var s in Runs.Select(r => r.Skills.GetValueOrDefault(id)).OfType<SkillUse>())
        {
            all.Casts += s.Casts; all.Whiffs += s.Whiffs; all.Hits += s.Hits; all.Damage += s.Damage; all.Overkill += s.Overkill; all.Kills += s.Kills;
        }
        return all;
    }
    public double DamageShare(string id) => TotalDamage == 0 ? 0 : Skill(id).Damage / (double)TotalDamage;
    public double WhiffShare(string id) { var s = Skill(id); return s.Casts == 0 ? 0 : s.Whiffs / (double)s.Casts; }
    public double OverkillShare(string id) { var s = Skill(id); return s.Damage + s.Overkill == 0 ? 0 : s.Overkill / (double)(s.Damage + s.Overkill); }

    /// <summary>Useful damage per second of the time the skill locks the hero (recovery), from what the bot actually dealt.</summary>
    public double DamagePerLockedSecond(string id)
    {
        var s = Skill(id); var def = SkillName.Def(id);
        return s.Casts == 0 || def == null ? 0 : s.Damage / (s.Casts * def.RecoveryTicks / (double)SliceData.TicksPerSecond);
    }

    private double Share(Func<RunRecord, int> part, Func<RunRecord, int> whole)
    {
        double w = Runs.Sum(whole);
        return w == 0 ? 0 : Runs.Sum(part) / w;
    }

    private static int SkillOrder(string id) => id == SliceData.GolpeBrutal.Id ? 0 : id == SliceData.Ruptura.Id ? 1 : 2;

    public static double Mean(IEnumerable<double> values) { var v = values.ToList(); return v.Count == 0 ? 0 : v.Average(); }
    public static double Median(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToList();
        if (v.Count == 0) return 0;
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }
}

public static class SkillName
{
    public static SkillDef? Def(string id) =>
        new[] { SliceData.GolpeBrutal, SliceData.Ruptura, SliceData.Investida }.FirstOrDefault(s => s.Id == id);
    public static string Of(string id) => Def(id)?.Name ?? id;
}
