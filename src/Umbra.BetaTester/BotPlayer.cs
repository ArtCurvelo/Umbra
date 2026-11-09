using System.Numerics;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Enemies;
using Umbra.Domain.Play;
using EnemyActor = Umbra.Domain.Enemies.Enemy;

namespace Umbra.BetaTester;

/// <summary>
/// Plays like a person of a given <see cref="Persona"/> on UMBRA's real rules (step 5.8c): it only reads what the screen
/// shows (positions, an enemy's wind-up and the cell it is aimed at, cooldowns, Fúria) and only answers with an
/// <see cref="InputFrame"/>, the same thing the controls send. Reaction time, aim error and habits come from the persona;
/// randomness comes from its own seeded generator, so a run replays exactly. Skill buttons are pressed for one tick
/// (the game fires a skill on the press).
/// </summary>
public sealed class BotPlayer
{
    private readonly Persona _p;
    private readonly Random _rng;
    private InputFrame _held;
    private int _nextThink;
    private int _dodgeUntil = -1, _nextPotion;
    private Vector2 _dodgeDir;
    private readonly HashSet<(EnemyActor Enemy, int Start)> _judged = new();
    private readonly HashSet<EnemyActor> _dodging = new();
    /// <summary>Items the hero stood on that stayed on the ground (e.g. potions at the cap): not worth walking to again.</summary>
    public HashSet<int> Skipped { get; } = new();

    /// <summary>Dodges tried, and those that came too late (the strike still landed).</summary>
    public int DodgesStarted { get; private set; }
    public int DodgesLocked { get; private set; }

    public BotPlayer(Persona persona, int seed) { _p = persona; _rng = new Random(seed); }

    /// <summary>What the bot learns after a tick: whether a strike it tried to dodge landed anyway.</summary>
    public void Observe(GameSession s)
    {
        foreach (var e in s.Events.OfType<StrikeEvent>())
        {
            var enemy = s.EnemyById(e.EnemyId);
            if (enemy != null && _dodging.Remove(enemy) && e.Landed) DodgesLocked++;
        }
        // the boss's blows and abilities: the threat is over; it was late if the hero was hurt as it resolved
        bool hurt = s.Events.Any(e => e is DamageEvent { OnHero: true });
        foreach (var boss in _dodging.Where(e => e.Boss && s.Threat(e) == null).ToList())
        {
            _dodging.Remove(boss);
            if (hurt) DodgesLocked++;
        }
        foreach (var item in s.Items) if (item.Cell == s.Hero.Position) Skipped.Add(item.Id);
    }

    public InputFrame Next(GameSession s)
    {
        var here = s.HeroWorld;
        if (_p.PotionAt > 0 && s.Hero.Alive && s.Hero.Inventory.Potion > 0 && s.Hero.Health < s.Hero.MaxHealth * _p.PotionAt && s.Tick >= _nextPotion)
        {
            _nextPotion = s.Tick + 30;                       // one press, then a moment before the next decision to drink
            return InputFrame.None(here + s.Facing) with { Potion = true };
        }
        if (s.Tick < _dodgeUntil) return new InputFrame(_dodgeDir, null, here + _dodgeDir, false, false, false);
        if (Threat(s) is { } threat) return threat;
        if (s.Tick < _nextThink) return _held with { Skill1 = false, Skill2 = false };
        _nextThink = s.Tick + _p.ThinkTicks;
        _held = _rng.NextDouble() < _p.Chaos ? Random(here) : Decide(s);
        return _held;
    }

    /// <summary>Steps out of the cells an attack in preparation will hit (the red tiles: an enemy's marked cell, the boss's
    /// blow or ability area), once the persona has had time to react.</summary>
    private InputFrame? Threat(GameSession s)
    {
        if (_p.DodgeChance <= 0) return null;
        var hero = s.Hero; var here = s.HeroWorld;
        foreach (var enemy in s.Enemies)
        {
            if (s.Threat(enemy) is not { } t || !t.Cells.Contains(hero.Position)) continue;
            int elapsed = t.Elapsed;
            int start = s.Tick - elapsed;
            if (elapsed < _p.ReactionTicks || !_judged.Add((enemy, start))) continue;
            if (_rng.NextDouble() >= _p.DodgeChance) continue;
            DodgesStarted++; _dodging.Add(enemy);
            var centre = t.Cells.Count > 1 ? new Vector2((float)t.Cells.Average(c => c.X) + .5f, (float)t.Cells.Average(c => c.Y) + .5f) : GameSession.ToWorld(enemy.Position);
            var away = here - centre;
            if (away.LengthSquared() < 1e-4f) away = here - GameSession.ToWorld(enemy.Position);
            var dir = away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : -s.Facing;
            // Back away, bending toward the side with room (a wall behind keeps the hero on the marked cell).
            var side = new Vector2(-dir.Y, dir.X) * (_rng.Next(2) == 0 ? 1 : -1);
            _dodgeDir = Vector2.Normalize(Open(s, here, dir) ? dir + side * .4f : Open(s, here, side) ? side : -side);
            _dodgeUntil = s.Tick + t.Left + 2;
            return new InputFrame(_dodgeDir, null, here + _dodgeDir, false, false, false);
        }
        return null;
    }

