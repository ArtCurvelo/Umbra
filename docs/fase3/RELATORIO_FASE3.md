# Fase 3 — visual: relatório de entrega (2026-11-07)

Pedido do diretor criativo: "Fase 3: visual". Formato da seção 12 do documento de direção. Guia de estilo resultante:
[../GUIA_VISUAL.md](../GUIA_VISUAL.md). Tudo é procedural e próprio (decisão D2), no renderizador Compatibility (D1).

## 1. O que foi implementado de fato
| Área | Antes (slice) | Agora |
|---|---|---|
| **Piso** | ruído celular só com cor | lajes irregulares com tom por pedra, rejunte, sujeira e escurecimento de contato junto às paredes (mapa de proximidade calculado do mapa) |
| **Paredes** | ruído celular | cantaria em fiadas desencontradas com argamassa, pé úmido e topo escuro; a janela transparente em volta do herói continua |
| **Cenário** | nenhum | pilares no ritmo das paredes, entulho, ossos e crânios, manchas no chão; tudo pela semente do mapa, sem colisão (as regras não mudam) |
| **Tochas** | esfera que brilha | chama em lâmina que tremula, brasas e luz tremulante |
| **Personagens** | material padrão, mais uma passada extra para o lampejo | shader próprio: borda de luz (quente no herói, vermelho-fria nos inimigos), lampejo de acerto e escurecimento ao morrer na mesma passada (uma chamada de desenho a menos por modelo); recuo ao levar golpe |
| **Luz do herói** | não havia | raio de luz quente que acompanha o herói |
| **Efeitos** | leque branco, cone laranja | crescente de aço no Golpe; na Ruptura, onda, rachaduras incandescentes, poeira e clarão; poeira na Investida; faíscas e jato de sangue no acerto; clarões de luz curtos (Médio e Alto); poeira flutuando (Alto) |
| **Câmera** | fixa | tremor curto em impactos fortes; o código já pode desligá-lo (`CameraRig.ShakeEnabled`), falta a opção no menu de configurações (Fase 4) |
| **Pós-processamento** | nenhum | gradação fria nas sombras e quente nas luzes, contraste, vinheta (Médio e Alto) e grão fino (Alto); o HUD fica fora |

**Desempenho, sem mudar nenhuma regra:**
- os efeitos reaproveitam malhas, materiais e luzes, em vez de criar novos a cada golpe;
- durante o carregamento, todos os efeitos e um item de cada raridade são desenhados de forma invisível, para compilar os shaders e o código antes da luta;
- recomeçar a luta reaproveita os modelos;
- o HUD foi dividido em cinco partes que só redesenham quando mudam.

## 2. Arquivos
- **Novos:**
  - shaders: `game/shaders/floor.gdshader`, `actor.gdshader`, `post.gdshader`;
  - scripts: `game/scripts/Props.cs`, `PostProcess.cs`;
  - documentos: `docs/GUIA_VISUAL.md`, `docs/fase3/` (antes/depois, comparações, VFX, este relatório);
  - ferramenta: `tools/shot.ps1`.
- **Alterados:** `wall_cutaway.gdshader`, `Materials.cs`, `WorldView.cs`, `ActorLayer.cs`, `Effects.cs`, `CameraRig.cs`, `Quality.cs`, `Hud.cs`, `Main.cs`, `LaunchOptions.cs`, `Bench.cs`, `Profiler.cs`, `tools/evidence.ps1`.
- **O domínio (regras) não foi tocado.**

## 3. Testes executados e resultados
- **`tools/check.ps1`:** xUnit 28/28; autoteste do Godot 20/20 (combate inteiro, pausa por controle simulado, três presets, duas câmeras).
- **Capturas no mesmo instante congelado:** `fase3/antes/` e `fase3/depois/`, com montagens lado a lado em `comparacao_baixo.png`, `comparacao_medio.png`, `comparacao_alto.png` e `comparacao_camera_topdown.png`.
- **Efeitos** (seguros a 35% da vida com `--fx`): `vfx_golpe.png`, `vfx_ruptura.png`, `vfx_investida.png`.

