using System.Numerics;

namespace Umbra.Domain.Gameplay;

/// <summary>What happened in one tick, for the presentation layer (sounds, flashes, numbers). Rules never read these.</summary>
public abstract record GameEvent;
public sealed record HitEvent(int TargetId, int Amount, Vector2 Position, bool OnHero) : GameEvent;
public sealed record DiedEvent(int ActorId, Vector2 Position) : GameEvent;
public sealed record TelegraphEvent(int EnemyId, int Ticks) : GameEvent;
public sealed record StrikeEvent(int EnemyId, bool Landed) : GameEvent;
public sealed record SkillEvent(string SkillId, Vector2 Position, Vector2 Direction) : GameEvent;
public sealed record DroppedEvent(Drop Drop) : GameEvent;
public sealed record PickedUpEvent(Drop Drop) : GameEvent;
public sealed record HeroDiedEvent : GameEvent;

/// <summary>Player intent for one tick, already converted to world units by the input layer.</summary>
public readonly record struct InputFrame(Vector2 Move, Vector2? Destination, Vector2 Aim, bool Attack, bool Skill1, bool Skill2)
{
    public static InputFrame None(Vector2 aim) => new(Vector2.Zero, null, aim, false, false, false);
}
