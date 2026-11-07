# UMBRA — guia visual (Fase 3, 2026-11-07)

**Estado:** proposta para aprovação do diretor criativo. Vale para tudo o que for desenhado no Godot.
Referências de gênero:
- **Diablo II: Resurrected:** atmosfera, contraste, silhuetas legíveis, economia de efeitos.
- **Diablo IV:** clareza do retorno visual.

Nada é copiado deles: nem nomes, nem arte, nem interface.

## 1. Princípios
1. **Legibilidade antes de beleza.** O herói, os inimigos, os avisos de golpe e os itens precisam ser lidos em menos de meio segundo, em qualquer preset.
2. **Luz conta a história.** A cripta é escura; a luz vem das tochas (quente) e do herói (raio de luz próprio). O que importa fica iluminado.
3. **Frio contra quente.** A pedra e as sombras puxam para o azul-acinzentado; o fogo, a Fúria e o ouro puxam para o âmbar.
4. **Efeito curto.** Clarões duram até 0,2 s; rastros, até 0,4 s; marcas no chão, até 1,2 s. A tela nunca fica coberta.
5. **O preset muda o acabamento, não a informação.** Avisos, alcances, cores de raridade e números aparecem iguais do Baixo ao Alto.

## 2. Cores (tokens)
| Token | RGB | Uso |
|---|---|---|
| Tinta | 5, 5, 8 | fundo, escuridão |
| Ferro escuro / Ferro / Ferro claro | 20,18,22 · 46,42,48 · 98,90,86 | molduras da interface ("Ferro e Cinza") |
| Osso | 236, 222, 196 | texto principal, aço do herói |
| Cinza | 150, 142, 132 | texto secundário, poeira |
| Ouro | 220, 176, 98 | foco, títulos, cantos das molduras |
| Brasa | 190, 82, 44 | Fúria, habilidades de fogo e de impacto |
| Sangue | 168, 36, 40 | vida, dano no herói, sangue |
| Aviso inimigo | 220, 50, 40 | anel de golpe no chão (sempre o mesmo vermelho) |
| Raridades | Comum 196,196,200 · Mágico 70,140,255 · Raro 250,222,70 · Lendário 255,128,32 | sempre com a etiqueta [C]/[M]/[R]/[L], nunca só a cor |

## 3. Luz
| Fonte | Cor | Energia / alcance | Observação |
|---|---|---|---|
| Tocha | 255, 148, 72 | 1,9 / 6,5 m | tremula (duas senoides); só as mais próximas acendem (Baixo 4, Médio 6, Alto 8) |
| Raio de luz do herói | 255, 209, 158 | 0,85 / 6,5 m | sempre ligada; o mundo se revela em volta do herói |
| Lua | 140, 158, 217 | 0,22 | dá forma às paredes; projeta as sombras dos personagens só no Alto |
| Clarão de impacto | conforme o efeito | até 2,4 / 5 m, 0,12–0,2 s | Médio e Alto |
| Ambiente | 77, 82, 102 | 0,22 | sombra nunca é preto puro |

## 4. Materiais
- **Piso:**
  - lajes irregulares (Voronoi) de 0,5 a 0,8 m, tom variando por pedra, rejunte escuro e sujeira em baixa frequência;
  - escurecimento de contato a até 1,2 m das paredes (`shaders/floor.gdshader`).
- **Parede:**
  - cantaria em fiadas desencontradas (blocos de 0,62 × 0,36 m), argamassa escura e pé úmido mais escuro;
  - topo escuro, que lê como o alto do muro;
  - janela transparente entre a câmera e o herói (`shaders/wall_cutaway.gdshader`).
- **Personagens:**
  - cor por vértice com borda de luz, quente no herói e vermelho-fria nos inimigos;
  - lampejo vermelho ao levar golpe e escurecimento ao morrer (`shaders/actor.gdshader`).
- **Cenário:** pilares no ritmo das paredes, entulho ao pé delas, ossos e crânios, manchas no chão (`scripts/Props.cs`). Tudo vem da semente do mapa, sem colisão.
- **Proveniência:** todo material é procedural e gerado em código. Nenhuma textura externa (decisão D2).

## 5. Efeitos (VFX)
| Efeito | Forma | Cor | Duração |
|---|---|---|---|
| Golpe Brutal | crescente fino na ponta da lâmina, apagando para dentro e nas pontas | osso | 0,17 s |
| Ruptura | onda em cone, rachaduras incandescentes no chão, poeira em três pontos, clarão de brasa | brasa | 0,3 s (onda), 1,1 s (rachaduras) |
| Investida | nuvem de poeira na saída, rastro de brasas no corpo | cinza / brasa | 0,35–0,55 s |
| Acerto | faíscas de aço e jato de sangue escuro; o inimigo recua e pisca | osso / sangue | 0,55 s |
| Golpe no herói | o mesmo, com clarão vermelho e tremor de câmera leve | sangue | 0,12 s |
| Morte | poeira; o corpo cai e escurece em 1,5 s | cinza | — |
| Aviso do inimigo | anel vermelho com preenchimento que cresce até o golpe | aviso inimigo | 35 ticks |
| Ambiente (Alto) | poeira flutuando na luz do herói; brasas das tochas | âmbar baixo | contínuo |

**Tremor de câmera:** 0,08 (Investida), 0,16 (golpe recebido), 0,22 (Ruptura), decaindo em menos de 0,2 s. Precisa de uma opção de desligar no menu de configurações (Fase 4; o código já tem `CameraRig.ShakeEnabled`).

## 6. Pós-processamento (Médio e Alto)
- Uma passada em tela cheia (`shaders/post.gdshader`): saturação 0,86; sombras puxadas ao frio (0,86/0,92/1,08); luzes ao quente (1,08/1,00/0,88); contraste 1,06; vinheta 0,42 centrada um pouco acima do meio (onde fica o herói).
- **Grão:** 0,035, só no Alto.
- **Interface:** o HUD fica fora da gradação.

## 7. Proporções e câmera
- **Escala:** 1 tile = 1 m; herói de cerca de 1,72 m; Bruto com escala 1,12; paredes de 2,2 m; pilares de 2,5 m.
- **Câmera isométrica:** 52° de inclinação, giro em passos de 45°, campo de visão de 40°.
- **Zoom:** 30% = 12,2 m (PROVISÓRIO).
- **Top-down:** 84°.

## 8. Orçamento por preset (ThinkPad T440, Compatibility; medido na Fase 3)
| Preset | Recursos | FPS médio / 1% baixo |
|---|---|---|
| Baixo | 4 tochas, sem névoa, sem pós, sem sombra, partículas 30% | 47,4 / 28,4 |
| Médio | 6 tochas, névoa, pós, clarões de impacto, partículas 70% | 40,2 / 28,5 |
| Alto | 8 tochas, sombra da lua (personagens) e da tocha mais próxima, brilho, pós com grão, poeira, partículas 100% | 22,4 / 13,8 |

**O que não usar no Compatibility desta GPU** (medido):
- escala 3D abaixo de 1 (cerca de 22 ms por quadro);
- MSAA 2x (cerca de 9 ms).

## 9. Próximos passos de arte (dependem de decisão)
- **Modelos com esqueleto e animação:** precisam de produção em Blender ou de assets licenciados.
- **Texturas PBR:** podem vir de bibliotecas CC0, por exemplo Poly Haven, ou ser encomendadas. Proposta no relatório da fase.
- **Ícones das habilidades e fonte UMBRA Pixel no Godot.**
