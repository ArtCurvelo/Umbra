using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Umbra.Domain.Gameplay;

namespace Umbra.BetaTester;

/// <summary>
/// One game played by one persona: the bot drives the real <see cref="Simulation"/> tick by tick, uses the inventory and the
/// tree the way the menus do, and everything that happens is measured into a <see cref="RunRecord"/>. Exceptions, stuck
/// heroes and runs that never end are results too, not crashes of the tester.
/// </summary>
public static class Session
{
    public const int MaxTicks = 10 * 60 * SliceData.TicksPerSecond;   // 10 minutes of game time
    public const int StuckLimit = 3 * SliceData.TicksPerSecond;       // trying to move for 3 s without moving

    /// <summary>Points plan of a player who spends them (the slice starts with 5): the attack, then the spender, then the dash.</summary>
    public static readonly string[] PointsPlan =
    {
        SliceData.GolpeBrutal.Id, SliceData.Ruptura.Id, SliceData.GolpeBrutal.Id, SliceData.Ruptura.Id, SliceData.Investida.Id,
    };

    public static RunRecord Play(Persona persona, ulong seed)
    {
        var rec = new RunRecord { Persona = persona.Id, Seed = seed };
        try { Run(persona, seed, rec); }
        catch (Exception ex) { rec.Outcome = Outcome.Crashed; rec.Error = $"{ex.GetType().Name}: {ex.Message}"; }
        return rec;
    }

    private static int StableSeed(Persona persona, ulong seed) =>
        (int)((seed * 2654435761UL + (ulong)persona.Id.Sum(c => c * 31)) % int.MaxValue);

