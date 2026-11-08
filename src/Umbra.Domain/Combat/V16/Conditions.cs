using Umbra.Domain.Combat;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Combat.V16;

/// <summary>
/// Conditions of riders (`when`) and conditional damage bonuses (`bonus`) — port of game/combat_system/v16/conditions.py.
/// debilitated, controlled, status:&lt;kind&gt;, low:/healthy:&lt;fraction&gt;, isolated, behind, front, far:/near:&lt;tiles&gt;,
/// resource&gt;:/resource&lt;:&lt;n&gt;, life&gt;:/life&lt;:&lt;fraction&gt;, alone:&lt;tiles&gt;, shield, stacks:&lt;n&gt;, last, any:&lt;a&gt;,&lt;b&gt;.
/// The Necromancer's own conditions (boss, crit, fortified) arrive through ISkillHooks.NecroCondition.
/// </summary>
public static class Conditions
{
    public static readonly string[] Control = { "stun", "root", "slow", "blind" };

    public static bool Controlled(ITarget enemy) =>
        Control.Any(k => Status.Has(enemy, k)) || enemy.NecroFrozenTicks > 0 || enemy.NecroSlowTicks > 0 || enemy.NecroPrisonTicks > 0;

    public static bool Check(Ctx ctx, ITarget? enemy, string when)
    {
        int colon = when.IndexOf(':');
        string kind = colon < 0 ? when : when[..colon], arg = colon < 0 ? "" : when[(colon + 1)..];
        var p = ctx.Player;
        switch (kind)
        {
            case "debilitated": return enemy != null && (Controlled(enemy) || Status.Dot.Keys.Any(k => Status.Has(enemy, k)));
            case "controlled": return enemy != null && Controlled(enemy);
            case "status": return enemy != null && Status.Has(enemy, arg);
            case "low": return enemy != null && enemy.Health <= enemy.MaxHealth * Py.Float(arg);
            case "healthy": return enemy != null && enemy.Health >= enemy.MaxHealth * Py.Float(arg);
            case "isolated": return enemy != null && ctx.Near(enemy.Position, 2, enemy).Count == 0;
            case "behind": return enemy != null && Geo.Behind(enemy, p);
            case "far": return enemy != null && Geo.Steps(enemy.Position, p.Position) >= Py.Float(arg);
            case "near": return enemy != null && Geo.Steps(enemy.Position, p.Position) <= Py.Float(arg);
        }
        int resource = ctx.State.ResourceBefore ?? p.Resource;            // Ira Ancestral reads the Fúria before the cost
        switch (kind)
        {
            case "resource>": return resource > Py.Float(arg);
            case "resource<": return resource < Py.Float(arg);
            case "life>": return p.Health > p.MaxHealth * Py.Float(arg);
            case "life<": return p.Health < p.MaxHealth * Py.Float(arg);
            case "alone": return ctx.Near(p.Position, Py.Float(arg)).Count == 1;
            case "shield": return p.HasShield;
            case "stacks": return Stacks(p, ctx.Spec.Str("id")) >= int.Parse(arg);
            case "last": return ctx.State.Last;
            case "front": return enemy != null && !Geo.Behind(enemy, p);
            case "any": return enemy != null && arg.Split(',').Any(k => Status.Has(enemy, k));
        }
        var found = SkillEnv.Hooks.NecroCondition(ctx, enemy, when);
        if (found != null) return found.Value;
        throw new ArgumentException("Condição desconhecida: " + when);
    }

    /// <summary>Current stacks of a skill's stacking buff (Frenesi).</summary>
    public static int Stacks(ISkillUser p, string skillId) =>
        p.Skills.Stacks.TryGetValue(skillId, out var row) && row.Ticks > 0 ? row.Count : 0;

    /// <summary>Product of the conditional bonuses of the resolved skill that hold for this target.</summary>
    public static double Factor(Ctx ctx, ITarget? enemy)
    {
        double value = 1;
        foreach (var row in ctx.Spec.Rows("bonus"))
            if (Check(ctx, enemy, row.Str("when"))) value *= row.Num("factor");
        return value;
    }
}
