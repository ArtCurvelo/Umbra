# Fase 5 — migração dos sistemas do UMBRA para C#

Pedido do diretor criativo: "Fase 5: migração" (2026-11-07).
Regras do documento de direção:
- portar por módulos;
- manter a mesma regra de comportamento;
- preservar os saves (ou migrá-los por processo testado e reversível);
- não apagar funcionalidades.

**O jogo Python continua sendo o jogo jogável** até o C# alcançá-lo.

## Método (vale para todo módulo)
1. **Dados nunca são redigitados.**
   - `tools/export_umbra_data.py` lê o repositório do UMBRA e grava `game/data/umbra/*.json`, com um manifesto (commit de origem e SHA-256).
   - O teste `DataTests` acusa qualquer arquivo alterado à mão.
2. **Paridade comprovada, não presumida.**
   - Um roteiro em `tools/parity/` executa sequências de operações **no código Python real do UMBRA** e grava o retorno e o estado completo depois de cada passo.
   - O port em C# repete as mesmas sequências e precisa chegar aos mesmos números.
   - Os casos são escritos à mão para cada regra, mais centenas de sequências aleatórias com semente.
3. **Prova de mutação.** Erros pequenos inseridos de propósito no port precisam fazer a paridade falhar. Isso garante que o teste mede alguma coisa.
4. **Fiel primeiro, melhor depois.**
   - O port mantém as regras e os nomes do Python, inclusive o arredondamento bancário e a ordem de inserção.
   - Qualquer mudança de regra passa pelo diretor criativo, depois da paridade.
5. **O que depende de sistemas ainda não portados** fica registrado no próprio módulo, como gancho ou pendência, nunca escondido.

## Tamanho real do UMBRA (linhas de Python)
| Pacote | Arquivos | Linhas | Destino |
|---|---|---|---|
| combat_system (inclui o motor v16 de habilidades) | 29 | 4 943 | domínio C# |
| skills (catálogos, árvore, graus, Grimório) | 34 | 3 330 | dados exportados + domínio |
| world (regiões, cidades, masmorras, passagem) | 24 | 2 521 | domínio + cenas Godot |
| core (motor, ciclo de vida, composição) | 17 | 2 365 | substituído pela arquitetura nova (sem monkeypatch) |
| equipment | 24 | 1 517 | domínio |
| entities | 5 | 1 160 | domínio |
| input | 8 | 1 255 | já refeito na Fase 4 (regras mantidas) |
| coop | 7 | 964 | domínio + Godot (por último) |
| persistence | 9 | 890 | conversor de saves |
| sound | 9 | 653 | WAVs reaproveitados; mixagem no Godot |
| ui | 82 | 10 349 | refeito no Godot (Fases 3 e 4) |

## Ordem e estado
| # | Módulo | Estado |
|---|---|---|
| 5.1 | Dados (classes, inimigos, chefes, itens, catálogos de habilidades 0.16, tabelas de regras) | **Feito** (2026-11-08) |
| 5.2 | Estados de combate e dano tipado (`status.py`, `elements.py`) | **Feito, com paridade** (2026-11-08) |
| 5.3 | Recursos compartilhados (Barreira em camadas, Fortificação, Orbes de Sangue, acúmulos: `resources.py`) | **Feito, com paridade** (2026-11-08) |
| 5.4 | Motor de habilidades v16 (formas, efeitos, condições, mundo por tick) e opções C/E/T, para as 5 classes sem invocações | **Feito, com paridade** (2026-11-08) |
| 5.4b | O resto do motor: invocações, cadáveres e Jaula (`allies.py`, formas `summon`/`corpse`/`cage`), junto com o Bruxo e o Necromante | Entra na 5.8 |
| 5.5 | Árvore e graus reais, espaços de habilidade, passivas de classe | **Feito, com paridade** (2026-11-08) |
| 5.6 | Itens da Reforge: quedas, raridades, afixos, espaços de equipamento, páginas da mochila, gemas, engastes, têmpera, encantamento, reciclagem, reparo, Códice, joalheiro, materiais | **Feito, com paridade** (2026-11-08) |
| 5.7 | Inimigos e chefes: criação por andar, IA (percepção, perseguição, memória, recuo, ataque com preparação), estados, elites, dormência, os 8 chefes (fases, 12 habilidades, armadura óssea, zonas) | **Feito, com paridade** (2026-11-08) |
| 5.8a | Núcleo do herói (todas as classes): atributos com equipamento, nível e experiência, recurso, dano recebido, poções, movimento, investida, passo por tick, coletas | **Feito, com paridade** (2026-11-09) |
| 5.8b | Ataque básico com preparação (resolver) e habilidades usadas pelos espaços | **Feito, com paridade** (2026-11-09) |
| 5.8c | A slice com o Bárbaro real (graus, itens e inimigos reais no lugar dos PROVISÓRIOS) | **Feito** (2026-11-09), telas aprovadas |
| 5.8d | Classes uma a uma nas suas regras próprias: d1 as 4 classes sem invocações em jogo; d2 Bruxo; d3 Necromante (invocações, cadáveres, estruturas e Jaula, 5.4b); d4 Necromante (`necro3`, Grimório, especializações) | d1 **feito** (2026-11-09); d2 próximo |
| 5.9 | Mundo: as 6 regiões, cidades, teleportes, masmorras por família, dificuldade | Pendente |
| 5.10 | Saves: conversor dos `slot_N.json` (versões 1, 2 e 3 e seções) para o formato novo, sem tocar nos originais | Pendente |
| 5.11 | Co-op local | Pendente |

## Pendências registradas pelo que já foi portado
- **`Status.Apply`:** falta a proteção "Sacrificial" do Bruxo (`warlock.protected`). Entra com a classe (5.8).
- **`Elements.DefenseFactor`:** resolvido na 5.5. `Passives.Use` instala o gancho `Elements.IncomingFactor` (Pele Endurecida, Guardião).
- **`Elements.Deal`:**
  - falta a recompensa de abate de alvo marcado (`skill_procs.mark_reward`). Ela depende de campos que o herói grava (`_marked_target`, `mark_kill_resource`), e por isso entra com as classes (5.8);
  - som e lista de eventos de acerto são apresentação e são ligados no Godot.
