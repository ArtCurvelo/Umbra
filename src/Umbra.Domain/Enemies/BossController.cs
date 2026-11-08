using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;

namespace Umbra.Domain.Enemies;

/// <summary>
/// Boss abilities, phases, telegraphs and encounter state — port of game/combat_system/bosses.BossController (drawing is
/// presentation). Phase II at half life; every ability is announced and has a wind-up before it lands; offensive
/// abilities are blocked by walls; the Colosso starts with bone armor.
/// </summary>
public sealed class BossController
{
    public readonly Enemy Enemy;
    public readonly int Floor;
    public readonly PyDict Profile;
    public int Phase = 1;
    public bool PhaseTwoTriggered;
    /// <summary>Insertion-ordered, like the Python dict.</summary>
    public List<KeyValuePair<string, int>> AbilityCooldowns { get; } = new();
    public int ActionTimer = 45, MessageTimer;
    public string Message = "";
    public bool SummonRequested, TeleportRequested;
    public string? PendingAbility, LastAbility;
    public int PendingTimer, PendingTotal;
    public (int X, int Y)? PendingTarget, LastBasicTarget, BasicTarget;
    public (int X, int Y) PendingDirection = (0, 1);
    public int BasicTimer, BasicTotal, BoneArmor;
    public List<((int X, int Y) Position, int Timer)> Zones { get; } = new();

    private static readonly Dictionary<string, int> Windups = new()
    {
        ["investida"] = 16, ["impacto"] = 20, ["onda_cinzas"] = 22, ["combo"] = 14, ["corte_sanguineo"] = 19, ["drenagem"] = 20,
        ["projetil_vazio"] = 18, ["zona_corrupcao"] = 20, ["teleporte"] = 17, ["pisao"] = 24, ["invocar_esqueletos"] = 22, ["armadura_ossea"] = 20,
    };
    private static readonly HashSet<string> Offensive = new()
        { "investida", "impacto", "onda_cinzas", "combo", "corte_sanguineo", "drenagem", "projetil_vazio", "zona_corrupcao", "pisao" };

    public BossController(Enemy enemy, int floor)
    {
        Enemy = enemy; Floor = floor; Profile = FoeTables.BossProfile(floor);
        BoneArmor = Name.ToLowerInvariant().Contains("colosso") ? 45 : 0;
    }

    public string Name => Profile.Str("name");
    public List<string> Abilities => Profile.List("abilities").Cast<string>().ToList();

    public void Announce(string message, int duration = 120) { Message = message; MessageTimer = duration; }

    public void CheckPhase()
    {
        if (PhaseTwoTriggered) return;
        if (Enemy.Health <= Enemy.MaxHealth * 0.5)
        {
            Phase = 2; PhaseTwoTriggered = true;
            if (BoneArmor != 0) BoneArmor += 45;
            Announce("FASE II — " + Name.ToUpperInvariant(), 180);
        }
    }

    public void TickCooldowns()
    {
        for (int i = 0; i < AbilityCooldowns.Count; i++) AbilityCooldowns[i] = new(AbilityCooldowns[i].Key, AbilityCooldowns[i].Value - 1);
        AbilityCooldowns.RemoveAll(kv => kv.Value <= 0);
        if (MessageTimer > 0) MessageTimer--;
        for (int i = 0; i < Zones.Count; i++) Zones[i] = (Zones[i].Position, Zones[i].Timer - 1);
        Zones.RemoveAll(z => z.Timer <= 0);
    }

    public bool CanUse(string ability) => Abilities.Contains(ability) && AbilityCooldowns.All(kv => kv.Key != ability);

    public bool Use(string ability, int cooldown)
    {
        if (!CanUse(ability)) return false;
        AbilityCooldowns.Add(new(ability, cooldown)); return true;
    }

    public int AbsorbDamage(int damage)
    {
        damage = Math.Max(0, damage);
        if (BoneArmor <= 0) return damage;
        int absorbed = Math.Min(BoneArmor, damage); BoneArmor -= absorbed;
        return damage - absorbed;
    }

    public int Distance(IFoeTarget p) => Math.Abs(p.X - Enemy.X) + Math.Abs(p.Y - Enemy.Y);

