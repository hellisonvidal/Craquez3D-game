# Changelog

Formato: [Versionamento Semântico](https://semver.org/lang/pt-BR/). A versão do app vem do arquivo `VERSION`.

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