    private static bool Open(GameSession s, Vector2 from, Vector2 dir)
    {
        var (x, y) = GameSession.ToCell(from + dir * 1.1f);
        return s.Grid.Cell(x, y) == 0 && s.Hero.CanEnter(x, y, s.Grid);
    }

    private static int Steps(EnemyActor e, (int X, int Y) at) => Math.Max(Math.Abs(e.X - at.X), Math.Abs(e.Y - at.Y));

    private InputFrame Decide(GameSession s)
    {
        var hero = s.Hero; var here = s.HeroWorld;
        var target = s.Enemies.Where(e => e.Alive).OrderBy(e => Vector2.Distance(GameSession.ToWorld(e.Position), here)).FirstOrDefault();
        if (target == null)
        {
            var item = s.Items.Where(i => !Skipped.Contains(i.Id)).OrderBy(i => Vector2.Distance(GameSession.ToWorld(i.Cell), here)).FirstOrDefault();
            if (item == null) return InputFrame.None(here + s.Facing);
            return new InputFrame(Walk(s, item.Cell), null, here, false, false, false);
        }
        var at = GameSession.ToWorld(target.Position);
        float distance = Vector2.Distance(here, at);
        var toward = Rotate(Vector2.Normalize(at - here + new Vector2(1e-4f, 0)), (float)(_rng.NextDouble() * 2 - 1) * _p.AimErrorDegrees);
        var aim = here + toward * distance;
        if (s.Adjacent(target))
        {
            var cone = s.Slot(1);
            int group = s.Enemies.Count(e => e.Alive && InCone(hero.Position, e, toward, s.RangeOf(Facts.Ruptura)));
            bool spend = _p.ConeGroup > 0 && cone != null && hero.Resource >= cone.Num("cost", 0)
                         && hero.Skills.Cooldowns.GetValueOrDefault(Facts.Ruptura) == 0 && group >= _p.ConeGroup;
            return new InputFrame(Vector2.Zero, null, aim, !spend, spend, false);
        }
        bool dash = _p.UsesDash && hero.Skills.Cooldowns.GetValueOrDefault(Facts.Investida) == 0 && Steps(target, hero.Position) is >= 2 and <= 4
                    && Geo.LineOfSight(s.Grid, hero.Position, target.Position);
        return new InputFrame(dash ? Vector2.Zero : Walk(s, target.Position), null, aim, false, false, dash);
    }

    /// <summary>Like a player steering: toward the centre of the next cell on UMBRA's own path (Navigation.shared_step,
    /// around the other enemies), not in a straight line that catches on corners.</summary>
    private static Vector2 Walk(GameSession s, (int X, int Y) goal)
    {
        var here = s.HeroWorld;
        var bodies = s.Enemies.Where(e => e.Alive && e.Position != goal).Select(e => (e.X, e.Y));
        var next = Navigation.SharedStep(s.Grid, s.Hero.Position, goal, bodies) ?? goal;
        var to = GameSession.ToWorld(next) - here;
        return to.LengthSquared() > 1e-4f ? Vector2.Normalize(to) : Vector2.Zero;
    }

    /// <summary>The bot's eye for Ruptura's cone: within reach (tiles) and 45° of the aim.</summary>
    private static bool InCone((int X, int Y) from, EnemyActor e, Vector2 dir, double range)
    {
        var to = new Vector2(e.X - from.X, e.Y - from.Y);
        if (to.LengthSquared() < 1e-4f || Steps(e, from) > range) return false;
        return Vector2.Dot(Vector2.Normalize(to), dir) >= MathF.Cos(MathF.PI / 4) - 1e-4f;
    }

    private InputFrame Random(Vector2 here)
    {
        float angle = (float)(_rng.NextDouble() * Math.PI * 2);
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        return new InputFrame(_rng.Next(3) == 0 ? Vector2.Zero : dir, null, here + dir * 2,
                              _rng.Next(3) == 0, _rng.Next(4) == 0, _rng.Next(6) == 0);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * MathF.PI / 180f, c = MathF.Cos(r), s = MathF.Sin(r);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }
}
