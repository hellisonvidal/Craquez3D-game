# Changelog

Formato: [Versionamento Semântico](https://semver.org/lang/pt-BR/). A versão do app vem do arquivo `VERSION`.

## [1.8.0] — 2026-11-26

- **Postura parada com pés 100% plantados no gramado:** removida a movimentação anti-natural das pernas quando parados (`Idle.fbx` levantava os pés até 35,6 cm e `GK_Pronto.fbx` até 32 cm). Gerados novos clipes nativos `Parado.fbx` (jogadores de linha e arbitragem) e `GK_Parado.fbx` (goleiros) com oscilação vertical zero nos pés e apenas respiração sutil de tronco/ombros, somados a zona morta de velocidade zero (`LocomotionAnim`, `PlayerController`, `GoalkeeperAI`) e histerese de chegada na formação (`MatchPlayer` e `RefereeAgent`).
- **Física e animações de quedas corrigidas (fim de jogadores presos/afundados no terreno):** corrigido bug crítico em `CraquezImportSettings` onde clipes com altura livre (`FreeHeightClips`) tinham `lockRootHeightY = false` sem Root Motion ativo (subtraindo ~0,88 m e enterrando corpos caídos a -0,81 m sob a grama); trocado o estado `Empurrado` em pé para `Idle_Shield_Break` (evitando ir ao chão durante um simples desequilíbrio); geradas animações nativas de futebol `Queda.fbx`, `QuedaCostas.fbx`, `CaidoDor.fbx` e `Levantar.fbx` com margem mínima garantida acima do solo, proteção anti-penetração em `FootGrounder`, seleção direcional (queda de frente vs. de costas), desvio suave ao redor de jogadores caídos e *watchdog* de recuperação automática.
- **Cobrador caminhando até a bola nas faltas e bolas paradas:** o cobrador (tanto controlado pelo jogador humano quanto pela IA) agora vai caminhando/trotando naturalmente até a bola antes de autorizar e bater faltas, escanteios, laterais e pênaltis, sem ficar travado à distância nem disparar em sprint ou teletransportar em cima da bola (`MatchSession.StartRestart`, `UpdateRestart` e `MatchPlayer.SetPieceTick`), além de impedir a troca automática de roubar o foco durante a caminhada.
- **Regras atuais do futebol de campo (IFAB 2024/2025/2026):**
  - **Acréscimos oficiais (Regra 7):** cálculo dinâmico de tempo adicional por gols, faltas, cartões, atendimento médico e substituições, anúncio de `+X MIN DE ACRÉSCIMO` aos 45'/90' e exibição `45+X'` / `90+X'` no placar.
  - **5 substituições em até 3 janelas + intervalo (Regra 3):** banco com 7 suplentes por equipe (camisas 12 a 18), opção `SUBSTITUIR ATLETA` no menu de Pausa, substituições automáticas de cansaço/tática pela IA e troca automática após atendimento médico em contusões graves, com entrada pela linha divisória.
  - **Regra dos 8 segundos do goleiro (Regra 12 — atualização IFAB 2025/2026), recuo com o pé e limite da área:** goleiro impedido de usar as mãos fora da própria grande área ou após passe deliberado com o pé de companheiro (jogando com os pés ou sofrendo tiro livre indireto; recuo de cabeça segue permitido), e punição com **escanteio** caso segure a bola com as mãos por mais de 8 segundos.
  - **DOGSO (último homem), dupla punição na área e Vantagem real de 3 s (Regra 12):** falta impedindo chance clara de gol fora da área resulta em vermelho direto; dentro da área disputando a bola aplica amarelo + pênalti; vantagem monitorada por 3 s (voltando a falta se a posse for perdida em seguida) e aplicação de cartão amarelo pendente na parada seguinte.
  - **Tiro livre indireto, regra dos dois toques, tiro de meta atual e bola ao chão (Regras 8, 9, 13–17):** cobrança indireta exige 2º toque para validar gol; cobrador proibido de tocar duas vezes seguidas na bola antes de outro atleta; tiro de meta batido com a bola imóvel no gramado permitindo saída curta dentro da própria área; bola ao chão a 4 m quando a bola atinge o árbitro em velocidade.
  - **Disputa de pênaltis (Regra 10):** opção de desempate oficial em série alternada ABAB de 5 cobranças + morte súbita quando a partida termina empatada (você bate com seus jogadores e defende com seu goleiro).
- **Malhas e texturas dos uniformes ajustadas ao corpo (`Jogador.fbx` e `Goleiro.fbx`):** modelagem refeita em `tools/blender/ajustar_uniformes.py` eliminando a barra do calção aberta em "saia/abajur", afinando a cintura cilíndrica da camisa em corte atlético em V, corrigindo o peito quadrado do goleiro, ajustando as mangas aos braços e suavizando pesos de axila e virilha; texturas e normal maps regenerados por fronteiras geométricas 3D puras em `tools/uniformes.py`, sem manchas sob os braços nem costuras serrilhadas.
- **Nova câmera `Lateral próxima (2 lados)` (`CameraRig`):** posicionada junto à linha lateral frontal em altura próxima do gramado com abertura calibrada à proporção da tela para exibir simultaneamente os dois lados e as duas traves do campo de frente.

## [1.7.0] — 2026-11-26

- **Física da bola e colisões corrigidas:** eliminada a exceção `UnityException: Tag: Ground is not defined` em `BallPhysics.OnCollisionEnter` (que abortava sons, partículas e o enfunar da rede a cada contato da bola); quique sub-passo corrigido com gravidade do passo, posição exata no plano do gramado e resgate de restituição quando o PhysX zera a velocidade vertical; sons procedurais de trave e rede e micro-vibração de câmera funcionando também no Jogo-treino 11 x 11.
- **Lógica da partida e regras revisadas:** corrigido o impedimento (Lei 11 da FIFA) para distinguir defesa/espalmada do goleiro ou desvio acidental (mantém o impedimento da origem) de domínio e passe deliberado do defensor (zera o impedimento); autoridade sobre a bola concedida ao finalizador em voleios e sobras sem dono; arremesso com as mãos do goleiro usando `PlaceAt` na mão estendida (sem nascer atrás do tronco) e varredura `OverlapCapsuleNonAlloc` escalada e sem lixo na memória; goleiros titular e adversário com rostos garantidamente distintos na partida.
- **Revisão dos corpos e esqueletos (`Jogador.fbx` e `Goleiro.fbx`):** coluna do goleiro realinhada no plano sagital (eliminado o zigue-zague ântero-posterior de ±10 cm entre `Spine`, `Spine1`, `Spine2` e `Neck`), pivôs de coxa e joelho centralizados no volume real das pernas, simetria esquerda/direita exata em todos os pares de ossos dos dois modelos com deriva zero dos vértices em repouso (`tools/blender/revisar_corpos.py`) e os 8 clipes de jogadas (`Animations/Jogador/Jogadas/*.fbx`) regenerados sobre o esqueleto revisado.
- **Cabeças, cabelos 3D e portes proporcionais:** os formatos de rosto (`FaceShapes`: testa alta, orelhas, maxilar, crânio) agora acompanham também todas as malhas de corte de cabelo (`Cabelo_*`), impedindo que o crânio atravesse o penteado; transição suave de escala no pescoço e na cabeça conforme o `Porte` e raio do `CapsuleCollider` proporcional à largura física do jogador; valores antigos do `Jogador.prefab` alinhados à física atual.
- **Movimentação e finalização aprimoradas:** arrancada de primeiro passo mais viva, inclinação procedural suave do corpo nas curvas, acelerações e freadas (`MixamoBoneSockets.SetProceduralLean`), ponto de condução dinâmico conforme a velocidade (corrigido bug em que `LateUpdate` sobrescrevia o avanço da bola a cada quadro), estabilização e impulso de aproximação na janela do chute para não "furar" bolas em movimento ou quicando baixo, checagem nos dois pés escalada pela estatura, trombadas bilaterais simétricas respeitando massa e escala real, postura defensiva de **cercar** (*jockey*) virado para a bola e fim da tremedeira dos jogadores da IA ao chegar na posição tática ou formar barreira.
- **Controles aprimorados (toque, teclado, gamepad e TV):** removido o conflito da tecla `C` entre passe e câmera (`C`/`V`/`Tab` trocam a câmera), glifos completos para PlayStation, Xbox, Nintendo Switch e TV, curva radial precisa no analógico esquerdo e espiar o campo pelo analógico direito (`InputBridge.Look`), contexto ofensivo inteligente (`HumanAttackingContext`) que impede dar carrinho ou trocar de jogador por engano durante a condução ou recepção de passe, *buffer* de chute/voleio de primeira, comando **Cercar** (`R1` / `RB` / `F` / botão `CERCAR`), troca de jogador preditiva e direcional com indicador `▽` no próximo companheiro, proteção de botões segurados na transição de posse e faixa superior (`PAUSA`/`CÂMERA`) livre de captura acidental do toque.
- **Câmeras aprimoradas:** visão em primeira pessoa (`VisaoJogador`) refeita com `ShadowCastingMode.ShadowsOnly` na cabeça e no cabelo (mantém a sombra no gramado, funciona também no Treino e elimina o "funil" que esticava o pescoço ao encolher o osso da cabeça), estabilização vertical da corrida na visão do jogador, câmera de TV com antecipação suave da velocidade da bola e zoom dinâmico ao entrar na grande área, câmera de goleiro compatível com os dois gols (`+X` e `-X`), transição suave perto da linha de fundo sem chicotear nos escanteios e contenção dentro do alambrado.
- **Instalador Windows (`Craquez3D-v1.7.0-setup.exe`):** adicionado instalador gráfico oficial em Português (NSIS, `tools/installer/build_windows_installer.py`) com ícone próprio, atalhos na Área de Trabalho e Menu Iniciar, desinstalador integrado ao Windows e prioridade automática no atualizador do jogo (`AppUpdater`).

## [1.6.0] — 2026-11-18

- **Rostos e cabelos 3D novos:** a cabeça dos jogadores e dos goleiros agora é uma malha separada (`Cabeca`) com fotos sintéticas projetadas em 3D (`Resources/Rostos/Jogador` e `Goleiro`, sem repetir dentro da partida) e 6 cortes de cabelo 3D ajustados ao crânio (`Curto`, `BlackPower`, `Moicano`, `Topete`, `Raspado`, `Cacheado`, além de `Careca`), com a cor do cabelo e o tom de pele do corpo acompanhando o rosto (`tools/blender/cabeca_cabelos.py` e `tools/rostos.py`).
- **Uniformes novos e trama de tecido:** camisa, calção, meiões e chuteiras pintados em 3D direto no corpo (`tools/uniformes.py`), contínuos entre as ilhas da UV e sem marcas antigas: Craquez FC azul com listras finas brancas, Atlético Celeste branco com faixa diagonal celeste, goleiro da casa verde, goleiro visitante laranja e arbitragem preta com detalhes amarelo-limão. Cada visual de pele tem sua própria cor de chuteira e a trama do tecido (`Fabric026`, CC0) foi somada ao normal map das regiões de pano.
- **Versão para TV (`Craquez3D-TV-v1.6.0.apk`, Google TV / Android TV 4K):** pacote próprio (`com.craquez.craquez3d.tv`, nome "Craquez 3D TV"), 32 e 64 bits (ARMv7 + ARM64, compatível com TVs Google TV / Android TV de 32 bits como TCL), banner na tela inicial da TV (Leanback), funcionamento completo por controle remoto (setas + OK + Voltar) ou joystick Bluetooth/USB sem botões de toque na tela, renderização 3D escalada (720p/1080p/1440p) mantendo menus e placar nítidos em 4K nativo, e atualização pelo próprio app buscando o APK de TV.

## [1.5.0] — 2026-11-18

- **Jogadas novas (animações próprias, para os dois lados):** finta de corpo (ameaça um lado e sai pelo outro), pedalada (a perna passa por cima da bola antes de sair), puxada com a sola, arrancada explosiva (primeiras passadas com o tronco à frente) e freada brusca vinda do sprint. Geradas com o kit de poses do projeto no esqueleto do próprio Jogador.fbx (`tools/blender/jogadoranim`).
- **Drible:** devagar continua o corte seco; em velocidade, o botão de drible faz finta de corpo ou pedalada e a bola só sai para o lado no fim da finta. Para trás, puxada com a sola; a arrancada e a freada tocam a animação própria.
- **Sprint:** a corrida de velocidade máxima usa o `Sprint_Loop` da Quaternius (passada de 7,3 m/s): no sprint a animação acelera só ~1,2x (antes 1,5x), pés sem deslizar.
- **Passes mais fortes:** o passe rasteiro chega ao companheiro a 7,5–11 m/s (antes 4,5–7,5); um passe de 20 m sai a ~13 m/s. O lançamento é mais tenso (menos tempo no ar).
- **Chutes mais fortes:** um toque rápido no chute já sai com ~40% da força (antes ~10%, chute "mascado"); força cresce mais rápido no começo e carrega em 0,7 s (antes 0,85 s). Faixas: forte 15–34 m/s, colocado 13–26, rasteiro 15–30.

## [1.4.0] — 2026-11-17

- **Corpos com proporção humana normal:** jogador e goleiro tinham tronco ~30% comprido, pernas curtas (canela principalmente) e pelve larga. Os dois modelos foram corrigidos de forma definitiva nos próprios FBX, usando como referência o esqueleto dos Universal Base Characters / Universal Animation Library da Quaternius: quadril a 52% da altura, joelho a 29%, ombros na mesma altura, pelve mais estreita. Cabeça, mãos, pés, braços, UVs, pesos e nomes dos ossos foram mantidos, então uniformes, rostos e animações continuam valendo. Script reproduzível em `tools/blender/corpo_proporcoes.py` e comparação em `tools/blender/proporcoes_antes_depois.png`.
- **Passadas maiores:** com pernas mais longas, a corrida cobre 4,5 m/s por passada (antes 3,8): menos deslizamento dos pés.
- **Condução de bola colada ao pé:** ponto de controle a 28–36 cm do jogador (antes 35–45), toques mais curtos (trote 14–30 cm, sprint 35–60 cm; antes 24–48 e 55–95), a bola se adianta no máximo 48/85 cm (antes 72/125) e o domínio "puxa" a bola com mais força.
- **Mãos do goleiro no mergulho:** o clipe de mergulho cruzava os braços em X na extensão (mãos "embaralhadas", pior no lado espelhado). Na janela do mergulho, do salto e do soco, as mãos agora vão juntas para a bola por IK, a do lado do mergulho por baixo e a outra por cima, com os cotovelos abertos.

## [1.3.0] — 2026-11-17

- **Jogo-treino:** o modo 11 x 11 passa a se chamar Jogo-treino (menu principal, tela de escolha e configurações).
- **Postura:** o ajuste dos pés no chão puxava o corpo para baixo sempre que os dois pés ficavam no ar (fase aérea da corrida): o jogador afundava até 35 cm e balançava. Agora só corrige o afundamento na hora e a flutuação sustentada, bem devagar.
- **Corpo sem deformar:** o porte (magro, forte, pesado) escalava o corpo inteiro só na largura; braços e pernas esticavam ou achatavam conforme giravam. Agora a escala é sempre uniforme, o porte alarga só o tronco e cabeça e braços mantêm a proporção.
- **Pés sem deslizar:** as animações são importadas "no lugar", então a velocidade de cada uma era 0 e o blend de locomoção usava valores fixos de chute. O setup agora mede a passada real de cada clipe (pé apoiado) e calibra o blend; a altura de cada jogador também entra na conta, e acima do clipe mais rápido a animação acelera até 1,5x.
- **Árbitros:** os assistentes corriam de frente para o lance mesmo indo para o lado (pés deslizando). Agora correm na direção do movimento e só viram para o lance quando estão parados ou devagar.
- **Física dos jogadores:** velocidades realistas (corrida ~4,7–6,1 m/s; sprint ~7,7–9,9 m/s; antes o sprint chegava a 11 m/s), aceleração que diminui perto da velocidade máxima (antes 18 m/s² constantes) e frenagem de 14 m/s². No alambrado o jogador para de verdade (antes ficava "correndo parado" contra a grade).
- **Controles de toque:** analógico flutuante, fixo ou ancorado; arrastar o analógico até a borda externa corre (anel verde); tamanho dos botões pequeno, médio ou grande (Configurações > Toque).
- **Joysticks físicos:** ao usar um controle Bluetooth/USB os botões de toque saem da tela; um toque na tela traz de volta.
- **Configurações:** linhas mais compactas e o Jogo-treino na segunda coluna, para caber as opções novas.

## [1.2.1] — 2026-11-17

- **Texturas dos uniformes:** o uniforme do goleiro visitante (laranja) e o da arbitragem (amarelo) tinham o verde/vermelho original sobrando fora das ilhas da UV (20% e 5% da textura), que vazava como filetes nas costuras pelos mipmaps; a gola do goleiro visitante também ficava verde. `tools/make_kits.py` agora recolore até o pescoço e preenche o espaço entre as ilhas com a cor vizinha; as duas texturas foram regeneradas.
- **Cabelo do goleiro:** o uniforme era aplicado depois do penteado 3D, então black power, coque e faixa do goleiro ganhavam a textura do uniforme. A ordem foi corrigida e o penteado nunca recebe o material do uniforme.
- **Partículas:** o gerenciador de efeitos nunca era criado (poeira de chute e de passada, carrinho e mergulho do goleiro nunca apareciam). Ele foi refeito: materiais URP próprios (sem partículas rosa), uma textura do Kenney por efeito (o modo Sprites exigia todas na mesma textura), pool reaproveitado em vez de criar/destruir a cada passo, sem o rastro da bola que ficaria tocando no centro do campo, poeira que some com o gramado molhado, tufo de grama no chute forte e estrelas no gol.
- **Qualidade baixa:** texturas em meia resolução e menos poeira, para celulares com pouca memória.
- **Atualizador:** se a Release ainda não tem o APK (só o build Windows saiu), o Android não oferece a atualização. Antes baixava a página da Release como se fosse o APK.
- **Memória:** ao sair da partida, efeitos são limpos e as texturas de pele/cabelo dos jogadores são liberadas.
- **Diagnóstico (CI):** a partida automática grava o log pelo stdout (o arquivo de log não era criado e o passo só estourava o tempo) e tira fotos da partida para conferir as texturas.

## [1.2.0] — 2026-11-17

- **Partida 11 x 11 (modo novo):** Craquez FC x Atlético Celeste, com escolha do seu time, clima, duração (2 x 2 a 2 x 12 min), dificuldade e câmera. Tem dois tempos com troca de lado no intervalo, placar, relógio, estatísticas no fim e a opção de jogar de novo.
- **Corpos novos:** jogador e goleiro trocados pelos modelos novos, com esqueleto Mixamo próprio (pesos automáticos, braços em T). As animações antigas usam o esqueleto original como referência e chegam aos corpos novos pelo retarget Humanoid da Unity. **Bola nova** com a arte "Craquez".
- **Uniformes iguais por time:** todos da casa vestem o uniforme azul e todos os visitantes o branco, transferidos dos modelos de uniforme para o corpo do jogador (peito, número e escudos completos). Goleiro da casa verde, goleiro visitante laranja, arbitragem preta e amarela.
- **Elencos:** 22 jogadores e 2 goleiros, com nome e número.
- **Cada jogador é diferente:** 8 aparências, combinando pele (clara, morena, negra), cabelo (preto, castanho, loiro, ruivo) e barba (cheia, sem barba, cavanhaque, bigode, por fazer); **formato do rosto 3D** próprio (maxilar largo ou fino, queixo longo ou curto, nariz grande, bochechas, testa alta, orelhas de abano, rosto comprido), gerado pelo jogo sobre a malha da cabeça; penteado (natural, black power, moicano, coque ou faixa), altura de 1,66 m a 1,97 m conforme a posição e porte (magro, normal, forte, pesado) com peso em kg. O corpo influencia trombadas, velocidade, aceleração e cabeceio. Altura, peso e porte aparecem no cartão do jogador.
- **Inteligência:** cada jogador tem atributos e mentalidade próprios (ofensividade, criatividade, disciplina, agressividade, espírito de equipe, frieza, liderança). Com a bola, ele escolhe entre finalizar, passar, lançar, cruzar, conduzir, driblar e afastar. Sem a bola, dá opção de passe, infiltra sem ficar impedido, pressiona, fecha linhas de passe, marca e recompõe. Na comemoração, os mais solidários correm para abraçar o autor do gol.
- **Regras:** saída de bola, lateral, escanteio, tiro de meta, falta (com barreira), pênalti, impedimento, vantagem, cartões amarelo e vermelho (expulsão) e atendimento médico.
- **Arbitragem:** árbitro no sistema diagonal e dois assistentes em laterais opostas, cada um alinhado ao penúltimo defensor da sua metade, com bandeira, cartões e apito.
- **Física dos jogadores:** trombadas com troca de quantidade de movimento conforme massa e velocidade, desequilíbrio, quedas, contusões (o jogador fica mais lento), carrinho, bote, divididas, domínio que pode escapar com a bola rápida e escorregões no gramado molhado.
- **Animações gratuitas:** Universal Animation Library 1 e 2 da Quaternius (licença CC0): quedas, levantar, atingido, empurrado, carrinho, arremesso lateral, comemorações, reclamação e locomoção e gestos dos árbitros.
- **HUD:** nomes fixos sobre os jogadores, cartão do jogador controlado (função, perfil e fôlego) e minimapa no centro, embaixo. Nomes e minimapa podem ser desligados em Configurações ou na Pausa.
- **Câmera:** nova **visão do jogador** (primeira pessoa, sem ver a própria cabeça), além de TV, atrás do jogador, aérea e dinâmica.
- **Chuva e tempestade:** a chuva vem em ondas de intensidade, o gramado encharca com o tempo, há névoa de respingos rente ao chão, rastro d'água da bola, gotas na lente da câmera, raios visíveis no céu e torcida e apito sintetizados.
- **Controles da partida:** com a bola, ✕ = passe, △ = lançamento, ○/□ = chute. Sem a bola, ✕ = desarme, ○ = carrinho e L1 = trocar de jogador. Os botões de toque mudam de nome conforme a situação.
- **Comandos na tela:** um painel mostra os comandos do momento ("COM A BOLA" / "SEM A BOLA") e pisca ao ganhar ou perder a posse; mostra os botões do controle, as teclas ou os botões de toque, conforme o aparelho.
- **Toque mais responsivo:** o toque é lido antes da partida em cada quadro (sem atraso de um quadro), toques muito rápidos contam, a área dos botões é maior, o analógico responde mais cedo, e o passe apertado um pouco antes de a bola chegar é feito assim que você domina.
- **Câmera TV mais próxima** do lance, com o mesmo ângulo. **Minimapa mais transparente.**
- **Erros na tela:** a caixa vermelha que cobria o jogo virou um selo pequeno e translúcido no canto (toque para ver os detalhes), sem repetir o mesmo erro.
- O esqueleto antigo e não utilizado do modo 11 x 11 (`TeamAI`, `CollectiveMatchMode`, `FootballRulesEngine`, `RefereeSystem`, `PlayerAttributes`) foi substituído por esta implementação.
- **Branch `feat/partida-11x11` mesclada na `main`:** o 11 x 11 da 1.1.4 (não publicada; `MatchSession11v11`, passes do `TeamAI`) foi substituído por esta implementação. Ficam da main o build Windows (`CraquezCI.BuildWindows`), os nomes alternativos de ossos do `MixamoBoneSockets` e as verificações nulas do `TrainingSession`.
- **Correção:** `MixamoBoneSockets` não usa mais `RightArm`/`LeftArm` (braço) como antebraço quando o antebraço não é achado pelo nome; agora cai no Avatar Humanoid.
- **CI Windows:** o workflow "Windows: build e Release" rodava scripts bash no PowerShell do `windows-latest` (falhava no primeiro passo) e o game-ci no Windows exige licença Pro. Agora roda em Linux (imagem `windows-mono`), gera o `.zip` e o anexa à mesma Release do APK.
- **CI:** Android e Windows publicam na mesma Release sem conflito (quem chegar primeiro cria, o outro só anexa o arquivo).

## [1.1.3] — 2026-11-16

- **Correções de Malha e Ossos:** `MixamoBoneSockets` agora suporta nomes de ossos `LeftToes`/`RightToes` (usados pelo modelo `Jogador.fbx`), eliminando jogadores desfigurados/IK quebrado.
- **Controle de Bola:** `ballControlDistance` corrigido de `0.24` para `0.40` (faixa válida 0.30–0.50) nos prefabs `Jogador` e `Goleiro`.
- **Input System:** Habilitado *Active Input Handling = Both* (`activeInputHandler: 2`) para ativar o Input System 1.20.1 já instalado, melhorando suporte a gamepads.
- **Unity:** Confirmada versão 6000.6.9f1 (Unity 6), compatível com todos os pacotes.

## [1.1.2] — 2026-11-16

- **Modo 11v11:** Inclui o modo coletivo (`CollectiveMatchMode`, `TeamAI`, `FootballRulesEngine`, `RefereeSystem`, `PlayerAttributes`) e os sistemas de áudio e partículas (`AudioManager`, `ParticleEffectsManager`), que ainda não tinham sido incluídos em um APK.
- **CI:** Removido o workflow "Unity Build" (`unity-build.yml`), que tentava gerar builds de Windows e Linux e falhava (Unity 6000.0.86f1 em vez de 6000.6.9f1, sem ativação de licença). O jogo é Android: o APK e a Release saem só do workflow "Android: build e Release".
- **Versão:** As tags `v1.1.0` e `v1.1.1` já existiam sem APK, por isso esta versão é a 1.1.2.

## [0.5.9] — 2026-11-15

- **Goleiro (Locomoção 2D e Novas Animações):** Adicionadas animações `GK_Andar` e `GK_Correr` (de `Craquez-GK.zip`), `GK_Recuar`, `GK_Espera` e `GK_Soco`, com BlendTree 2D (`Lateral` + `Forward`) para avançar/recuar/deslocar naturalmente, e duração sincronizada de mergulho e levantamento.
- **Atacante (Física de Chutes Aéreos e Domínio):** Liberado contato em bolas altas (`y <= 2.35m`) para `Cabeceio` e `Voleio`, e acionamento de `Dominio` ao parar a bola.

## [0.5.8] — 2026-11-15

- **Goleiro:** Restaurado mergulho lateral real em `GK_Mergulho` (esquerda/direita via `Mirror`) e incluídas todas as animações extras (`GK_Orientar`, `GK_PosicionarBola`, `GK_Frustracao`, `GK_Carrinho`).
- **Atacante:** Incluídas todas as animações disponíveis de futebol (`Dominio`, `Cabeceio`, `Voleio`, `Carrinho`) integradas ao `AnimatorController` e ao `PlayerController`.

## [0.5.7] — 2026-11-15

- **Goleiro:** Corrigida a colisão contínua em `FixedUpdate` para defender/bloquear qualquer bola na direção do corpo ou luvas mesmo antes do mergulho completo; reação reajustada e faixa central reduzida (`< 0.42m`) para acionar mergulhos laterais (`GK_Mergulho`) e saltos (`GK_Salto`) além da defesa central.
- **Atacante:** Velocidade de corrida aumentada (`jogSpeed = 6.8 m/s`, `sprintSpeed = 10.4 m/s`, `acceleration = 24 m/s²`) com escala de animação sincronizada no `AnimatorController`.

## [0.5.6] — 2026-11-15

- **Atacante:** Substituído pelo modelo `Atacante.glb` (sem `Icosphere`, texturas Albedo/Normal/MetallicSmoothness atualizadas) com todos os movimentos (`Andar` e `Correr` nativos + `Idle`, `Trote`, `Disparada`, `ChuteForte`, `ChuteColocado`, `Cavadinha`, `ChuteRasteiro` e `Drible` retargeted).
- **Goleiro:** Removida elevação artificial `p.y` que fazia o goleiro flutuar no mergulho/salto, removido IK forçado nas mãos durante defesas e recuperações, ampliado ganho de locomoção lateral/frontal e substituído `GK_Defesa` por mocap real Mixamo.

## [0.5.5] — 2026-11-15

- **Goleiro:** Animações de defesa (`GK_Mergulho`, `GK_Caido`, `GK_Levantar`, `GK_Salto`) substituídas por mocap real Mixamo, reação sincronizada ao tempo de voo da bola e movimentação lateral suavizada.
- **Atacante:** Corpo alinhado sem distorção de `Hips` ou `LookAt` IK durante o chute; contato do chute validado apenas com o pé à frente e ponto de preparo estável à frente do corpo.

## [0.5.4] — 2026-11-15

- **Goleiro:** Removido o IK estático dos braços durante o posicionamento normal, liberando as animações nativas (`GK_Pronto`, `GK_DeslocarEsq`, `GK_DeslocarDir`).
- **Sem Vulto:** Desativado `RigidbodyInterpolation.Interpolate` nos corpos cinemáticos do atacante e goleiro.
- **Chutes e Condução:** Janelas de impacto sincronizadas ao movimento real do pé (`speed = 1.25x`) e condução magnética mais próxima ao pé (`0.24m–0.34m`).

## [0.5.3] — 2026-11-15

- **HUD e Menus:** Removidos os avisos centrais de gol/fora/nova bola e o botão "Nova Bola"; texto "Toque na tela para iniciar" agora aparece limpo (sem moldura) no quadro final do logo.
- **Goleiro:** Reposição automática com as mãos (`GK_Lancar`) ou chutão (`GK_Chutao`) após defesas e gols.
- **Atacante e Rosto:** Novos chutes (`ChuteForte`, `ChuteColocado`, `Cavadinha`, `ChuteRasteiro`) e drible (`Drible`) no rig `SmartRigArmature`, corrigindo distorções faciais e pés flutuantes.
- **Poças e Chuva:** Passadas geram ondas e respingos nas poças (com leve arrasto), e uniformes/pele refletem a chuva.

## [0.5.2] — 2026-11-15

- **Jogador (`Idle`):** Removido o movimento de mexer as pernas olhando para baixo; postura parada limpa e natural.
- **Goleiro:** Implementado o pacote de animações reais (base pronta, passadas laterais, salto alto e mergulho) com pés firmes no chão.
- **Poças d'água:** Novo visual orgânico com bordas suaves (`Poca_Mascara`), reflexo calibrado e anéis concêntricos.
- **Atualizações:** Notas de versão sintetizadas e diretas.

## [0.5.1] — 2026-11-14

- **Modelos 3D:** Goleiro e jogador substituídos pelas malhas originais de alta definição do Meshy.
- **Texturas 4K:** Mapas PBR em PNG sem perdas (Albedo, Normal e Metallic/Smoothness).
- **Esqueleto:** Alinhamento em T-pose canônica preservando normais originais.

## [0.5.0] — 2026-11-14

- **Colisão em 3 Camadas:** Cápsula principal isolada da bola (fim dos rebotes invisíveis).
- **Locomoção e Chutes:** Curva de aceleração/frenagem suave, condução magnética e chutes precisos.
- **Sistema de Goleiro:** Posicionamento no arco do gol, defesa por quadrantes e encaixe nas luvas.

## [0.4.0] — 2026-11-14

- **Animações:** Transferência das animações nativas sem torção nos pés.
- **Defesas:** Detecção contínua nas mãos do goleiro contra chutes fortes.

## [0.3.1] — 2026-11-14

- **Aterramento (`FootGrounder`):** Ajuste automático da altura dos pés sobre o gramado.

## [0.3.0] — 2026-11-14

- **Esqueleto e Câmera:** Correção da pose base dos modelos, câmera mais próxima e botão Sair.

## [0.2.0] — 2026-11-14

- **Interface e Comandos:** Novo visual dos menus/HUD, condução por toques, chute rasteiro e dribles.

## [0.1.4] — 2026-11-14

- **Android:** Correção do colisor das traves no build IL2CPP e novo ícone adaptativo.

## [0.1.3] — 2026-11-14

- **Vídeo e Sombras:** Abertura convertida para WebM e sombras suaves no Unity 6 (URP).

## [0.1.2] — 2026-11-14

- **CI/CD:** Geração e publicação automática do APK assinado via GitHub Actions.

## [0.1.1] — 2026-11-14

- **Pesos de Malha:** Correção dos pesos de pele na cabeça do goleiro e mãos do jogador.

## [0.1.0] — 2026-11-14

- **Lançamento Inicial:** Arena de Treino com física da bola, climas dinâmicos (Sol, Chuva, Tempestade), modos Atacante/Goleiro e atualização integrada.
