using System.Numerics;
using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Enemies;
using InputFrame = Umbra.Domain.Gameplay.InputFrame;
using Umbra.Domain.Heroes;
using Umbra.Domain.Items;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;
using Umbra.Domain.World;
using EnemyActor = Umbra.Domain.Enemies.Enemy;

namespace Umbra.Domain.Play;

/// <summary>The slice crypt as the rules' tile map (UMBRA: tile (x, y) is centred on the integer point).</summary>
public sealed class CryptGrid : IMap
{
    private readonly DungeonMap _map;
    public CryptGrid(DungeonMap map) => _map = map;
    public int Width => _map.Width;
    public int Height => _map.Height;
    public int Cell(int x, int y) => _map.IsWall(x, y) ? 1 : 0;
    public bool Sanctuary(int x, int y) => false;
}

/// <summary>An item lying on a tile (world/sessions: Item with its data).</summary>
public sealed class GroundItem
{
    public required int Id;
    public required string Kind;
    public required PyDict Data;
    public (int X, int Y) Cell;
}

/// <summary>What happened in one tick, for the presentation layer. Rules never read these.</summary>
public abstract record PlayEvent;
public sealed record DamageEvent(int TargetId, int Amount, Vector2 Position, bool OnHero) : PlayEvent;
public sealed record DeathEvent(int EnemyId, Vector2 Position) : PlayEvent;
public sealed record WindupEvent(int EnemyId, int Ticks) : PlayEvent;
public sealed record CastEvent(string SkillId, string Anim, Vector2 Position, Vector2 Direction) : PlayEvent;
public sealed record ItemDroppedEvent(GroundItem Item) : PlayEvent;
public sealed record ItemTakenEvent(GroundItem Item, bool ToBag) : PlayEvent;
public sealed record LevelUpEvent(int Level) : PlayEvent;
public sealed record HeroDownEvent : PlayEvent;

/// <summary>
/// The vertical slice on UMBRA's real rules (step 5.8c): the real Bárbaro (Heroes.Hero, HeroCombat, Locomotion), the real
/// Bruto Abissal (Enemies.Enemy with its AI), real drops (Items.Loot) and the engine's tick order (core/engine.py update,
/// then the lifecycle steps on the hero, then the 0.16 world). Deaths follow world/sessions.process_enemy_deaths for
/// ordinary enemies: experience, Fúria, resource orb, drop roll. Positions for drawing are in slice world units
/// (tile (x, y) covers [x, x+1)): the rules' cell (x, y) is drawn at (x + .5, y + .5).
/// The rules use process-wide services (SkillEnv: random generator, hooks, clock); one session runs at a time.
/// </summary>
public sealed class GameSession
{
    public const int Floor = 1;
    /// <summary>PROVISÓRIO (slice): the hero starts at level 6 with the three skills of the slice learned and equipped,
    /// so Ruptura (level 3) and Investida (level 6) are available as before. A new UMBRA hero starts at level 1.</summary>
    public const int StartLevel = 6;
    public static readonly string[] StartSkills = { "barb2_golpe_brutal", "barb2_ruptura", "barb2_investida" };

    private readonly PyRandom _rng;
    private bool _held1, _held2;
    private readonly HeroSkillHooks _hooks = new();
    private int _nextId = 1000;
    private readonly HashSet<EnemyActor> _processed = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<EnemyActor, int> _ids = new(ReferenceEqualityComparer.Instance);

    public DungeonMap Map { get; }
    public CryptGrid Grid { get; }
    public Hero Hero { get; }
    public int HeroId => 1;
    public List<EnemyActor> Enemies { get; } = new();
    public List<GroundItem> Items { get; } = new();
    public List<PlayEvent> Events { get; } = new();
    public int Tick { get; private set; }
    public int Kills { get; private set; }

    public int EnemiesRemaining => Enemies.Count(e => e.Alive);
    public bool Cleared => EnemiesRemaining == 0;