- **`Resources`:**
  - os afixos de item do Necromante (`necro_affixes.share`: barreira, fortificação) chegam pelo gancho `Resources.AffixShare` (5.6);
  - os bônus de sacrifício do Grimório (`grimoire.bonus`: velocidade de ataque, armadura) chegam por `Resources.GrimoireBonus` (5.8);
  - ganhar recurso ao beber um orbe é regra do herói (`IHero.GainResource`: bônus da árvore e passivas de classe), ligada com o herói (5.8; os fatores já foram portados na 5.5);
  - desenhar os orbes é apresentação (Godot); a posição do herói em tiles (`IHero.X/Y`) é ligada à slice com o mundo (5.9).
- **Motor de habilidades (`Combat/V16`):** o que pertence a outras etapas chega por `ISkillHooks` (padrão neutro em `NeutralHooks`):
  - passivas de classe (dano, duração, cura, barreira, custo em vida, corrente, eventos de acerto/lançamento/abate/esquiva): 5.5;
  - bônus por etiqueta, Grimório e afixos de item: 5.6;
  - regras do Bruxo e do Necromante (ações, condições, zonas, formas `summon`/`corpse`/`cage`, ações `swap`/`ally_intangible`): 5.8 (5.4b).
- **Árvore e passivas (`Skills/SkillTree.cs`, `Combat/V16/Passives.cs`):**
  - a slice ainda usa os graus PROVISÓRIOS (`SkillRanks`) e a tela de árvore da Fase 4 com 3 habilidades. Ela passa para a árvore real quando o Bárbaro for ligado às regras reais (5.8), porque graus sem o motor real não mudariam nada no combate;
  - o ponto por nível (`level_up` → `add_point`), a recuperação de recurso (`regen_factor`) e o ganho de recurso (`resource_gain_factor`) são chamados pelo herói (5.8). As funções já estão portadas e conferidas;
  - recalcular atributos depois de uma compra (`refresh_stats`) e cancelar o golpe em preparação são do herói (`ITreeOwner`, 5.8);
  - as passivas de lacaios (`minion_health`, `minion_damage`, `ally_lost`) entram com as invocações (5.8);
  - converter a árvore dos saves antigos (`respec.migrate`) é a 5.10.
- **Itens (`Items/`):**
  - o baú compartilhado por mundo (`inventory/stash.py`) é um arquivo à parte do save do herói e entra com os saves (5.10);
  - os atributos do herói a partir do equipamento (`equipment/stats.recalculate`), a comparação de itens e os efeitos dos aspectos lendários (`legendary.install`: golpe básico, dano recebido, poções, esquiva) são do herói (5.8). Por enquanto `IGearOwner.RefreshStats` só sincroniza os apelidos do equipamento, que é a parte de `recalculate` que mexe em itens;
  - a tabela de raridade das dificuldades Difícil e Extremo (`world.difficulty.rarity`) entra com o mundo (5.9); por enquanto vale a rolagem do Normal;
  - a slice ainda usa os itens PROVISÓRIOS (`ItemRules`); ela passa para os itens reais junto com o Bárbaro (5.8);
  - aparência do item no modelo 3D (`skin_geometry`, `appearance.signature`) é apresentação.
- **Inimigos (`Enemies/`):**
  - estruturas do Necromante (gaiolas e muros que bloqueiam e podem ser atacados) e a Sentença Espectral chegam por `IFoeHooks` (5.8);
  - a criação dos inimigos de uma região (quais tipos, onde, elites sorteadas com o gerador da região, escala de dificuldade `difficulty.scale`) e as cidades são o mundo (5.9). Por enquanto o santuário só tem o gancho `IMap.Sanctuary`/`Gate`;
  - a morte dos inimigos (XP, ouro, quedas, `process_enemy_deaths`) e as invocações pedidas pelo chefe (`process_summons`) são do motor (5.8/5.9);
  - `EnemyAI.Active` faz o papel de `dungeon._active_enemies` (a lista de inimigos do mapa, definida pelo motor antes do passo dos inimigos).
- **Herói (`Heroes/Hero.cs`):**
  - o ataque básico (`resolver`: preparação, golpe, combo, cancelamento) chega por `IHeroHooks` e é a 5.8b. Por enquanto só correm os seus temporizadores;
  - o desvio de dano para o Colosso de Ferro (`necro_specializations.redirect`) e os passos do Bruxo e do Necromante no tick (`warlock.tick`, `necro3.tick`) vêm com essas classes;
  - morte, renascimento, Hardcore e desgaste por morte são do motor e dos saves (5.9/5.10);
  - `character_id` (uuid) é gerado na criação e não entra na paridade.
- **Fora do domínio (apresentação, no Godot):** `skill_started_at`, som do lançamento, `del motion` do Retorno.
- **Ligação com a slice:** o combate da slice ainda usa o modelo da Fase 2. Pôr o Bárbaro nas regras reais (cooldowns, recurso, golpe básico com preparação) é a 5.8.
- **Gatilhos que o jogo chama e o domínio não decide:**
  - o canal dura enquanto a tecla está segura (`world.poll_hold` → `SkillState.Hold`), ligado à entrada no Godot;
  - a recarga por tick de `skill_cooldowns` é do herói (UMBRA a faz no `update` do jogador), entra com as classes.

## Entrega 5.1 + 5.2 (2026-11-08)
- **Arquivos novos:**
  - `tools/export_umbra_data.py`, `tools/parity/status_parity.py`;
  - `game/data/umbra/*.json` (exportados do UMBRA `release/0.17.0-reforge` @ `425c03c`);
  - `src/Umbra.Domain/Data/UmbraData.cs`;
  - `src/Umbra.Domain/Combat/Combatant.cs`, `Status.cs`, `Elements.cs`;
  - testes `DataTests.cs` e `ParityStatusTests.cs`, mais `tests/parity/status_cases.json`.
- **Testes: xUnit 211/211.**
  - 34 anteriores;
  - 3 de dados: exportação íntegra, 7 classes e 77 habilidades, os números da slice iguais aos do UMBRA, raridades;
  - **173 sequências de paridade (4 566 operações) iguais ao Python**, mais 1 de constantes.
- **Prova de mutação:**
  - pressão de chefe de 0,25 para 0,26: 4 sequências falham;
  - arredondamento comum no lugar do bancário: 110 falham;
  - restaurado: tudo passa.
