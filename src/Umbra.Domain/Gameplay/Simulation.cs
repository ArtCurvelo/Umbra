using System.Numerics;
using Umbra.Domain.Core;
using Umbra.Domain.World;

namespace Umbra.Domain.Gameplay;

/// <summary>
/// The whole slice as a fixed-step simulation (60 ticks per second). Presentation reads the state and the events of the
/// last tick; it never changes rules. The same seed and the same inputs give the same result, so tests can replay a run.
/// </summary>
public sealed class Simulation
{
    public const float TickSeconds = 1f / SliceData.TicksPerSecond;
    private const float StrikeSlack = .35f;

    private readonly Rng _rng;
    private int _nextId = 1;
    private bool _anyDrop;

    public DungeonMap Map { get; }
    public Hero Hero { get; }
    public List<Enemy> Enemies { get; } = new();
    public List<Drop> Drops { get; } = new();
    public const int BagSize = 30;
    public List<Item> Inventory { get; } = new();
    public bool BagFull => Inventory.Count >= BagSize;
    public List<GameEvent> Events { get; } = new();
    public int Tick { get; private set; }

    public int EnemiesRemaining => Enemies.Count(e => e.Alive);
    public bool Cleared => EnemiesRemaining == 0;

    public Simulation(ulong seed)
    {
        Map = CryptGenerator.Generate(seed);
        _rng = new Rng(seed ^ 0x5DEECE66DUL);
        Hero = new Hero(_nextId++, Map.Start);
        foreach (var spawn in Map.EnemySpawns) Enemies.Add(new Enemy(_nextId++, spawn) { Facing = new Vector2(-1, 0) });
    }

    public void Step(in InputFrame input)
    {
        Events.Clear();
        Tick++;
        if (Hero.Alive) StepHero(input);
        foreach (var enemy in Enemies) if (enemy.Alive) StepEnemy(enemy);
        Separate();
        Pickups();
    }

    // ---------------------------------------------------------------- hero

    private void StepHero(in InputFrame input)
    {
        foreach (var key in Hero.Cooldowns.Keys.ToList()) if (Hero.Cooldowns[key] > 0) Hero.Cooldowns[key]--;
        if (Hero.Recovery > 0) Hero.Recovery--;
        var aim = Direction(Hero.Position, input.Aim, Hero.Facing);

        if (Hero.DashTicks > 0) { StepDash(); return; }
        if (Hero.Recovery == 0)
        {
            if (input.Skill2 && Hero.Cooldown(SliceData.Investida.Id) == 0) { StartDash(aim); return; }
            if (input.Skill1 && Hero.Fury >= SliceData.Ruptura.Cost) { CastCone(SliceData.Ruptura, aim); return; }
            if (input.Attack) { Swing(SliceData.GolpeBrutal, aim); return; }
            Walk(input);
        }
    }

    private void Walk(in InputFrame input)
    {
        float maxStep = Hero.Stats.Speed * TickSeconds;
        Vector2 velocity = Vector2.Zero;
        if (input.Move.LengthSquared() > 1e-4f)
        {
            Hero.Destination = null;
            velocity = Vector2.Normalize(input.Move) * maxStep * Math.Min(1f, input.Move.Length());
        }
        else
        {
            if (input.Destination is { } goal) Hero.Destination = goal;
            if (Hero.Destination is { } dest)
            {
                var to = dest - Hero.Position;
                if (to.Length() < .08f) Hero.Destination = null;
                else velocity = Vector2.Normalize(to) * Math.Min(maxStep, to.Length());
            }
        }
        if (velocity == Vector2.Zero) return;
        var next = Collision.Move(Map, Hero.Position, velocity, Hero.Radius);
        if (Hero.Destination != null && Vector2.Distance(next, Hero.Position) < velocity.Length() * .1f) Hero.Destination = null; // stuck
        Hero.Position = next;
        Hero.Facing = Vector2.Normalize(velocity);
    }

    private void Swing(SkillDef skill, Vector2 aim)
    {
        Begin(skill, aim);
        Enemy? target = null; float best = float.MaxValue;
        foreach (var enemy in Enemies)
        {
            if (!enemy.Alive || !InArc(enemy, aim, skill.Range, skill.ArcDegrees)) continue;
            float d = Vector2.Distance(Hero.Position, enemy.Position);
            if (d < best) { best = d; target = enemy; }
        }
        if (target == null) return;
        Hit(target, skill);
        Hero.Fury = Math.Min(SliceData.FuryMax, Hero.Fury + skill.FuryGain);
    }