    public string? ChooseAbility(int distance, IRandom rng)
    {
        var available = Abilities.Where(CanUse).ToList();
        if (available.Count == 0) return null;
        string name = Name; List<string> preferred;
        if (name.Contains("Guardião")) preferred = Phase == 2 ? new() { "onda_cinzas", "impacto", "investida" } : new() { "impacto", "investida", "onda_cinzas" };
        else if (name.Contains("Cavaleiro")) preferred = Phase == 2 ? new() { "drenagem", "corte_sanguineo", "combo" } : new() { "combo", "corte_sanguineo", "drenagem" };
        else if (name.Contains("Oráculo")) preferred = Phase == 2 ? new() { "zona_corrupcao", "projetil_vazio", "teleporte" } : new() { "projetil_vazio", "teleporte", "zona_corrupcao" };
        else if (name.Contains("Rainha")) preferred = new() { "zona_corrupcao", "teleporte", "projetil_vazio" };
        else if (name.Contains("Devorador")) preferred = new() { "drenagem", "combo", "pisao" };
        else if (name.Contains("Dragão")) preferred = new() { "onda_cinzas", "investida", "armadura_ossea" };
        else if (name.Contains("Rei Sem Sol")) preferred = new() { "corte_sanguineo", "zona_corrupcao", "pisao" };
        else preferred = Phase == 2 ? new() { "invocar_esqueletos", "pisao", "armadura_ossea" } : new() { "pisao", "armadura_ossea", "invocar_esqueletos" };
        if (distance <= 2) preferred.Reverse();
        foreach (var ability in preferred) if (available.Contains(ability)) return ability;
        return rng.Choice(available);
    }

    public void DamagePlayer(IFoeTarget player, long amount)
    {
        if (player.Alive) Elements.Deal(player, amount, FoeTables.Element(Enemy), Enemy, "boss_ability");
    }

    public (int X, int Y) DirectionTo(IFoeTarget player)
    {
        int dx = player.X - Enemy.X, dy = player.Y - Enemy.Y;
        if (Math.Abs(dx) >= Math.Abs(dy)) return (dx > 0 ? 1 : -1, 0);
        return (0, dy > 0 ? 1 : -1);
    }

    public void MoveToward(IFoeTarget player, IMap map, int steps = 1, (int X, int Y)? target = null)
    {
        var goal = target ?? player.Position;
        for (int i = 0; i < Math.Max(1, steps); i++)
        {
            var occupied = EnemyAI.Active.Where(e => !ReferenceEquals(e, Enemy) && e.Alive).Select(e => e.Position).ToList();
            occupied.Add(player.Position);
            var step = Navigation.BfsStep(map, Enemy.Position, goal, occupied);
            if (step == null || step == player.Position) break;
            Enemy.PreviousPosition = Enemy.Position; (Enemy.X, Enemy.Y) = step.Value;
        }
    }

