using System.Text;
using static Umbra.BetaTester.Analyst;

namespace Umbra.BetaTester;

/// <summary>The report the Game Director reads: what to change first, the evidence, then the raw numbers per player profile.</summary>
public static class Report
{
    private static readonly Dictionary<Severity, string> Label = new()
    {
        [Severity.Bug] = "Bug", [Severity.Alta] = "Prioridade alta", [Severity.Media] = "Prioridade média",
        [Severity.Baixa] = "Prioridade baixa", [Severity.Ok] = "Está bom",
    };

    private static readonly string[] Rarities = { "Comum", "Mágico", "Raro", "Lendário", "Único", "Único Mítico" };

    public static string Markdown(IReadOnlyList<Summary> all, IReadOnlyList<Finding> findings, string date, string version, ulong firstSeed)
    {
        var md = new StringBuilder();
        int runs = all.Sum(s => s.Count);
        md.AppendLine($"# Relatório do beta tester — {date}").AppendLine();
        md.AppendLine($"Versão testada: `{version}` (vertical slice: {Facts.ClassName} nível {Facts.Hero().Level} na cripta, inimigos da família das criptas no nível do herói, em grupos, com elites e o chefe; regras reais do UMBRA desde a 5.8c). ");
        md.AppendLine($"{runs} partidas jogadas por {all.Count} perfis de jogador, sementes {firstSeed} a {firstSeed + (ulong)all.Max(s => s.Count) - 1}, " +
                      "sem janela, uma de cada vez, com as regras do UMBRA portadas para C#. O bot só aperta os mesmos botões de um jogador (andar, mirar, golpes, inventário, árvore).").AppendLine();

        var todo = findings.Where(x => x.Severity != Severity.Ok).ToList();
        md.AppendLine("## Resumo").AppendLine();
        md.AppendLine(todo.Count == 0 ? "Nada a mudar dentro das metas atuais." :
            string.Join(" · ", todo.GroupBy(x => x.Severity).Select(g => $"{Label[g.Key]}: {g.Count()}")));
        md.AppendLine();
        foreach (var x in todo.Take(5)) md.AppendLine($"- **{x.Title}** — {x.Suggestion}");
        md.AppendLine();

        md.AppendLine("## O que mudar").AppendLine();
        if (todo.Count == 0) md.AppendLine("Nenhum ponto fora das metas.").AppendLine();
        foreach (var x in todo)
        {
            md.AppendLine($"### [{Label[x.Severity]}] {x.Title}");
            md.AppendLine($"*{x.Area}*. {x.Evidence}").AppendLine();
            md.AppendLine($"**Sugestão:** {x.Suggestion}").AppendLine();
        }

        var ok = findings.Where(x => x.Severity == Severity.Ok).ToList();
        if (ok.Count > 0)
        {
            md.AppendLine("## O que está bom").AppendLine();
            foreach (var x in ok) md.AppendLine($"- **{x.Title}** ({x.Area}). {x.Evidence}");
            md.AppendLine();
        }

        md.AppendLine("## Números por perfil").AppendLine();
        md.AppendLine("| Perfil | Partidas | Venceu | Morreu | Travou/erro | Tempo (mediana) | Pior vida (média) | Dano tomado | Golpes inimigos que acertaram | Esquivas tardias | Recurso cheio | Poções/partida | Itens/partida |");
        md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var s in all)
        {
            double broken = s.Rate(Outcome.Stuck) + s.Rate(Outcome.Crashed) + s.Rate(Outcome.Timeout);
            md.AppendLine($"| {s.Persona.Name} | {s.Count} | {P(s.Rate(Outcome.Cleared))} | {P(s.Rate(Outcome.Died))} | {P(broken)} | " +
                          $"{N(s.MedianClearSeconds, "0")} s | {P(s.MeanLowestHealthShare)} | {N(s.MeanDamageTaken, "0")} | {P(s.StrikeLandedShare)} | " +
                          $"{(s.DodgesWanted == 0 ? "—" : P(s.DodgeLockedShare))} | {P(s.FuryCappedShare)} | {N(s.MeanPotions)} | {N(s.DropsPerRun)} |");
        }
        md.AppendLine();
        foreach (var s in all) md.AppendLine($"- **{s.Persona.Name}:** {s.Persona.Description}");
        md.AppendLine();

