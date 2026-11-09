# Relatório do beta tester — 2026-11-09

Versão testada: `07fb29c` (vertical slice: Ladino nível 6 na cripta, inimigos da família das criptas no nível do herói, em grupos, com elites e o chefe; regras reais do UMBRA desde a 5.8c). 
200 partidas jogadas por 5 perfis de jogador, sementes 1 a 40, sem janela, uma de cada vez, com as regras do UMBRA portadas para C#. O bot só aperta os mesmos botões de um jogador (andar, mirar, golpes, inventário, árvore).

## Resumo

Prioridade alta: 4 · Prioridade média: 3 · Prioridade baixa: 4

- **Difícil demais para quem está começando** — Menos inimigos por grupo, preparação mais longa dos golpes ou mais vida inicial ao Ladino (decisão do Game Director).
- **Até o jogador habilidoso morre** — Algum golpe não é esquivável na prática: revisar o alcance e a preparação dos golpes (inimigos e chefe).
- **O aviso dos golpes não dá tempo de esquivar** — Aumentar a preparação dos golpes (hoje, em quadros: Sombra 20, Espectro Velado 17, Cultista 28).
- **A esquiva chega tarde demais** — Preparação mais longa ou arranque mais rápido do movimento.
- **Punitivo mesmo jogando bem** — Reduzir o dano por golpe ou dar cura entre salas.

## O que mudar

### [Prioridade alta] Difícil demais para quem está começando
*Dificuldade*. O Novato morreu em 98% das partidas (meta: até 40%).

**Sugestão:** Menos inimigos por grupo, preparação mais longa dos golpes ou mais vida inicial ao Ladino (decisão do Game Director).

### [Prioridade alta] Até o jogador habilidoso morre
*Dificuldade*. O Veterano (reage em 0,13 s e esquiva sempre) morreu em 65% das partidas.

**Sugestão:** Algum golpe não é esquivável na prática: revisar o alcance e a preparação dos golpes (inimigos e chefe).

### [Prioridade alta] O aviso dos golpes não dá tempo de esquivar
*Combate*. Mesmo esquivando sempre, 45% dos golpes dos inimigos comuns acertaram o Veterano (385 golpes).

**Sugestão:** Aumentar a preparação dos golpes (hoje, em quadros: Sombra 20, Espectro Velado 17, Cultista 28).

### [Prioridade alta] A esquiva chega tarde demais
*Combate*. Em 35% das vezes que o Veterano esquivou (reage em 0,13 s), o golpe acertou mesmo assim: sair das casas marcadas levou mais que o resto da preparação (em quadros: Sombra 20, Espectro Velado 17, Cultista 28; o chefe tem a sua).

**Sugestão:** Preparação mais longa ou arranque mais rápido do movimento.

### [Prioridade média] Punitivo mesmo jogando bem
*Dificuldade*. O Veterano chegou a 7% da vida no pior momento, em média.

**Sugestão:** Reduzir o dano por golpe ou dar cura entre salas.

### [Prioridade média] Esquivar quase não compensa
*Combate*. O Veterano (esquiva) tomou 224 de dano por partida; o Agressivo (nunca esquiva), 212. Diferença: -6%.

**Sugestão:** No UMBRA o acerto cancela a preparação do inimigo comum: golpear protege mais que esquivar. Decisão do Game Director se a esquiva deve valer mais.

### [Prioridade média] Inimigos morrem rápido demais
*Balanceamento*. Mediana de 0,7 s do primeiro golpe à morte.

**Sugestão:** Mais vida para os inimigos comuns.

### [Prioridade baixa] A cripta acaba rápido demais
*Ritmo*. O Casual limpa a cripta em 15 s (mediana; meta 60–240 s).

**Sugestão:** Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.

### [Prioridade baixa] Ranks de Punhalada que não mudam nada contra o Cultista
*Árvore*. Golpes para derrotar um Cultista por rank (sem crítico nem combo): rank 1 = 5 (15 de dano), rank 2 = 5 (17 de dano), rank 3 = 4 (19 de dano), rank 4 = 4 (21 de dano), rank 5 = 4 (23 de dano), rank 6 = 4 (24 de dano), rank 7 = 3 (25 de dano), rank 8 = 3 (26 de dano), rank 9 = 3 (27 de dano), rank 10 = 3 (28 de dano), rank 11 = 3 (29 de dano), rank 12 = 3 (30 de dano), rank 13 = 3 (31 de dano), rank 14 = 3 (32 de dano), rank 15 = 3 (33 de dano). Os ranks 2, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

### [Prioridade baixa] Ranks de Rajada que não mudam nada contra o Cultista
*Árvore*. Golpes para derrotar um Cultista por rank (sem crítico nem combo): rank 1 = 15 (5 de dano), rank 2 = 13 (6 de dano), rank 3 = 13 (6 de dano), rank 4 = 11 (7 de dano), rank 5 = 10 (8 de dano), rank 6 = 9 (9 de dano), rank 7 = 9 (9 de dano), rank 8 = 9 (9 de dano), rank 9 = 8 (10 de dano), rank 10 = 8 (10 de dano), rank 11 = 7 (11 de dano), rank 12 = 7 (11 de dano), rank 13 = 7 (12 de dano), rank 14 = 7 (12 de dano), rank 15 = 7 (12 de dano). Os ranks 3, 7, 8, 10, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

