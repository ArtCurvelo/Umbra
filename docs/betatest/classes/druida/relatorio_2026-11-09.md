# Relatório do beta tester — 2026-11-09

Versão testada: `07fb29c` (vertical slice: Druida nível 6 na cripta, inimigos da família das criptas no nível do herói, em grupos, com elites e o chefe; regras reais do UMBRA desde a 5.8c). 
200 partidas jogadas por 5 perfis de jogador, sementes 1 a 40, sem janela, uma de cada vez, com as regras do UMBRA portadas para C#. O bot só aperta os mesmos botões de um jogador (andar, mirar, golpes, inventário, árvore).

## Resumo

Prioridade alta: 2 · Prioridade média: 1 · Prioridade baixa: 3

- **O aviso dos golpes não dá tempo de esquivar** — Aumentar a preparação dos golpes (hoje, em quadros: Sombra 20, Espectro Velado 17, Cultista 28).
- **A esquiva chega tarde demais** — Preparação mais longa ou arranque mais rápido do movimento.
- **Esquivar quase não compensa** — No UMBRA o acerto cancela a preparação do inimigo comum: golpear protege mais que esquivar. Decisão do Game Director se a esquiva deve valer mais.
- **A cripta acaba rápido demais** — Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.
- **Ranks de Espinho Vivo que não mudam nada contra o Cultista** — Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

## O que mudar

### [Prioridade alta] O aviso dos golpes não dá tempo de esquivar
*Combate*. Mesmo esquivando sempre, 32% dos golpes dos inimigos comuns acertaram o Veterano (60 golpes).

**Sugestão:** Aumentar a preparação dos golpes (hoje, em quadros: Sombra 20, Espectro Velado 17, Cultista 28).

### [Prioridade alta] A esquiva chega tarde demais
*Combate*. Em 31% das vezes que o Veterano esquivou (reage em 0,13 s), o golpe acertou mesmo assim: sair das casas marcadas levou mais que o resto da preparação (em quadros: Sombra 20, Espectro Velado 17, Cultista 28; o chefe tem a sua).

**Sugestão:** Preparação mais longa ou arranque mais rápido do movimento.

### [Prioridade média] Esquivar quase não compensa
*Combate*. O Veterano (esquiva) tomou 119 de dano por partida; o Agressivo (nunca esquiva), 125. Diferença: 5%.

**Sugestão:** No UMBRA o acerto cancela a preparação do inimigo comum: golpear protege mais que esquivar. Decisão do Game Director se a esquiva deve valer mais.

### [Prioridade baixa] A cripta acaba rápido demais
*Ritmo*. O Casual limpa a cripta em 17 s (mediana; meta 60–240 s).

**Sugestão:** Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.

### [Prioridade baixa] Ranks de Espinho Vivo que não mudam nada contra o Cultista
*Árvore*. Golpes para derrotar um Cultista por rank (sem crítico nem combo): rank 1 = 4 (19 de dano), rank 2 = 4 (22 de dano), rank 3 = 4 (24 de dano), rank 4 = 3 (27 de dano), rank 5 = 3 (29 de dano), rank 6 = 3 (31 de dano), rank 7 = 3 (32 de dano), rank 8 = 3 (33 de dano), rank 9 = 3 (35 de dano), rank 10 = 3 (36 de dano), rank 11 = 2 (37 de dano), rank 12 = 2 (39 de dano), rank 13 = 2 (40 de dano), rank 14 = 2 (41 de dano), rank 15 = 2 (42 de dano). Os ranks 2, 3, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

### [Prioridade baixa] Ranks de Tempestade Verde que não mudam nada contra o Cultista
*Árvore*. Golpes para derrotar um Cultista por rank (sem crítico nem combo): rank 1 = 4 (24 de dano), rank 2 = 3 (27 de dano), rank 3 = 3 (30 de dano), rank 4 = 3 (33 de dano), rank 5 = 2 (37 de dano), rank 6 = 2 (38 de dano), rank 7 = 2 (40 de dano), rank 8 = 2 (41 de dano), rank 9 = 2 (43 de dano), rank 10 = 2 (44 de dano), rank 11 = 2 (46 de dano), rank 12 = 2 (48 de dano), rank 13 = 2 (49 de dano), rank 14 = 2 (51 de dano), rank 15 = 2 (52 de dano). Os ranks 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

## O que está bom

- **Nenhuma exceção, travamento ou partida sem fim** (Estabilidade). 200 partidas; 15 repetidas com a mesma semente deram o mesmo resultado.
- **Risco adequado para o Novato** (Dificuldade). Morreu em 35% das partidas.
- **Tempo para derrotar um inimigo comum adequado** (Balanceamento). Mediana de 1,2 s.