- **Correção encontrada pelo teste de integridade:** no Windows, `write_text` trocava as quebras de linha por CRLF e o hash do manifesto não batia. O exportador agora grava os bytes exatos, e os JSON exportados são marcados como binários no git.
- **Sem efeito no jogo ainda:** o combate da slice continua com o modelo simples da Fase 2. Os estados e o dano tipado portados entram em jogo quando as habilidades reais forem portadas (5.4).

## Entrega 5.3 (2026-11-08)
- **Arquivos novos:**
  - `tools/parity/resources_parity.py` e `tests/parity/resources_cases.json`;
  - `src/Umbra.Domain/Combat/Resources.cs` (`IHero`, `BloodOrb`, Barreira em camadas, Fortificação, acúmulos, Orbes de Sangue);
  - teste `ParityResourcesTests.cs`.
- **Exportação:** as constantes de `resources.py` entram em `rules.json` (seção `resources`); um teste confere o C# com elas.
- **Paridade: 127 sequências (3 692 operações) iguais ao Python.** Depois de cada passo são conferidos:
  - o retorno;
  - vida, recurso, Fortificação (reserva, temporizador, resto) e estados de cada entidade;
  - os orbes no chão.
- Os casos escritos à mão cobrem:
  - afixo e limite na vida máxima, mistura com a barreira única da 0.16;
  - cura por segundo, expiração, herói cheio, herói morto;
  - limites de acúmulo e bônus do Grimório;
  - alcance e limite de 24 orbes.
- Os valores dos ganchos (afixos, Grimório) foram calculados pelo código real do UMBRA para cada entidade.
- **Testes: xUnit 339/339; autoteste do Godot 35/35.**
- **Prova de mutação** (cada mutação foi restaurada depois, e tudo volta a passar):
  - resto da Fortificação arredondado em vez de truncado: 12 sequências falham;
  - limite de 25 orbes em vez de 24: 1 falha;
  - alcance do orbe de 0 tile em vez de 1: 20 falham;
  - Fortificação durando 1 tick a menos: 108 falham.
- **Sem efeito no jogo ainda:** nenhuma habilidade da slice gera Barreira, Fortificação, orbes ou acúmulos. Eles entram com as habilidades (5.4) e as classes (5.8).

## Entrega 5.4 (2026-11-08)
- **Arquivos novos:**
  - `src/Umbra.Domain/Util/PyRandom.cs`: o gerador do `random` do Python (Mersenne Twister), sorteio por sorteio, para a paridade;
  - `src/Umbra.Domain/Skills/PyDict.cs` (habilidade como dados abertos, com `get` e verdade do Python) e `Tuning.cs` (graus e opções C/E/T);
  - `src/Umbra.Domain/Combat/V16/`: `Geometry.cs` (com a linha de visão), `Conditions.cs`, `Contracts.cs` (`ITarget`, `ISkillUser`, `SkillState`, `ISkillHooks`), `Engine.cs` (contexto, dano, custos, recargas, cargas, `Cast`, `Strike`), `Shapes.cs`, `Riders.cs`, `World.cs` e `WorldTick.cs` (zonas, torres, minas, golpes atrasados, efeitos com fim, auras, canais, mortes, estados do herói);
  - `tools/parity/skills_parity.py`, `tests/parity/skills_cases.json`, testes `ParitySkillsTests.cs` e `PyRandomTests.cs`.
- **Paridade: 198 sequências (8 550 operações) iguais ao Python.** Depois de cada passo são conferidos:
  - o retorno e a habilidade resolvida (grau e opções);
  - o herói inteiro: vida, recurso, posição, estados, recargas, cargas, acúmulos, imbuição, rampa, zonas, eventos, efeitos, auras e canal;
  - cada um dos 7 inimigos.
- **Cobertura:**
  - todas as 45 habilidades do Bárbaro, Mago, Ladino, Paladino e Druida, cada uma com 10 combinações de opções e graus de 1 a 6;
  - 150 sequências aleatórias com graus de 1 a 15;
  - casos de santuário, recurso insuficiente, mira, paredes e linha de visão, e projéteis extras em leque.
- **Ganchos verificados como neutros:** o roteiro embrulha cada função das passivas, etiquetas, Grimório, afixos, Bruxo e Necromante, e falha se alguma devolver algo diferente do neutro ou mexer no herói. Assim os casos medem só o motor.
- **Gerador aleatório:** 6 sementes conferidas contra o CPython 3.14 (`random`, `randint`, `choice`).
- **Testes: xUnit 543/543; autoteste do Godot 35/35.**
- **Prova de mutação:**

  | Erro inserido | Sequências que falham |
  |---|---|
  | graus +13% em vez de +12% | 180 |
  | pulso de zona a cada 31 ticks | 142 |
  | estado próprio da aura 1 tick mais curto | 40 |
  | queda da corrente já no 1º alvo | 35 |
  | crítico garantido ×1,4 | 3 |
  | meteoros sorteados em −5..4 | 3 |
  | chance dos efeitos ×0,9 | 2 |
  | cone dos projéteis extras 0,55 em vez de 0,3 | 1 |

- **Mutações sem efeito possível** (nenhuma entrada consegue distingui-las, por isso foram trocadas por outras na mesma linha):
  - o padrão 0,85 da queda da corrente: os catálogos sempre dão o valor;
  - o cone 0,30 contra 0,31: nenhum tile inteiro ao alcance cai entre os dois;
  - `>=` contra `>` na chance: só difere se o sorteio cair exatamente no valor.
- **Falha que o teste de mutação encontrou na primeira versão dos casos:** nenhum caso exercitava o cone dos projéteis extras. Foi acrescentado o caso do leque da Rajada.
- **Sem efeito no jogo ainda:** o combate da slice continua com o modelo da Fase 2. Ligar o Bárbaro às regras reais é a 5.8.

## Entrega 5.5 (2026-11-08)
- **Dados:** `game/data/umbra/trees.json`. A árvore de cada uma das 7 classes foi exportada de um herói real do motor composto do UMBRA (todas as camadas instaladas, catálogos 0.16 ativos), na ordem dos nós, com o nível que `requirements.required_level` exige para cada nó. Também saem os tetos de efeito, os graus máximos e o número de espaços.
- **Arquivos novos:**
  - `src/Umbra.Domain/Skills/SkillTree.cs`:
    - `TreeCatalog` e `SkillTree`: compra, graus 1–15, exclusivos, nível exigido, efeitos com teto, lados das passivas, devolução;
    - `TreeRules`: `unlock_skill`, `refund`, os cinco espaços com uma Suprema só, e as opções escolhidas lidas da árvore;
  - `src/Umbra.Domain/Combat/V16/Passives.cs`:
    - as passivas de classe: valores, fatores, bônus temporários e eventos de acerto, lançamento, abate, esquiva e tick;
    - `PassiveHooks` liga as passivas ao motor de habilidades;
  - `tools/parity/tree_parity.py`, `tests/parity/tree_cases.json`, `ParityTreeTests.cs`.
