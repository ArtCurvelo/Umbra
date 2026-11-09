# Relatório do beta tester — 2026-11-09

Versão testada: `07fb29c` (vertical slice: Mago nível 6 na cripta, inimigos da família das criptas no nível do herói, em grupos, com elites e o chefe; regras reais do UMBRA desde a 5.8c). 
200 partidas jogadas por 5 perfis de jogador, sementes 1 a 40, sem janela, uma de cada vez, com as regras do UMBRA portadas para C#. O bot só aperta os mesmos botões de um jogador (andar, mirar, golpes, inventário, árvore).

## Resumo

Prioridade alta: 2 · Prioridade média: 3 · Prioridade baixa: 3

- **Difícil demais para quem está começando** — Menos inimigos por grupo, preparação mais longa dos golpes ou mais vida inicial ao Mago (decisão do Game Director).
- **A esquiva chega tarde demais** — Preparação mais longa ou arranque mais rápido do movimento.
- **Esquivar quase não compensa** — No UMBRA o acerto cancela a preparação do inimigo comum: golpear protege mais que esquivar. Decisão do Game Director se a esquiva deve valer mais.
- **Inimigos morrem rápido demais** — Mais vida para os inimigos comuns.
- **MANA fica cheia e é desperdiçada** — Outra forma de gastar MANA, ou habilidade 1 útil contra um alvo só.

## O que mudar

### [Prioridade alta] Difícil demais para quem está começando
*Dificuldade*. O Novato morreu em 65% das partidas (meta: até 40%).

**Sugestão:** Menos inimigos por grupo, preparação mais longa dos golpes ou mais vida inicial ao Mago (decisão do Game Director).

### [Prioridade alta] A esquiva chega tarde demais
*Combate*. Em 32% das vezes que o Veterano esquivou (reage em 0,13 s), o golpe acertou mesmo assim: sair das casas marcadas levou mais que o resto da preparação (em quadros: Sombra 20, Espectro Velado 17, Cultista 28; o chefe tem a sua).

**Sugestão:** Preparação mais longa ou arranque mais rápido do movimento.

### [Prioridade média] Esquivar quase não compensa
*Combate*. O Veterano (esquiva) tomou 90 de dano por partida; o Agressivo (nunca esquiva), 84. Diferença: -7%.

**Sugestão:** No UMBRA o acerto cancela a preparação do inimigo comum: golpear protege mais que esquivar. Decisão do Game Director se a esquiva deve valer mais.

### [Prioridade média] Inimigos morrem rápido demais
*Balanceamento*. Mediana de 0,3 s do primeiro golpe à morte.

**Sugestão:** Mais vida para os inimigos comuns.

### [Prioridade média] MANA fica cheia e é desperdiçada
*Habilidades*. O Casual (usa Bola de Fogo quando pode) passou 35% da partida com MANA no máximo.

**Sugestão:** Outra forma de gastar MANA, ou habilidade 1 útil contra um alvo só.

### [Prioridade baixa] A cripta acaba rápido demais
*Ritmo*. O Casual limpa a cripta em 15 s (mediana; meta 60–240 s).

**Sugestão:** Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.

### [Prioridade baixa] Ranks de Projétil Arcano que não mudam nada contra o Cultista
*Árvore*. Golpes para derrotar um Cultista por rank (sem crítico nem combo): rank 1 = 3 (25 de dano), rank 2 = 3 (29 de dano), rank 3 = 3 (32 de dano), rank 4 = 3 (35 de dano), rank 5 = 2 (39 de dano), rank 6 = 2 (40 de dano), rank 7 = 2 (42 de dano), rank 8 = 2 (44 de dano), rank 9 = 2 (45 de dano), rank 10 = 2 (47 de dano), rank 11 = 2 (49 de dano), rank 12 = 2 (51 de dano), rank 13 = 2 (52 de dano), rank 14 = 2 (54 de dano), rank 15 = 2 (56 de dano). Os ranks 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

### [Prioridade baixa] Ranks de Bola de Fogo que não mudam nada contra o Cultista
*Árvore*. Golpes para derrotar um Cultista por rank (sem crítico nem combo): rank 1 = 2 (47 de dano), rank 2 = 2 (53 de dano), rank 3 = 2 (59 de dano), rank 4 = 2 (65 de dano), rank 5 = 2 (71 de dano), rank 6 = 1 (74 de dano), rank 7 = 1 (77 de dano), rank 8 = 1 (80 de dano), rank 9 = 1 (83 de dano), rank 10 = 1 (86 de dano), rank 11 = 1 (89 de dano), rank 12 = 1 (92 de dano), rank 13 = 1 (95 de dano), rank 14 = 1 (98 de dano), rank 15 = 1 (101 de dano). Os ranks 2, 3, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

## O que está bom

- **Nenhuma exceção, travamento ou partida sem fim** (Estabilidade). 200 partidas; 15 repetidas com a mesma semente deram o mesmo resultado.

## Números por perfil

