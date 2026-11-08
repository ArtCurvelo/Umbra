using Umbra.Domain.Combat;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Combat.V16;

/// <summary>A zone on the ground (turrets and mines included). Never saved: everything here lasts seconds.</summary>
public sealed class Zone
{
    public required Ctx Ctx;
    public required string SkillId;
    public (int X, int Y) Center;
    public double Radius, Dps, Heal, ResourcePs;
    public int Ticks, Timer, ShotTimer, Interval;
    public string? Element;
    public List<PyDict> States = new();
    public bool Follows, Skill, Wander;
    public PyDict? Trigger, Turret;
    public object? MaxTargets;
    public HashSet<ITarget> Touched = new(ReferenceEqualityComparer.Instance);
    public List<object?> SelfStatesRaw = new();
}

public sealed class SkillEvent
{
    public required string Kind;            // echo, blast, recall
    public required Ctx Ctx;
    public int Ticks;
    public double Factor, Heal;
    public List<ITarget>? Targets;
    public (int X, int Y) Center, Origin;
}

public sealed class SkillEffect { public required Ctx Ctx; public int Ticks; public double Absorbed; }
public sealed class AuraRow { public required Ctx Ctx; public int Timer, UpkeepTimer; }
public sealed class ChannelRow { public required Ctx Ctx; public int Timer, UpkeepTimer; public required string Action; }

/// <summary>Everything a skill leaves running on one hero (player.v16_world).</summary>
public sealed class SkillWorld
{
    public const int Pulse = 30, MaxZones = 16, MaxEvents = 32;

    public List<Zone> Zones { get; } = new();
    public List<SkillEvent> Events { get; } = new();
    public List<SkillEffect> Effects { get; } = new();
    /// <summary>Insertion-ordered, like the Python dict (switching an aura off and on moves it to the end).</summary>
    public List<(string Id, AuraRow Row)> Auras { get; } = new();
    public ChannelRow? Channel;
    public Dictionary<string, (int Max, int Cooldown)> Charges { get; } = new();
    public int TrailTimer;

    public static SkillWorld State(ISkillUser p) => p.Skills.World ??= new SkillWorld();

    // ------------------------------------------------------------------ registration (called by shapes and riders)
    public static void AddZone(Ctx ctx, (int X, int Y) center, PyDict rider)
    {
        var zones = State(ctx.Player).Zones; string skillId = ctx.Spec.Str("id");
        double radius = rider.Num("radius", 1); int ticks = (int)PyMath.Round(rider.Num("seconds", 3) * 60);
        if (rider.T("bloom"))                                            // Floração: a nearby cloud of the same skill grows
            foreach (var old in zones)
                if (old.SkillId == skillId && Geo.Steps(old.Center, center) <= old.Radius + 2) { old.Radius += 1; old.Ticks += 120; return; }
        if (rider.T("max_active"))
        {
            var mine = zones.Where(z => z.SkillId == skillId).ToList();
            foreach (var old in mine.Take(Math.Max(0, mine.Count - Py.Int(rider.Num("max_active")) + 1))) zones.Remove(old);
        }
        var turret = rider.Sub("turret");
        double interval = rider.T("turret_interval") ? rider.Num("turret_interval") : turret?.Num("interval", 1) ?? 1;
        zones.Add(new Zone
        {
            Ctx = ctx, SkillId = skillId, Center = center, Radius = radius, Ticks = ticks, Timer = Pulse,
            Dps = rider.Num("dps", 0), Element = rider.StrOrNull("element") is { Length: > 0 } e ? e : ctx.Spec.StrOrNull("element"),
            States = rider.Rows("states").ToList(), Heal = rider.Num("heal", 0), Follows = rider.T("follows"),
            Trigger = rider.Sub("trigger"), Skill = rider.T("skill_riders"), Turret = turret, ShotTimer = 0,
            Interval = (int)PyMath.Round(60 * interval), SelfStatesRaw = rider.List("self_states"),
            ResourcePs = rider.Num("resource_ps", 0), MaxTargets = rider.Get("max_targets"), Wander = rider.T("wander"),
        });
        if (zones.Count > MaxZones) zones.RemoveRange(0, zones.Count - MaxZones);
    }

