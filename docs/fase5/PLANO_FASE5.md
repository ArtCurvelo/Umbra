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
| 5.4 | Motor de habilidades v16 (formas, efeitos, condições, mundo por tick, aliados: `combat_system/v16`, 1 439 linhas) e opções C/E/T | Próximo |
| 5.5 | Árvore e graus reais, passivas de classe (substitui os graus PROVISÓRIOS da slice) | Pendente |
| 5.6 | Itens da Reforge: raridades, afixos, gemas, engastes, forja, baú, materiais (substitui os itens PROVISÓRIOS da slice) | Pendente |
| 5.7 | Inimigos e chefes (IA, habilidades dos chefes) | Pendente |
| 5.8 | Classes, uma por vez (Bárbaro primeiro; Necromante por último, com lacaios e Grimório) | Pendente |
| 5.9 | Mundo: as 6 regiões, cidades, teleportes, masmorras por família, dificuldade | Pendente |
| 5.10 | Saves: conversor dos `slot_N.json` (versões 1, 2 e 3 e seções) para o formato novo, sem tocar nos originais | Pendente |
| 5.11 | Co-op local | Pendente |

## Pendências registradas pelo que já foi portado
- **`Status.Apply`:** falta a proteção "Sacrificial" do Bruxo (`warlock.protected`). Entra com a classe (5.8).
- **`Elements.DefenseFactor`:** as passivas de classe chegam pelo gancho `Elements.IncomingFactor` (5.5).
- **`Elements.Deal`:**
  - falta a recompensa de abate de alvo marcado (`skill_procs.mark_reward`), que entra com as habilidades (5.4);
  - som e lista de eventos de acerto são apresentação e são ligados no Godot.
- **`Resources`:**
  - os afixos de item do Necromante (`necro_affixes.share`: barreira, fortificação) chegam pelo gancho `Resources.AffixShare` (5.6);
  - os bônus de sacrifício do Grimório (`grimoire.bonus`: velocidade de ataque, armadura) chegam por `Resources.GrimoireBonus` (5.8);
  - ganhar recurso ao beber um orbe é regra do herói (`IHero.GainResource`: bônus da árvore e passivas de classe), ligada com a árvore (5.5);
  - desenhar os orbes é apresentação (Godot); a posição do herói em tiles (`IHero.X/Y`) é ligada à slice com o mundo (5.9).

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