| Perfil | Partidas | Venceu | Morreu | Travou/erro | Tempo (mediana) | Pior vida (média) | Dano tomado | Golpes inimigos que acertaram | Esquivas tardias | Recurso cheio | Poções/partida | Itens/partida |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 40 | 35% | 65% | 0% | 20 s | 5% | 174 | 100% | — | 99% | 1,2 | 2,5 |
| Casual | 40 | 100% | 0% | 0% | 15 s | 30% | 94 | 25% | 48% | 35% | 0,8 | 4,4 |
| Veterano | 40 | 98% | 3% | 0% | 14 s | 32% | 90 | 0% | 32% | 23% | 0,7 | 4,2 |
| Agressivo | 40 | 100% | 0% | 0% | 13 s | 34% | 84 | 100% | — | 29% | 0,6 | 4,4 |
| Caótico | 40 | 53% | 48% | 0% | 25 s | 10% | 164 | 48% | 36% | 18% | 1,3 | 3,0 |

- **Novato:** Reação lenta, mira imprecisa, só o ataque básico, não esquiva, equipa só em espaço vazio, ignora a árvore; bebe poção tarde (25% da vida).
- **Casual:** Reação média, usa Ruptura em grupos de 3, esquiva metade das vezes, equipa melhorias e gasta pontos.
- **Veterano:** Reação rápida, mira precisa, esquiva sempre que vê o golpe, Ruptura em grupos de 2, Investida, melhor equipamento.
- **Agressivo:** Reação rápida mas nunca esquiva: troca golpes e gasta Fúria sempre que pode.
- **Caótico:** Metade das decisões é aleatória (andar, mirar e apertar botões a esmo): procura travamentos e exceções.

## Habilidades

| Perfil | Habilidade | Usos | No vazio | Dano | % do dano | Dano por uso | Abates |
|---|---|---|---|---|---|---|---|
| Novato | Projétil Arcano | 1843 | 22% | 32308 | 100% | 18 | 266 |
| Casual | Projétil Arcano | 1051 | 28% | 21072 | 50% | 20 | 144 |
| Casual | Bola de Fogo | 357 | 0% | 20987 | 50% | 59 | 217 |
| Veterano | Projétil Arcano | 912 | 28% | 18349 | 44% | 20 | 125 |
| Veterano | Bola de Fogo | 400 | 0% | 23700 | 56% | 59 | 235 |
| Agressivo | Projétil Arcano | 940 | 21% | 20650 | 49% | 22 | 145 |
| Agressivo | Bola de Fogo | 360 | 0% | 21409 | 51% | 59 | 216 |

## Itens

| Perfil | Comum | Mágico | Raro | Lendário | Único | Único Mítico | Ataque do equipamento | Armadura do equipamento | Vida do equipamento | Níveis ganhos |
|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 59 | 19 | 21 | 0 | 0 | 0 | +2,6 | +1,5 | +4,2 | 0,0 |
| Casual | 107 | 44 | 26 | 0 | 0 | 0 | +6,5 | +2,9 | +13,1 | 0,0 |
| Veterano | 88 | 49 | 29 | 2 | 0 | 0 | +6,1 | +3,5 | +12,5 | 0,0 |
| Agressivo | 102 | 45 | 28 | 1 | 0 | 0 | +6,6 | +3,3 | +14,5 | 0,0 |
| Caótico | 72 | 32 | 16 | 1 | 0 | 0 | +3,7 | +1,6 | +6,3 | 0,0 |

## Metas usadas (PROVISÓRIO)

São suposições do testador sobre a experiência desejada; o Game Director decide as metas reais (`src/Umbra.BetaTester/Analyst.cs`, classe `Targets`).

| Meta | Valor |
|---|---|
| Mortes do Novato | 10%–40% |
| Mortes do Veterano | até 5% |
| Pior vida do Veterano | 30%–75% |
| Tempo do Casual na cripta | 60–240 s |
| Golpes de inimigos comuns que acertam o Veterano | até 30% |
| Esquivas que chegam tarde (o golpe acerta mesmo assim) | até 25% |
| Tempo para derrotar um inimigo comum | 1,0–5,0 s |
| Parte de Bola de Fogo no dano do Veterano | ao menos 15% |
| Tempo com MANA cheia | até 15% |
| Golpes no vazio | até 30% |
| Ataque vindo do equipamento | até +60% da base |

## Limites deste teste

- Joga só o que a slice tem: Mago nível 6 (PROVISÓRIO), uma cripta (sombra, espectro, cultista e o Guardião das Cinzas), três habilidades nos espaços (Projétil Arcano, Bola de Fogo, Teleporte). Bruxo e Necromante entram na 5.8d2–d4.
- Não avalia visual, som, leitura da tela nem diversão; mede números de jogo. Os perfis são aproximações de jogadores reais.
- Regras, itens e ranks são os do UMBRA. Ficam fora o dano desperdiçado (a partida informa só a vida tirada) e o crítico e o combo na tabela de golpes por rank.
