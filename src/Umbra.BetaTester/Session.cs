using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Umbra.Domain.Items;
using Umbra.Domain.Play;
using Umbra.Domain.Skills;

namespace Umbra.BetaTester;

/// <summary>
/// One game played by one persona: the bot drives the real <see cref="GameSession"/> (UMBRA's rules, step 5.8c) tick by
/// tick, uses the inventory and the tree the way the menus do, and everything that happens is measured into a
/// <see cref="RunRecord"/>. Exceptions, stuck heroes and runs that never end are results too, not crashes of the tester.
/// Runs share the rules' process-wide services (SkillEnv, EnemyAI.Active), so they must run one at a time.
/// </summary>
public static class Session
{
    public const int MaxTicks = 10 * 60 * Facts.TicksPerSecond;   // 10 minutes of game time
    public const int StuckLimit = 3 * Facts.TicksPerSecond;       // trying to move for 3 s without moving
    /// <summary>Ticks after a cast in which a hit still counts for it (the Básica lands after its 5-frame wind-up; Investida
    /// travels a few tiles).</summary>
    public const int HitWindow = 14;

    /// <summary>Points plan of a player who spends them (the slice starts with 3): the attack, the spender, the attack again,
    /// then the dash. The tree screen does the same through TreeRules.Learn.</summary>
    public static readonly string[] PointsPlan =
        { "learn:" + Facts.Golpe, "learn:" + Facts.Ruptura, "learn:" + Facts.Golpe, "learn:" + Facts.Investida };

    private static readonly object RunGate = new();

    public static RunRecord Play(Persona persona, ulong seed)
    {
        Facts.Load();
        var rec = new RunRecord { Persona = persona.Id, Seed = seed };
        lock (RunGate)
        {
            try { Run(persona, seed, rec); }
            catch (Exception ex) { rec.Outcome = Outcome.Crashed; rec.Error = $"{ex.GetType().Name}: {ex.Message}"; }
        }
        return rec;
    }

    private static int StableSeed(Persona persona, ulong seed) =>
        (int)((seed * 2654435761UL + (ulong)persona.Id.Sum(c => c * 31)) % int.MaxValue);