- **Paridade da árvore: 107 sequências (6 954 operações) num herói real de cada classe**, com o `get_effect` composto pelo `balance.py`. A cada passo são conferidos:
  - pontos, nós comprados, investimentos e graus (até 15);
  - lados das passivas e os cinco espaços;
  - todos os totais de efeito e as opções ativas de cada habilidade;
  - os 29 valores de passiva e os 16 fatores.
- **Paridade do motor com passivas reais: mais 120 sequências** (24 por classe, passivas em graus aleatórios), entre elas Convergência, Último Bastião, Alternância, Instinto, Sequência, Pós-esquiva e o critério de crítico. O motor tem agora 318 sequências (14 070 operações). Nelas o relógio do Python (`pygame.time.get_ticks`) segue os ticks, como o `SkillEnv.Clock` do C#.
- **Testes: xUnit 771/771; autoteste do Godot 35/35.**
- **Prova de mutação** (10 erros inseridos, 10 detectados):

  | Erro inserido | Sequências que falham |
  |---|---|
  | passiva no grau 3 valendo como grau 2 | 83 |
  | nível exigido estrito (`<=`) | 42 |
  | passiva valendo sem a habilidade equipada | 30 |
  | modificadores deixam de ser exclusivos | 27 |
  | Último Bastião com recarga de 44 s | 16 |
  | Convergência com janela de 4 s | 10 |
  | devolução de grau devolvendo até 2 pontos | 8 |
  | grau máximo 14 | 7 |
  | duas Supremas equipadas ao mesmo tempo | 2 |
  | Sede com limiar de 40% | 2 |

- **Sem efeito no jogo ainda:** a slice continua com os graus PROVISÓRIOS e o combate da Fase 2 (ver pendências).

## Entrega 5.6 (2026-11-08)
- **Dados:** `game/data/umbra/equipment.json`, com todas as tabelas de itens lidas depois de o motor ser composto. Duas camadas mudam valores:
  - `balance.py` troca os multiplicadores de raridade (1,12 / 1,26 / 1,62, não os brutos de `items.py`);
  - `legendary.install` completa os nomes de aspectos da forja.
- **Arquivos novos em `src/Umbra.Domain/Items/`:**
  - `ItemTables.cs`: raridades, níveis de item e campos da Reforge (durabilidade, qualidade, engastes, têmpera, aparência escolhida);
  - `Loot.cs`: quedas, afixos do Necromante, aspecto lendário e aparência do item;
  - `Gear.cs`: espaços por classe, migração de saves antigos, apelidos, equipar e desequipar, páginas da mochila e itens perdidos;
  - `Crafting.cs`: materiais, gemas, têmpera, encantamento, reciclagem e reparo, forja, Códice, guarda-roupa e joalheiro.
- **Ferramentas e testes:** `tools/parity/items_parity.py`, `tests/parity/items_cases.json` e `ParityItemsTests.cs`.
- **A queda real tem três camadas, e o port segue a mesma ordem de sorteios:**
  1. `create_data` original. Monta o dicionário de itens simples a cada chamada, então sorteia ouro e essência mesmo para uma espada.
  2. O rebalanceamento de `balance.py`.
  3. `forge.attach_aspect` de `core/compatibility.py`, que sorteia um aspecto da forja para armas Lendárias.

  Depois disso, `loot.roll` refaz o registro e guarda só a raridade.
- **Não determinístico no Python:**
  - `loot.roll` sorteia a família da arma de um `set`, cuja ordem muda com o hash de strings do processo. O roteiro grava a ordem que viu, e o C# usa essa ordem na paridade;
  - no jogo em C#, a ordem é a dos espaços da classe.
- **Inteiro e decimal:** o Python distingue `5` de `5.0`, e a qualidade arredonda os dois de jeitos diferentes. `PyDict` passou a guardar inteiros como `long`. A comparação dos itens é estrita no tipo.
  - A prova de tipo encontrou três lugares onde o `?:` do C# transformava `long` em `double`: a leitura do JSON, o afixo maximizado e a soma de gemas.
- **Paridade: 90 sequências (5 602 operações) iguais ao Python.** Elas rodam em heróis reais das 7 classes com `Item(...)` real, e a cada passo conferem:
  - ouro, nível e materiais;
  - as quatro páginas da mochila;
  - o equipamento, os apelidos (por espaço) e os itens perdidos;
  - o registro do mundo, os aspectos ativos, os bônus de gemas e os afixos do Necromante.
- **Testes: xUnit 863/863; autoteste do Godot 35/35.**
- **Prova de mutação: 14 erros inseridos, 14 detectados.**

  | Erro inserido | Sequências que falham |
  |---|---|
  | bônus elemental do Raro de 6% | 71 |
  | preço de venda ×4 por nível | 65 |
  | valor da arma 0,27 por nível | 63 |
  | encantamento começando no 2º preço | 52 |
  | sem a camada `attach_aspect` | 11 |
  | segundo anel sempre no espaço 1 | 4 |
  | pilha de gemas aceitando 100 | 3 |
  | foco sem tirar o cajado do mago | 2 |
  | arredondamento bancário na qualidade | 1 |
  | gema no último engaste livre | 1 |
  | têmpera sem gastar redefinição | 1 |
  | Códice aprendendo aspecto de qualquer raridade | 1 |
  | alcance de cadáver +1 desde o nível 50 | 1 |
  | espada de duas mãos sem tirar o escudo | 1 |

- **Mutações sem efeito possível (registradas):**
  - o limite do rebalanceamento da arma: `loot.roll` descarta o valor rebalanceado e guarda só a raridade;
  - aceitar uma pilha cheia (99) para fundir: o espaço livre é 0, então o resultado é o mesmo.
- **Lacunas que a prova de mutação revelou nos primeiros casos:** conflitos de mãos, aspecto em item Raro reciclado, afixos do Necromante perto do nível 60 e pilhas que enchem pela metade. Ganharam casos próprios.
- **Sem efeito no jogo ainda:** a slice continua com os itens PROVISÓRIOS (ver pendências).

