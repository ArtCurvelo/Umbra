using System.Globalization;

namespace Umbra.BetaTester;

public enum Severity { Bug, Alta, Media, Baixa, Ok }

/// <summary>One thing the tester thinks should change (or a target that was met), with the numbers behind it.</summary>
public sealed record Finding(Severity Severity, string Area, string Title, string Evidence, string Suggestion);

/// <summary>
/// Targets the analyst compares against. PROVISÓRIO: these are the tester's starting assumptions about the experience
/// the slice should give; the Game Director decides the real ones. They live only here, never in the game.
/// </summary>
public static class Targets
{
    public const double NovatoDeathMin = .10, NovatoDeathMax = .40;   // a beginner should feel danger but usually win
    public const double VeteranoDeathMax = .05;                         // a careful expert almost never dies
    public const double VeteranoLowestMin = .30, VeteranoLowestMax = .75; // ...but still ends a run with scars
    public const double ClearSecondsMin = 60, ClearSecondsMax = 240;   // casual clear of the crypt
    public const double VeteranoStrikeLandedMax = .30;                 // telegraphs must be dodgeable on reaction
    public const double DodgeLockedMax = .25;                           // dodges that come too late (the strike lands anyway)
    public const double KillSecondsMin = 1.0, KillSecondsMax = 5.0;    // first hit -> death of a common enemy
    public const double SpenderShareMin = .15;                          // Ruptura's share of an expert's damage
    public const double FuryCappedMax = .15;                            // share of the run sitting at full Fúria
    public const double WhiffMax = .30;                                 // casts that hit nothing
    public const double GearAttackShareMax = .60;                       // gear attack vs the base attack
    public const double IdleMax = .15;
}

