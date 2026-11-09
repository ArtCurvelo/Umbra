# Relatório do beta tester — 2026-11-09

Versão testada: `07fb29c` (vertical slice: Bárbaro nível 6 na cripta, inimigos da família das criptas no nível do herói, em grupos, com elites e o chefe; regras reais do UMBRA desde a 5.8c). 
200 partidas jogadas por 5 perfis de jogador, sementes 1 a 40, sem janela, uma de cada vez, com as regras do UMBRA portadas para C#. O bot só aperta os mesmos botões de um jogador (andar, mirar, golpes, inventário, árvore).

## Resumo

Prioridade alta: 1 · Prioridade média: 3 · Prioridade baixa: 3

- **A esquiva chega tarde demais** — Preparação mais longa ou arranque mais rápido do movimento.
- **Esquivar quase não compensa** — No UMBRA o acerto cancela a preparação do inimigo comum: golpear protege mais que esquivar. Decisão do Game Director se a esquiva deve valer mais.
- **Inimigos morrem rápido demais** — Mais vida para os inimigos comuns.
- **FÚRIA fica cheia e é desperdiçada** — Outra forma de gastar FÚRIA, ou habilidade 1 útil contra um alvo só.
- **A cripta acaba rápido demais** — Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.

## O que mudar

### [Prioridade alta] A esquiva chega tarde demais
*Combate*. Em 34% das vezes que o Veterano esquivou (reage em 0,13 s), o golpe acertou mesmo assim: sair das casas marcadas levou mais que o resto da preparação (em quadros: Sombra 20, Espectro Velado 17, Cultista 28; o chefe tem a sua).

**Sugestão:** Preparação mais longa ou arranque mais rápido do movimento.

### [Prioridade média] Esquivar quase não compensa
*Combate*. O Veterano (esquiva) tomou 174 de dano por partida; o Agressivo (nunca esquiva), 119. Diferença: -46%.

**Sugestão:** No UMBRA o acerto cancela a preparação do inimigo comum: golpear protege mais que esquivar. Decisão do Game Director se a esquiva deve valer mais.

### [Prioridade média] Inimigos morrem rápido demais
*Balanceamento*. Mediana de 0,5 s do primeiro golpe à morte.

**Sugestão:** Mais vida para os inimigos comuns.

### [Prioridade média] FÚRIA fica cheia e é desperdiçada
*Habilidades*. O Casual (usa Ruptura quando pode) passou 77% da partida com FÚRIA no máximo.

**Sugestão:** Outra forma de gastar FÚRIA, ou habilidade 1 útil contra um alvo só.

### [Prioridade baixa] A cripta acaba rápido demais
*Ritmo*. O Casual limpa a cripta em 15 s (mediana; meta 60–240 s).

**Sugestão:** Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.

### [Prioridade baixa] Ranks de Golpe Brutal que não mudam nada contra o Cultista
*Árvore*. Golpes para derrotar um Cultista por rank (sem crítico nem combo): rank 1 = 3 (25 de dano), rank 2 = 3 (28 de dano), rank 3 = 3 (32 de dano), rank 4 = 3 (35 de dano), rank 5 = 2 (38 de dano), rank 6 = 2 (40 de dano), rank 7 = 2 (42 de dano), rank 8 = 2 (43 de dano), rank 9 = 2 (45 de dano), rank 10 = 2 (46 de dano), rank 11 = 2 (48 de dano), rank 12 = 2 (50 de dano), rank 13 = 2 (51 de dano), rank 14 = 2 (53 de dano), rank 15 = 2 (55 de dano). Os ranks 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

### [Prioridade baixa] Ranks de Ruptura que não mudam nada contra o Cultista
*Árvore*. Golpes para derrotar um Cultista por rank (sem crítico nem combo): rank 1 = 3 (34 de dano), rank 2 = 2 (38 de dano), rank 3 = 2 (42 de dano), rank 4 = 2 (47 de dano), rank 5 = 2 (51 de dano), rank 6 = 2 (53 de dano), rank 7 = 2 (55 de dano), rank 8 = 2 (58 de dano), rank 9 = 2 (60 de dano), rank 10 = 2 (62 de dano), rank 11 = 2 (64 de dano), rank 12 = 2 (66 de dano), rank 13 = 2 (69 de dano), rank 14 = 2 (71 de dano), rank 15 = 2 (73 de dano). Os ranks 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

## O que está bom

- **Nenhuma exceção, travamento ou partida sem fim** (Estabilidade). 200 partidas; 15 repetidas com a mesma semente deram o mesmo resultado.
- **Risco adequado para o Novato** (Dificuldade). Morreu em 38% das partidas.

