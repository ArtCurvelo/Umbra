# Passagem de bastão para o Codex: migração do UMBRA para Godot (Fase 5)

Escrito em 2026-11-09 por Claude Code (Lead Engineer até aqui), a pedido do Game Director.

**Quem lê:** o Codex, que assume como vice-desenvolvedor geral. **O objetivo:** que ele continue sem precisar reconstruir o que já foi decidido, medido ou descoberto.

> **Fonte da verdade:**
> - o código e o `docs/fase5/PLANO_FASE5.md` deste repositório;
> - o `CLAUDE.md` do repositório Python (`umbra-ashes-below`).
>
> Se este documento divergir deles, eles vencem, e este documento deve ser corrigido.

---

## 1. Papéis e regras de trabalho

- **Game Director (GD):** o usuário. Escreve em português e decide visão criativa, balanceamento, identidade visual e prioridades.
  - **Pede aprovação antes:** mudar jogabilidade ou números, telas novas (maquete primeiro), remover funcionalidade, trocar sistemas inteiros, dependências grandes, quebrar saves.
  - **Pode fazer sem perguntar (autonomia técnica):** corrigir bugs, escrever testes, refatorar localmente, ganhar desempenho, documentar.
- **Ritmo combinado:** o GD aprova cada etapa ("Siga para a 5.x"). Ao fim de cada etapa:
  1. testes rodados, com os números reais;
  2. entrega documentada no `PLANO_FASE5.md`;
  3. commit;
  4. pergunta se segue para a próxima.
- **Mudanças visuais:**
  - **Primeiro:** capturar as telas (`tools/telas.ps1` ou `--shot`) e mostrar ao GD.
  - **Depois da aprovação:** mesclar no `main`. O trabalho fica num branch até lá.
- **Regras invioláveis:**
  - **Nunca declarar algo testado sem ter rodado.** Registrar o resultado real, inclusive falhas.
  - **Nunca tocar** em `saves/`, `data/*_settings.json`, `data/controls.json`, `data/ranking.json`, `backups/` ou `.umbra-updates/` do repositório Python.
  - **Git:** commit só como parte de uma etapa aprovada; **nunca dar push**.
  - **Staging:** adicionar só caminhos explícitos, porque outras sessões escrevem na mesma pasta (veja a seção 9).
  - **Assinatura:** commits terminam com a linha de coautoria do agente.
  - **Ambiente:** Windows; use PowerShell ou Git Bash.
- **Testes no repositório Python:** todo arquivo de teste começa com `import _isolation`.

## 2. Repositórios

| Pasta | O que é |
|---|---|
| `C:\Users\silvi\Pictures\Arthur\umbra-ashes-below` | O jogo **Python/pygame** (fonte das regras). Branch `release/0.17.0-reforge`. Plano da migração: `docs/PLANO_GODOT.md` (decisões D1–D5 aprovadas). **Trabalho novo de ferramentas e funcionalidades vai para o Godot, não para cá.** |
| `C:\Users\silvi\Pictures\Arthur\umbra-godot` | **Este repositório:** Godot 4.7.2 .NET + C#. `main` em `5d1bc03`. |
| `C:\Users\silvi\Pictures\Arthur\umbra-godot-58c` | Worktree antiga do branch `fase-5.8c` (já mesclado). Pode ser removida (`git worktree remove`) com o OK do GD. |

**Estrutura do umbra-godot:**
- `src/Umbra.Domain/`: regras em C# puro, sem Godot (net8.0).
- `src/Umbra.BetaTester/`: testador por IA.
- `tests/Umbra.Domain.Tests/`: xUnit (net10.0).
- `game/`: projeto Godot, renderizador **Compatibility** (o T440 do GD só roda esse).
- `tools/`: scripts e harnesses de paridade em `tools/parity/`.
- `docs/`: planos, relatórios, capturas.

## 3. Comandos

