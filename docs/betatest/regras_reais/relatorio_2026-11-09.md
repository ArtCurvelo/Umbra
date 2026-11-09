# Relatório do beta tester — 2026-11-09

Versão testada: `8c470a0` (vertical slice: Bárbaro nível 6 na cripta com 6 Brutos Abissais, regras reais do UMBRA desde a 5.8c). 
200 partidas jogadas por 5 perfis de jogador, sementes 1 a 40, sem janela, uma de cada vez, com as regras do UMBRA portadas para C#. O bot só aperta os mesmos botões de um jogador (andar, mirar, golpes, inventário, árvore).

## Resumo

Prioridade média: 4 · Prioridade baixa: 3

- **Sem risco nem para o Novato** — Decisão do Game Director: mais inimigos por sala, nível inicial menor, dificuldade maior, ou rever o cancelamento da preparação por acerto.
- **Fácil demais para quem joga bem** — Grupos maiores, um segundo tipo de inimigo ou golpes do Bruto que exijam mais de uma esquiva.
- **Inimigos morrem rápido demais** — Mais vida para o Bruto.
- **Fúria fica cheia e é desperdiçada** — Ruptura útil contra um alvo só, ou outra forma de gastar Fúria.
- **A cripta acaba rápido demais** — Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.

## O que mudar

### [Prioridade média] Sem risco nem para o Novato
*Dificuldade*. O Novato morreu em 0% das partidas, só com o ataque básico e sem esquivar (meta: ao menos 10%). Terminou com 96% da vida em média no pior momento. Cada golpe do Bruto que acertou tirou em média 21 de 230 de vida (ataque do Bruto 20, armadura do Bárbaro 9). No UMBRA todo acerto num inimigo comum cancela a preparação do golpe dele e o atordoa por 4 quadros: quem golpeia sem parar quase não é atingido.

**Sugestão:** Decisão do Game Director: mais inimigos por sala, nível inicial menor, dificuldade maior, ou rever o cancelamento da preparação por acerto.

### [Prioridade média] Fácil demais para quem joga bem
*Dificuldade*. O Veterano terminou com 98% da vida no pior momento, em média (meta: 30%–75%).

**Sugestão:** Grupos maiores, um segundo tipo de inimigo ou golpes do Bruto que exijam mais de uma esquiva.

### [Prioridade média] Inimigos morrem rápido demais
*Balanceamento*. Mediana de 0,5 s do primeiro golpe à morte.

**Sugestão:** Mais vida para o Bruto.

### [Prioridade média] Fúria fica cheia e é desperdiçada
*Habilidades*. O Casual (usa Ruptura com 3+ inimigos no cone) passou 71% da partida com a Fúria no máximo.

**Sugestão:** Ruptura útil contra um alvo só, ou outra forma de gastar Fúria.

### [Prioridade baixa] A cripta acaba rápido demais
*Ritmo*. O Casual limpa a cripta em 10 s (mediana; meta 60–240 s).

**Sugestão:** Mais inimigos ou uma terceira sala; para a slice, pode ficar assim.

### [Prioridade baixa] Ranks de Golpe Brutal que não mudam nada contra o Bruto
*Árvore*. Golpes para derrotar um Bruto por rank (sem crítico nem combo): rank 1 = 5 (25 de dano), rank 2 = 4 (28 de dano), rank 3 = 4 (32 de dano), rank 4 = 3 (35 de dano), rank 5 = 3 (38 de dano), rank 6 = 3 (40 de dano), rank 7 = 3 (42 de dano), rank 8 = 3 (43 de dano), rank 9 = 3 (45 de dano), rank 10 = 3 (46 de dano), rank 11 = 3 (48 de dano), rank 12 = 3 (50 de dano), rank 13 = 2 (51 de dano), rank 14 = 2 (53 de dano), rank 15 = 2 (55 de dano). Os ranks 3, 5, 6, 7, 8, 9, 10, 11, 12, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

### [Prioridade baixa] Ranks de Ruptura que não mudam nada contra o Bruto
*Árvore*. Golpes para derrotar um Bruto por rank (sem crítico nem combo): rank 1 = 3 (34 de dano), rank 2 = 3 (38 de dano), rank 3 = 3 (42 de dano), rank 4 = 3 (47 de dano), rank 5 = 2 (51 de dano), rank 6 = 2 (53 de dano), rank 7 = 2 (55 de dano), rank 8 = 2 (58 de dano), rank 9 = 2 (60 de dano), rank 10 = 2 (62 de dano), rank 11 = 2 (64 de dano), rank 12 = 2 (66 de dano), rank 13 = 2 (69 de dano), rank 14 = 2 (71 de dano), rank 15 = 2 (73 de dano). Os ranks 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 gastam ponto sem tirar nenhum golpe.

**Sugestão:** Normal com ranks de ganho pequeno (+12% por passo; do 6 ao 15, meio passo). Se incomodar, é balanceamento do UMBRA (Game Director).

## O que está bom

- **Nenhuma exceção, travamento ou partida sem fim** (Estabilidade). 200 partidas; 15 repetidas com a mesma semente deram o mesmo resultado.