    public GameSession(ulong seed, string playerClass = "barbaro")
    {
        Map = CryptGenerator.Generate(seed);
        Grid = new CryptGrid(Map);
        _rng = new PyRandom((long)(seed % long.MaxValue));
        Use();
        Hero = new Hero(ToCell(Map.Start), playerClass) { ActiveMap = Grid };
        for (int i = 1; i < StartLevel; i++) Hero.LevelUp();
        Hero.Health = Hero.MaxHealth;
        foreach (var id in StartSkills.Where(id => Hero.Tree.Nodes.ContainsKey("learn:" + id))) TreeRules.Learn(Hero, "learn:" + id);
        for (int slot = 0; slot < StartSkills.Length; slot++)
            if (Hero.Tree.Has("learn:" + StartSkills[slot])) TreeRules.Equip(Hero, StartSkills[slot], slot);
        foreach (var spawn in Map.EnemySpawns)
        {
            var enemy = new EnemyActor(ToCell(spawn), Floor, false, "bruto") { AttackDirection = (-1, 0) };
            _ids[enemy] = _nextId++; Enemies.Add(enemy);
        }
        Locomotion.Sync(Hero, Grid);
    }

    // ---------------------------------------------------------------- coordinates
    public static (int X, int Y) ToCell(Vector2 world) => ((int)MathF.Floor(world.X), (int)MathF.Floor(world.Y));
    public static Vector2 ToWorld((int X, int Y) cell) => new(cell.X + .5f, cell.Y + .5f);
    public Vector2 HeroWorld => Hero.Motion is { } m ? new((float)m.Position.X + .5f, (float)m.Position.Y + .5f) : ToWorld(Hero.Position);
    public int IdOf(EnemyActor enemy) => _ids[enemy];

    /// <summary>Points the process-wide rule services at this session (random generator, hooks, clock).</summary>
    public void Use()
    {
        SkillEnv.Rng = _rng; SkillEnv.Hooks = _hooks; SkillEnv.Clock = () => Tick * 1000L / 60;
        EnemyAI.Active = Enemies;
    }

    // ---------------------------------------------------------------- one tick
    /// <summary>Input first (UMBRA handles actions in its event pass), then the engine's update and the lifecycle steps.</summary>
    public void Step(in InputFrame input)
    {
        Use(); Events.Clear(); Tick++;
        var hero = Hero;
        hero.ActiveEnemies = Enemies; hero.CombatEnemies = Enemies; hero.ActiveMap = Grid;
        var before = Snapshot();
        if (input.Aim.LengthSquared() > 1e-4f) hero.AimTarget = ToCell(input.Aim);
        bool skill1 = input.Skill1 && !_held1, skill2 = input.Skill2 && !_held2;          // a skill fires on the press
        _held1 = input.Skill1; _held2 = input.Skill2;
        if (hero.Alive)
        {
            if (skill2) HeroCombat.UseSkill(hero, Enemies, 1);
            else if (skill1) HeroCombat.UseSkill(hero, Enemies, 0);
            else if (input.Attack) HeroCombat.AttackEnemy(hero, Enemies);         // held: the resolver buffers the next swing
        }
        if (SkillWorld.Channeling(hero) != null && hero.Skills.World?.Channel is { } channel)   // world.poll_hold
            hero.Skills.Hold = channel.Action switch { "attack" => input.Attack, "skill_1" => input.Skill1, "skill_2" => input.Skill2, _ => false };
        var previous = hero.Position;
        Locomotion.Advance(hero, Grid, MoveVector(input));
        if (hero.Position != previous) Collect();
        hero.Update();
        foreach (var enemy in Enemies.ToList())
            if (enemy.Alive && !EnemyActor.Dormant(enemy, hero.X, hero.Y)) enemy.Update(hero, Grid, _rng);
        ProcessDeaths();
        Status.TickOnce(hero); Resources.Tick(hero);
        Resources.TickOrbs(null, new[] { hero });
        SkillWorldTick.Tick(new SkillScene(hero, Enemies, Grid));
        Report(before);
    }

    private (double X, double Y) MoveVector(in InputFrame input)
    {
        if (input.Move.LengthSquared() > 1e-4f) { Destination = null; return (input.Move.X, input.Move.Y); }
        if (input.Destination is { } goal) Destination = goal;
        if (Destination is not { } dest) return (0, 0);
        var to = dest - HeroWorld;
        if (to.Length() < .12f) { Destination = null; return (0, 0); }
        var dir = Vector2.Normalize(to);
        return (dir.X, dir.Y);
    }