    private static void Run(Persona persona, ulong seed, RunRecord rec)
    {
        var sim = new Simulation(seed);
        var hero = sim.Hero;
        var bot = new BotPlayer(persona, StableSeed(persona, seed));
        rec.Enemies = sim.Enemies.Count; rec.LowestHealth = hero.Health;
        if (persona.SpendsPoints) foreach (var skill in PointsPlan) if (hero.Ranks.Raise(skill)) rec.PointsSpent++;

        var firstHit = new Dictionary<int, int>();
        var lastSkill = new Dictionary<int, string>();
        var trace = new StringBuilder();
        bool wasDashing = false; int stuck = 0;
        rec.Outcome = Outcome.Timeout;

        while (sim.Tick < MaxTicks)
        {
            if (!hero.Alive) { rec.Outcome = Outcome.Died; break; }
            if (sim.Cleared && (sim.Drops.Count == 0 || sim.BagFull)) { rec.Outcome = Outcome.Cleared; break; }

            var healthBefore = sim.Enemies.ToDictionary(e => e.Id, e => e.Health);
            var input = bot.Next(sim);
            var before = hero.Position;
            sim.Step(input);

            string? cast = null; int hitsThisTick = 0;
            foreach (var ev in sim.Events.OfType<SkillEvent>()) { cast = ev.SkillId; rec.Skill(ev.SkillId).Casts++; }
            string current = cast ?? (hero.DashTicks > 0 || wasDashing ? SliceData.Investida.Id : hero.LastSkill ?? SliceData.GolpeBrutal.Id);
            foreach (var ev in sim.Events)
            {
                switch (ev)
                {
                    case HitEvent { OnHero: true } h:
                        rec.DamageTaken += h.Amount; break;
                    case HitEvent h:
                        var use = rec.Skill(current);
                        int left = healthBefore.GetValueOrDefault(h.TargetId);
                        use.Hits++; use.Damage += Math.Min(h.Amount, left); use.Overkill += Math.Max(0, h.Amount - left);
                        hitsThisTick++; lastSkill[h.TargetId] = current;
                        if (!firstHit.ContainsKey(h.TargetId)) firstHit[h.TargetId] = sim.Tick;
                        if (rec.FirstContactTick < 0) rec.FirstContactTick = sim.Tick;
                        break;
                    case DiedEvent d:
                        rec.Kills++;
                        rec.Skill(lastSkill.GetValueOrDefault(d.ActorId, current)).Kills++;
                        rec.KillTicks.Add(sim.Tick - firstHit.GetValueOrDefault(d.ActorId, sim.Tick));
                        break;
                    case StrikeEvent s:
                        if (s.Landed) rec.StrikesLanded++; else rec.StrikesMissed++;
                        break;
                    case DroppedEvent dr:
                        rec.DropsByRarity[(int)dr.Drop.Item.Rarity]++; break;
                    case PickedUpEvent:
                        rec.PickedUp++; Gear(persona, sim, rec); break;
                }
            }
            if (cast != null && cast != SliceData.Investida.Id && hitsThisTick == 0) rec.Skill(cast).Whiffs++;
            if (wasDashing && hero.DashTicks == 0 && hero.DashHits.Count == 0) rec.Skill(SliceData.Investida.Id).Whiffs++;
            wasDashing = hero.DashTicks > 0;

            if (hero.Fury >= SliceData.FuryMax) rec.FuryCappedTicks++;
            if (sim.BagFull && sim.Drops.Count > 0) rec.BagFullTicks++;
            bool pressing = input.Attack || input.Skill1 || input.Skill2 || input.Move.LengthSquared() > 1e-4f || hero.Destination != null;
            if (!pressing && !sim.Cleared && hero.Recovery == 0 && hero.DashTicks == 0) rec.IdleTicks++;
            bool wantsToMove = input.Move.LengthSquared() > 1e-4f || hero.Destination != null;
            stuck = wantsToMove && hero.Recovery == 0 && hero.DashTicks == 0 && Vector2.Distance(before, hero.Position) < .002f ? stuck + 1 : 0;
            rec.LongestStuckTicks = Math.Max(rec.LongestStuckTicks, stuck);
            if (stuck >= StuckLimit) { rec.Outcome = Outcome.Stuck; break; }
            rec.LowestHealth = Math.Min(rec.LowestHealth, hero.Health);
            if (sim.Tick % 60 == 0) trace.Append($"{hero.Position.X:F3},{hero.Position.Y:F3},{hero.Health},{sim.EnemiesRemaining};");
        }

        rec.Ticks = sim.Tick;
        rec.MaxHealth = hero.MaxHealth;
        rec.DodgesStarted = bot.DodgesStarted; rec.DodgesLocked = bot.DodgesLocked;
        rec.AttackFromGear = hero.Equipment.Total(Stat.Attack);
        rec.DefenseFromGear = hero.Equipment.Total(Stat.Defense);
        rec.HealthFromGear = hero.Equipment.Total(Stat.Health);
        trace.Append(rec.Outcome).Append('|').Append(string.Join(',', sim.Inventory.Select(i => i.Name + i.Tag + i.Power)));
        rec.Digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace.ToString())))[..16];
    }

    /// <summary>The bot's own judgement of an item (a player's rule of thumb, not a game rule).</summary>
    public static double Score(Item? item) =>
        item == null ? -1 : item.Total(Stat.Attack) * 3 + item.Total(Stat.Defense) * 2 + item.Total(Stat.Health) * .5;

    /// <summary>Equips from the bag the way the persona would in the inventory screen.</summary>
    public static void Gear(Persona persona, Simulation sim, RunRecord rec)
    {
        if (persona.Gear == GearHabit.Never) return;
        for (int i = sim.Inventory.Count - 1; i >= 0; i--)
        {
            var item = sim.Inventory[i];
            if (sim.Hero.Equipment.TargetFor(item) is not { } slot) continue;
            var worn = sim.Hero.Equipment[slot];
            bool take = worn == null || (persona.Gear == GearHabit.Upgrades && Score(item) > Score(worn));
            if (take && sim.Equip(i)) rec.Equipped++;
        }
    }
}
