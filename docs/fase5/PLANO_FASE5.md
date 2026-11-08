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
| 5.7 | Inimigos e chefes (IA, habilidades dos chefes) | Próximo |
| 5.8 | Classes, uma por vez (Bárbaro primeiro; Necromante por último, com lacaios e Grimório) | Pendente |
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