```powershell
# verificação completa: xUnit + build do jogo + importação + autoteste do Godot sem janela (~2-3 min)
powershell -ExecutionPolicy Bypass -File tools\check.ps1
# depois do check, desfazer o que a importação reescreve:
git restore game/assets game/project.godot

dotnet test tests\Umbra.Domain.Tests -nologo                     # só o domínio (~35 s)
dotnet build game\Umbra.Game.csproj -nologo -v q                  # só o jogo

# beta tester (relatório Markdown + JSON); classes jogáveis: barbaro, mago, ladino, paladino, druida
dotnet run --project src\Umbra.BetaTester -c Release -- --classe barbaro --runs 40 --seed 1 --saida docs\betatest\<pasta>

# exportar as tabelas do UMBRA (Python do repo do jogo); regrava game/data/umbra/*.json + manifest
..\umbra-ashes-below\.venv\Scripts\python.exe tools\export_umbra_data.py

# capturas: Godot console + argumentos
#   --preset medio --ticks N --freeze --shot <png> --frames 30 [--tela inventario|arvore|config:2|menu|pausa]
```

- **Godot (console):** `%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`. O caminho fica em `tools/godot.ps1`.
- **Autoteste:** `--headless --path game -- --selftest`, com código de saída 0 quando passa.
- **Limite de tempo:** comandos longos podem estourar 10 minutos. Use `timeout` e rode testes pesados em segundo plano.

## 4. O método de paridade (como cada sistema foi portado)

1. **Harness Python:** `tools/parity/<sistema>_parity.py` importa o motor **composto** do UMBRA (`import game.core.engine`; sem isso as classes ficam "cruas") e roda casos escritos à mão e aleatórios. O JSON de cada passo vai para `tests/parity/<sistema>_cases.json`.
   - **Relógio:** `pygame.time.get_ticks` é trocado por um relógio de ticks controlado.
   - **Sorteio:** o `random` global recebe semente por caso.
   - **Ambiente:** `UMBRA_DATA_HOME` aponta para `.export-home`.
2. **Teste C#:** `tests/Umbra.Domain.Tests/Parity<Sistema>Tests.cs` repete as operações e compara **o estado inteiro a cada passo** com `Compare.SameValue`.
3. **Prova de mutação:** inserir erros pequenos no C#, ver os testes falharem e restaurar. Os números entram no plano.
   - **Erros "sobreviventes":** viram casos novos, ou são registrados como equivalentes, com justificativa.

**Fidelidade ao Python (armadilhas já pagas):**
- **Sorteio:** `random` do Python = `Util/PyRandom.cs` (MT19937 com `choice`, `choices` e `sample`). O `choices` ponderado ainda **não** existe; na 5.8c o `bisect` foi feito à mão em `GameSession.Populate`.
- **Arredondamento:** `round` do Python é bancário, use `PyMath.Round`. Já `int()` trunca.
- **`PyDict`:** guarda inteiros como `long` e reais como `double`. A ternária `cond ? long : double` alarga para double; faça cast para `(object)`.
- **Ordem de `set`:** depende da semente de hash; os harnesses gravam a ordem (famílias de armas).
- **Serviços estáticos compartilhados:** `SkillEnv.Rng/Hooks/Clock`, `Resources.AffixShare`, `EnemyAI.Active`.
  - **Nos testes:** os que usam esses serviços vão em `[Collection("SkillEnv")]`.
  - **Ganchos:** encadeiam o anterior.
  - **Partidas simultâneas:** **uma `GameSession` por vez por processo.** O BetaTester roda em sequência por isso.
- **Camadas do Python:** use `tools/layer_map.py Classe.metodo` (no repo Python) para ver a cadeia real de wrappers. Exemplo: `balance.py` substitui a maioria dos métodos do `Player`.
- **Coordenadas:** o centro da casa é o ponto inteiro; na slice, a casa (x, y) é desenhada em (x+.5, y+.5). O herói anda de forma contínua dentro da casa.

## 5. Estado da Fase 5 (o que existe em C#)