    public void Teleport(IFoeTarget player, IMap map, IRandom rng)
    {
        var occupied = EnemyAI.Active.Where(e => !ReferenceEquals(e, Enemy) && e.Alive).Select(e => e.Position).ToHashSet();
        var candidates = new List<(int X, int Y)>();
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var p = (x, y);
                if (map.Cell(x, y) != 0 || p == player.Position || occupied.Contains(p)) continue;
                if (Math.Abs(x - player.X) + Math.Abs(y - player.Y) < 3) continue;
                candidates.Add(p);
            }
        if (candidates.Count > 0) { Enemy.PreviousPosition = Enemy.Position; (Enemy.X, Enemy.Y) = rng.Choice(candidates); }
    }

    public void BeginAbility(string ability, IFoeTarget player)
    {
        PendingAbility = ability; PendingTimer = Windups.GetValueOrDefault(ability, 18); PendingTotal = PendingTimer;
        PendingTarget = player.Position; PendingDirection = DirectionTo(player);
        Announce("PREPARANDO: " + ability.Replace("_", " ").ToUpperInvariant(), PendingTimer + 12);
    }

    public void UpdateZones(IFoeTarget player)
    {
        foreach (var zone in Zones)
            if (player.Position == zone.Position && zone.Timer % 30 == 0) DamagePlayer(player, Math.Max(2, Enemy.Attack / 2));
    }

    public void ExecuteAbility(string ability, IFoeTarget player, IMap map, IRandom rng)
    {
        var marked = PendingTarget ?? player.Position;
        int markedDistance = Math.Abs(player.X - marked.X) + Math.Abs(player.Y - marked.Y);
        int attack = Enemy.Attack; double bonus = Phase == 2 ? 1.35 : 1.0; var direction = PendingDirection;
        bool InArea(int radius) => markedDistance <= radius && Geo.LineOfSight(map, marked, player.Position);
        switch (ability)
        {
            case "investida":
                MoveToward(player, map, 2, marked);
                if (markedDistance <= 1 && Distance(player) <= 1) DamagePlayer(player, (long)(attack * 1.5 * bonus));
                Announce("INVESTIDA!"); break;
            case "impacto":
                if (InArea(1)) DamagePlayer(player, (long)(attack * 1.35 * bonus));
                Announce("IMPACTO!"); break;
            case "onda_cinzas":
                if (InArea(2)) DamagePlayer(player, (long)(attack * bonus));
                Announce("ONDA DE CINZAS!"); break;
            case "combo":
                if (InArea(1)) for (int i = 0; i < (Phase == 1 ? 2 : 3); i++) DamagePlayer(player, (long)(attack * 0.65 * bonus));
                Announce("COMBO SANGRENTO!"); break;
            case "corte_sanguineo":
            {
                var (dx, dy) = direction;
                bool aligned = (dx == 0 && player.X == Enemy.X) || (dy == 0 && player.Y == Enemy.Y);
                if (aligned && InArea(1)) DamagePlayer(player, (long)(attack * 1.5 * bonus));
                Announce("CORTE SANGUÍNEO!"); break;
            }
            case "drenagem":
                if (InArea(1))
                {
                    long dealt = (long)(attack * 1.2 * bonus); int before = player.Health;
                    DamagePlayer(player, dealt);
                    int actual = Math.Max(0, before - player.Health);
                    Enemy.Health = Math.Min(Enemy.MaxHealth, Enemy.Health + (int)(actual * 0.5));
                }
                Announce("DRENAGEM!"); break;
            case "projetil_vazio":
                if (player.Position == marked && Geo.LineOfSight(map, Enemy.Position, marked)) DamagePlayer(player, (long)(attack * bonus));
                Announce("PROJÉTIL DO VAZIO!"); break;
            case "zona_corrupcao": Zones.Add((marked, 180)); Announce("ZONA DE CORRUPÇÃO!"); break;
            case "teleporte": Teleport(player, map, rng); Announce("DESLOCAMENTO DO VAZIO!"); break;
            case "pisao":
                if (InArea(1)) DamagePlayer(player, (long)(attack * 1.5 * bonus));
                Announce("PISÃO COLOSSAL!"); break;
            case "invocar_esqueletos": SummonRequested = true; Announce("OS RESTOS SE ERGUEM!"); break;
            case "armadura_ossea": BoneArmor = Math.Min(150, BoneArmor + 60); Announce("ARMADURA ÓSSEA!"); break;
        }
    }

    public void Update(IFoeTarget player, IMap map, IRandom rng)
    {
        if (!Enemy.Alive) return;
        TickCooldowns(); CheckPhase(); UpdateZones(player);
        if (!player.Alive) return;
        if (BasicTimer > 0)
        {
            BasicTimer--;
            if (BasicTimer <= 0)
            {
                var target = BasicTarget;
                LastBasicTarget = target;
                if (target is { } t && player.Position == t && Distance(player) <= 1 && Geo.LineOfSight(map, Enemy.Position, t))
                    DamagePlayer(player, Enemy.Attack);
                BasicTarget = null; ActionTimer = 45;
            }
            return;
        }
        if (PendingAbility != null)
        {
            PendingTimer--;
            if (PendingTimer <= 0)
            {
                var ability = PendingAbility; PendingAbility = null; PendingTimer = 0; LastAbility = ability;
                var marked = PendingTarget ?? player.Position;
                if (!Offensive.Contains(ability) || Geo.LineOfSight(map, Enemy.Position, marked)) ExecuteAbility(ability, player, map, rng);
                else Announce("ATAQUE BLOQUEADO PELA PAREDE");
                ActionTimer = Phase == 1 ? 65 : 48;
            }
            return;
        }
        ActionTimer--;
        if (ActionTimer > 0) return;
        int distance = Distance(player);
        if (!Geo.LineOfSight(map, Enemy.Position, player.Position)) { MoveToward(player, map); ActionTimer = 38; return; }
        string? chosen = Enemy.Statuses?.Get("numb") is { Count: > 0 } ? null : ChooseAbility(distance, rng);     // Entorpecimento
        if (chosen != null && Use(chosen, Phase == 1 ? 150 : 105)) { BeginAbility(chosen, player); return; }
        if (distance > 1) MoveToward(player, map);
        else { BasicTotal = 8; BasicTimer = BasicTotal; BasicTarget = player.Position; Announce("PREPARANDO: GOLPE BÁSICO", 22); }
        ActionTimer = 45;
    }
}