    /// <summary>Click-to-move target in world units (the click navigation of UMBRA walks there; PROVISÓRIO straight line).</summary>
    public Vector2? Destination { get; private set; }

    // ---------------------------------------------------------------- deaths and items
    private void ProcessDeaths()
    {
        foreach (var enemy in Enemies)
        {
            if (enemy.Alive || !_processed.Add(enemy)) continue;
            if (enemy.Summoned) continue;
            Kills++;
            int level = Hero.Level;
            Hero.GainXp(Elites.Xp(enemy, ItemLevelsXp(Math.Max(Floor, Hero.Level))));
            if (Hero.Level > level) Events.Add(new LevelUpEvent(Hero.Level));
            if (Elites.Rank(enemy) == "elite") Drop(_rng.Choice(new[] { "weapon", "armor", "accessory" }), enemy.Position);
            if (Hero.ResourceType is "fury" or "shards") Hero.GainResource(Hero.ResourceType == "fury" ? 5 : 10, "kill");
            if (_rng.Random() < 0.55)
            {
                var orb = Drop("resource_orb", enemy.Position);
                orb.Data["name"] = "Orbe de " + System.Globalization.CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(Hero.ResourceLabel.ToLowerInvariant());
            }
            double roll = _rng.Random();
            string? kind = roll < 0.05 ? "potion" : roll < 0.20 ? "gold" : roll < 0.29 ? "weapon" : roll < 0.37 ? "armor" : roll < 0.43 ? "accessory" : null;
            if (kind != null) Drop(kind, enemy.Position);
        }
    }

    /// <summary>skills/leveling.enemy_xp_reward (ordinary enemies); difficulty.xp is Normal (1.0) until the world step.</summary>
    public static long ItemLevelsXp(int floor) => 20 + Math.Max(1, floor) * 3;

    private GroundItem Drop(string kind, (int X, int Y) cell)
    {
        var item = new GroundItem { Id = _nextId++, Kind = kind, Data = Loot.Drop(kind, Floor, Hero.Level, Hero.PlayerClass, _rng), Cell = cell };
        Items.Add(item); Events.Add(new ItemDroppedEvent(item));
        return item;
    }

    /// <summary>Drops a bag item at the hero's feet (nothing is destroyed; stepping back onto the cell picks it up again).
    /// Slice-only action: UMBRA's inventory sells or recycles instead.</summary>
    public GroundItem? Discard(int index)
    {
        var bag = Hero.Inventory.Items;
        if (index < 0 || index >= bag.Count) return null;
        var record = bag[index]; bag.RemoveAt(index);
        string kind = record.Str("kind") is "weapon" or "armor" or "accessory" ? record.Str("kind") : "armor";
        var item = new GroundItem { Id = _nextId++, Kind = kind, Data = record, Cell = Hero.Position };
        Items.Add(item);
        return item;
    }

