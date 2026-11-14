# Craquez 3D

Jogo de futebol realista (estilo FIFA / eFootball / UFL) para **Android**, com versão **Windows** planejada.
Esta primeira versão (**v0.1.0**) é a **Arena de Treino de finalização**: um atacante e um goleiro, para validar física, animações e clima.

> Jogo totalmente programado por Inteligência Artificial.

## Unreal ou Unity?

Escolhemos **Unity 6 (URP)**. A Unreal não é impossível, mas para **Android** ela é bem mais complexa:
- Lumen e Nanite (o que deixa a Unreal tão bonita) **não rodam no celular**; o renderizador mobile da Unreal fica parecido com o da Unity;
- o projeto exige C++, Android SDK/NDK em versões exatas, mais de 100 GB de editor e builds bem mais lentas;
- APKs maiores e mais pesados para aparelhos intermediários.

A Unity com URP entrega iluminação PBR, sombras, pós-processamento e boa performance no Android, e exporta para Windows sem retrabalho.

## Como abrir

1. Instale o **Unity Hub** e o **Unity 6 LTS (6000.0 ou mais novo)** com o módulo **Android Build Support** (OpenJDK + SDK/NDK).
2. Clone o repositório e, no Hub, use **Add → Add project from disk**.
3. Na primeira abertura o projeto se configura sozinho (**Craquez 3D → Configurar Projeto** refaz tudo):
   URP mobile, materiais, controladores de animação, prefabs, cena `Assets/Craquez/Scenes/ArenaTreino.unity`, Build/Player Settings.
4. Aperte **Play**. Para o celular: **File → Build Settings → Android → Build**.

> Se a Unity perguntar sobre o *novo Input System*, aceite e reinicie (o jogo funciona com os dois sistemas).

## O que tem na v0.1.0

**Abertura e menus**
- Aviso "jogo totalmente programado por IA" → vídeo de abertura → **"Toque na tela para iniciar"**.
- Menu principal: **Arena de Treino** e **Configurações**. Pausa no jogo (Continuar, Nova bola, Câmera, Configurações, Sair).
- Tudo navegável por toque, mouse, teclado e controle.

**Arena de treino**
- Estádio do rascunho em escala real; gramado oficial 105 × 68 m com faixas de corte e todas as marcações; gols oficiais (7,32 × 2,44 m) com **rede que estufa**.
- Sem placar e sem tempo — apenas avisos de GOL / DEFESA / TRAVE / PRA FORA.
- **Você escolhe quem controla:** o **atacante** (contra o goleiro com IA) ou o **goleiro** (a IA chuta em você).

**Clima** (escolha Sol, Chuva, Tempestade ou **Aleatório**)
- Sol: céu limpo, gramado seco e rápido, vento fraco.
- Chuva: chuva constante, gramado molhado, poças, vento moderado.
- Tempestade: chuva severa, raios e trovões (com atraso real do som), vento forte com rajadas, muitas poças, refletores acesos.
- **Poças com marcas dos pingos** caindo e reflexo; a bola **freia e pode parar** dentro delas.
- **Áreas encharcadas**: película d'água em que a bola **derrapa e ganha velocidade** ao quicar.
- O **vento** desvia a bola de verdade (a seta e a velocidade aparecem no HUD).

**Física**
- Bola de 430 g / 22 cm: arrasto com crise de arrasto, **efeito Magnus** (curva), **bola sem efeito oscilando** em chutes fortes.
- Quique com restituição dependente da velocidade e da umidade, atrito no contato trocando velocidade por rotação (backspin "segura", topspin acelera), rolamento por superfície.
- Chute: força carregada, mira dentro do gol, trajetória resolvida com a mesma física da bola (o vento você compensa), soma da velocidade da corrida, qualidade do contato com o pé, bola molhada gira menos, imprecisão por força excessiva/sprint/chuva.
- Goleiro: tempo de reação humano (pior na chuva), leitura da trajetória, mergulho com alcance físico limitado, **IK das mãos na bola**; encaixe, espalmada pela normal do contato, **ponta dos dedos** (pode ainda entrar) ou rebote no corpo; bola molhada escorrega.