## Números por perfil

| Perfil | Partidas | Venceu | Morreu | Travou/erro | Tempo (mediana) | Pior vida (média) | Dano tomado | Golpes inimigos que acertaram | Esquivas tardias | Recurso cheio | Poções/partida | Itens/partida |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 40 | 63% | 38% | 0% | 19 s | 11% | 295 | 88% | — | 83% | 1,1 | 3,3 |
| Casual | 40 | 93% | 8% | 0% | 15 s | 27% | 200 | 56% | 44% | 77% | 1,0 | 4,1 |
| Veterano | 40 | 100% | 0% | 0% | 16 s | 34% | 174 | 29% | 34% | 44% | 0,7 | 4,4 |
| Agressivo | 40 | 100% | 0% | 0% | 12 s | 49% | 119 | 66% | — | 3% | 0,2 | 4,5 |
| Caótico | 40 | 78% | 23% | 0% | 24 s | 19% | 281 | 49% | 36% | 2% | 1,4 | 3,8 |

- **Novato:** Reação lenta, mira imprecisa, só o ataque básico, não esquiva, equipa só em espaço vazio, ignora a árvore; bebe poção tarde (25% da vida).
- **Casual:** Reação média, usa Ruptura em grupos de 3, esquiva metade das vezes, equipa melhorias e gasta pontos.
- **Veterano:** Reação rápida, mira precisa, esquiva sempre que vê o golpe, Ruptura em grupos de 2, Investida, melhor equipamento.
- **Agressivo:** Reação rápida mas nunca esquiva: troca golpes e gasta Fúria sempre que pode.
- **Caótico:** Metade das decisões é aleatória (andar, mirar e apertar botões a esmo): procura travamentos e exceções.

## Habilidades

| Perfil | Habilidade | Usos | No vazio | Dano | % do dano | Dano por uso | Abates |
|---|---|---|---|---|---|---|---|
| Novato | Golpe Brutal | 1980 | 19% | 35894 | 100% | 18 | 308 |
| Casual | Golpe Brutal | 1591 | 22% | 35002 | 85% | 22 | 296 |
| Casual | Ruptura | 43 | 0% | 4747 | 12% | 110 | 56 |
| Casual | Investida | 53 | 2% | 1526 | 4% | 29 | 0 |
| Veterano | Golpe Brutal | 1213 | 25% | 25603 | 61% | 21 | 192 |
| Veterano | Ruptura | 199 | 0% | 14727 | 35% | 74 | 168 |
| Veterano | Investida | 65 | 2% | 1729 | 4% | 27 | 1 |
| Agressivo | Golpe Brutal | 878 | 8% | 23017 | 55% | 26 | 206 |
| Agressivo | Ruptura | 416 | 0% | 17698 | 42% | 43 | 155 |
| Agressivo | Investida | 41 | 2% | 1344 | 3% | 33 | 0 |

## Itens

| Perfil | Comum | Mágico | Raro | Lendário | Único | Único Mítico | Ataque do equipamento | Armadura do equipamento | Vida do equipamento | Níveis ganhos |
|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 83 | 23 | 23 | 1 | 0 | 0 | +4,7 | +2,4 | +7,7 | 0,0 |
| Casual | 87 | 42 | 32 | 1 | 0 | 0 | +6,9 | +3,5 | +13,5 | 0,0 |
| Veterano | 111 | 33 | 30 | 0 | 0 | 0 | +7,4 | +3,2 | +13,6 | 0,0 |
| Agressivo | 112 | 34 | 31 | 2 | 0 | 0 | +7,1 | +3,7 | +13,2 | 0,0 |
| Caótico | 87 | 38 | 25 | 3 | 0 | 0 | +6,2 | +2,9 | +11,6 | 0,0 |

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
| Parte de Ruptura no dano do Veterano | ao menos 15% |
| Tempo com FÚRIA cheia | até 15% |
| Golpes no vazio | até 30% |
| Ataque vindo do equipamento | até +60% da base |

## Limites deste teste

- Joga só o que a slice tem: Bárbaro nível 6 (PROVISÓRIO), uma cripta (sombra, espectro, cultista e o Guardião das Cinzas), três habilidades nos espaços (Golpe Brutal, Ruptura, Investida). Bruxo e Necromante entram na 5.8d2–d4.
- Não avalia visual, som, leitura da tela nem diversão; mede números de jogo. Os perfis são aproximações de jogadores reais.
- Regras, itens e ranks são os do UMBRA. Ficam fora o dano desperdiçado (a partida informa só a vida tirada) e o crítico e o combo na tabela de golpes por rank.