    private static void Run(Persona persona, ulong seed, RunRecord rec)
    {
        var s = new GameSession(seed);
        var hero = s.Hero;
        var bot = new BotPlayer(persona, StableSeed(persona, seed));
        rec.Enemies = s.Enemies.Count; rec.EnemyLife = s.Enemies.Sum(e => e.MaxHealth); rec.LowestHealth = hero.Health;
        if (persona.SpendsPoints) foreach (var key in PointsPlan) if (TreeRules.Learn(hero, key)) rec.PointsSpent++;

        var firstHit = new Dictionary<int, int>();
        var lastSkill = new Dictionary<int, string>();
        var trace = new StringBuilder();
        string? open = null; int openTick = 0; bool openHit = false;   // the latest cast and whether it has hit yet
        int stuck = 0;
        rec.Outcome = Outcome.Timeout;

        void Close() { if (open != null && !openHit) rec.Skill(open).Whiffs++; open = null; }

        while (s.Tick < MaxTicks)
        {
            if (!hero.Alive) { rec.Outcome = Outcome.Died; break; }
            if (s.Cleared && s.Items.All(i => bot.Skipped.Contains(i.Id) || BagFull(s) && i.Kind is "weapon" or "armor" or "accessory"))
            { rec.Outcome = Outcome.Cleared; break; }

            var input = bot.Next(s);
            var before = s.HeroWorld;
            s.Step(input);
            bot.Observe(s);

            foreach (var ev in s.Events.OfType<CastEvent>())
            {
                Close();
                open = ev.SkillId; openTick = s.Tick; openHit = false; rec.Skill(ev.SkillId).Casts++;
            }
            if (open != null && s.Tick - openTick > HitWindow) Close();
            string current = open ?? s.LastCast ?? Facts.Golpe;
            foreach (var ev in s.Events)
            {
                switch (ev)
                {
                    case DamageEvent { OnHero: true } h:
                        rec.DamageTaken += h.Amount; break;
                    case DamageEvent h:
                        var use = rec.Skill(current);
                        use.Hits++; use.Damage += h.Amount; openHit = true;
                        lastSkill[h.TargetId] = current;
                        firstHit.TryAdd(h.TargetId, s.Tick);
                        if (rec.FirstContactTick < 0) rec.FirstContactTick = s.Tick;
                        break;
                    case DeathEvent d:
                        rec.Kills++;
                        rec.Skill(lastSkill.GetValueOrDefault(d.EnemyId, current)).Kills++;
                        rec.KillTicks.Add(s.Tick - firstHit.GetValueOrDefault(d.EnemyId, s.Tick));
                        break;
                    case StrikeEvent st:
                        if (st.Landed) rec.StrikesLanded++; else rec.StrikesMissed++;
                        break;
                    case ItemDroppedEvent dr when ItemView.Equipment(dr.Item.Data):
                        var rarity = ItemView.Rarity(dr.Item.Data);
                        rec.DropsByRarity[rarity] = rec.DropsByRarity.GetValueOrDefault(rarity) + 1;
                        break;
                    case ItemTakenEvent { ToBag: true }:
                        rec.PickedUp++; Gear(persona, s, rec); break;
                    case LevelUpEvent:
                        rec.LevelUps++; break;
                    case PotionEvent:
                        rec.PotionsUsed++; break;
                }
            }

            if (hero.MaxResource > 0 && hero.Resource >= hero.MaxResource) rec.FuryCappedTicks++;
            if (BagFull(s) && s.Items.Any(i => ItemView.Equipment(i.Data))) rec.BagFullTicks++;
            bool busy = hero.Pending != null || s.Dashing;
            bool pressing = input.Attack || input.Skill1 || input.Skill2 || input.Move.LengthSquared() > 1e-4f || s.Destination != null;
            if (!pressing && !s.Cleared && !busy) rec.IdleTicks++;
            bool wantsToMove = input.Move.LengthSquared() > 1e-4f || s.Destination != null;
            stuck = wantsToMove && !busy && Vector2.Distance(before, s.HeroWorld) < .002f ? stuck + 1 : 0;
            rec.LongestStuckTicks = Math.Max(rec.LongestStuckTicks, stuck);
            if (stuck >= StuckLimit) { rec.Outcome = Outcome.Stuck; break; }
            rec.LowestHealth = Math.Min(rec.LowestHealth, hero.Health);
            if (s.Tick % 60 == 0) trace.Append($"{s.HeroWorld.X:F3},{s.HeroWorld.Y:F3},{hero.Health},{s.EnemiesRemaining};");
        }
        Close();

        rec.Ticks = s.Tick;
        rec.MaxHealth = hero.MaxHealth;
        rec.DodgesStarted = bot.DodgesStarted; rec.DodgesLocked = bot.DodgesLocked;
        // Attack and armor above the base: the slice's Bárbaro buys no tree effects that add them, so it is the gear.
        rec.AttackFromGear = hero.Attack - hero.BaseAttack;
        rec.DefenseFromGear = hero.Defense - hero.BaseDefense;
        rec.HealthFromGear = (int)Umbra.Domain.Items.Gear.Records(hero).Sum(r => ItemView.Total(r, "health"));
        trace.Append(rec.Outcome).Append('|').Append(string.Join(',', hero.Inventory.Items.Select(i => ItemView.Name(i) + ItemView.Tag(i))));
        rec.Digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace.ToString())))[..16];
    }

    public static bool BagFull(GameSession s) => s.Hero.Inventory.Items.Count >= s.Hero.Inventory.Capacity;

    /// <summary>The bot's own judgement of an item (a player's rule of thumb, not a game rule): what the tooltip shows.</summary>
    public static double Score(PyDict? item) =>
        item == null ? -1 : ItemView.Total(item, "attack") * 3 + ItemView.Total(item, "defense") * 2 + ItemView.Total(item, "health") * .5
                            + item.Num("value", 0) * 2;

    /// <summary>Equips from the bag the way the persona would in the inventory screen (Gear.from_inventory).</summary>
    public static void Gear(Persona persona, GameSession s, RunRecord rec)
    {
        if (persona.Gear == GearHabit.Never) return;
        var hero = s.Hero; var loadout = Umbra.Domain.Items.Gear.Ensure(hero);
        for (int i = hero.Inventory.Items.Count - 1; i >= 0; i--)
        {
            var item = hero.Inventory.Items[i];
            if (!ItemView.Equipment(item) || Umbra.Domain.Items.Gear.TargetSlot(hero, item) is not { } slot) continue;
            var worn = loadout.Get(slot) as PyDict;
            bool take = worn == null || persona.Gear == GearHabit.Upgrades && Score(item) > Score(worn);
            if (take && Umbra.Domain.Items.Gear.FromInventory(hero, i).Ok) rec.Equipped++;
        }
    }
}
