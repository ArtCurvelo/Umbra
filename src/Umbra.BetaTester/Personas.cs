namespace Umbra.BetaTester;

/// <summary>How the bot equips loot: never, only into empty slots, or whatever scores higher.</summary>
public enum GearHabit { Never, EmptySlots, Upgrades }

/// <summary>
/// A kind of player. Times are simulation ticks (60 per second). The numbers describe people, not rules: they are the
/// tester's assumptions about players (PROVISÓRIO, adjust with the Game Director) and never touch the game's data.
/// </summary>
/// <param name="ReactionTicks">Ticks between seeing an enemy wind up and starting to dodge.</param>
/// <param name="ThinkTicks">Ticks between decisions (in between, the last input is held).</param>
/// <param name="AimErrorDegrees">Maximum aim error per decision.</param>
/// <param name="DodgeChance">Chance of trying to dodge a telegraphed strike (0 = never).</param>
/// <param name="ConeGroup">Minimum enemies in the cone to spend Fúria on Ruptura (0 = never uses it).</param>
/// <param name="UsesDash">Uses Investida to close distance.</param>
/// <param name="Gear">How loot is equipped.</param>
/// <param name="SpendsPoints">Spends the skill points on the tree.</param>
/// <param name="Chaos">Share of decisions replaced by random input (robustness and stuck tests).</param>
/// <param name="PotionAt">Drinks a potion when life falls below this share (0 = never).</param>
public sealed record Persona(string Id, string Name, string Description, int ReactionTicks, int ThinkTicks, float AimErrorDegrees,
                             double DodgeChance, int ConeGroup, bool UsesDash, GearHabit Gear, bool SpendsPoints, double Chaos = 0,
                             double PotionAt = .35)
{
    public static readonly Persona Novato = new("novato", "Novato",
        "Reação lenta, mira imprecisa, só o ataque básico, não esquiva, equipa só em espaço vazio, ignora a árvore; bebe poção tarde (25% da vida).",
        24, 12, 25f, 0, 0, false, GearHabit.EmptySlots, false, PotionAt: .25);

    public static readonly Persona Casual = new("casual", "Casual",
        "Reação média, usa Ruptura em grupos de 3, esquiva metade das vezes, equipa melhorias e gasta pontos.",
        16, 8, 12f, .5, 3, true, GearHabit.Upgrades, true);

    public static readonly Persona Veterano = new("veterano", "Veterano",
        "Reação rápida, mira precisa, esquiva sempre que vê o golpe, Ruptura em grupos de 2, Investida, melhor equipamento.",
        8, 3, 4f, 1, 2, true, GearHabit.Upgrades, true);

    public static readonly Persona Agressivo = new("agressivo", "Agressivo",
        "Reação rápida mas nunca esquiva: troca golpes e gasta Fúria sempre que pode.",
        8, 3, 6f, 0, 1, true, GearHabit.Upgrades, true);

    public static readonly Persona Caotico = new("caotico", "Caótico",
        "Metade das decisões é aleatória (andar, mirar e apertar botões a esmo): procura travamentos e exceções.",
        10, 6, 45f, .3, 1, true, GearHabit.Upgrades, true, .5);

    public static readonly IReadOnlyList<Persona> All = new[] { Novato, Casual, Veterano, Agressivo, Caotico };

    public static Persona? Find(string id) => All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