## Números por perfil

| Perfil | Partidas | Venceu | Morreu | Travou/erro | Tempo (mediana) | Pior vida (média) | Dano tomado | Golpes inimigos que acertaram | Esquivas tardias | Recurso cheio | Poções/partida | Itens/partida |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 40 | 65% | 35% | 0% | 23 s | 9% | 228 | 99% | — | 87% | 1,3 | 3,3 |
| Casual | 40 | 100% | 0% | 0% | 17 s | 32% | 135 | 79% | 49% | 3% | 0,9 | 4,3 |
| Veterano | 40 | 100% | 0% | 0% | 17 s | 39% | 119 | 32% | 31% | 1% | 0,6 | 4,1 |
| Agressivo | 40 | 98% | 3% | 0% | 16 s | 35% | 125 | 100% | — | 3% | 0,8 | 4,3 |
| Caótico | 40 | 38% | 63% | 0% | 27 s | 7% | 249 | 64% | 38% | 2% | 1,3 | 2,9 |

- **Novato:** Reação lenta, mira imprecisa, só o ataque básico, não esquiva, equipa só em espaço vazio, ignora a árvore; bebe poção tarde (25% da vida).
- **Casual:** Reação média, usa Ruptura em grupos de 3, esquiva metade das vezes, equipa melhorias e gasta pontos.
- **Veterano:** Reação rápida, mira precisa, esquiva sempre que vê o golpe, Ruptura em grupos de 2, Investida, melhor equipamento.
- **Agressivo:** Reação rápida mas nunca esquiva: troca golpes e gasta Fúria sempre que pode.
- **Caótico:** Metade das decisões é aleatória (andar, mirar e apertar botões a esmo): procura travamentos e exceções.

## Habilidades

| Perfil | Habilidade | Usos | No vazio | Dano | % do dano | Dano por uso | Abates |
|---|---|---|---|---|---|---|---|
| Novato | Espinho Vivo | 2614 | 20% | 36526 | 100% | 14 | 302 |
| Casual | Espinho Vivo | 1478 | 25% | 23393 | 56% | 16 | 232 |
| Casual | Tempestade Verde | 410 | 0% | 18666 | 44% | 46 | 129 |
| Veterano | Espinho Vivo | 1442 | 24% | 23145 | 55% | 16 | 222 |
| Veterano | Tempestade Verde | 425 | 0% | 18914 | 45% | 45 | 139 |
| Agressivo | Espinho Vivo | 1367 | 17% | 23584 | 56% | 17 | 243 |
| Agressivo | Tempestade Verde | 402 | 0% | 18193 | 44% | 45 | 116 |

## Itens

| Perfil | Comum | Mágico | Raro | Lendário | Único | Único Mítico | Ataque do equipamento | Armadura do equipamento | Vida do equipamento | Níveis ganhos |
|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 87 | 27 | 17 | 1 | 0 | 0 | +4,5 | +2,1 | +9,1 | 0,0 |
| Casual | 103 | 37 | 32 | 1 | 0 | 0 | +7,6 | +3,4 | +14,4 | 0,0 |
| Veterano | 104 | 34 | 25 | 0 | 0 | 0 | +7,6 | +2,9 | +14,2 | 0,0 |
| Agressivo | 102 | 41 | 27 | 2 | 0 | 0 | +7,6 | +3,2 | +14,2 | 0,0 |
| Caótico | 66 | 28 | 20 | 1 | 0 | 0 | +3,2 | +1,9 | +8,0 | 0,0 |

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
| Parte de Tempestade Verde no dano do Veterano | ao menos 15% |
| Tempo com ESSÊNCIA cheia | até 15% |
| Golpes no vazio | até 30% |
| Ataque vindo do equipamento | até +60% da base |

## Limites deste teste

- Joga só o que a slice tem: Druida nível 6 (PROVISÓRIO), uma cripta (sombra, espectro, cultista e o Guardião das Cinzas), três habilidades nos espaços (Espinho Vivo, Tempestade Verde, Salto Lupino). Bruxo e Necromante entram na 5.8d2–d4.
- Não avalia visual, som, leitura da tela nem diversão; mede números de jogo. Os perfis são aproximações de jogadores reais.
- Regras, itens e ranks são os do UMBRA. Ficam fora o dano desperdiçado (a partida informa só a vida tirada) e o crítico e o combo na tabela de golpes por rank.