| Etapa | Conteúdo | Arquivos principais | Estado |
|---|---|---|---|
| 5.1–5.2 | Dados exportados; estados e dano tipado | `Data/UmbraData.cs`, `Combat/Status.cs`, `Combat/Elements.cs` | paridade |
| 5.3 | Barreira, Fortificação, Orbes, acúmulos | `Combat/Resources.cs` | paridade |
| 5.4 | Motor de habilidades 0.16 | `Combat/V16/*` (Engine, Shapes, Riders, World, WorldTick, Conditions, Contracts com ganchos `ISkillHooks`) | paridade |
| 5.5 | Árvore, ranks, espaços, passivas de classe | `Skills/SkillTree.cs`, `Skills/Tuning.cs`, `Combat/V16/Passives.cs` | paridade |
| 5.6 | Itens da Reforge | `Items/*` | paridade |
| 5.7 | Inimigos, IA, elites, chefes | `Enemies/Enemy.cs`, `EnemyAI.cs`, `BossController.cs` | paridade |
| 5.8a | Núcleo do herói | `Heroes/Hero.cs` | paridade |
| 5.8b | Ataque básico (resolver) e habilidades nos espaços | `Heroes/HeroCombat.cs` | paridade (5 classes sem invocações) |
| 5.8c | A slice com o Bárbaro real; movimento contínuo | `Heroes/Locomotion.cs` (paridade 63/63), `Play/GameSession.cs`, `Play/ItemView.cs`, `Play/SessionPilot.cs`, telas no Godot | **aprovado e no main** |
| Testador | Joga a `GameSession` real | `src/Umbra.BetaTester/*` (`Facts` lê números do jogo) | no main |
| Desafio | Cripta como no UMBRA + chefe + poção + casas de perigo | `GameSession.Populate/BossDeath/Danger/Threat`, HUD (barra do chefe), `ActorLayer` (tintas, casas vermelhas) | **aprovado e no main** |
| 5.8d1 | Mago, Ladino, Paladino e Druida em jogo (domínio e testador) | `GameSession.StartKit`, `--classe` | no main (`5d1bc03`) |
| **5.8d2** | **Bruxo + base dos aliados** | — | **em análise; nenhum código escrito** |
| 5.8d3 | Necromante 1: esqueletos, golem, cadáveres, estruturas, Jaula | — | pendente |
| 5.8d4 | Necromante 2: `necro3`, Grimório, especializações, estados, persistência | — | pendente |
| 5.9 | Mundo: 6 regiões, cidades, teleportes, masmorras por família, dificuldade | — | pendente |
| 5.10 | Saves: conversor de `slot_N.json` (versões 1/2/3), baú, migração da árvore | — | pendente |
| 5.11 | Co-op local | — | pendente |

**Testes no `main`:** xUnit 1 289/1 289, autoteste do Godot 36/36, `check.ps1` aprovado.

**Jogo no Godot:** só o Bárbaro, porque a árvore e o modelo 3D são dele. Escolher a classe no jogo pede maquete e aprovação.

**Como a slice funciona hoje (`Play/GameSession.cs`):**
- **Mapa:** `CryptGenerator` (3 salas). O herói começa no **nível 6 (PROVISÓRIO)**, com o kit da classe aprendido e equipado.
- **População (`Populate`):** a regra de masmorra do UMBRA (`sessions.create_floor`) sobre os 6 pontos de surgimento.
  - **Inimigos:** família das criptas (sombra, espectro, cultista), no nível do herói, até 10, com elites.
  - **Chefe:** Guardião das Cinzas (nível 5) no fundo da câmara.
- **Ordem do `Step`:**
  1. entrada (habilidades e poção disparam ao apertar);
  2. locomoção;
  3. coleta;
  4. `hero.Update`;
  5. inimigos acordados;
  6. mortes (`process_enemy_deaths`, inclusive o ramo do chefe com a relíquia);
  7. estados e recursos;
  8. mundo das habilidades;
  9. eventos para a tela.
- **Eventos (`PlayEvent`):** Damage, Death, Windup, Strike (golpe lançado, acertou ou não), Cast, ItemDropped, ItemTaken, LevelUp, HeroDown, Potion.

## 6. Onde parei: 5.8d2 (Bruxo + base dos aliados)

### 6.1 Descobertas que mudaram o plano

