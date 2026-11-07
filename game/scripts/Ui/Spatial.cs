using Godot;

namespace Umbra.Game.Ui;

/// <summary>Directional focus between free-placed elements (paper doll, bag, tree nodes): the nearest element in the
/// pressed direction, preferring ones straight ahead.</summary>
public static class Spatial
{
    public static int Next(IReadOnlyList<Vector2> centers, int from, Nav nav)
    {
        if (from < 0 || from >= centers.Count) return centers.Count > 0 ? 0 : -1;
        var dir = nav switch { Nav.Up => Vector2.Up, Nav.Down => Vector2.Down, Nav.Left => Vector2.Left, _ => Vector2.Right };
        int best = from; float bestScore = float.MaxValue;
        for (int i = 0; i < centers.Count; i++)
        {
            if (i == from) continue;
            var d = centers[i] - centers[from];
            float along = d.Dot(dir);
            if (along <= 4) continue;
            float across = Mathf.Abs(d.Dot(new Vector2(-dir.Y, dir.X)));
            float score = along + across * 2.2f;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        return best;
    }
}