## Entrega 5.7 (2026-11-08)
- **Dados:** `game/data/umbra/foes.json`. Traz os 14 tipos de inimigo, os 8 chefes e sua ordem, os multiplicadores por andar, as elites e o alcance de dormência.
- **Arquivos novos em `src/Umbra.Domain/Enemies/`:**
  - `Enemy.cs`: o inimigo como o motor composto o monta. `Update` passa pelas camadas nesta ordem:
    1. estados compartilhados (`necro_status`): atordoar, congelar, prisão, lentidão, Gelidez e provocação;
    2. guarda do santuário;
    3. o próprio inimigo.
  - `Enemy.cs` também tem o dano (para chefes: mitigação por fase, teto por golpe, armadura óssea), a preparação e a resolução do ataque (cegueira, Muralha de Fé refletindo tiros), as elites e a dormência;
  - `EnemyAI.cs`: a IA dos inimigos comuns (`ai.py`) e os caminhos:
    - o campo de distâncias com a busca local limitada;
    - a busca em largura simples dos chefes;
    - a evacuação do santuário.
  - `BossController.cs`: fases, escolha de habilidade por chefe e distância, preparação, bloqueio por parede, as 12 habilidades, zonas de corrupção e teleporte.
- **Ferramentas e testes:** `tools/parity/enemies_parity.py`, `tests/parity/enemies_cases.json` e `ParityEnemiesTests.cs`.
- **Paridade: 138 sequências (5 787 operações) com inimigos reais do motor composto.** Elas cobrem:
  - os 14 tipos;
  - chefes em 10 andares (os 8 chefes e os ciclos acima do andar 20);
  - todos os estados de controle, as elites e a dormência;
  - 110 sequências aleatórias.

  A cada passo são conferidos todos os campos de cada inimigo e do herói. Todas as 12 habilidades de chefe aparecem nos casos.
- **Testes: xUnit 1 001/1 001; autoteste do Godot 35/35.**
- **Prova de mutação: 14 erros detectados.**

  | Erro inserido | Sequências que falham |
  |---|---|
  | vida por andar contando o 1º andar | 136 |
  | memória de 179 quadros | 127 |
  | atordoamento por dano de 3 quadros | 116 |
  | elite com 2,4× de vida | 37 |
  | busca local de caminho limitada a 16 | 34 |
  | teto por golpe do chefe de 25% | 26 |
  | acólito virando atirador | 18 |
  | mitigação do chefe de 10% na fase I | 16 |
  | preferência do chefe invertida só colado | 12 |
  | zona de corrupção a cada 31 quadros | 7 |
  | recuo sem linha de visão no desempate | 6 |
  | lentidão necromântica de 25% | 6 |
  | dormência a 30 em vez de 31 casas | 2 |
  | fase II a 55% | 1 |

- **Mutações sem efeito possível (registradas):**
  - o tiro à queima-roupa: o ataque à distância só começa a 2+ casas e só acerta se o herói continuar na casa marcada;
  - a armadura óssea extra da fase II (`bone_armor += 45` em `check_phase`) **nunca acontece no UMBRA**: o golpe que derruba o Colosso abaixo de 50% sempre gasta a armadura antes. É uma regra inerte no original, mantida igual. Vale a decisão do diretor criativo se ela deveria funcionar.
- **Lacunas que a prova de mutação revelou nos primeiros casos:** nenhum caso parava um chefe entre 50% e 55% da vida, e nenhum mapa permitia um inimigo a exatamente 30 casas. Ganharam casos próprios.
- **Sem efeito no jogo ainda:** a slice continua com o inimigo simples da Fase 2. A ligação acontece com o Bárbaro e o motor (5.8).

## Entrega 5.8a (2026-11-09)
- **Divisão da 5.8:**
  - 5.8a: núcleo do herói, comum a todas as classes;
  - 5.8b: ataque básico e habilidades nos espaços;
  - 5.8c: a slice passando para o Bárbaro real;
  - 5.8d: as regras próprias de cada classe.
- **Dados:** `game/data/umbra/heroes.json`. Traz os atributos de classe ao vivo (depois de `balance.py`), os recursos de cada classe e a Ira do Bruxo.
- **`src/Umbra.Domain/Heroes/Hero.cs`:** um só `Hero` serve o motor de habilidades, os itens, a árvore, os inimigos e os recursos. Segue as cadeias compostas do UMBRA:
  - **criação:** 5 camadas;
  - **`recalculate_stats`:** equipamento, depois `compatibility` (+ forja antiga), depois `balance`, depois passivas (vida) e auras (recurso);
  - **`take_damage`:** aspectos lendários, depois santuário, depois `balance` (intangível, Último Fôlego, guarda, barreira, escudo), depois o original (redução, ×1,38, armadura, defesa), e por fim a Ira do Bruxo;
  - **`gain_xp`:** `balance` reduz para 58% e depois sobe de nível pela curva da Reforge;
  - **`get_attack_damage`:** `balance` com os aspectos lendários. Sorteia o crítico sempre, mesmo com chance 0;
  - **outros:** poção (com Bastião), movimento, investida (com Passos do Vento e Passo do Eclipse), coleta e o tick.
- **Ganchos ligados:**
  - os afixos de item do herói real entram nos recursos (`Resources.AffixShare`) e no motor de habilidades (`HeroSkillHooks`);
  - os ganchos compartilhados passaram a encadear, para que classes de teste em paralelo não se sobrescrevam;
  - os testes do motor e do herói ficam na mesma coleção do xUnit, porque usam o mesmo `SkillEnv`.
- **Paridade: 119 sequências (7 672 operações) com heróis reais das 7 classes.**
  - Cobrem níveis até 70, dano com cada camada, recurso, regeneração, poções, movimento e investida, estados, acúmulos, Fortificação, passivas de classe, equipamento, 8 aspectos lendários e coletas.
  - A cada passo são conferidos todos os atributos, temporizadores, estados e contadores do herói.