- **Base dos aliados:** o Bruxo depende do sistema de **lacaios**. Imps, demônios superiores, o Portal e os Filhotes de Prótese são `necromancy.Minion`, criados por `combat_system/v16/allies.py` e movidos pela IA de lacaios (`necro_ai.py` + `necro_formation.py`). Por isso a 5.8d2 traz essa base junto.
- **Quem tem aliados temporários:** nenhuma classe sem invocações tem. Só o Bruxo (imp, demon) e o Necromante (spectre). A paridade da base é testada **pelos casos do Bruxo**.
- **Ganchos prontos:** o motor C# já chama todos os ganchos do Bruxo nos lugares certos (`ISkillHooks`: `WarlockAllowed`, `WarlockDamage`, `WarlockBroke`, `WarlockOnHit`, `WarlockOnCast`, `WarlockOnKills`, `TagDamage`, `Shape` para `summon`). Hoje todos são neutros (`NeutralHooks`/`HeroSkillHooks`).
- **Já portado:** `Hero.WarlockHurt` (`warlock.hurt`: Ira por vida perdida e fim da Furtividade). A IA de inimigos C# já trata `stealth` e `taunt`.
- **Estados prontos:** os estados do Bruxo já estão em `Status.Kinds` (demon_form, shadow_form, stealth, legion, brutal, curse, eviscerated, weaken).
- **Neutros para o Bruxo:** os ganchos do Necromante que a IA de lacaios chama (`necro3.ally_factor`, `necro3_summons.ally_hit/ally_removed`, `grimoire_effects.*`) retornam neutros para quem não é Necromante. Podem ficar neutros até a 5.8d3.
- **Linha de visão:** `game.world.dungeon.has_line_of_sight` (usada pela IA de lacaios) é a mesma função já portada como `Geo.LineOfSight`.
- **`guard_mark_bonus`:** só é escrito no caminho anterior à 0.16. Na 0.16 vale sempre 0,30.

### 6.2 O que portar (fonte Python → destino sugerido)

| Python (`umbra-ashes-below/game/...`) | Destino C# sugerido | Observações |
|---|---|---|
| `combat_system/necromancy.py` (`Minion`, `state`, `open_cell`, `spawn_cells`, `next_step`, `relocate`) | `Allies/Minion.cs`, `Allies/Necromancy.cs` | `spawn_cells`: casas a distância de Manhattan ≤ 3, ordenadas por (distância, x, y), sem lacaios, herói ou inimigos vivos (`player._active_enemies`). `next_step`: BFS limitado a 256 nós e 10 passos. `round()` das posições é bancário. |
| `combat_system/necro_formation.py` | `Allies/Formation.cs` | `facing`: a velocidade da `Motion` vence a mira; guarda `_formation_facing`; a tupla `(0, 0)` é "verdadeira" em Python, então o `or` só cai se for `None`. Atenção ao caso `.5` no ramo de `slots` (arredondamento bancário). |
| `combat_system/necro_ai.py` (`update`, `choose_target`, `attack`) | `Allies/MinionAI.cs` | A **retaliação** usa `target.last_attack_at` e janela de 600 ms. É preciso **criar `Enemy.LastAttackAt`** (gravado no início de `PerformAttack`, logo depois de `Sentence`, com `SkillEnv.Clock()`). O chefe não usa `PerformAttack`, então nunca dispara retaliação. A lista `living` é calculada uma vez por tick, sem novo filtro de vivos. |
| `combat_system/v16/allies.py` (`_temporary`, `cast`, `on_removed`, `taunt_around`, `AllyTarget`) | `Allies/Summons.cs` | `AllyTarget` precisa implementar `IFoeTarget` (a provocação dos inimigos o usa como alvo). Esqueleto e golem ficam para a 5.8d3 (lançar erro explícito). |
| `combat_system/warlock.py` (todo) | `Combat/Warlock.cs` | Constantes: **exportar**, não redigitar (ex.: `'WARLOCK': {k: v for k, v in vars(warlock).items() if k.isupper()}` em `heroes()` do exportador; `FRAGMENTS` vira lista ordenada). `random.random()` da Evisceração = `SkillEnv.Rng.Random()`. |
| `skills/specializations.py` | `Skills/Specializations.cs` | Estado por herói: `{quest, shards, active, fragments}`. Exportar `SHARDS` e `UNLOCK_LEVEL`. |
| `skills/tags.py` | `Skills/Tags.cs` | `tags(row, class)`, `damage_factor` (gancho `TagDamage`), `add_buff`, `tick`. |
| `skills/v16/passives.py` (`minion_health`, `minion_damage`, `ally_lost`) | `Combat/V16/Passives.cs` | `minion_damage` usa o valor `king` com 4 ou mais lacaios vivos; `ally_lost` usa `pact_barrier`. |
| `combat_system/status.py` (`warlock.protected` no `apply`) | `Combat/Status.cs` | Logo depois do teste de `unstoppable`: controle num herói (`hasattr(target, 'player_class')`) → `warlock.protected`, e devolve 0 se ele recusar. |