## Números por perfil

| Perfil | Partidas | Venceu | Morreu | Travou/erro | Tempo (mediana) | Pior vida (média) | Dano tomado | Golpes do Bruto que acertaram | Esquivas tardias | Fúria cheia | Itens/partida |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 40 | 100% | 0% | 0% | 12 s | 96% | 6 | 100% | — | 80% | 1,4 |
| Casual | 40 | 100% | 0% | 0% | 10 s | 98% | 1 | 50% | 0% | 71% | 1,4 |
| Veterano | 40 | 100% | 0% | 0% | 9 s | 98% | 0 | 0% | 0% | 50% | 1,5 |
| Agressivo | 40 | 100% | 0% | 0% | 8 s | 98% | 0 | 0% | — | 0% | 1,4 |
| Caótico | 40 | 100% | 0% | 0% | 18 s | 93% | 12 | 27% | 10% | 0% | 1,2 |

- **Novato:** Reação lenta, mira imprecisa, só o ataque básico, não esquiva, equipa só em espaço vazio, ignora a árvore.
- **Casual:** Reação média, usa Ruptura em grupos de 3, esquiva metade das vezes, equipa melhorias e gasta pontos.
- **Veterano:** Reação rápida, mira precisa, esquiva sempre que vê o golpe, Ruptura em grupos de 2, Investida, melhor equipamento.
- **Agressivo:** Reação rápida mas nunca esquiva: troca golpes e gasta Fúria sempre que pode.
- **Caótico:** Metade das decisões é aleatória (andar, mirar e apertar botões a esmo): procura travamentos e exceções.

## Habilidades

| Perfil | Habilidade | Usos | No vazio | Dano | % do dano | Dano por uso | Abates |
|---|---|---|---|---|---|---|---|
| Novato | Golpe Brutal | 1104 | 15% | 24480 | 100% | 22 | 240 |
| Casual | Golpe Brutal | 799 | 13% | 22819 | 93% | 29 | 234 |
| Casual | Ruptura | 8 | 0% | 861 | 4% | 108 | 6 |
| Casual | Investida | 40 | 0% | 800 | 3% | 20 | 0 |
| Veterano | Golpe Brutal | 608 | 11% | 17202 | 70% | 28 | 181 |
| Veterano | Ruptura | 88 | 0% | 6435 | 26% | 73 | 58 |
| Veterano | Investida | 42 | 0% | 843 | 3% | 20 | 1 |
| Agressivo | Golpe Brutal | 454 | 6% | 12546 | 51% | 28 | 179 |
| Agressivo | Ruptura | 260 | 0% | 11134 | 45% | 43 | 61 |
| Agressivo | Investida | 40 | 0% | 800 | 3% | 20 | 0 |

## Itens

| Perfil | Comum | Mágico | Raro | Lendário | Único | Único Mítico | Ataque do equipamento | Armadura do equipamento | Vida do equipamento | Níveis ganhos |
|---|---|---|---|---|---|---|---|---|---|---|
| Novato | 30 | 18 | 7 | 1 | 0 | 0 | +3,3 | +1,6 | +4,0 | 0,0 |
| Casual | 26 | 18 | 13 | 0 | 0 | 0 | +3,7 | +1,4 | +3,9 | 0,0 |
| Veterano | 32 | 14 | 12 | 0 | 0 | 0 | +3,9 | +1,3 | +4,6 | 0,0 |
| Agressivo | 24 | 22 | 11 | 0 | 0 | 0 | +3,3 | +1,7 | +4,0 | 0,0 |
| Caótico | 28 | 14 | 7 | 0 | 0 | 0 | +3,0 | +1,0 | +3,3 | 0,0 |

## Metas usadas (PROVISÓRIO)

São suposições do testador sobre a experiência desejada; o Game Director decide as metas reais (`src/Umbra.BetaTester/Analyst.cs`, classe `Targets`).

| Meta | Valor |
|---|---|
| Mortes do Novato | 10%–40% |
| Mortes do Veterano | até 5% |
| Pior vida do Veterano | 30%–75% |
| Tempo do Casual na cripta | 60–240 s |
| Golpes do Bruto que acertam o Veterano | até 30% |
| Esquivas que chegam tarde (o golpe acerta mesmo assim) | até 25% |
| Tempo para derrotar um Bruto | 1,0–5,0 s |
| Parte da Ruptura no dano do Veterano | ao menos 15% |
| Tempo com Fúria cheia | até 15% |
| Golpes no vazio | até 30% |
| Ataque vindo do equipamento | até +60% da base |

## Limites deste teste

- Joga só o que a slice tem: Bárbaro nível 6 (PROVISÓRIO), Brutos Abissais, uma cripta, três habilidades nos espaços. Outras classes entram com a 5.8d.
- Não avalia visual, som, leitura da tela nem diversão; mede números de jogo. Os perfis são aproximações de jogadores reais.
- Regras, itens e ranks são os do UMBRA. Ficam fora o dano desperdiçado (a partida informa só a vida tirada) e o crítico e o combo na tabela de golpes por rank.
