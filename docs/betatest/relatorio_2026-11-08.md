# Relatório do beta tester — 2026-11-08

Versão testada: `954ed04` (vertical slice: Bárbaro na cripta com 6 Brutos Abissais). 
200 partidas jogadas por 5 perfis de jogador, sementes 1 a 40, sem janela, com as regras reais do domínio. O bot só aperta os mesmos botões de um jogador (andar, mirar, golpes, inventário, árvore).

## Resumo

Prioridade média: 4 · Prioridade baixa: 3

- **Sem risco nem para o Novato** — Mais inimigos por sala, Bruto um pouco mais forte, ou um inimigo que puna ficar parado.
- **Fácil demais para quem joga bem** — Grupos maiores, um segundo tipo de inimigo ou golpes do Bruto que exijam mais de uma esquiva.
- **Inimigos morrem rápido demais** — Mais vida para o Bruto.
- **Fúria fica cheia e é desperdiçada** — Ruptura útil contra um alvo só, ou outra forma de gastar Fúria.
- **A cripta acaba rápido demais** — Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.

## O que mudar

### [Prioridade média] Sem risco nem para o Novato
*Dificuldade*. O Novato morreu em 0% das partidas, só com o ataque básico e sem esquivar (meta: ao menos 10%). Terminou com 80% da vida em média no pior momento. Cada golpe do Bruto tira 6 de 190 de vida (ataque 14 − defesa 8).

**Sugestão:** Mais inimigos por sala, Bruto um pouco mais forte, ou um inimigo que puna ficar parado.

### [Prioridade média] Fácil demais para quem joga bem
*Dificuldade*. O Veterano terminou com 90% da vida no pior momento, em média (meta: 30%–75%).

**Sugestão:** Grupos maiores, um segundo tipo de inimigo ou golpes do Bruto que exijam mais de uma esquiva.

### [Prioridade média] Inimigos morrem rápido demais
*Balanceamento*. Mediana de 0,8 s do primeiro golpe à morte.

**Sugestão:** Mais vida para o Bruto.

### [Prioridade média] Fúria fica cheia e é desperdiçada
*Habilidades*. O Casual (usa Ruptura com 3+ inimigos no cone) passou 19% da partida com a Fúria no máximo.

**Sugestão:** Ruptura útil contra um alvo só, ou outra forma de gastar Fúria.

### [Prioridade baixa] A cripta acaba rápido demais
*Ritmo*. O Casual limpa a cripta em 11 s (mediana; meta 60–240 s).

**Sugestão:** Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.

### [Prioridade baixa] Graus de Golpe Brutal que não mudam nada contra o Bruto
*Árvore*. Golpes para derrotar um Bruto por grau: grau 1 = 3 (22 de dano), grau 2 = 3 (24 de dano), grau 3 = 3 (27 de dano), grau 4 = 3 (29 de dano), grau 5 = 3 (31 de dano). Os graus 2, 3, 4, 5 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Ajustar o ganho por grau (hoje +10%, PROVISÓRIO) ou a vida do Bruto para cada grau cruzar um ponto de corte.

### [Prioridade baixa] Graus de Ruptura que não mudam nada contra o Bruto
*Árvore*. Golpes para derrotar um Bruto por grau: grau 1 = 3 (29 de dano), grau 2 = 2 (33 de dano), grau 3 = 2 (36 de dano), grau 4 = 2 (39 de dano), grau 5 = 2 (42 de dano). Os graus 3, 4, 5 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Ajustar o ganho por grau (hoje +10%, PROVISÓRIO) ou a vida do Bruto para cada grau cruzar um ponto de corte.

## O que está bom

- **Nenhuma exceção, travamento ou partida sem fim** (Estabilidade). 200 partidas; 15 repetidas com a mesma semente deram o mesmo resultado.
- **Esquivar compensa** (Combate). Quem esquiva toma 49% menos dano (8 contra 16).

## Números por perfil

| Perfil | Partidas | Venceu | Morreu | Travou/erro | Tempo (mediana) | Pior vida (média) | Dano tomado | Golpes do Bruto que acertaram | Esquivas bloqueadas | Fúria cheia | Itens/partida |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 40 | 100% | 0% | 0% | 12 s | 80% | 29 | 100% | — | 36% | 3,6 |
| Casual | 40 | 100% | 0% | 0% | 11 s | 87% | 14 | 56% | 12% | 19% | 3,6 |
| Veterano | 40 | 100% | 0% | 0% | 13 s | 90% | 8 | 25% | 1% | 2% | 3,6 |
| Agressivo | 40 | 100% | 0% | 0% | 10 s | 86% | 16 | 100% | — | 0% | 3,6 |
| Caótico | 40 | 100% | 0% | 0% | 25 s | 83% | 22 | 60% | 2% | 0% | 3,6 |

