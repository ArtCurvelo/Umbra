# Fase 4 — interface: proposta com mockups (2026-11-07)

A Fase 3 (visual) foi aprovada pelo diretor criativo em 2026-11-07 ("Aprovo, siga").

A seção 8 do documento de direção pede wireframes, protótipos ou capturas reais antes de construir telas grandes. Os
mockups abaixo foram desenhados com o kit real ("Ferro e Cinza") e a fonte real, por cima da cena real do jogo, com dados
de exemplo. **Nenhuma tela foi implementada ainda;** a implementação começa depois da sua aprovação.

## O que já entrou de fato (não é mockup)
- **UMBRA Pixel no Godot.**
  - `tools/export_umbra_pixel.py` gera `game/assets/fonts/umbra_pixel.fnt` e `.png` a partir dos glifos do próprio jogo Python: 155 glifos, todos os acentos do português, pixels idênticos.
  - Tamanhos sempre em múltiplos inteiros (11/22/33/44/55/66 px), sem suavização, com filtro nítido.
  - O HUD e o menu de pausa já usam a fonte.
  - Ela não tem o glifo "·"; o texto que o usava foi trocado.
- **Escala da interface verificada:**
  - 1280x720 (1x);
  - 1920x1080 (1,5x): o Godot redesenha o texto direto em 33 px = 3x a fonte, e os pixels continuam uniformes;
  - 2560x1440 (2x).
  - Provas: `mockups/escala_1080p_fracionaria.png` e `mockups/escala_1440p.png`.

## Mockups (`mockups/*.png`, 1280x720)
| Tela | Arquivo | Pontos de design |
|---|---|---|
| **Menu principal** | `menu.png` | Título UMBRA em ouro; a cripta aparece ao fundo, escurecida à esquerda, onde fica o menu. "Continuar" mostra herói, classe, nível e lugar. Foco com moldura e seta de ouro. Atalhos do dispositivo no rodapé. |
| **Configurações: Vídeo** | `config.png` | Abas com LB/RB (Q/E); valores alinhados à direita (escolha `< >`, ligado/desligado com cor e palavra, deslizantes). Painel à direita explica a opção em foco e mostra o **custo medido** no desempenho. Inclui brilho e tremor de câmera, pedidos na Fase 3. |
| **Configurações: Controles** | `controles.png` | Remapeamento por ação, com colunas de teclado/mouse e controle; a linha em edição mostra "Pressione uma tecla". Ao lado: dispositivo detectado e escolha de ícones (automático, Xbox, PlayStation), com os dois conjuntos desenhados. Conflitos de tecla pedem confirmação. |
| **Personagem e inventário** | `inventario.png` | Boneco com 9 espaços em volta (moldura na cor da raridade e etiqueta), atributos ao lado, mochila 10x3. O item em foco abre a dica com a comparação ao equipado em cor **e** palavra ("melhor" ou "pior", para daltonismo), e a dica do item equipado fica acima, para comparar lado a lado. Abas Equipamento, Materiais, Gemas e Baú, como no UMBRA. |
| **Árvore de habilidades (Bárbaro)** | `arvore.png` | Tronco de baixo para cima pelas categorias (Básica → Suprema), com as habilidades do catálogo 0.16. Ouro = aprendida, osso = disponível, ferro = bloqueada, com graus "2/5". As opções da habilidade em foco se abrem em leque. O painel à direita mostra o grau, o texto do catálogo e as opções C/E. **O cursor virtual existe só aqui** (decisão do diretor). |

## Regras que valem para todas as telas
- **Entrada:** um toque = um passo em todos os menus (regra do UMBRA). O foco é sempre visível: moldura de ouro e seta.
- **Botões:** os atalhos do rodapé seguem o último dispositivo usado; teclado, Xbox e PlayStation estão desenhados.
- **Cor nunca sozinha:** raridade tem etiqueta; comparação tem palavra.
- **Leitor de tela:** cada tela nova ganha o texto do foco, como no UMBRA atual (`announcer.focus_text`), na implementação.

## Perguntas para o diretor criativo
1. Aprova os layouts das cinco telas?
2. **Inventário:** prefere a mochila em grade com espaços de tamanho único (como no mockup e no UMBRA atual) ou itens de tamanhos diferentes (estilo grade "Tetris")?
3. **Árvore:** mantém o formato de tronco por categoria (mockup) ou prefere o formato ramificado livre, como a árvore do Necromante no UMBRA?
4. **Pendências da Fase 3**, que podem ser respondidas agora:
   - brilho;
   - cenário miúdo no Baixo;
   - texturas CC0.