- **Testes: xUnit 1 130/1 130** (inclui os do beta tester, de outra sessão); **autoteste do Godot 35/35.**
- **Prova de mutação: 16 erros inseridos, 16 detectados.**

  | Erro inserido | Sequências que falham |
  |---|---|
  | experiência a 60% | 119 |
  | ataque base a cada 3 níveis | 115 |
  | vida +9 por nível | 115 |
  | curva de nível 1,5 | 107 |
  | poção curando 45% | 68 |
  | dano recebido ×1,40 | 61 |
  | amuleto contando duas vezes | 44 |
  | sem o sorteio de crítico com chance 0 | 40 |
  | investida de 2 casas | 25 |
  | defesa absorvendo 60% | 24 |
  | intangível levando dano | 23 |
  | teto da arma 0,35 por nível | 17 |
  | regeneração só acima de 1 | 16 |
  | Bastião de 10% | 14 |
  | investida dando 2 de Fúria | 7 |
  | guarda ancestral de 15% | 3 |

- **Encontrado pela paridade:** a primeira versão não ligava os afixos de Fortificação do Necromante ao herói real. Três sequências falharam até ligar.

## Entrega 5.8b (2026-11-09)
- **`src/Umbra.Domain/Heroes/HeroCombat.cs`:** porta o `resolver.py` no caminho da 0.16, o `skills.use_skill`, as funções de `loadout` (`attack_slot`, `basic_from_slot`) e as portas de entrada compostas.
  - **O golpe básico:**
    - escolhe o inimigo mais próximo (ou o da mira) e trava a casa marcada;
    - 5 quadros de preparação, recuperação pela velocidade de ataque (com Ferocidade);
    - buffer de 5 quadros e combo de 3 golpes (×1,00 / ×1,07 / ×1,15) com janela de 38 quadros;
    - cancelamento por movimento, dano ou troca de espaço;
    - na resolução, o golpe da 0.16 (`strike`) e o aspecto lendário do golpe básico.
  - **Os espaços:**
    - uma habilidade comum no espaço 1 é conjurada pelo botão de ataque;
    - uma Básica nos espaços 2–5 ataca como o botão;
    - as verificações de recarga e custo vêm antes;
    - o escudo dos aspectos ao conjurar.
  - **`Hero`:** passou a ter o golpe pendente, o buffer, o resultado do último golpe e a recarga global (`skill_cooldown`), que agora é o mesmo campo do motor de habilidades, como no Python.
- **Caminho antigo encontrado pela paridade:**
  - **O que acontece:** o golpe básico anterior à 0.16 parecia inalcançável, mas o UMBRA o executa num caso. Uma Básica usada por um espaço deixa o golpe pendente, o botão de ataque apaga a Básica do espaço, e o espaço 1 tem uma habilidade comum. O golpe então resolve sem Básica, como dano físico simples com 5 de recurso.
  - **No port:** esse caso foi reproduzido igual. Qualquer outro uso do caminho antigo lança erro.
- **Paridade: 105 sequências (13 372 operações) com heróis reais (5 classes sem invocações) contra inimigos reais com a IA da 5.7, no laço completo do motor.**
  - Cobrem 1 981 golpes, 495 habilidades conjuradas, golpes contra parede e casa vazia, buffer, cancelamentos e aspectos lendários no golpe e ao conjurar.
  - A cada passo são conferidos o herói inteiro e cada inimigo.
- **Testes: xUnit 1 235/1 235; autoteste do Godot 35/35.**
- **Prova de mutação: 13 erros detectados.**

  | Erro inserido | Sequências que falham |
  |---|---|
  | ataque sem escolher a arma da Básica | 99 |
  | preparação de 6 quadros | 82 |
  | recuperação de 13 quadros | 82 |
  | botão de ataque mantendo a Básica do espaço | 75 |
  | janela de combo de 37 | 63 |
  | golpe básico com 1 de recurso a menos | 57 |
  | cancelamento deixando 8 de recarga | 38 |
  | janela de buffer de 4 | 9 |
  | 2º golpe do combo ×1,08 | 7 |
  | habilidade exigindo recurso acima do custo | 5 |
  | alvo procurado só até 7 casas | 3 |
  | escudo ao conjurar de 10% (caso acrescentado depois da primeira rodada) | 2 |
  | alvo pelo passo antes da distância | 1 |

- **Mutação sem efeito possível (registrada):** o atordoamento de 4 quadros do caminho antigo. O próprio dano do inimigo já aplica 4 quadros em inimigos comuns; só faria diferença em chefes.

## Entrega 5.8c (2026-11-09) — telas aprovadas pelo Game Director e mescladas no main
- **A slice joga com o Bárbaro real.** Saíram da partida os graus, os itens e o inimigo PROVISÓRIOS da Fase 2. A `Simulation` antiga continua no repositório só para o BetaTester, até ele mudar para a sessão nova.
- **`src/Umbra.Domain/Play/GameSession.cs`:** a partida no laço do motor do UMBRA.
  - **Início:** herói real (`Hero`), Brutos Abissais reais (`EnemyActor`, IA da 5.7) e o mapa da cripta.
  - **Ordem de cada tick:** entrada, locomoção, coleta ao mudar de casa, `hero.Update`, inimigos (os dormentes não rodam), mortes, estados, recursos, orbes e mundo das habilidades.
  - **Mortes (`process_enemy_deaths`):** XP real, Fúria +5, orbe de recurso 55%, quedas 5/15/9/8/6% (poção, ouro, arma, armadura, acessório).
  - **Eventos de apresentação:** dano, morte, preparação, conjuração, item no chão, item recolhido, nível, herói caído.
  - **`Discard`:** largar um item da mochila no chão. É ação só da slice: o UMBRA vende ou recicla.
- **`src/Umbra.Domain/Heroes/Locomotion.cs`:** movimento contínuo dentro da casa (`world/locomotion.py`: velocidade 4,8, raio 0,21, zona morta 0,22, deslize nas paredes). Paridade: 63/63 casos (`tools/parity/motion_parity.py`).
- **Godot:**
  - **Partida, HUD e atores (`Main`, `Hud`, `ActorLayer`):** leem a sessão por id.
  - **Personagem:** os 12 espaços reais do Bárbaro (capacete, peitoral, luvas, calça, botas, amuleto, 2 anéis, concussão, corte, 2 rápidas), mochila de 30 e dicas com atributos, afixos, nível, iLvl e durabilidade. Equipar e guardar passam por `Gear.FromInventory`/`Gear.Unequip`.
  - **Árvore:** a árvore real.
    - Ranks 1–15; nós bloqueados mostram o nível exigido.
    - Dano atual e do próximo rank pelo multiplicador resolvido (`Tuning.Resolve`).
    - As 7 opções de cada habilidade, compráveis e devolvíveis por `TreeRules.Learn/Refund`.
    - Y/R equipa a habilidade em foco.
    - As tabelas do UMBRA (`game/data/umbra`) são lidas no início do jogo.
