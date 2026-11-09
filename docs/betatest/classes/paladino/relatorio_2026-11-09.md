# Relatório do beta tester — 2026-11-09

Versão testada: `07fb29c` (vertical slice: Paladino nível 6 na cripta, inimigos da família das criptas no nível do herói, em grupos, com elites e o chefe; regras reais do UMBRA desde a 5.8c). 
200 partidas jogadas por 5 perfis de jogador, sementes 1 a 40, sem janela, uma de cada vez, com as regras do UMBRA portadas para C#. O bot só aperta os mesmos botões de um jogador (andar, mirar, golpes, inventário, árvore).

## Resumo

Prioridade alta: 4 · Prioridade média: 3 · Prioridade baixa: 3

- **Difícil demais para quem está começando** — Menos inimigos por grupo, preparação mais longa dos golpes ou mais vida inicial ao Paladino (decisão do Game Director).
- **Até o jogador habilidoso morre** — Algum golpe não é esquivável na prática: revisar o alcance e a preparação dos golpes (inimigos e chefe).
- **O aviso dos golpes não dá tempo de esquivar** — Aumentar a preparação dos golpes (hoje, em quadros: Sombra 20, Espectro Velado 17, Cultista 28).
- **A esquiva chega tarde demais** — Preparação mais longa ou arranque mais rápido do movimento.
- **Punitivo mesmo jogando bem** — Reduzir o dano por golpe ou dar cura entre salas.

## O que mudar

### [Prioridade alta] Difícil demais para quem está começando
*Dificuldade*. O Novato morreu em 83% das partidas (meta: até 40%).

**Sugestão:** Menos inimigos por grupo, preparação mais longa dos golpes ou mais vida inicial ao Paladino (decisão do Game Director).

### [Prioridade alta] Até o jogador habilidoso morre
*Dificuldade*. O Veterano (reage em 0,13 s e esquiva sempre) morreu em 20% das partidas.

**Sugestão:** Algum golpe não é esquivável na prática: revisar o alcance e a preparação dos golpes (inimigos e chefe).

### [Prioridade alta] O aviso dos golpes não dá tempo de esquivar
*Combate*. Mesmo esquivando sempre, 37% dos golpes dos inimigos comuns acertaram o Veterano (468 golpes).

**Sugestão:** Aumentar a preparação dos golpes (hoje, em quadros: Sombra 20, Espectro Velado 17, Cultista 28).

### [Prioridade alta] A esquiva chega tarde demais
*Combate*. Em 36% das vezes que o Veterano esquivou (reage em 0,13 s), o golpe acertou mesmo assim: sair das casas marcadas levou mais que o resto da preparação (em quadros: Sombra 20, Espectro Velado 17, Cultista 28; o chefe tem a sua).

**Sugestão:** Preparação mais longa ou arranque mais rápido do movimento.

### [Prioridade média] Punitivo mesmo jogando bem
*Dificuldade*. O Veterano chegou a 20% da vida no pior momento, em média.

**Sugestão:** Reduzir o dano por golpe ou dar cura entre salas.

### [Prioridade média] Esquivar quase não compensa
*Combate*. O Veterano (esquiva) tomou 257 de dano por partida; o Agressivo (nunca esquiva), 192. Diferença: -33%.

**Sugestão:** No UMBRA o acerto cancela a preparação do inimigo comum: golpear protege mais que esquivar. Decisão do Game Director se a esquiva deve valer mais.

### [Prioridade média] Inimigos morrem rápido demais
*Balanceamento*. Mediana de 0,7 s do primeiro golpe à morte.

**Sugestão:** Mais vida para os inimigos comuns.

### [Prioridade baixa] A cripta acaba rápido demais
*Ritmo*. O Casual limpa a cripta em 16 s (mediana; meta 60–240 s).

**Sugestão:** Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.

### [Prioridade baixa] Ranks de Golpe Sagrado que não mudam nada contra o Cultista
*Árvore*. Golpes para derrotar um Cultista por rank (sem crítico nem combo): rank 1 = 5 (16 de dano), rank 2 = 5 (18 de dano), rank 3 = 4 (20 de dano), rank 4 = 4 (22 de dano), rank 5 = 4 (24 de dano), rank 6 = 3 (25 de dano), rank 7 = 3 (26 de dano), rank 8 = 3 (27 de dano), rank 9 = 3 (28 de dano), rank 10 = 3 (30 de dano), rank 11 = 3 (31 de dano), rank 12 = 3 (32 de dano), rank 13 = 3 (33 de dano), rank 14 = 3 (34 de dano), rank 15 = 3 (35 de dano). Os ranks 2, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

### [Prioridade baixa] Ranks de Julgamento que não mudam nada contra o Cultista
*Árvore*. Golpes para derrotar um Cultista por rank (sem crítico nem combo): rank 1 = 3 (25 de dano), rank 2 = 3 (29 de dano), rank 3 = 3 (32 de dano), rank 4 = 3 (35 de dano), rank 5 = 2 (39 de dano), rank 6 = 2 (40 de dano), rank 7 = 2 (42 de dano), rank 8 = 2 (44 de dano), rank 9 = 2 (45 de dano), rank 10 = 2 (47 de dano), rank 11 = 2 (49 de dano), rank 12 = 2 (51 de dano), rank 13 = 2 (52 de dano), rank 14 = 2 (54 de dano), rank 15 = 2 (56 de dano). Os ranks 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

