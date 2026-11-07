# UMBRA — Godot .NET + C# (vertical slice)

Migração incremental do UMBRA: Ashes Below (Python/pygame) para Godot 4.7.2 .NET + C#.
Plano e decisões: `umbra-ashes-below/docs/PLANO_GODOT.md`. Relatório desta entrega: [docs/RELATORIO_SLICE.md](docs/RELATORIO_SLICE.md).
O jogo Python continua sendo o jogo jogável; este repositório não toca nele.

## Estrutura
```
src/Umbra.Domain/         regras em C# puro, sem Godot (simulação a 60 ticks/s, mundo, combate, IA, loot)
tests/Umbra.Domain.Tests/ testes xUnit do domínio
game/                     projeto Godot (renderizador Compatibility): cena, câmera, HUD, menus, presets, benchmark
tools/                    check.ps1 (verificação completa), evidence.ps1 (capturas + benchmark), jogar.ps1
docs/                     relatório, capturas de tela, resultados de benchmark
```

## Comandos (PowerShell)
```powershell
powershell -ExecutionPolicy Bypass -File tools\jogar.ps1 -Preset baixo     # jogar
powershell -ExecutionPolicy Bypass -File tools\check.ps1                   # testes do domínio + build + autoteste do Godot
powershell -ExecutionPolicy Bypass -File tools\evidence.ps1                # capturas dos presets e benchmark (abre janela)
powershell -ExecutionPolicy Bypass -File tools\telas.ps1                   # capturas das telas (menu, configurações, inventário, árvore)
```
Para editar no Godot, abra `game/project.godot`. Os scripts `.cs` abrem no VS Code, que está configurado como editor externo.

## Controles
| Ação | Teclado e mouse | Controle (Xbox / PlayStation) |
|---|---|---|
| Andar | WASD, ou clique esquerdo no chão | Analógico esquerdo |
| Golpe Brutal | Clique esquerdo no inimigo (o herói vai até ele), Espaço ou Shift + clique | A / Cruz |
| Ruptura (28 de Fúria) | Clique direito ou Q | X / Quadrado |
| Investida (recarga de 9 s) | E | Y / Triângulo |
| Mirar | Mouse | Analógico direito, ou a direção do movimento |
| Personagem e inventário | I | Back/View |
| Árvore de habilidades | K | R3 (clique do analógico direito) |
| Câmera isométrica ou top-down | Tab | L3 (clique do analógico esquerdo) |
| Girar a câmera em 45° | Z / C | LB / RB |
| Zoom | Roda do mouse | Direcional para cima / para baixo |
| Pausa | Esc | Start |

Os ícones dos botões no HUD mudam conforme o último dispositivo usado (teclado, Xbox ou PlayStation). Tudo pode ser remapeado em
Configurações > Controles; as configurações ficam em `user://configuracoes.json` (no Windows: `%APPDATA%\Godot\app_userdata\UMBRA — vertical slice\`).
O jogo abre no menu principal.

## Opções de linha de comando (depois de `--`)
`--jogo` (pula o menu), `--tela menu|pausa|config[:aba]|inventario|arvore`, `--seed N`, `--preset baixo|medio|alto`, `--camera iso|top`, `--yaw N`, `--zoom P`, `--autopilot`, `--ticks N`, `--freeze`,
`--shot arquivo.png --frames N`, `--bench arquivo.json --seconds S --warmup W`, `--selftest`.
Capturas de efeitos: `--fx golpe|ruptura|investida` (segura o efeito a 35% da vida). Guia visual: [docs/GUIA_VISUAL.md](docs/GUIA_VISUAL.md).
Diagnóstico: `--nohud`, `--scale X`, `--lights N`, `--skip partes` (torches, walls, floor, props, enemies, env, hero, herolight, fx, moon,
scaling, msaa, torchshadows, glow, moonshadow, post, measure).