    public static void Later(Ctx ctx, double delay, double factor, List<ITarget> targets)
    {
        var events = State(ctx.Player).Events;
        events.Add(new SkillEvent { Kind = "echo", Ctx = ctx, Ticks = (int)PyMath.Round(delay * 60), Factor = factor, Targets = targets });
        if (events.Count > MaxEvents) events.RemoveRange(0, events.Count - MaxEvents);
    }

    public static void LaterBlast(Ctx ctx, (int X, int Y) center, double delay)
    {
        var events = State(ctx.Player).Events;
        events.Add(new SkillEvent { Kind = "blast", Ctx = ctx, Ticks = (int)PyMath.Round(delay * 60), Center = center });
        if (events.Count > MaxEvents) events.RemoveRange(0, events.Count - MaxEvents);
    }

    public static void Recall(Ctx ctx, double delay, double heal) =>
        State(ctx.Player).Events.Add(new SkillEvent { Kind = "recall", Ctx = ctx, Ticks = (int)PyMath.Round(delay * 60), Origin = ctx.Player.Position, Heal = heal });

    public static void TrackEffect(Ctx ctx, double seconds)
    {
        if (seconds == 0 || !ctx.Spec.Rows("riders").Any(r => r.StrOrNull("on") == "end")) return;
        State(ctx.Player).Effects.Add(new SkillEffect { Ctx = ctx, Ticks = (int)PyMath.Round(seconds * 60), Absorbed = ctx.Player.AbsorbedTotal });
    }

    public static bool AuraActive(ISkillUser p, string skillId) => State(p).Auras.Any(a => a.Id == skillId);

    public static void StartAura(Ctx ctx)
    {
        var auras = State(ctx.Player).Auras; string id = ctx.Spec.Str("id"); var row = new AuraRow { Ctx = ctx, Timer = Pulse, UpkeepTimer = 60 };
        int index = auras.FindIndex(a => a.Id == id);
        if (index >= 0) auras[index] = (id, row); else auras.Add((id, row));
        if (ctx.Spec.T("resource_penalty")) ctx.Player.RecalculateStats();
    }

    public static void StopAura(ISkillUser p, string skillId)
    {
        var auras = State(p).Auras; int index = auras.FindIndex(a => a.Id == skillId);
        if (index < 0) return;
        var row = auras[index].Row; auras.RemoveAt(index);
        if (row.Ctx.Spec.T("resource_penalty")) p.RecalculateStats();
    }

    public static double ResourcePenalty(ISkillUser p) => p.Skills.World?.Auras.Sum(a => a.Row.Ctx.Spec.Num("resource_penalty", 0)) ?? 0;

    public static void StartChannel(Ctx ctx)
    {
        var world = State(ctx.Player); int slot = ctx.Player.ActiveMainSlot;
        string action = ctx.Spec.Str("category") == "Básica" ? "attack" : $"skill_{slot + 1}";
        world.Channel = new ChannelRow { Ctx = ctx, Timer = 0, UpkeepTimer = 1, Action = action };
        ctx.Player.Skills.Hold = true;
    }

    public static void StopChannel(ISkillUser p) { State(p).Channel = null; p.Skills.Hold = false; }

    public static PyDict? Channeling(ISkillUser p) => p.Skills.World?.Channel?.Ctx.Spec;

    public static void RegisterCharges(ISkillUser p, PyDict spec)
    {
        if (spec.Num("charges") > 1) State(p).Charges[spec.Str("id")] = (Py.Int(spec.Num("charges")), Py.Int(spec.Num("cooldown")));
    }
}

/// <summary>What the world tick needs from the engine: player 1, the enemies of the map and the map.</summary>
public sealed record SkillScene(ISkillUser Player, IReadOnlyList<ITarget> Enemies, IMap? Map);