- **Campos novos no `Hero`:**
  - lista `Minions` e `Specialization`;
  - estado do Bruxo: `warlock_dominance`, `abodian_ticks`, `legion_free`, `lesser_lost`, `legion_count`, `pup_timer`, `jailer_carry`, `spent`;
  - outros: `tag_bonus`, `tag_buffs`, `_formation_facing`, `fallen_allies`, `v16_pact_ticks` (se ainda não existir).
- **Campo novo no `Enemy`:** `WarlockCurses`.
- **Ordem do tick no UMBRA (`game/core/lifecycle.py::update`), que a `GameSession` e o harness devem seguir:**
  1. simulação: `p.update`, inimigos, mortes;
  2. `status.tick(p)`;
  3. `tags.tick(p)`;
  4. `warlock.tick(engine, p)`;
  5. `resources.tick(p)` e `tick_orbs`;
  6. `necro3.tick` (neutro para o Bruxo);
  7. estruturas (neutro);
  8. `poll_hold` e o mundo v16;
  9. **por último**, `necromancy.update(engine)`, a IA dos lacaios (via `ossuary.after_update`, só se o herói vive).
- **Catálogo do Bruxo:**
  - **Base:** as 9 aprovadas em `skills.json` (bruxo/skills).
  - **Reforge:** 6 em `skills.json` (bruxo/extra): `warlock2_forma_demoniaca`, `warlock2_forma_sombra` e `warlock2_invocar_{aegron,abodian,lalish,valok}` (exigem o estilhaço ativo).
  - **Árvore:** tem nós para elas.
  - **Portal:** `warlock2_portal` é Suprema `summon` (demon 10 s, ou 3 imps na opção T).
- **Kit pela regra da 5.8d1:** Toque, Fenda, Passo. Não usa demônios: o kit do Bruxo na slice talvez precise de decisão do GD.

### 6.3 Harness proposto para a paridade

- **Arquivo:** `tools/parity/warlock_parity.py`, que **reaproveita** `combat_parity.py` (importa `Dungeon`, `Engine`, `snap`, `run`; o `main` dele está protegido). Os casos vão para `tests/parity/warlock_cases.json`.
- **Tick estendido:** `status.tick` → `tags.tick` → `warlock.tick` → `resources.tick` → mundo v16 → `necromancy.update(engine)`.
- **Operações novas:**
  - `spec`: liga um estilhaço e um fragmento direto em `player.specialization`;
  - `hurt`: chama `p.take_damage(n)`;
  - `control`: aplica `stun` no herói, para testar a Sacrificial;
  - mais todas as da 5.8b.
- **Estado gravado:**
  - **herói:** o da 5.8b mais os campos do Bruxo;
  - **lacaios:** x e y em float, vida e máximo, cooldown, lifetime, frame, papel, variante, demon, scale, `target_cell`, `ai_state`, subjugação, intangível, `attack_flash`;
  - **inimigos:** o da 5.8b mais `warlock_curses` e `last_attack_at`.
- **Casos:**
  - à mão: os 12 pares estilhaço:fragmento, cada um com formas, demônios, Portal, Maldição, Fenda/Chama e muitos ticks de IA;
  - aleatórios: ~80 casos.
- **Teste C#:** `ParityWarlockTests.cs`, espelhando `ParityCombatTests.cs`.

### 6.4 Depois da paridade