public static class Analyst
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    public static string P(double share) => (share * 100).ToString("0", Pt) + "%";
    public static string N(double value, string format = "0.0") => value.ToString(format, Pt);

    public static List<Finding> Analyze(IReadOnlyList<Summary> all, IReadOnlyList<(RunRecord A, RunRecord B)> replays)
    {
        var f = new List<Finding>();
        Summary? Get(Persona p) => all.FirstOrDefault(s => s.Persona.Id == p.Id && s.Count > 0);
        Bugs(all, replays, f);
        if (Get(Persona.Novato) is { } novato) Novice(novato, f);
        if (Get(Persona.Veterano) is { } vet) Expert(vet, f);
        if (Get(Persona.Veterano) is { } v && Get(Persona.Agressivo) is { } a) DodgeReward(v, a, f);
        if (Get(Persona.Casual) is { } casual) Pace(casual, f);
        Combat(all.Where(s => s.Persona.Chaos == 0 && s.Count > 0).ToList(), f);
        return f.OrderBy(x => x.Severity).ToList();
    }

    private static void Bugs(IReadOnlyList<Summary> all, IReadOnlyList<(RunRecord A, RunRecord B)> replays, List<Finding> f)
    {
        foreach (var s in all)
        {
            foreach (var r in s.Runs.Where(r => r.Outcome == Outcome.Crashed).Take(3))
                f.Add(new(Severity.Bug, "Estabilidade", $"Exceção durante a partida ({s.Persona.Name}, semente {r.Seed})",
                          r.Error ?? "?", "Reproduzir com a mesma semente e perfil (o bot é determinístico) e corrigir."));
            foreach (var r in s.Runs.Where(r => r.Outcome == Outcome.Stuck).Take(3))
                f.Add(new(Severity.Bug, "Movimento", $"Herói preso ({s.Persona.Name}, semente {r.Seed})",
                          $"Tentou andar por {N(r.LongestStuckTicks / 60.0)} s sem sair do lugar, aos {N(r.Seconds)} s de jogo.",
                          "Verificar colisão e separação dos corpos nesse ponto do mapa (corredor estreito ou canto)."));
            foreach (var r in s.Runs.Where(r => r.Outcome == Outcome.Timeout).Take(3))
                f.Add(new(Severity.Bug, "Progresso", $"Partida sem fim em 10 min ({s.Persona.Name}, semente {r.Seed})",
                          $"{r.Kills}/{r.Enemies} inimigos derrotados.", "Ver se algum inimigo fica inalcançável ou parado fora da vista."));
        }
        foreach (var (a, b) in replays.Where(p => p.A.Digest != p.B.Digest))
            f.Add(new(Severity.Bug, "Determinismo", $"A mesma partida deu resultados diferentes ({a.Persona}, semente {a.Seed})",
                      $"{a.Digest} ≠ {b.Digest}", "Alguma regra usa estado fora da semente (hora, ordem de dicionário, aleatório global)."));
        if (!f.Any() && replays.Count > 0)
            f.Add(new(Severity.Ok, "Estabilidade", "Nenhuma exceção, travamento ou partida sem fim",
                      $"{all.Sum(s => s.Count)} partidas; {replays.Count} repetidas com a mesma semente deram o mesmo resultado.", "Nada a mudar."));
    }

    private static void Novice(Summary s, List<Finding> f)
    {
        double death = s.Rate(Outcome.Died);
        if (death > Targets.NovatoDeathMax)
            f.Add(new(Severity.Alta, "Dificuldade", "Difícil demais para quem está começando",
                      $"O Novato morreu em {P(death)} das partidas (meta: até {P(Targets.NovatoDeathMax)}).",
                      "Menos inimigos por grupo, preparação mais longa dos golpes ou mais vida inicial ao Bárbaro (decisão do Game Director)."));
        else if (death < Targets.NovatoDeathMin)
            f.Add(new(Severity.Media, "Dificuldade", "Sem risco nem para o Novato",
                      $"O Novato morreu em {P(death)} das partidas, só com o ataque básico e sem esquivar (meta: ao menos {P(Targets.NovatoDeathMin)}). " +
                      $"Terminou com {P(s.MeanLowestHealthShare)} da vida em média no pior momento. {StrikeLine(s)} " +
                      "No UMBRA todo acerto num inimigo comum cancela a preparação do golpe dele e o atordoa por 4 quadros: quem golpeia sem parar quase não é atingido.",
                      "Decisão do Game Director: mais inimigos por sala, nível inicial menor, dificuldade maior, ou rever o cancelamento da preparação por acerto."));
        else
            f.Add(new(Severity.Ok, "Dificuldade", "Risco adequado para o Novato", $"Morreu em {P(death)} das partidas.", "Nada a mudar."));
        double whiff = s.WhiffShare(Facts.Golpe);
        if (whiff > Targets.WhiffMax)
            f.Add(new(Severity.Media, "Controles", "Golpes no vazio demais para quem mira mal",
                      $"{P(whiff)} dos Golpes Brutais do Novato não acertaram nada (mira com até {N(s.Persona.AimErrorDegrees, "0")}° de erro).",
                      "Ver por que o golpe não achou alvo (a Básica do UMBRA escolhe o inimigo mais próximo ou o da mira, a 1 casa)."));
    }

    private static void Expert(Summary s, List<Finding> f)
    {
        double death = s.Rate(Outcome.Died), low = s.MeanLowestHealthShare;
        if (death > Targets.VeteranoDeathMax)
            f.Add(new(Severity.Alta, "Dificuldade", "Até o jogador habilidoso morre",
                      $"O Veterano (reage em {N(s.Persona.ReactionTicks / 60.0, "0.00")} s e esquiva sempre) morreu em {P(death)} das partidas.",
                      "Algum golpe não é esquivável na prática: revisar o alcance e a preparação dos golpes (inimigos e chefe)."));
        if (low > Targets.VeteranoLowestMax)
            f.Add(new(Severity.Media, "Dificuldade", "Fácil demais para quem joga bem",
                      $"O Veterano terminou com {P(low)} da vida no pior momento, em média (meta: {P(Targets.VeteranoLowestMin)}–{P(Targets.VeteranoLowestMax)}).",
                      "Grupos maiores, mais elites ou golpes que exijam mais de uma esquiva."));
        else if (low < Targets.VeteranoLowestMin)
            f.Add(new(Severity.Media, "Dificuldade", "Punitivo mesmo jogando bem",
                      $"O Veterano chegou a {P(low)} da vida no pior momento, em média.", "Reduzir o dano por golpe ou dar cura entre salas."));
        double landed = s.StrikeLandedShare;
        if (s.Strikes > 0 && landed > Targets.VeteranoStrikeLandedMax)
            f.Add(new(Severity.Alta, "Combate", "O aviso dos golpes não dá tempo de esquivar",
                      $"Mesmo esquivando sempre, {P(landed)} dos golpes dos inimigos comuns acertaram o Veterano ({s.Strikes} golpes).",
                      $"Aumentar a preparação dos golpes (hoje, em quadros: {Facts.Windups()})."));
        double locked = s.DodgeLockedShare;
        if (s.DodgesWanted > 0 && locked > Targets.DodgeLockedMax)
            f.Add(new(Severity.Alta, "Combate", "A esquiva chega tarde demais",
                      $"Em {P(locked)} das vezes que o Veterano esquivou (reage em {N(s.Persona.ReactionTicks / 60.0, "0.00")} s), o golpe acertou mesmo assim: " +
                      $"sair das casas marcadas levou mais que o resto da preparação (em quadros: {Facts.Windups()}; o chefe tem a sua).",
                      "Preparação mais longa ou arranque mais rápido do movimento."));
    }

    private static void DodgeReward(Summary vet, Summary agg, List<Finding> f)
    {
        if (agg.MeanDamageTaken <= 0) return;
        double saved = 1 - vet.MeanDamageTaken / agg.MeanDamageTaken;
        if (saved < .25)
            f.Add(new(Severity.Media, "Combate", "Esquivar quase não compensa",
                      $"O Veterano (esquiva) tomou {N(vet.MeanDamageTaken, "0")} de dano por partida; o Agressivo (nunca esquiva), {N(agg.MeanDamageTaken, "0")}. Diferença: {P(saved)}.",
                      "No UMBRA o acerto cancela a preparação do inimigo comum: golpear protege mais que esquivar. Decisão do Game Director se a esquiva deve valer mais."));
        else
            f.Add(new(Severity.Ok, "Combate", "Esquivar compensa",
                      $"Quem esquiva toma {P(saved)} menos dano ({N(vet.MeanDamageTaken, "0")} contra {N(agg.MeanDamageTaken, "0")}).", "Nada a mudar."));
    }

    private static void Pace(Summary s, List<Finding> f)
    {
        double t = s.MedianClearSeconds;
        if (t <= 0) return;
        if (t < Targets.ClearSecondsMin)
            f.Add(new(Severity.Baixa, "Ritmo", "A cripta acaba rápido demais",
                      $"O Casual limpa a cripta em {N(t, "0")} s (mediana; meta {N(Targets.ClearSecondsMin, "0")}–{N(Targets.ClearSecondsMax, "0")} s).",
                      "Mais inimigos ou uma terceira sala; para a slice, pode ficar assim."));
        else if (t > Targets.ClearSecondsMax)
            f.Add(new(Severity.Media, "Ritmo", "A cripta demora demais", $"O Casual leva {N(t, "0")} s (mediana).", "Menos vida nos inimigos ou mais dano básico."));
        if (s.IdleShare > Targets.IdleMax)
            f.Add(new(Severity.Baixa, "Ritmo", "Muito tempo parado", $"{P(s.IdleShare)} da partida sem nenhuma ação com inimigos vivos.",
                      "Inimigos que venham até o herói ou salas mais próximas."));
    }

    private static string StrikeLine(Summary s)
    {
        var hero = Facts.Hero();
        string attacks = string.Join(", ", Facts.Foes().Select(f => $"{f.Name} {f.Attack}"));
        return s.DamagePerStrike <= 0 ? $"Nenhum golpe de inimigo comum acertou (ataques: {attacks}; armadura do Bárbaro {hero.Defense})."
            : $"O dano tomado por golpe de inimigo comum que acertou foi, em média, {N(s.DamagePerStrike, "0")} de {hero.MaxHealth} de vida (inclui o chefe; ataques: {attacks}; armadura do Bárbaro {hero.Defense}).";
    }

    private static void Breakpoints(List<Finding> f)
    {
        foreach (var id in new[] { Facts.Golpe, Facts.Ruptura })
        {
            var rows = Facts.HitsToKill(id).ToList();
            var dead = rows.Skip(1).Where(r => r.Hits == rows[rows.IndexOf(r) - 1].Hits).Select(r => r.Rank).ToList();
            if (dead.Count == 0) continue;
            f.Add(new(Severity.Baixa, "Árvore", $"Ranks de {Facts.Name(id)} que não mudam nada contra o {Facts.Toughest().Name}",
                      $"Golpes para derrotar um {Facts.Toughest().Name} por rank (sem crítico nem combo): {string.Join(", ", rows.Select(r => $"rank {r.Rank} = {r.Hits} ({r.Damage} de dano)"))}. " +
                      $"Os ranks {string.Join(", ", dead)} gastam ponto sem tirar nenhum golpe.",
                      "Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director)."));
        }
    }

    private static void Combat(List<Summary> all, List<Finding> f)
    {
        if (all.Count == 0) return;
        var vet = all.FirstOrDefault(s => s.Persona.Id == Persona.Veterano.Id) ?? all[^1];
        double kill = Summary.Median(all.SelectMany(s => s.Runs).SelectMany(r => r.KillTicks).Select(t => t / 60.0));
        if (kill < Targets.KillSecondsMin)
            f.Add(new(Severity.Media, "Balanceamento", "Inimigos morrem rápido demais", $"Mediana de {N(kill)} s do primeiro golpe à morte.", "Mais vida para os inimigos comuns."));
        else if (kill > Targets.KillSecondsMax)
            f.Add(new(Severity.Media, "Balanceamento", "Inimigos são esponjas de dano", $"Mediana de {N(kill)} s do primeiro golpe à morte.", "Menos vida ou mais dano base."));
        else
            f.Add(new(Severity.Ok, "Balanceamento", "Tempo para derrotar um inimigo comum adequado", $"Mediana de {N(kill)} s.", "Nada a mudar."));

        double spender = vet.DamageShare(Facts.Ruptura);
        if (spender < Targets.SpenderShareMin)
            f.Add(new(Severity.Media, "Habilidades", "Ruptura quase não entra na rotação",
                      $"Ruptura fez só {P(spender)} do dano do {vet.Persona.Name} (meta: ao menos {P(Targets.SpenderShareMin)}).",
                      $"Custo menor (hoje {Facts.Cost(Facts.Ruptura)} de Fúria) ou mais Fúria por Golpe Brutal (hoje {Facts.Gain(Facts.Golpe)})."));
        // Only players who spend Fúria say something about the resource (the Novato never casts Ruptura by habit).
        var spenders = all.Where(s => s.Persona.ConeGroup > 0).ToList();
        if (spenders.Count > 0 && spenders.MaxBy(s => s.FuryCappedShare) is { } cappedBy && cappedBy.FuryCappedShare > Targets.FuryCappedMax)
            f.Add(new(Severity.Media, "Habilidades", "Fúria fica cheia e é desperdiçada",
                      $"O {cappedBy.Persona.Name} (usa Ruptura com {cappedBy.Persona.ConeGroup}+ inimigos no cone) passou {P(cappedBy.FuryCappedShare)} da partida com a Fúria no máximo.",
                      "Ruptura útil contra um alvo só, ou outra forma de gastar Fúria."));
        Breakpoints(f);
        var dash = vet.Skill(Facts.Investida);
        if (dash.Casts > 0 && vet.WhiffShare(Facts.Investida) > .5)
            f.Add(new(Severity.Baixa, "Habilidades", "Investida erra o alvo com frequência",
                      $"{P(vet.WhiffShare(Facts.Investida))} das Investidas do {vet.Persona.Name} não acertaram ninguém.",
                      "Mirar a Investida no inimigo mais próximo da direção ou aumentar a largura do acerto."));
        int baseAttack = Facts.Hero().Attack;
        double attackShare = vet.MeanAttackFromGear / baseAttack;
        if (attackShare > Targets.GearAttackShareMax)
            f.Add(new(Severity.Media, "Itens", "Equipamento pesa demais no ataque",
                      $"O equipamento somou {N(vet.MeanAttackFromGear)} de ataque ao Bárbaro (base {baseAttack}, +{P(attackShare)}) numa cripta só.",
                      "Balanceamento das tabelas de itens do UMBRA (afixos de ataque, chance de raridade): decisão do Game Director."));
        var bag = all.Sum(s => s.BagFullRuns);
        if (bag > 0)
            f.Add(new(Severity.Baixa, "Itens", "Mochila cheia durante a partida", $"Aconteceu em {bag} partidas.", "Avisar no HUD e permitir descartar rápido."));
    }
}