## 4. Benchmark (ThinkPad T440, Compatibility via ANGLE, 1280x720, 20 s de combate contínuo)
| Preset | Antes: FPS médio / 1% baixo | Depois: FPS médio / 1% baixo | Draw calls antes → depois |
|---|---|---|---|
| Baixo | 59,1 / 29,8 | **47,4 / 28,4** | 99 → 101 |
| Médio | 36,8 / 15,1 | **40,2 / 28,5** | 141 → 102 |
| Alto | 25,3 / 10,9 | **22,4 / 13,8** | 206 → 228 |

**Leitura:**
- **Médio:** ficou mais rápido e muito mais estável. O 1% baixo quase dobrou porque os picos foram eliminados e a sombra da lua saiu dele.
- **Baixo:** perdeu cerca de 12 FPS médios, pelo custo do piso novo, do cenário e da luz do herói, mas manteve o 1% baixo.
- **Alto:** continua abaixo de 30 FPS nesta máquina.
- **Ruído:** entre execuções iguais, a variação é de ±3–5 FPS. Os números de antes estão em `fase3/antes/benchmark`.

**Como cada decisão foi medida** (Médio, eliminando um recurso por vez; média de 15 s):
- sombra da lua: cerca de 7,5 FPS. Saiu do Médio e ficou só no Alto.
- pós-processamento: cerca de 4 FPS. Ficou, porque é o maior ganho visual por custo.
- cenário: cerca de 2 FPS.
- luz do herói: dentro do ruído.

**Picos (antes da correção):**
- morte com item caído: 67 a 104 ms, por compilação de shader e de código na primeira vez;
- recomeço da luta: até 100 ms, por recriar os modelos.

Depois da correção, o Médio ficou sem nenhum quadro acima de 40 ms nos 20 s, e o Baixo com 1.

## 5. Problemas e limitações
- **Modelos:** continuam sem esqueleto, com animação procedural. Personagens com animação de verdade exigem modelagem e rigging (ver seção 7).
- **Rastro do Golpe Brutal:** ainda parece mais um clarão que um corte. Ajusto com a sua opinião depois de jogar.
- **Cena mais escura que antes:** é de propósito (vinheta, gradação e cripta iluminada pelo herói). Se ficar escura demais na sua tela, o ajuste é rápido: vinheta, exposição e luz ambiente.
- **Baixo** caiu para 47 FPS e não segura 60 nesta máquina com o cenário ligado. Opções: desligar o cenário miúdo no Baixo, ou aceitar.
- **Tremor de câmera:** falta a opção no menu de configurações (Fase 4). Pode desconfortar algumas pessoas.
- **Forward+:** os presets de alta fidelidade continuam sem ser vistos, porque esta máquina não roda Forward+.

## 6. Próxima tarefa proposta
Ajustes finos com a sua avaliação (brilho geral, rastro do golpe, cenário no Baixo). Depois, a Fase 4 (interface: menus, inventário, configurações com o tremor e o brilho) ou a Fase 5 (migração dos sistemas).

## 7. Decisões para o diretor criativo
1. **Aprovar a direção visual** (guia em `docs/GUIA_VISUAL.md`): cripta escura, frio contra quente, raio de luz do herói, efeitos curtos.
2. **Brilho:** está no ponto, ou mais claro?
3. **Arte de verdade (licenças são decisão sua).** Proposta para a próxima etapa visual:
   - **Texturas PBR CC0** (domínio público, sem custo) para pedra, madeira e metal, por exemplo da Poly Haven ou da ambientCG. Elas substituem as texturas procedurais e mantêm o guia.
   - **Personagens com esqueleto e animação:** produção própria em Blender (lenta), pacotes CC0 genéricos (rápido, mas de estilo genérico) ou arte encomendada (custo). Recomendo começar pelo CC0 só para validar a técnica de animação e decidir a arte final depois.
4. **Baixo:** desligar o cenário miúdo para voltar a cerca de 55–60 FPS, ou manter como está?