    /// <summary>world/sessions.collect_items: what lies on the hero's new cell is taken (equipment into the bag).</summary>
    private void Collect()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            if (item.Cell != Hero.Position) continue;
            if (Hero.Collect(item.Kind, item.Data)) { Items.RemoveAt(i--); Events.Add(new ItemTakenEvent(item, item.Kind is "weapon" or "armor" or "accessory")); }
        }
    }

    // ---------------------------------------------------------------- what the drawing reads (never the rules)
    public string? LastCast { get; private set; }
    public int LastCastTick { get; private set; } = -1000;
    public Vector2 Facing { get; private set; } = new(1, 0);
    public EnemyActor? EnemyById(int id) => Enemies.FirstOrDefault(e => _ids[e] == id);
    public Vector2 PositionOf(int id) => id == HeroId ? HeroWorld : ToWorld(EnemyById(id)!.Position);
    public Vector2 FacingOf(int id)
    {
        if (id == HeroId) return Facing;
        var (x, y) = EnemyById(id)!.AttackDirection;
        return x == 0 && y == 0 ? new Vector2(-1, 0) : Vector2.Normalize(new Vector2(x, y));
    }
    public bool AliveOf(int id) => id == HeroId ? Hero.Alive : EnemyById(id)!.Alive;
    /// <summary>0..1 while an enemy winds up (null when it is not).</summary>
    public float? WindupOf(int id)
    {
        var e = EnemyById(id);
        return e is { Alive: true, Windup: > 0 } ? 1f - e.Windup / (float)Math.Max(1, e.WindupDuration) : null;
    }
    public bool Dashing => LastCast == "barb2_investida" && Tick - LastCastTick < 12;

    /// <summary>The skill in one of the five slots, tuned by its rank and options (0 = the attack button).</summary>
    public PyDict? Slot(int index)
    {
        if (index == 0)
        {
            var id = Hero.BasicSkillId;
            var row = HeroTables.BasicRow(id) ?? HeroTables.SkillRow(id);
            return row == null ? null : TreeRules.Resolve(Hero.Tree, row);
        }
        return HeroCombat.EquippedSkill(Hero, index - 1);
    }

    /// <summary>Reach of a skill in tiles (catalog range, tuned), for the effects that show it.</summary>
    public double RangeOf(string skillId)
    {
        var row = HeroTables.SkillRow(skillId) ?? HeroTables.BasicRow(skillId);
        return row == null ? 1 : TreeRules.Resolve(Hero.Tree, row).Num("range", 1);
    }
    /// <summary>Who stands next to the hero (UMBRA melee reach: one tile, diagonals included).</summary>
    public bool Adjacent(EnemyActor e) => Math.Max(Math.Abs(e.X - Hero.X), Math.Abs(e.Y - Hero.Y)) <= 1;

    // ---------------------------------------------------------------- presentation events
    private (int Health, Dictionary<EnemyActor, (int Health, int Windup)> Enemies, int Casts, CombatResult? Last, bool Alive) Snapshot() =>
        (Hero.Health, Enemies.ToDictionary(e => e, e => (e.Health, e.Windup), ReferenceEqualityComparer.Instance as IEqualityComparer<EnemyActor>),
         Hero.Skills.Casts, Hero.LastResult, Hero.Alive);

    private void Report((int Health, Dictionary<EnemyActor, (int Health, int Windup)> Enemies, int Casts, CombatResult? Last, bool Alive) before)
    {
        foreach (var enemy in Enemies)
        {
            if (!before.Enemies.TryGetValue(enemy, out var was)) continue;
            if (enemy.Health < was.Health) Events.Add(new DamageEvent(IdOf(enemy), was.Health - enemy.Health, ToWorld(enemy.Position), false));
            if (was.Health > 0 && !enemy.Alive) Events.Add(new DeathEvent(IdOf(enemy), ToWorld(enemy.Position)));
            if (was.Windup == 0 && enemy.Windup > 0) Events.Add(new WindupEvent(IdOf(enemy), enemy.Windup));
        }
        if (Hero.Health < before.Health) Events.Add(new DamageEvent(HeroId, before.Health - Hero.Health, HeroWorld, true));
        if (before.Alive && !Hero.Alive) Events.Add(new HeroDownEvent());
        var aim = new Vector2(Hero.AttackDirection.X, Hero.AttackDirection.Y);
        if (aim.LengthSquared() > 0) aim = Vector2.Normalize(aim);
        if (!ReferenceEquals(Hero.LastResult, before.Last) && Hero.LastResult?.Reason == "windup")
            Events.Add(new CastEvent(Hero.BasicOverride ?? Hero.BasicSkillId ?? "", "basic", HeroWorld, aim));
        if (Hero.Skills.Casts != before.Casts)
            Events.Add(new CastEvent(Hero.Skills.LastSkillId ?? "", Hero.Skills.LastSkillAnim ?? "", HeroWorld, aim));
        foreach (var e in Events.OfType<CastEvent>()) { LastCast = e.SkillId; LastCastTick = Tick; }
        if (Hero.Alive && Hero.AttackDirection != (0, 0)) Facing = aim;
    }
}
