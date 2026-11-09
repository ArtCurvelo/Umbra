using System.Numerics;

namespace Umbra.Domain.Play;

/// <summary>Player intent for one tick, already converted to world units by the input layer (Godot, the autopilots and
/// the beta tester all speak this).</summary>
public readonly record struct InputFrame(Vector2 Move, Vector2? Destination, Vector2 Aim, bool Attack, bool Skill1, bool Skill2,
                                         bool Potion = false)
{
    public static InputFrame None(Vector2 aim) => new(Vector2.Zero, null, aim, false, false, false);
}