- **Testes:**
  - xUnit 1 304/1 304, dos quais 6 da sessão: determinismo, laço completo em 3 sementes, Brutos ferem herói parado;
  - autoteste do Godot 36/36: luta, personagem com item real, árvore real com compra e devolução de rank, configurações, menu;
  - `tools/check.ps1` aprovado.
- **Telas para aprovação:** `docs/fase5/telas_58c/` (`combate.png`, `inventario.png`, `arvore.png`). O layout é o das maquetes aprovadas na Fase 4; mudaram o conteúdo, os 12 espaços do boneco e as opções em leque.
- **Para o Game Director:**
  - **Travamento por golpes (regra real do UMBRA):**
    - **A regra:** todo acerto num inimigo comum cancela a preparação dele e o atordoa 4 quadros.
    - **O efeito:** um Bárbaro que não para de golpear (golpes a cada ~14 quadros) quase nunca é atingido por um Bruto (preparação de 35 quadros).
    - **Na prática:** no autoteste a cripta caiu em 9 s com o herói de vida cheia.
    - **Decisão do Game Director:** se a slice deve ter outro desafio (mais inimigos, nível inicial menor, dificuldade).
  - **Nível inicial 6 (PROVISÓRIO):** escolhido para a Ruptura (nível 3) e a Investida estarem disponíveis. O herói começa com 3 pontos livres.
  - **Rótulo cortado:** o rótulo do UMBRA "CONCUSSÃO" não cabe no espaço de 52 px.
  - **Passivas de classe (`class:*`) fora da árvore:** ainda não aparecem na tela e esperam uma maquete.
- **Risco:** as tabelas são lidas do disco (`res://data/umbra` globalizado). Um executável exportado precisa levar a pasta junto (Fase 6, empacotamento).

## BetaTester nas regras reais e fim da `Simulation` (2026-11-09)
- **O testador joga a `GameSession`:** herói, Brutos, itens e árvore reais.
  - **Bot (`BotPlayer`):** lê só o que a tela mostra (preparação do Bruto e a casa marcada, recargas, Fúria). Esquiva saindo da casa marcada, porque `perform_attack` só acerta quem continua nela.
  - **Pontos:** o perfil que gasta pontos sobe ranks por `TreeRules.Learn`.
  - **Equipamento:** passa por `Gear.FromInventory`.
- **Números do relatório:** vêm do jogo (`Facts`: herói, Bruto, custo e ganho das habilidades) ou são medidos na partida (dano por golpe do Bruto que acertou). A tabela de golpes por rank usa `attack_damage` e a armadura do Bruto; um teste confere que o primeiro golpe real tira exatamente o valor do rank 1.
- **Mudanças de medida:**
  - **"Esquivas bloqueadas" virou "esquivas tardias":** no UMBRA andar cancela o próprio golpe, então a esquiva nunca fica presa; agora conta o golpe que acertou mesmo assim.
  - **Dano desperdiçado saiu:** a partida só informa a vida tirada.
  - **Raridades:** passaram a ser as 6 do UMBRA.
- **Execução sequencial:** os serviços das regras (`SkillEnv`, `EnemyAI.Active`) são do processo inteiro, como no UMBRA. 200 partidas levam 19 s.
- **Removidos:**
  - `Umbra.Domain.Gameplay` (Simulation, SliceData, ItemRules, SkillRanks, o autopiloto antigo) e os testes dele;
  - na camada do Godot, as sobrecargas de itens PROVISÓRIOS.
  - `InputFrame` foi para `Umbra.Domain.Play`.
  - O que ainda valia dos testes antigos foi para `GameSessionTests`: gerador da cripta, clicar para andar, largar item.
- **Testes:** xUnit 1 279/1 279 (eram 1 304; saíram os testes da `Simulation`), autoteste do Godot 36/36, `check.ps1` aprovado.
- **Relatório (40 partidas por perfil, sementes 1–40):** `docs/betatest/regras_reais/`.
  - **Resultado:** todos os perfis venceram todas as partidas, com a pior vida média entre 93% e 98%. A cripta cai em 8–18 s e um Bruto morre ~0,5 s depois do primeiro golpe. O Novato quase não é atingido.
  - **Causa:** o travamento por golpes descrito na 5.8c. Ele está nas regras do UMBRA, não no port.
  - **O teste "esquivar reduz o dano" saiu:** sem golpes que acertem, os dois perfis tomam ~0 de dano. Ele volta quando a slice tiver desafio (decisão do Game Director).

## Desafio da slice: cripta como no UMBRA + chefe (2026-11-09) — aprovado pelo Game Director e mesclado no main
- **Decisão do Game Director:** opções A + B, com a família das criptas (sombra, espectro, cultista).
- **População (`GameSession.Populate`):** a regra de masmorra do UMBRA (`world/sessions.create_floor`), aplicada à cripta da slice.
  - **Grupos:** cada um dos 6 pontos de surgimento vale por uma sala: grupo com chance de ≈79%, de 1/2/3 inimigos (pesos 42/40/18), de um tipo só. O limite é de 10 inimigos, o do andar 1.
  - **Nível:** os inimigos têm o nível do herói (`adventure_maps`: `max(1, andar, nível)`).
  - **Elites:** sorteadas por `elites.roll` (5%).
  - **Ajuste da slice:** a cripta tem 3 salas, e não as ~32 do UMBRA. A regra por sala, aplicada literalmente, daria ~3 inimigos.
- **Chefe:** o Guardião das Cinzas (nível 5, `max(5, andar)`) no fundo da câmara.
  - **Morte:** a recompensa é a de `process_enemy_deaths`: Fúria 28, XP e ouro do perfil, poção, ouro, arma, acessório, orbe e a relíquia própria do chefe (Brasa do Primeiro Carrasco).
  - **Exportação:** a relíquia exigiu exportar `RELIC_BY_BOSS`. A exportação regravou só `equipment.json` e o manifesto.
  - **Regra do UMBRA, mantida:** o chefe nunca dorme e usa habilidades a qualquer distância em que tenha linha de visão.
  - **Fora:** as invocações do chefe (`process_summons`) não foram portadas; o Guardião não invoca.
