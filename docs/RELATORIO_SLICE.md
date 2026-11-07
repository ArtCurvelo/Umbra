# Vertical slice 3D — relatório de entrega (2026-11-07)

Responde à seção 14 do documento de direção técnica e artística e ao plano `umbra-ashes-below/docs/PLANO_GODOT.md`
(decisões D1–D5 aprovadas em 2026-11-07). Formato da seção 12 do documento.

## 1. O que foi implementado de fato
| # | Entrega | Estado |
|---|---|---|
| S1 | Repositório `umbra-godot`: domínio C# puro (`src/Umbra.Domain`), testes xUnit, projeto Godot (`game/`) que referencia o domínio; scripts `tools/check.ps1`, `tools/evidence.ps1` e `tools/jogar.ps1` | Feito |
| S2 | Cripta 3D gerada pela semente (salão, corredor, câmara): piso e paredes de pedra procedurais, tochas com luz tremulante e brasas, luz da lua, névoa, brilho | Feito |
| S3 | Câmera isométrica (52°) e top-down (84°) sobre o mesmo mundo; giro em passos de 45°; zoom de 0 a 100% (30% = 12,2 unidades com campo de visão de 40°, **calibração PROVISÓRIA**); paredes entre a câmera e o herói ficam transparentes | Feito |
| S4 | Movimento por WASD, clique no chão e analógico, relativo à câmera; colisão com deslizamento nas paredes, sem atravessar mesmo em movimento rápido | Feito |
| S5 | Bárbaro com Golpe Brutal, Ruptura e Investida e Bruto Abissal com detecção, perseguição, golpe com aviso no chão (anel que se enche em 35 ticks) e golpe comprometido. Números dos dados do UMBRA. Dá para desviar saindo do alcance. Retorno visual: lampejo, faíscas, números de dano, queda ao morrer | Feito |
| S6 | Itens caem (60% por morte; a última morte garante um, se nada caiu antes); raridade Comum, Mágico, Raro ou Lendário com cor e etiqueta [C]/[M]/[R]/[L]; o herói recolhe ao passar por cima | Feito |
| S7 | HUD "Ferro e Cinza": vida, Fúria, três espaços com custo, recarga e ícone do botão do dispositivo em uso (teclado, Xbox ou PlayStation); objetivo; registro de itens; barras sobre os inimigos; faixas de vitória e de derrota. Menu de pausa operável por teclado, mouse e controle | Feito |
| S8 | Três presets (Baixo, Médio, Alto) no Compatibility; benchmark reproduzível com JSON; capturas comparativas | Feito; os números estão abaixo |

## 2. Arquivos
- **Domínio** (`src/Umbra.Domain`):
  - `Core/Rng.cs`
  - `World/DungeonMap.cs`, `World/CryptGenerator.cs`, `World/Collision.cs`
  - `Gameplay/Data.cs`, `Gameplay/Actors.cs`, `Gameplay/Loot.cs`, `Gameplay/Events.cs`, `Gameplay/Simulation.cs`, `Gameplay/Autopilot.cs`
- **Testes** (`tests/Umbra.Domain.Tests`): `WorldTests.cs`, `CombatTests.cs`, `RunTests.cs`.
- **Godot** (`game/`):
  - `project.godot`, `Umbra.Game.csproj`, `Main.tscn`, `shaders/wall_cutaway.gdshader`
  - `scripts/`: `Main`, `LaunchOptions`, `Bindings`, `CameraRig`, `WorldView`, `Materials`, `ActorLayer`, `Effects`, `UiKit`, `Hud`, `PauseMenu`, `Quality`, `Bench`, `Profiler` e `SelfTest`
  - `assets/models/barbaro.gltf` e `bruto.gltf`: cópias dos modelos procedurais do próprio UMBRA
- **Total:** cerca de 2700 linhas de C#. Nenhum asset de terceiros.

## 3. Testes executados e resultados
- **`tools/check.ps1`:**
  - xUnit **28/28**: geração determinística e conexa; colisão e deslizamento; linha de visão; regra de dano; golpe só no alvo à frente; ritmo do golpe; custo e cone da Ruptura; Investida com recarga; aviso antes do golpe e desvio; detecção; corpos sem sobreposição; distribuição de raridades; replay idêntico com a mesma semente; clique para andar; velocidade de 4,8 tiles/s; e a cripta inteira limpa pelo piloto automático com 5 sementes.
  - **Autoteste no Godot sem janela: 20/20.** Ele cobre:
    - a cripta limpa em 11 s pelo piloto automático, com os itens recolhidos e o HUD e a faixa corretos;
    - um controle simulado: Start abre a pausa e pausa o mundo, dois toques no direcional = dois passos, A troca a qualidade e a câmera, Start e B fecham;
    - os três presets aplicados;
    - a top-down a −84° e a isométrica a −52°.
