using System.Numerics;
using Umbra.Domain.Gameplay;
using Umbra.Domain.World;

namespace Umbra.BetaTester;

/// <summary>
/// Plays like a person of a given <see cref="Persona"/>: it only sees what is on screen (positions, the enemy wind-up,
/// cooldowns, Fúria) and only answers with an <see cref="InputFrame"/>, the same thing the controls send. Reaction time,
/// aim error and habits come from the persona; randomness comes from its own seeded generator, so a run replays exactly.
/// </summary>
public sealed class BotPlayer
{
    /// <summary>What a player reads from the strike animation: the enemy's reach plus the slack of the swing (tiles).</summary>
    public const float ThreatMargin = .55f + .35f + .25f;

    private readonly Persona _p;
    private readonly Random _rng;
    private InputFrame _held;
    private int _nextThink;
    private int _dodgeUntil = -1;
    private Vector2 _dodgeDir;
    private readonly HashSet<(int Enemy, int Start)> _judged = new();

    /// <summary>Dodges started, and dodges the hero wanted but could not start (still recovering from its own swing).</summary>
    public int DodgesStarted { get; private set; }
    public int DodgesLocked { get; private set; }

    public BotPlayer(Persona persona, int seed) { _p = persona; _rng = new Random(seed); }

    public InputFrame Next(Simulation sim)
    {
        var hero = sim.Hero;
        if (sim.Tick < _dodgeUntil) return new InputFrame(_dodgeDir, null, hero.Position + _dodgeDir, false, false, false);
        if (Threat(sim) is { } threat) return threat;
        if (sim.Tick < _nextThink) return _held;
        _nextThink = sim.Tick + _p.ThinkTicks;
        _held = _rng.NextDouble() < _p.Chaos ? Random(hero) : Decide(sim);
        return _held;
    }

    /// <summary>Steps out of a telegraphed strike once the persona has had time to react.</summary>
    private InputFrame? Threat(Simulation sim)
    {
        if (_p.DodgeChance <= 0) return null;
        var hero = sim.Hero;
        foreach (var enemy in sim.Enemies)
        {
            if (!enemy.Alive || enemy.State != AiState.Windup) continue;
            int elapsed = SliceData.BruteWindupTicks - enemy.StateTicks;
            int start = sim.Tick - elapsed;
            if (elapsed < _p.ReactionTicks || !_judged.Add((enemy.Id, start))) continue;
            float reach = enemy.Radius + hero.Radius + ThreatMargin;
            var away = hero.Position - enemy.Position;
            if (away.Length() > reach || _rng.NextDouble() >= _p.DodgeChance) continue;
            // Like a person: let go of the attack and push away. The step starts as soon as the hero's own recovery ends;
            // it counts as locked when that recovery outlasts the wind-up (the strike lands before the hero can move).
            int busy = Math.Max(hero.Recovery, hero.DashTicks);
            if (busy >= enemy.StateTicks) DodgesLocked++; else DodgesStarted++;
            var dir = away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : -hero.Facing;
            // Back away, bending toward the side with room (a wall behind would cancel the step).
            var side = new Vector2(-dir.Y, dir.X) * (_rng.Next(2) == 0 ? 1 : -1);
            _dodgeDir = Vector2.Normalize(Open(sim, hero.Position, dir) ? dir + side * .4f : side);
            _dodgeUntil = sim.Tick + enemy.StateTicks + 2;
            return new InputFrame(_dodgeDir, null, hero.Position + _dodgeDir, false, false, false);
        }
        return null;
    }

    private static bool Open(Simulation sim, Vector2 from, Vector2 dir) => !Collision.Blocked(sim.Map, from + dir * 1.2f, sim.Hero.Radius);

    private InputFrame Decide(Simulation sim)
    {
        var hero = sim.Hero;
        var target = sim.Enemies.Where(e => e.Alive).OrderBy(e => Vector2.Distance(e.Position, hero.Position)).FirstOrDefault();
        if (target == null)
        {
            var drop = sim.Drops.OrderBy(d => Vector2.Distance(d.Position, hero.Position)).FirstOrDefault();
            return drop == null || sim.BagFull ? InputFrame.None(hero.Position + hero.Facing)
                                               : new InputFrame(Vector2.Zero, drop.Position, drop.Position, false, false, false);
        }
        var toward = Rotate(Vector2.Normalize(target.Position - hero.Position), (float)(_rng.NextDouble() * 2 - 1) * _p.AimErrorDegrees);
        var aim = hero.Position + toward * Vector2.Distance(hero.Position, target.Position);
        float distance = Vector2.Distance(hero.Position, target.Position);
        if (sim.InArc(target, toward, SliceData.GolpeBrutal.Range, SliceData.GolpeBrutal.ArcDegrees))
        {
            int group = sim.Enemies.Count(e => e.Alive && sim.InArc(e, toward, SliceData.Ruptura.Range, SliceData.Ruptura.ArcDegrees));
            bool cone = _p.ConeGroup > 0 && hero.Fury >= SliceData.Ruptura.Cost && group >= _p.ConeGroup;
            return new InputFrame(Vector2.Zero, null, aim, !cone, cone, false);
        }
        bool dash = _p.UsesDash && hero.Cooldown(SliceData.Investida.Id) == 0 && distance is > 2.5f and < 4.5f
                    && Collision.LineOfSight(sim.Map, hero.Position, target.Position);
        var walk = Vector2.Normalize(target.Position - hero.Position);
        return new InputFrame(dash ? Vector2.Zero : walk, null, aim, false, false, dash);
    }

    private InputFrame Random(Hero hero)
    {
        float angle = (float)(_rng.NextDouble() * Math.PI * 2);
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        return new InputFrame(_rng.Next(3) == 0 ? Vector2.Zero : dir, null, hero.Position + dir * 2,
                              _rng.Next(3) == 0, _rng.Next(4) == 0, _rng.Next(6) == 0);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * MathF.PI / 180f, c = MathF.Cos(r), s = MathF.Sin(r);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }
}