### [Prioridade baixa] Passo Sombrio erra o alvo com frequência
*Habilidades*. 100% dos usos de Passo Sombrio do Veterano não acertaram ninguém.

**Sugestão:** Normal para habilidades de movimento sem dano; se tiver dano, mirar no inimigo mais próximo da direção.

## O que está bom

- **Nenhuma exceção, travamento ou partida sem fim** (Estabilidade). 200 partidas; 15 repetidas com a mesma semente deram o mesmo resultado.

## Números por perfil

| Perfil | Partidas | Venceu | Morreu | Travou/erro | Tempo (mediana) | Pior vida (média) | Dano tomado | Golpes inimigos que acertaram | Esquivas tardias | Recurso cheio | Poções/partida | Itens/partida |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 40 | 3% | 98% | 0% | 20 s | 0% | 241 | 89% | — | 96% | 1,2 | 1,0 |
| Casual | 40 | 28% | 73% | 0% | 15 s | 5% | 223 | 54% | 43% | 7% | 1,2 | 2,6 |
| Veterano | 40 | 35% | 65% | 0% | 18 s | 7% | 224 | 45% | 35% | 6% | 1,4 | 2,5 |
| Agressivo | 40 | 48% | 53% | 0% | 15 s | 8% | 212 | 82% | — | 9% | 1,3 | 2,9 |
| Caótico | 40 | 0% | 100% | 0% | 0 s | 0% | 240 | 59% | 38% | 10% | 1,1 | 1,3 |

- **Novato:** Reação lenta, mira imprecisa, só o ataque básico, não esquiva, equipa só em espaço vazio, ignora a árvore; bebe poção tarde (25% da vida).
- **Casual:** Reação média, usa Ruptura em grupos de 3, esquiva metade das vezes, equipa melhorias e gasta pontos.
- **Veterano:** Reação rápida, mira precisa, esquiva sempre que vê o golpe, Ruptura em grupos de 2, Investida, melhor equipamento.
- **Agressivo:** Reação rápida mas nunca esquiva: troca golpes e gasta Fúria sempre que pode.
- **Caótico:** Metade das decisões é aleatória (andar, mirar e apertar botões a esmo): procura travamentos e exceções.

## Habilidades

| Perfil | Habilidade | Usos | No vazio | Dano | % do dano | Dano por uso | Abates |
|---|---|---|---|---|---|---|---|
| Novato | Punhalada | 1477 | 16% | 18489 | 100% | 13 | 181 |
| Casual | Punhalada | 1491 | 22% | 19791 | 57% | 13 | 166 |
| Casual | Rajada | 642 | 0% | 15218 | 43% | 24 | 132 |
| Casual | Passo Sombrio | 45 | 100% | 0 | 0% | 0 | 0 |
| Veterano | Punhalada | 1251 | 31% | 15489 | 51% | 12 | 127 |
| Veterano | Rajada | 587 | 0% | 14671 | 49% | 25 | 138 |
| Veterano | Passo Sombrio | 59 | 100% | 0 | 0% | 0 | 0 |
| Agressivo | Punhalada | 1401 | 18% | 19832 | 57% | 14 | 169 |
| Agressivo | Rajada | 615 | 0% | 14818 | 43% | 24 | 138 |
| Agressivo | Passo Sombrio | 42 | 100% | 0 | 0% | 0 | 0 |

## Itens

| Perfil | Comum | Mágico | Raro | Lendário | Único | Único Mítico | Ataque do equipamento | Armadura do equipamento | Vida do equipamento | Níveis ganhos |
|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 23 | 6 | 10 | 0 | 0 | 0 | +0,7 | +0,5 | +1,1 | 0,0 |
| Casual | 66 | 21 | 15 | 1 | 0 | 0 | +2,5 | +1,2 | +4,6 | 0,0 |
| Veterano | 64 | 15 | 19 | 1 | 0 | 0 | +2,9 | +1,4 | +5,2 | 0,0 |
| Agressivo | 59 | 26 | 29 | 1 | 0 | 0 | +3,9 | +1,5 | +7,1 | 0,0 |
| Caótico | 36 | 8 | 6 | 0 | 0 | 0 | +1,5 | +0,6 | +1,3 | 0,0 |

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
| Parte de Rajada no dano do Veterano | ao menos 15% |
| Tempo com ENERGIA cheia | até 15% |
| Golpes no vazio | até 30% |
| Ataque vindo do equipamento | até +60% da base |

## Limites deste teste

- Joga só o que a slice tem: Ladino nível 6 (PROVISÓRIO), uma cripta (sombra, espectro, cultista e o Guardião das Cinzas), três habilidades nos espaços (Punhalada, Rajada, Passo Sombrio). Bruxo e Necromante entram na 5.8d2–d4.
- Não avalia visual, som, leitura da tela nem diversão; mede números de jogo. Os perfis são aproximações de jogadores reais.
- Regras, itens e ranks são os do UMBRA. Ficam fora o dano desperdiçado (a partida informa só a vida tirada) e o crítico e o combo na tabela de golpes por rank.