    private void CastCone(SkillDef skill, Vector2 aim)
    {
        Hero.Fury -= skill.Cost;
        Begin(skill, aim);
        foreach (var enemy in Enemies.ToList())
            if (enemy.Alive && InArc(enemy, aim, skill.Range, skill.ArcDegrees)) Hit(enemy, skill);
    }

    private void StartDash(Vector2 aim)
    {
        var skill = SliceData.Investida;
        Begin(skill, aim);
        Hero.Cooldowns[skill.Id] = skill.CooldownTicks;
        Hero.Destination = null;
        Hero.DashTicks = skill.RecoveryTicks;
        Hero.DashVelocity = aim * (skill.Range / skill.RecoveryTicks);
        Hero.DashHits.Clear();
    }

    private void StepDash()
    {
        Hero.Position = Collision.Move(Map, Hero.Position, Hero.DashVelocity, Hero.Radius);
        foreach (var enemy in Enemies)
            if (enemy.Alive && !Hero.DashHits.Contains(enemy.Id)
                && Vector2.Distance(enemy.Position, Hero.Position) <= enemy.Radius + Hero.Radius + .2f)
            {
                Hero.DashHits.Add(enemy.Id);
                Hit(enemy, SliceData.Investida);
            }
        Hero.DashTicks--;
    }

    private void Begin(SkillDef skill, Vector2 aim)
    {
        Hero.Facing = aim;
        Hero.Recovery = skill.RecoveryTicks;
        Hero.LastSkill = skill.Id;
        Hero.LastSkillTick = Tick;
        Hero.Destination = null;
        Events.Add(new SkillEvent(skill.Id, Hero.Position, aim));
    }

    /// <summary>Enemy inside a skill's reach (range from the hero's edge to the enemy's edge) and arc.</summary>
    public bool InArc(Enemy enemy, Vector2 aim, float range, float arcDegrees)
    {
        var to = enemy.Position - Hero.Position;
        float d = to.Length();
        if (d - enemy.Radius > range + Hero.Radius) return false;
        if (d < 1e-3f) return true;
        float cos = Vector2.Dot(to / d, aim);
        return cos >= MathF.Cos(arcDegrees * .5f * MathF.PI / 180f);
    }

    private void Hit(Enemy enemy, SkillDef skill)
    {
        int amount = SliceData.Damage(Hero.Attack, skill.Multiplier * Hero.Ranks.Factor(skill.Id), enemy.Stats.Defense);
        enemy.Health = Math.Max(0, enemy.Health - amount);
        enemy.LastHitTick = Tick;
        if (enemy.State == AiState.Idle) enemy.State = AiState.Chase;
        Events.Add(new HitEvent(enemy.Id, amount, enemy.Position, false));
        if (enemy.Alive) return;
        Events.Add(new DiedEvent(enemy.Id, enemy.Position));
        bool last = EnemiesRemaining == 0;
        if (_rng.Chance(Loot.DropChance) || (last && !_anyDrop))
        {
            var drop = new Drop(_nextId++, Loot.Roll(_rng), enemy.Position);
            Drops.Add(drop); _anyDrop = true;
            Events.Add(new DroppedEvent(drop));
        }
    }

    // ---------------------------------------------------------------- inventory (called by the interface while paused)

    /// <summary>Equips the bag item at <paramref name="index"/>; whatever was in that slot takes its place in the bag.</summary>
    public bool Equip(int index)
    {
        if (index < 0 || index >= Inventory.Count) return false;
        var item = Inventory[index];
        if (Hero.Equipment.TargetFor(item) is not { } slot) return false;
        var old = Hero.Equipment.Put(slot, item);
        if (old != null) Inventory[index] = old; else Inventory.RemoveAt(index);
        ClampHealth();
        return true;
    }

    public bool Unequip(EquipSlot slot)
    {
        if (BagFull || Hero.Equipment[slot] == null) return false;
        Inventory.Add(Hero.Equipment.Take(slot)!);
        ClampHealth();
        return true;
    }