        md.AppendLine("## Habilidades").AppendLine();
        md.AppendLine("| Perfil | Habilidade | Usos | No vazio | Dano | % do dano | Dano por uso | Abates |");
        md.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var s in all.Where(s => s.Persona.Chaos == 0))
            foreach (var id in s.SkillIds)
            {
                var u = s.Skill(id);
                md.AppendLine($"| {s.Persona.Name} | {SkillName.Of(id)} | {u.Casts} | {P(s.WhiffShare(id))} | {u.Damage} | {P(s.DamageShare(id))} | " +
                              $"{N(s.DamagePerCast(id), "0")} | {u.Kills} |");
            }
        md.AppendLine();

        md.AppendLine("## Itens").AppendLine();
        md.AppendLine($"| Perfil | {string.Join(" | ", Rarities)} | Ataque do equipamento | Armadura do equipamento | Vida do equipamento | Níveis ganhos |");
        md.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", Rarities.Length + 4)));
        foreach (var s in all)
            md.AppendLine($"| {s.Persona.Name} | {string.Join(" | ", Rarities.Select(r => s.Drops(r)))} | " +
                          $"+{N(s.MeanAttackFromGear)} | +{N(s.MeanDefenseFromGear)} | +{N(s.MeanHealthFromGear)} | {N(s.MeanLevelUps)} |");
        md.AppendLine();

        md.AppendLine("## Metas usadas (PROVISÓRIO)").AppendLine();
        md.AppendLine("São suposições do testador sobre a experiência desejada; o Game Director decide as metas reais (`src/Umbra.BetaTester/Analyst.cs`, classe `Targets`).").AppendLine();
        md.AppendLine("| Meta | Valor |").AppendLine("|---|---|");
        md.AppendLine($"| Mortes do Novato | {P(Targets.NovatoDeathMin)}–{P(Targets.NovatoDeathMax)} |");
        md.AppendLine($"| Mortes do Veterano | até {P(Targets.VeteranoDeathMax)} |");
        md.AppendLine($"| Pior vida do Veterano | {P(Targets.VeteranoLowestMin)}–{P(Targets.VeteranoLowestMax)} |");
        md.AppendLine($"| Tempo do Casual na cripta | {N(Targets.ClearSecondsMin, "0")}–{N(Targets.ClearSecondsMax, "0")} s |");
        md.AppendLine($"| Golpes de inimigos comuns que acertam o Veterano | até {P(Targets.VeteranoStrikeLandedMax)} |");
        md.AppendLine($"| Esquivas que chegam tarde (o golpe acerta mesmo assim) | até {P(Targets.DodgeLockedMax)} |");
        md.AppendLine($"| Tempo para derrotar um inimigo comum | {N(Targets.KillSecondsMin)}–{N(Targets.KillSecondsMax)} s |");
        md.AppendLine($"| Parte de {Facts.Name(Facts.Main)} no dano do Veterano | ao menos {P(Targets.SpenderShareMin)} |");
        md.AppendLine($"| Tempo com {Facts.ResourceLabel} cheia | até {P(Targets.FuryCappedMax)} |");
        md.AppendLine($"| Golpes no vazio | até {P(Targets.WhiffMax)} |");
        md.AppendLine($"| Ataque vindo do equipamento | até +{P(Targets.GearAttackShareMax)} da base |");
        md.AppendLine();

        md.AppendLine("## Limites deste teste").AppendLine();
        md.AppendLine($"- Joga só o que a slice tem: {Facts.ClassName} nível 6 (PROVISÓRIO), uma cripta (sombra, espectro, cultista e o Guardião das Cinzas), três habilidades nos espaços ({Facts.Name(Facts.Basic)}, {Facts.Name(Facts.Main)}, {Facts.Name(Facts.Mobility)}). Bruxo e Necromante entram na 5.8d2–d4.");
        md.AppendLine("- Não avalia visual, som, leitura da tela nem diversão; mede números de jogo. Os perfis são aproximações de jogadores reais.");
        md.AppendLine("- Regras, itens e ranks são os do UMBRA. Ficam fora o dano desperdiçado (a partida informa só a vida tirada) e o crítico e o combo na tabela de golpes por rank.");
        return md.ToString();
    }

}