- **Novato:** Reação lenta, mira imprecisa, só o ataque básico, não esquiva, equipa só em espaço vazio, ignora a árvore.
- **Casual:** Reação média, usa Ruptura em grupos de 3, esquiva metade das vezes, equipa melhorias e gasta pontos.
- **Veterano:** Reação rápida, mira precisa, esquiva sempre que vê o golpe, Ruptura em grupos de 2, Investida, melhor equipamento.
- **Agressivo:** Reação rápida mas nunca esquiva: troca golpes e gasta Fúria sempre que pode.
- **Caótico:** Metade das decisões é aleatória (andar, mirar e apertar botões a esmo): procura travamentos e exceções.

## Habilidades

| Perfil | Habilidade | Usos | No vazio | Dano | % do dano | Dano por s de recuperação | Dano desperdiçado | Abates |
|---|---|---|---|---|---|---|---|---|
| Novato | Golpe Brutal | 718 | 0% | 15600 | 100% | 54 | 4% | 240 |
| Casual | Golpe Brutal | 592 | 0% | 12843 | 82% | 54 | 21% | 218 |
| Casual | Ruptura | 22 | 0% | 1891 | 12% | 172 | 22% | 22 |
| Casual | Investida | 45 | 0% | 866 | 6% | 96 | 0% | 0 |
| Veterano | Golpe Brutal | 453 | 0% | 9790 | 63% | 54 | 22% | 163 |
| Veterano | Ruptura | 73 | 0% | 4465 | 29% | 122 | 27% | 73 |
| Veterano | Investida | 71 | 0% | 1345 | 9% | 95 | 4% | 4 |
| Agressivo | Golpe Brutal | 482 | 0% | 10425 | 67% | 54 | 22% | 172 |
| Agressivo | Ruptura | 122 | 0% | 4415 | 28% | 72 | 28% | 68 |
| Agressivo | Investida | 40 | 0% | 760 | 5% | 95 | 0% | 0 |

## Itens

| Perfil | Comum | Mágico | Raro | Lendário | Ataque do equipamento | Defesa do equipamento | Vida do equipamento |
|---|---|---|---|---|---|---|---|
| Novato | 85 | 41 | 15 | 2 | +2,6 | +5,9 | +12,5 |
| Casual | 85 | 41 | 15 | 2 | +2,7 | +6,3 | +12,9 |
| Veterano | 85 | 41 | 15 | 2 | +2,7 | +6,3 | +12,9 |
| Agressivo | 85 | 41 | 15 | 2 | +2,7 | +6,3 | +12,9 |
| Caótico | 85 | 41 | 15 | 2 | +2,7 | +6,3 | +12,9 |

## Metas usadas (PROVISÓRIO)

São suposições do testador sobre a experiência desejada; o Game Director decide as metas reais (`src/Umbra.BetaTester/Analyst.cs`, classe `Targets`).

| Meta | Valor |
|---|---|
| Mortes do Novato | 10%–40% |
| Mortes do Veterano | até 5% |
| Pior vida do Veterano | 30%–75% |
| Tempo do Casual na cripta | 60–240 s |
| Golpes do Bruto que acertam o Veterano | até 30% |
| Esquivas bloqueadas pela própria recuperação | até 25% |
| Tempo para derrotar um Bruto | 1,0–5,0 s |
| Parte da Ruptura no dano do Veterano | ao menos 15% |
| Tempo com Fúria cheia | até 15% |
| Golpes no vazio | até 30% |
| Ataque vindo do equipamento | até +60% da base |

## Limites deste teste

- Joga só o que a slice tem: Bárbaro, um tipo de inimigo, uma cripta, três habilidades. Sistemas portados na Fase 5 entram no teste quando forem ligados ao jogo (5.8).
- Não avalia visual, som, leitura da tela nem diversão; mede números de jogo. Os perfis são aproximações de jogadores reais.
- Os itens e graus da slice são PROVISÓRIOS (`ItemRules`, `SkillRanks`): achados sobre eles valem para a slice, não para o UMBRA final.
