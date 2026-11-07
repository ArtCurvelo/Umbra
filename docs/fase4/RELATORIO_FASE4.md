# Fase 4 — interface: relatório de entrega (2026-11-07)

Pedido do diretor criativo: "Aprovo, implemente" (mockups de `PROPOSTA_FASE4.md`). Formato da seção 12 do documento de direção.
As duas perguntas de design ficaram sem resposta, então valeu o que estava no mockup aprovado:
- mochila em grade de espaços iguais;
- árvore em tronco por categoria.

## 1. O que foi implementado de fato
| Tela | Estado |
|---|---|
| **Base das telas** | Pilha de telas modais com o mundo pausado. Um toque = um passo; segurar repete depois de 380 ms e a cada 110 ms (mesma regra do UMBRA). O mouse foca ao passar e confirma ao clicar. Os atalhos do rodapé seguem o dispositivo em uso (teclado, Xbox ou PlayStation). O HUD do jogo some enquanto uma tela está aberta. |
| **Menu principal** | O jogo abre nele. Continuar (desabilitado sem partida), Novo jogo, Configurações, Créditos, Sair. |
| **Pausa** | Esc ou Start. Continuar, Personagem, Habilidades, Configurações, Recomeçar a cripta, Menu principal, Sair do jogo. |
| **Configurações** | Cinco abas, aplicadas na hora e salvas em `user://configuracoes.json`:<br>• **Vídeo:** qualidade e cada parte dela (vira "Personalizado"), modo de tela, resolução, VSync, limite de quadros, escala da interface, brilho, com o custo medido no painel lateral;<br>• **Áudio:** volumes nos barramentos;<br>• **Controles:** remapeamento de teclado/mouse e de controle, conflito com confirmação e troca, ícones automático/Xbox/PlayStation, restaurar padrão;<br>• **Acessibilidade:** tremor de câmera, reduzir clarões, números de dano;<br>• **Jogo:** câmera inicial, zoom inicial, mostrar FPS. |
| **Personagem e inventário** | Boneco com 9 espaços (dois anéis) e atributos com o bônus em verde. Mochila de 30 espaços. A dica compara com o equipado em cor e por escrito, e mostra a dica do item equipado. Enter/A equipa ou guarda; Del/X larga o item no chão (nada é destruído). As abas Materiais, Gemas e Baú indicam a Fase 5. |
| **Árvore do Bárbaro** | Tronco por categoria com as 9 habilidades e os textos do catálogo 0.16. As 3 da slice recebem graus de verdade, com o dano atual e o do próximo grau. As outras 6 e as opções C/E/T aparecem bloqueadas com "Fase 5". **Cursor virtual só aqui:** o analógico esquerdo o move, ele gruda nos nós e empurrar a borda move a vista. Direcional pula entre nós; Q/E ou LB/RB e a roda dão zoom; o botão direito arrasta. |

**Domínio (regras, C# puro):**
- itens com atributo principal, poder e afixos;
- 9 espaços de equipamento que mudam ataque, defesa e vida no combate;
- mochila de 30 espaços: mochila cheia deixa o item no chão;
- largar o item no chão;
- graus de habilidade que aumentam o dano.
- **Números novos PROVISÓRIOS** numa tabela única (`ItemRules` e `SkillRanks`). Eles esperam a sua revisão de balanceamento:
  - base por tipo de item;
  - afixos de 5–12 de vida ou 1–3 de ataque ou defesa;
  - +10% por grau;
  - 5 pontos iniciais.
- Os multiplicadores de raridade são os do UMBRA (`rarity.MULTIPLIER`).

**Entrada:**
- Ações novas: I/Back = personagem, K/R3 = habilidades, Tab/L3 = câmera iso/top-down.
- A câmera saiu do Back e foi para o L3.
- O analógico e o direcional navegam os menus.

## 2. Correções encontradas pelos testes
1. **A pausa abria e fechava no mesmo instante:** a tecla que abre (Esc/Start) também significa voltar. Agora a tela nova ignora a entrada do quadro em que foi aberta.
2. **As telas ficavam com tamanho zero** dentro da camada de interface. Corrigido com âncoras e deslocamentos.
3. **As capturas travavam:** com o mundo pausado, a contagem de quadros parava. Ela passou a um nó que roda mesmo pausado.

## 3. Testes executados e resultados
- **xUnit: 34/34.** Os 6 novos cobrem:
  - tabela de itens;
  - equipar muda atributos e dano;
  - troca e dois anéis;
  - vida não passa do máximo ao desequipar;
  - mochila cheia e largar no chão;
  - graus gastam pontos e aumentam o dano.
- **Autoteste do Godot: 35/35.** O roteiro, com controle simulado pelo caminho real de entrada:
  - limpa a cripta;
  - abre a pausa;
  - um toque = um passo;
  - equipa um item e confere a mudança nos atributos;
  - fecha;
  - abre a árvore pelo R3, aprende e devolve um grau, dá zoom e muda de nó;
  - troca a qualidade nas configurações;
  - remapeia Andar para cima para F;
  - provoca o conflito de Q com a Ruptura e confirma a troca;
  - restaura o padrão;
  - pelo menu principal, começa um jogo novo.
  - Também confere que a execução de teste não toca nas configurações do jogador.
- **Capturas das telas reais:** `docs/fase4/telas/` (menu, pausa, as 5 abas de configurações, inventário em 720p e 1080p, árvore).
- **Desempenho (Médio, 15 s):**
  - três execuções: 34,7, 39,2 e 36,4 FPS (1% baixo de cerca de 21);
  - sem o HUD: 44,8;
  - na Fase 3 o Médio deu 40,2.
  - A diferença está perto do ruído (±5), mas pode haver cerca de 2 FPS a menos pelo texto do HUD em UMBRA Pixel (22 px). Fica anotado para a Fase 6.

## 4. Problemas e limitações
- **Áudio:** os volumes não têm efeito audível, porque a slice ainda não tem som.
- **Leitor de tela:** cada tela já expõe o texto do foco (`FocusText`), mas a voz ainda não foi ligada.
- **Inventário:**
  - sem arrastar com o mouse; o clique equipa;
  - as abas Materiais, Gemas e Baú aguardam a Fase 5.
- **Árvore:** só as 3 habilidades da slice funcionam; opções C/E/T só na Fase 5.
- **Ícones:** itens e habilidades usam marcas provisórias (siglas e traços) até a fase de arte.
- **Controle físico:** não foi testado; o controle foi simulado por eventos.

## 5. Próxima tarefa proposta
Fase 5, a migração dos sistemas do UMBRA, por módulos e com testes de paridade:
- dados e catálogos;
- estados e elementos;
- habilidades com opções;
- itens, afixos e gemas;
- as classes;
- saves com conversor.

Antes disso, se quiser: revisão de balanceamento dos números PROVISÓRIOS e uma rodada com o controle físico.

## 6. Decisões para o diretor criativo
1. Aprovar as telas como implementadas.
2. Números PROVISÓRIOS de itens e graus: revisar agora ou na Fase 5, junto com o sistema real de itens do UMBRA?
3. Ainda abertos da Fase 3: brilho, cenário no Baixo e texturas CC0.