## O que está bom

- **Nenhuma exceção, travamento ou partida sem fim** (Estabilidade). 200 partidas; 15 repetidas com a mesma semente deram o mesmo resultado.

## Números por perfil

| Perfil | Partidas | Venceu | Morreu | Travou/erro | Tempo (mediana) | Pior vida (média) | Dano tomado | Golpes inimigos que acertaram | Esquivas tardias | Recurso cheio | Poções/partida | Itens/partida |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 40 | 18% | 83% | 0% | 21 s | 2% | 324 | 89% | — | 84% | 1,2 | 1,9 |
| Casual | 40 | 85% | 15% | 0% | 16 s | 23% | 215 | 60% | 44% | 2% | 1,1 | 3,8 |
| Veterano | 40 | 80% | 20% | 0% | 20 s | 20% | 257 | 37% | 36% | 1% | 1,4 | 3,5 |
| Agressivo | 40 | 90% | 10% | 0% | 14 s | 27% | 192 | 83% | — | 3% | 0,9 | 3,7 |
| Caótico | 40 | 33% | 68% | 0% | 25 s | 5% | 327 | 58% | 38% | 1% | 1,4 | 2,4 |

- **Novato:** Reação lenta, mira imprecisa, só o ataque básico, não esquiva, equipa só em espaço vazio, ignora a árvore; bebe poção tarde (25% da vida).
- **Casual:** Reação média, usa Ruptura em grupos de 3, esquiva metade das vezes, equipa melhorias e gasta pontos.
- **Veterano:** Reação rápida, mira precisa, esquiva sempre que vê o golpe, Ruptura em grupos de 2, Investida, melhor equipamento.
- **Agressivo:** Reação rápida mas nunca esquiva: troca golpes e gasta Fúria sempre que pode.
- **Caótico:** Metade das decisões é aleatória (andar, mirar e apertar botões a esmo): procura travamentos e exceções.

## Habilidades

| Perfil | Habilidade | Usos | No vazio | Dano | % do dano | Dano por uso | Abates |
|---|---|---|---|---|---|---|---|
| Novato | Golpe Sagrado | 2208 | 18% | 28867 | 100% | 13 | 244 |
| Casual | Golpe Sagrado | 1626 | 22% | 23871 | 58% | 15 | 195 |
| Casual | Julgamento | 624 | 0% | 16125 | 39% | 26 | 159 |
| Casual | Investida do Templo | 51 | 2% | 1464 | 4% | 29 | 0 |
| Veterano | Golpe Sagrado | 1673 | 29% | 21933 | 55% | 13 | 186 |
| Veterano | Julgamento | 611 | 0% | 15980 | 40% | 26 | 152 |
| Veterano | Investida do Templo | 66 | 2% | 1726 | 4% | 26 | 2 |
| Agressivo | Golpe Sagrado | 1494 | 11% | 24393 | 59% | 16 | 203 |
| Agressivo | Julgamento | 614 | 0% | 15482 | 38% | 25 | 152 |
| Agressivo | Investida do Templo | 43 | 2% | 1361 | 3% | 32 | 0 |

## Itens

| Perfil | Comum | Mágico | Raro | Lendário | Único | Único Mítico | Ataque do equipamento | Armadura do equipamento | Vida do equipamento | Níveis ganhos |
|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 52 | 12 | 10 | 1 | 0 | 0 | +0,9 | +1,5 | +2,6 | 0,0 |
| Casual | 103 | 29 | 19 | 1 | 0 | 0 | +4,6 | +4,4 | +10,1 | 0,0 |
| Veterano | 83 | 32 | 23 | 2 | 0 | 0 | +3,7 | +5,8 | +11,0 | 0,0 |
| Agressivo | 94 | 27 | 26 | 1 | 0 | 0 | +4,6 | +5,3 | +11,3 | 0,0 |
| Caótico | 54 | 19 | 22 | 0 | 0 | 0 | +2,8 | +2,6 | +6,4 | 0,0 |

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
| Parte de Julgamento no dano do Veterano | ao menos 15% |
| Tempo com FÉ cheia | até 15% |
| Golpes no vazio | até 30% |
| Ataque vindo do equipamento | até +60% da base |

## Limites deste teste

- Joga só o que a slice tem: Paladino nível 6 (PROVISÓRIO), uma cripta (sombra, espectro, cultista e o Guardião das Cinzas), três habilidades nos espaços (Golpe Sagrado, Julgamento, Investida do Templo). Bruxo e Necromante entram na 5.8d2–d4.
- Não avalia visual, som, leitura da tela nem diversão; mede números de jogo. Os perfis são aproximações de jogadores reais.
- Regras, itens e ranks são os do UMBRA. Ficam fora o dano desperdiçado (a partida informa só a vida tirada) e o crítico e o combo na tabela de golpes por rank.