**Personagens e animações**
- Jogador do rascunho (seu rosto) e goleiro, com **esqueleto Humanoid** (ossos Mixamo) — qualquer animação humanoide serve para os dois.
- Animações dos packs Mixamo do rascunho (corrida, chutes, cavadinha, mergulho, salto…), olhar acompanhando a bola.

**Câmeras**: Atrás do atacante (padrão, enquadrando a corrida para o gol), Dinâmica baixa, TV, Aérea, Atrás do gol e Goleiro.

**Controles**
| Ação | Toque | Teclado | PlayStation | Xbox |
|---|---|---|---|---|
| Mover | analógico esquerdo | WASD / setas | analógico / direcional | analógico / direcional |
| Correr | CORRER | Shift | R2 | RT |
| Chute (segure p/ força) | CHUTE | Espaço | ○ *(ou □)* | B *(ou X)* |
| Chute colocado | COLOCADO | E | □ *(ou ○)* | X *(ou B)* |
| Cavadinha | CAVADINHA | Q | △ | Y |
| Câmera / Nova bola / Pausa | botões no topo | C / R / Esc | R3 / Share / Options | RS / View / Menu |

Em **Configurações → Layout do controle** você troca entre *Chute no ○/B* e *Chute no □/X*.
Funciona com DualShock 4, DualSense, Xbox (Bluetooth/USB) e controles genéricos reconhecidos como gamepad.
No modo goleiro: chute + direção = mergulhar, colocado = defesa parada, cavadinha = saltar, correr = sair do gol.

**Configurações**: layout do controle, câmera padrão, qualidade gráfica, volume, vibração, atualizações, controle conectado e versão.

## Atualizações dentro do app

O jogo consulta a **última Release publicada** deste repositório no GitHub. Se a versão for maior que a instalada,
a atualização é **obrigatória**: o jogo fica bloqueado na tela de atualização (sem opção de pular), baixa o APK com barra de progresso
e abre o instalador. O Android sempre exibe a confirmação final do sistema — é uma proteção do próprio Android que nenhum app comum consegue pular.

Para publicar uma atualização:
1. Aumente a versão no arquivo **`VERSION`** (ex.: `0.2.0`). Ela vira automaticamente `bundleVersion` e `versionCode` no build.
2. Gere o APK assinado **sempre com a mesma keystore** (Player Settings → Publishing Settings). Com outra assinatura o Android recusa a atualização.
3. Crie uma Release com a tag `v0.2.0` e anexe o `.apk`. Rascunhos (drafts) e pré-lançamentos são ignorados.

## Estrutura

```
Assets/Craquez/
  Models/        Jogador, Goleiro (T-pose, Humanoid), Bola, Estadio (otimizados para mobile)
  Animations/    clipes Mixamo usados (Jogador/, Goleiro/)
  Textures/      PBR (albedo, normal, metallic+smoothness) + texturas procedurais (grama, chuva, poças, rede…)
  Video/         Abertura.mp4
  Scripts/Runtime  física, IA, clima, câmeras, menus, atualizador
  Scripts/Editor   importação automática e setup do projeto
  Gerado/        criado pelo setup (materiais, animators, prefabs, URP)
Assets/Plugins/Android/CraquezUpdater.androidlib   instalador de APK e vibração
tools/           scripts Blender/Python usados para preparar os assets do rascunho
VERSION          versão do jogo
```

Os arquivos originais (alta resolução) continuam nos rascunhos das Releases. Em `tools/blender` estão os scripts que
reduziram polígonos (estádio 2,1 M → 160 k, bola 1,9 M → 8 k, goleiro 103 k → 32 k), colocaram o goleiro em T-pose,
cortaram o gramado/gols do estádio e geraram as texturas para Android.

## Próximos passos

Mais jogadores e times, partidas completas, passes e cruzamentos, versão Windows, rostos/uniformes personalizáveis.
Veja o [CHANGELOG](CHANGELOG.md).