- **Capturas** (`docs/capturas/`), com renderização real nesta GPU e o mesmo instante congelado:
  - `presets_baixo.png`, `presets_medio.png` e `presets_alto.png`;
  - `camera_topdown.png` e `camera_iso_girada.png` (giro de 90°).

## 4. Benchmark (ThinkPad T440, medido; `docs/benchmark/*.json`)
**Condições:**
- **Hardware:** Intel Core i5-4300U, Intel HD Graphics 4400 (driver 20.19.15.4531).
- **Renderizador:** Compatibility, OpenGL ES 3.0 via ANGLE sobre Direct3D 11. O Godot troca sozinho porque o OpenGL da Intel está na lista de drivers ruins.
- **Janela:** 1280x720, VSync do Godot desligado.
- **Cena:** cripta com semente 7, piloto automático em combate contínuo (a luta recomeça no mesmo mapa quando a cripta fica limpa).
- **Tempo:** 3 s de aquecimento e 20 s medidos.

| Preset | FPS médio | 1% baixo | Quadro p50 / p99 | CPU de renderização | Draw calls | Luzes | Nosso código (simulação + apresentação + HUD) |
|---|---|---|---|---|---|---|---|
| Baixo | **59,1** | 29,8 | 16,7 / 23,0 ms | 8,6 ms | 99 | 4 | 0,9 ms |
| Médio | **36,8** | 15,1 | 25,9 / 43,5 ms | 16,4 ms | 141 | 6 | 2,3 ms |
| Alto | **25,3** | 10,9 | 38,9 / 59,8 ms | 28,9 ms | 206 | 8 | 3,7 ms |

**Observações:**
- **O Baixo parece ter teto de 60 FPS:** a mediana é exatamente 16,67 ms. Provavelmente o ANGLE/DWM sincroniza mesmo com o VSync do Godot desligado, mas isso não foi confirmado.
- **Tempo de GPU:** o driver não informa por essa via (`gpu_ms_medio` = null).
- **1% baixo:** fica bem abaixo da média. Os picos ainda não foram investigados. Os suspeitos são a compilação de shader na primeira vez que um efeito aparece e a reconstrução dos atores quando a luta recomeça.

**Descobertas que mudaram os presets** (todas medidas por eliminação com `--skip`):
1. **Escala de resolução 3D abaixo de 1 deixava tudo mais lento,** não mais rápido. No Compatibility desta GPU, o buffer intermediário custava cerca de 22 ms por quadro: uma cena quase vazia caía de 180 para 38 FPS. Por isso, a escala 3D ficou em 1 nos três presets.
2. **MSAA 2x custava cerca de 9 ms** (Alto: de 17 para 26 FPS). Foi desligado.
3. **Sombra de tocha (cubemap) é cara.** O Alto ficou com uma só, a da tocha mais próxima.
4. **O HUD redesenhava todo o texto a cada quadro** (cerca de 10 ms). Agora os painéis só redesenham quando algo muda; as barras e os números sobre o mundo continuam a cada quadro. No Baixo, o HUD custa 0,65 ms.

## 5. Problemas e limitações (nada aqui está escondido)
- **Forward+ e Mobile não rodam nesta máquina,** então os presets de alta fidelidade do documento não foram vistos nem medidos (D1).
- **Os modelos não têm esqueleto,** e a animação é procedural: balanço ao andar, investida, inclinação, queda. A arte final é decisão de D2 e da Fase 3.
- **Médio e Alto ficam abaixo de 60 FPS** neste hardware. Só o Baixo é jogável com folga.
- **A fonte é a padrão do Godot.** A UMBRA Pixel ainda não foi convertida para um formato que o Godot leia.
- **Os ícones de habilidade são provisórios,** desenhados com linhas.
- **Não há som:** os 518 WAVs do UMBRA ainda não foram ligados.
- **O clique para andar segue em linha reta** e desliza nas paredes; não há busca de caminho. Na slice, as salas em linha não exigem.
- **O controle físico não foi testado:** o controle foi simulado por eventos.
- **Os picos do 1% baixo** ainda não foram investigados (seção 4).

## 6. Próxima tarefa proposta
Depois da sua avaliação:
- o **som** da slice (os WAVs do UMBRA);
- a **UMBRA Pixel** no Godot;
- a investigação dos **picos**;
- um **teste com o controle físico**.

Em seguida, a Fase 3 (visual) ou a Fase 5 (migração dos sistemas), conforme a sua prioridade.

## 7. Decisões para o diretor criativo
1. **Visual da slice:** aprovar a direção (cripta de pedra, tochas, névoa, câmera isométrica a 52°) como base da Fase 3.
2. **Zoom:** os "30%" (12,2 unidades, campo de visão de 40°) estão bons na tela, ou prefere mais perto ou mais longe?
3. **Presets:** aceitar os ajustes feitos a partir das medições (sem escala 3D nem MSAA no Compatibility; Alto com uma sombra de tocha).
4. **Próximo marco:** Fase 3 (visual) ou Fase 5 (migração dos sistemas)?
