# AGENTS.md — UMBRA em Godot .NET + C#

Antes de qualquer trabalho, leia **[docs/HANDOFF_CODEX.md](docs/HANDOFF_CODEX.md)**. Ele traz os papéis, as regras, os
comandos, o método de paridade, o estado da Fase 5 e onde o trabalho parou (5.8d2: Bruxo e a base dos aliados).

O resumo essencial:
- **Game Director:** o usuário; decide jogabilidade, números, telas e prioridades. Mudança visual só entra no `main` depois de ele ver as capturas.
- **Fonte das regras:** o jogo Python em `..\umbra-ashes-below` (leia o `CLAUDE.md` de lá). Cada sistema é portado com **paridade**: o harness em `tools/parity/` grava JSON e o xUnit repete os casos.
- **Fonte da verdade sobre o estado:** `docs/fase5/PLANO_FASE5.md`. Atualize-o a cada entrega.
- **Verificação:** `powershell -ExecutionPolicy Bypass -File tools\check.ps1`, depois `git restore game/assets game/project.godot`.
- **Testes:** nunca declare algo testado sem ter rodado.
- **Git:**
  - commit só em etapa aprovada; **nunca push**;
  - adicione só caminhos explícitos (há arquivos de outras sessões na pasta).
- **Saves e configurações:** não toque em saves nem em configurações do jogador no repositório Python.