- **Jogo:** pôr o Bruxo na `GameSession` (lacaios no `Step`, na ordem acima).
- **Testador:** entender lacaios (o dano de lacaios hoje não seria atribuído a nenhuma habilidade).
- **Testes:** `check.ps1`; testador `--classe bruxo`.
- **Entrega:** documentar no `PLANO_FASE5.md` e fazer commit.
- **Godot:** desenhar os lacaios exige decisão visual do GD (só há os modelos do Bárbaro e do Bruto).

## 7. Decisões pendentes do Game Director (não decidir sozinho)

1. **Esquivas tardias (34% no Veterano):** as preparações da família das criptas são curtas (17–28 quadros).
2. **Esquiva vs. golpe:** esquivar compensa menos que golpear, porque no UMBRA todo acerto cancela a preparação do inimigo comum e o atordoa por 4 quadros.
3. **Nível inicial 6 (PROVISÓRIO) e o kit por classe (PROVISÓRIO):** precisam de confirmação.
4. **Balanceamento por classe:** o Ladino é fraco na cripta (Novato vence 3%, Veterano 35%); o Paladino sofre com o Novato. Os números são do UMBRA, não do port.
5. **Projéteis:** só voam em 8 direções e erram alvos fora delas.
6. **Teclas da poção:** R / direcional esquerdo (PROVISÓRIO), porque Q/RB, as do UMBRA, já são Ruptura e câmera na slice.
7. **Tintas por tipo de inimigo (PROVISÓRIO até a fase de arte):** só existem os modelos do Bárbaro e do Bruto.
8. **Rótulo cortado:** "CONCUSSÃO" não cabe no espaço de arma da tela de personagem.
9. **Passivas de classe (`class:*`) na árvore do Godot:** ainda não aparecem; precisam de maquete.
10. **Regras mortas ou estranhas do UMBRA:**
    - a armadura óssea +45 da Fase II do Colosso nunca dispara;
    - o caminho do golpe básico anterior à 0.16 é alcançável num caso raro (portado só no caso da linha vazia).
11. **Outras decisões abertas da migração:** brilho, objetos do preset Baixo, texturas CC0, números PROVISÓRIOS em geral.

## 8. Riscos e dívidas conhecidas

- **Executável exportado:** as tabelas são lidas do disco (`res://data/umbra` globalizado). Um executável exportado precisa levar a pasta junto (Fase 6).
- **Invocações do chefe:** `process_summons` não foi portado; o Guardião das Cinzas não invoca.
- **`PyRandom.Choices` ponderado:** não existe.
- **Gancho `Hooks.Redirect` do `Hero`:** o desvio para o Colosso de Ferro é da 5.8d3/d4.
- **Fim de linha:** `.gitattributes`/`autocrlf` geram avisos de LF→CRLF no add; são inofensivos.

## 9. Outras sessões e arquivos de terceiros

- **Sessão do BetaTester (outra sessão de agente):** deixou **`docs/betatest/relatorio_2026-11-09.md` e `docs/betatest/dados_2026-11-09.json` não versionados** no `main`. Não são meus, não os apague nem faça commit deles. A mudança do testador para a `GameSession` já foi feita (commit `882c5b8`). Os relatórios novos ficam em pastas próprias: `docs/betatest/regras_reais`, `desafio`, `classes/<classe>`.
- **Antes de mexer em `src/Umbra.BetaTester`:** confira se aquela sessão ainda está ativa.

## 10. Onde está o histórico detalhado

- **`docs/fase5/PLANO_FASE5.md`:** tabela de estado, pendências por sistema e uma seção "Entrega" por etapa (testes, mutação, decisões).
- **`docs/betatest/*`:** relatórios do testador. **`docs/fase5/telas_58c*`:** telas aprovadas.
- **`git log --oneline`:** um commit por etapa, com mensagem descritiva.
- **No repositório Python:** `CLAUDE.md` (arquitetura do jogo original), `docs/PLANO_GODOT.md` (decisões da migração) e `docs/REFORGE_AUDITORIA.md` e `docs/NECRO_REBORN_AUDITORIA.md` (Bruxo e Necromante).
