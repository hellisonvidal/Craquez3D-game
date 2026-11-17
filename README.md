# Craquez 3D

Jogo de futebol realista (estilo FIFA / eFootball / UFL) para **Android**, com versão **Windows** planejada.
Esta versão (**v0.3.0**) é a **Arena de Treino de finalização**: um atacante e um goleiro, para validar física, animações e clima.

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

## O que tem no jogo

**Partida 11 x 11** (menu principal → PARTIDA 11 x 11)
- Craquez FC x Atlético Celeste: escolha seu time, o clima, a duração, a dificuldade e a câmera.
- 22 jogadores alternando os dois rostos do jogo, cada um com nome, número, atributos e **mentalidade própria** (ofensividade, criatividade, disciplina, agressividade, espírito de equipe, frieza e liderança), visão tática e jogo coletivo.
- Árbitro e dois assistentes em laterais opostas. Regras completas: lateral, escanteio, tiro de meta, falta com barreira, pênalti, impedimento, vantagem, cartões e atendimento médico.
- Trombadas, quedas, contusões, carrinho e escorregões na chuva.
- HUD com nomes fixos, cartão do jogador controlado e minimapa central (dá para desligar). Câmera **Visão do jogador**.
- Controles: com a bola, ✕ passe, △ lançamento, ○/□ chute, R1 drible e L1 arrancada. Sem a bola, ✕ desarme, ○ carrinho e L1 trocar de jogador (teclado: Z, Q, Espaço/E, F, X).