    /// <summary>Drops a bag item at the hero's feet (nothing is destroyed; it can be picked up again).</summary>
    public bool Discard(int index)
    {
        if (index < 0 || index >= Inventory.Count) return false;
        var drop = new Drop(_nextId++, Inventory[index], Hero.Position + Hero.Facing * .9f);
        Inventory.RemoveAt(index);
        Drops.Add(drop);
        Events.Add(new DroppedEvent(drop));
        return true;
    }

    private void ClampHealth() => Hero.Health = Math.Min(Hero.Health, Hero.MaxHealth);

    // ---------------------------------------------------------------- enemies

    private float Reach(Enemy enemy) => enemy.Radius + Hero.Radius + .55f;

    private void StepEnemy(Enemy enemy)
    {
        float distance = Vector2.Distance(enemy.Position, Hero.Position);
        switch (enemy.State)
        {
            case AiState.Idle:
                if (Hero.Alive && distance <= SliceData.BruteDetection && Collision.LineOfSight(Map, enemy.Position, Hero.Position))
                    enemy.State = AiState.Chase;
                break;
            case AiState.Chase:
                if (!Hero.Alive) { enemy.State = AiState.Idle; break; }
                enemy.Facing = Direction(enemy.Position, Hero.Position, enemy.Facing);
                if (distance <= Reach(enemy))
                {
                    enemy.State = AiState.Windup; enemy.StateTicks = SliceData.BruteWindupTicks;
                    Events.Add(new TelegraphEvent(enemy.Id, SliceData.BruteWindupTicks));
                    break;
                }
                enemy.Position = Collision.Move(Map, enemy.Position, enemy.Facing * enemy.Stats.Speed * TickSeconds, enemy.Radius);
                break;
            case AiState.Windup:                       // committed: the direction is fixed, so the hero can step out
                if (--enemy.StateTicks > 0) break;
                var to = Hero.Position - enemy.Position;
                bool landed = Hero.Alive && to.Length() <= Reach(enemy) + StrikeSlack
                              && (to.Length() < 1e-3f || Vector2.Dot(Vector2.Normalize(to), enemy.Facing) >= .5f);
                if (landed)
                {
                    int amount = SliceData.Damage(enemy.Stats.Attack, 1f, Hero.Defense);
                    Hero.Health = Math.Max(0, Hero.Health - amount);
                    Events.Add(new HitEvent(Hero.Id, amount, Hero.Position, true));
                    if (!Hero.Alive) Events.Add(new HeroDiedEvent());
                }
                Events.Add(new StrikeEvent(enemy.Id, landed));
                enemy.State = AiState.Recover; enemy.StateTicks = SliceData.BruteRecoverTicks;
                break;
            case AiState.Recover:
                if (--enemy.StateTicks <= 0) enemy.State = AiState.Chase;
                break;
        }
    }

    /// <summary>Bodies do not overlap: each overlapping pair is pushed apart, never into a wall.</summary>
    private void Separate()
    {
        var bodies = new List<Actor>(Enemies.Where(e => e.Alive));
        if (Hero.Alive) bodies.Add(Hero);
        for (int i = 0; i < bodies.Count; i++)
            for (int j = i + 1; j < bodies.Count; j++)
            {
                var a = bodies[i]; var b = bodies[j];
                var delta = b.Position - a.Position; float d = delta.Length();
                float overlap = a.Radius + b.Radius - d;
                if (overlap <= 0) continue;
                var normal = d > 1e-4f ? delta / d : new Vector2(1, 0);
                Push(a, -normal * overlap * .5f);
                Push(b, normal * overlap * .5f);
            }
    }

    private void Push(Actor actor, Vector2 delta) => actor.Position = Collision.Move(Map, actor.Position, delta, actor.Radius);

    private void Pickups()
    {
        if (!Hero.Alive) return;
        for (int i = Drops.Count - 1; i >= 0; i--)
        {
            var drop = Drops[i];
            if (Vector2.Distance(drop.Position, Hero.Position) > Hero.Radius + .45f || BagFull) continue;
            Drops.RemoveAt(i); Inventory.Add(drop.Item);
            Events.Add(new PickedUpEvent(drop));
        }
    }

    private static Vector2 Direction(Vector2 from, Vector2 to, Vector2 fallback)
    {
        var d = to - from;
        return d.LengthSquared() > 1e-4f ? Vector2.Normalize(d) : fallback;
    }
}