- **Poção:** botão novo (`player.use_potion`, 50% da vida). No UMBRA ela fica em Q/RB, que na slice já são a Ruptura e a câmera. Por isso usa R e o direcional esquerdo (PROVISÓRIO, remapeável). O contador aparece acima do painel.
- **Telas (PROVISÓRIO até a fase de arte):**
  - **Inimigos:** a família usa o modelo do Bruto com tinta e tamanho próprios, e o shader ganhou `tint` (branco = sem mudança). O chefe é maior, com a cor do perfil; elites têm borda dourada.
  - **Casas vermelhas:** marcam onde um golpe vai cair (a casa marcada pelo inimigo; o golpe e a área de habilidade do chefe; as zonas). O vermelho se intensifica com a preparação. `GameSession.Danger`/`Threat`, a tela e o testador leem a mesma coisa.
  - **Barra do chefe:** no topo, com nome, vida, fase e o que ele prepara.
  - **Objetivo:** "Inimigos: x de y — o guardião espera".
- **Testador e autopiloto:**
  - **Caminho:** os dois andam pelo caminho do UMBRA (`Navigation.shared_step`). A linha reta prendia o herói na quina entre o salão e o corredor: a locomoção do UMBRA bloqueia os dois eixos ali, e uma pessoa contorna.
  - **Poções:** os dois bebem; cada perfil tem o seu limite de vida (suposição sobre jogadores).
  - **Esquiva:** o bot também esquiva das áreas do chefe.
- **Testes:**
  - xUnit 1 283/1 283; os novos cobrem a população, a recompensa do chefe, as casas de perigo e o botão de poção.
  - Autoteste do Godot 36/36: o autopiloto limpa a cripta em 14,8 s com 112/230 de vida.
  - `check.ps1` aprovado.
- **Relatório (40 partidas por perfil):** `docs/betatest/desafio/`.

  | Perfil | Antes: venceu / pior vida | Agora: venceu / pior vida | Meta |
  |---|---|---|---|
  | Novato | 100% / 96% | 63% / 11% | morrer 10–40% |
  | Casual | 100% / 98% | 93% / 27% | — |
  | Veterano | 100% / 98% | 100% / 34% | pior vida 30–75% |
  | Agressivo | 100% / 98% | 100% / 49% | — |

- **Para o Game Director:**
  - **Esquivas tardias (34%):** a esquiva chega tarde em 34% das tentativas do Veterano. As preparações da família das criptas são curtas (Sombra 20, Espectro 17, Cultista 28 quadros).
  - **Esquiva vs. golpe:** quem nunca esquiva (Agressivo) toma menos dano que o Veterano, porque no UMBRA o acerto cancela a preparação do inimigo comum.
  - **Nível 6 (PROVISÓRIO):** o herói continua começando no nível 6.
- **Telas para aprovação:** `docs/fase5/telas_58c_desafio/` (combate com o chefe e as casas vermelhas, aba Controles com a Poção).

## Entrega 5.8d1 (2026-11-09): as classes sem invocações em jogo
- **Divisão da 5.8d:**
  - **d1:** Mago, Ladino, Paladino e Druida em jogo. As regras deles já têm paridade desde a 5.8b; o resto de `skill_procs` é o caminho anterior à 0.16, que os catálogos 0.16 não alcançam.
  - **d2:** Bruxo (`warlock.py`, especializações, formas).
  - **d3:** Necromante, parte 1: invocações, IA e formação, cadáveres, estruturas e Jaula (5.4b).
  - **d4:** Necromante, parte 2: `necro3`, Grimório, especializações, estados e persistência.
- **Kit da slice por classe (`GameSession.StartKit`, PROVISÓRIO):** uma regra sobre o catálogo 0.16. Entram a primeira Básica, a primeira Principal que não é canalizada e a habilidade de Mobilidade; o herói começa no nível 6.

  | Classe | Botão de ataque | Habilidade 1 | Habilidade 2 |
  |---|---|---|---|
  | Bárbaro | Golpe Brutal | Ruptura | Investida (igual à 5.8c) |
  | Mago | Projétil Arcano | Bola de Fogo | Teleporte |
  | Ladino | Punhalada | Rajada | Passo Sombrio |
  | Paladino | Golpe Sagrado | Julgamento | Investida do Templo |
  | Druida | Espinho Vivo | Tempestade Verde | Salto Lupino |

- **Autopiloto e testador:** atacam do alcance da Básica, com linha de visão, e usam o kit de cada classe.
  - **Testador:** ganhou `--classe`. Nomes, recurso e números do relatório seguem a classe.
  - **Regra do UMBRA:** projéteis e feixes andam em 8 direções (`geometry.direction`). Quem gasta recurso só lança a habilidade 1 alinhado com o alvo; sem isso, a Bola de Fogo do Veterano errava 63%.
  - **Execução sequencial:** as partidas rodam uma de cada vez, como antes.
- **O Godot continua só com o Bárbaro:**
  - **Árvore:** a tela tem os nós dele.
  - **Modelo:** só existe o dele.
  - **Escolha de classe:** escolher a classe no jogo pede maquete e aprovação.
- **Testes:**
  - xUnit 1 289/1 289. Os novos conferem o kit e uma partida de cada classe, e o testador jogando cada classe de forma determinística.
  - Autoteste do Godot 36/36; `check.ps1` aprovado.
  - O Bárbaro repete exatamente os números do relatório aprovado.
- **Relatórios (40 partidas por perfil):** `docs/betatest/classes/<classe>/`.

  | Classe (vida, ataque, armadura) | Novato venceu | Veterano venceu / pior vida |
  |---|---|---|
  | Bárbaro (190, 22, 8) | 63% | 100% / 34% |
  | Druida (135, 21, 6) | 65% | 100% / 39% |
  | Mago (85, 28, 2) | 35% | 98% / 32% |
  | Paladino (175, 15, 10) | 18% | 80% / 20% |
  | Ladino (115, 15, 4) | 3% | 35% / 7% |

- **Para o Game Director (balanceamento do UMBRA, não do port):**
  - **Ladino:** fraco na cripta no nível 6. Com pouca vida e ataque, a Punhalada tira ~12 por uso.
  - **Paladino:** sofre com o Novato.
  - **Projéteis:** erram alvos fora das 8 direções.