> Animações de queda, contusão, colisão, carrinho e árbitros: [Universal Animation Library](https://quaternius.com) 1 e 2 da Quaternius, licença CC0 (veja `Assets/Craquez/Animations/Quaternius/LICENCA.txt`).

**Abertura e menus**
- Aviso "jogo totalmente programado por IA" → vídeo de abertura → **"Toque na tela para iniciar"**.
- Menu principal: **Arena de Treino**, **Configurações** e **Sair do jogo**. Pausa no jogo (Continuar, Nova bola, Câmera, Configurações, Sair para o menu, Sair do jogo).
- Tela de preparação do treino com cartões de clima (o estádio ao fundo já mostra o clima escolhido) e de papel (atacante/goleiro).
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
- Jogador com animações dos packs Mixamo do rascunho (corrida, chutes, cavadinha), olhar acompanhando a bola.
- Goleiro com animações próprias (os rascunhos não têm pack de defesas): postura de espera, deslocamento lateral, mergulho com o corpo estendido, salto com as mãos no alto, defesa no peito, caído e levantar — geradas por `tools/blender/gkanim` sobre o esqueleto dele.
- Esqueletos endireitados para a T-pose canônica do Mixamo (`tools/blender/rest_align.py`), para as animações não entortarem pés e pernas.

**Câmeras**: Atrás do atacante (padrão, enquadrando a corrida para o gol), Dinâmica baixa, TV, Aérea, Atrás do gol e Goleiro.

**Controles**
| Ação | Toque | Teclado | PlayStation | Xbox |
|---|---|---|---|---|
| Mover | analógico (aparece onde o dedo encosta) | WASD / setas | analógico / direcional | analógico / direcional |
| Correr | CORRER | Shift | R2 | RT |
| Chute forte (segure p/ força) | CHUTE | Espaço | ○ *(ou □)* | B *(ou X)* |
| Chute colocado (curva) | COLOCADO | E | □ *(ou ○)* | X *(ou B)* |
| Cavadinha | CAVADINHA | Q | △ | Y |
| Chute rasteiro | RASTEIRO | Z | ✕ | A |
| Drible: corte (com direção), puxada (para trás), parar a bola (sem direção) | DRIBLE | F | R1 | RB |
| Arrancada (toque longo e disparada) | ARRANCADA | X | L1 | LB |
| Câmera / Nova bola / Pausa | botões no topo | C / R / Esc | R3 / Share / Options | RS / View / Menu |

Em **Configurações → Layout do controle** você troca entre *Chute no ○/B* e *Chute no □/X*.
Funciona com DualShock 4, DualSense, Xbox (Bluetooth/USB) e controles genéricos reconhecidos como gamepad.
No modo goleiro: chute + direção = mergulhar, colocado = defesa no peito, cavadinha = saltar, correr = sair do gol.

A condução é por **toques**: a bola é empurrada à frente e o jogador a alcança de novo — toques curtos no trote,
longos no sprint, domínio que amortece a bola que chega; logo após o toque a bola acompanha a direção pedida.

**Configurações**: layout do controle, câmera padrão, qualidade gráfica, volume, vibração, atualizações, controle conectado e versão.

## Atualizações dentro do app

O jogo consulta a **última Release publicada** deste repositório no GitHub. Se a versão for maior que a instalada,
a atualização é **obrigatória**: o jogo fica bloqueado na tela de atualização (sem opção de pular), baixa o APK com barra de progresso
e abre o instalador. O Android sempre exibe a confirmação final do sistema — é uma proteção do próprio Android que nenhum app comum consegue pular.

## Build automático no GitHub (sem abrir a Unity)

O workflow `.github/workflows/android-release.yml` gera o APK na nuvem (GitHub Actions + [GameCI](https://game-ci.com))
e publica a Release que o atualizador do app baixa.

**Publicar uma versão nova:** aumente o arquivo **`VERSION`** (ex.: `0.1.3`), descreva a versão no `CHANGELOG.md` e faça push/merge na **main**.
O GitHub gera o APK assinado, cria a tag `v0.1.3`, publica a Release com o APK e as notas do CHANGELOG — e os celulares com o jogo recebem a atualização.
Também dá para rodar manualmente: **Actions → "Android: build e Release" → Run workflow** (qualquer branch; desmarque *Publicar* para só baixar o APK como artefato).

**Configuração (uma vez só)** — em **Settings → Secrets and variables → Actions → New repository secret**:

| Segredo | O que é |
|---|---|
| `UNITY_EMAIL` / `UNITY_PASSWORD` | login da sua conta Unity (a licença Personal gratuita serve) |
| `UNITY_LICENSE` | conteúdo do arquivo de licença `Unity_lic.ulf` (aparece depois de ativar a licença Personal no Unity Hub uma vez: Windows `C:\ProgramData\Unity\Unity_lic.ulf`, macOS `/Library/Application Support/Unity/Unity_lic.ulf`, Linux `~/.local/share/unity3d/Unity/Unity_lic.ulf`) |
| `ANDROID_KEYSTORE_BASE64` | a keystore do app em base64 |
| `ANDROID_KEYSTORE_PASS` / `ANDROID_KEYALIAS_NAME` / `ANDROID_KEYALIAS_PASS` | senha e alias da keystore |

> A keystore é a "assinatura" do app. Todas as versões precisam da **mesma** — com outra, o Android recusa a atualização.
> Guarde uma cópia dela fora do GitHub.

A primeira execução leva ~30–60 min (importa todos os assets); as seguintes usam cache e são mais rápidas.
O build roda o mesmo setup do menu **Craquez 3D → Configurar Projeto** (`Craquez.EditorTools.CraquezCI.BuildAndroid`), então não é preciso commitar cena nem prefabs.

**Build manual** (alternativa): gere o APK pela Unity com a mesma keystore, crie uma Release com a tag `vX.Y.Z` e anexe o `.apk`. Rascunhos (drafts) e pré-lançamentos são ignorados pelo app.

## Estrutura

```
Assets/Craquez/
  Models/        Jogador, Goleiro (T-pose, Humanoid), Bola, Estadio (otimizados para mobile)
  Animations/    clipes Mixamo usados (Jogador/, Goleiro/)
  Textures/      PBR (albedo, normal, metallic+smoothness) + texturas procedurais (grama, chuva, poças, rede…)
  Video/         Abertura.webm
  Scripts/Runtime  física, IA, clima, câmeras, menus, atualizador
  Scripts/Editor   importação automática e setup do projeto
  Gerado/        criado pelo setup (materiais, animators, prefabs, URP)
Assets/Plugins/Android/CraquezUpdater.androidlib   instalador de APK e vibração
tools/           scripts Blender/Python usados para preparar os assets do rascunho
VERSION          versão do jogo (gera tag/Release automaticamente)
.github/workflows  build Android e publicação da Release
```

Os arquivos originais (alta resolução) continuam nos rascunhos das Releases. Em `tools/blender` estão os scripts que
reduziram polígonos (estádio 2,1 M → 160 k, bola 1,9 M → 8 k, goleiro 103 k → 32 k), colocaram o goleiro em T-pose,
cortaram o gramado/gols do estádio e geraram as texturas para Android.

## Próximos passos

Mais jogadores e times, partidas completas, passes e cruzamentos, versão Windows, rostos/uniformes personalizáveis.
Veja o [CHANGELOG](CHANGELOG.md).
