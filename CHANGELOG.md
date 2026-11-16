# Changelog

Formato: [Versionamento Semântico](https://semver.org/lang/pt-BR/). A versão do app vem do arquivo `VERSION`.

## [1.2.0] — 2026-11-16

- **Partida 11 x 11 (modo novo):** Craquez FC x Atlético Celeste, com escolha do seu time, clima, duração (2 x 2 a 2 x 12 min), dificuldade e câmera. Tem dois tempos com troca de lado no intervalo, placar, relógio, estatísticas no fim e a opção de jogar de novo.
- **Elencos:** 22 jogadores e 2 goleiros alternando os dois rostos do jogo, com uniformes por time (gerados por `tools/make_kits.py`, preservando pele, cabelo e rosto), altura variada, nome e número.
- **Inteligência:** cada jogador tem atributos e mentalidade próprios (ofensividade, criatividade, disciplina, agressividade, espírito de equipe, frieza, liderança). Com a bola, ele escolhe entre finalizar, passar, lançar, cruzar, conduzir, driblar e afastar. Sem a bola, dá opção de passe, infiltra sem ficar impedido, pressiona, fecha linhas de passe, marca e recompõe. Na comemoração, os mais solidários correm para abraçar o autor do gol.
- **Regras:** saída de bola, lateral, escanteio, tiro de meta, falta (com barreira), pênalti, impedimento, vantagem, cartões amarelo e vermelho (expulsão) e atendimento médico.
- **Arbitragem:** árbitro no sistema diagonal e dois assistentes em laterais opostas, cada um alinhado ao penúltimo defensor da sua metade, com bandeira, cartões e apito.
- **Física dos jogadores:** trombadas com troca de quantidade de movimento conforme massa e velocidade, desequilíbrio, quedas, contusões (o jogador fica mais lento), carrinho, bote, divididas, domínio que pode escapar com a bola rápida e escorregões no gramado molhado.
- **Animações gratuitas:** Universal Animation Library 1 e 2 da Quaternius (licença CC0): quedas, levantar, atingido, empurrado, carrinho, arremesso lateral, comemorações, reclamação e locomoção e gestos dos árbitros.
- **HUD:** nomes fixos sobre os jogadores, cartão do jogador controlado (função, perfil e fôlego) e minimapa no centro, embaixo. Nomes e minimapa podem ser desligados em Configurações ou na Pausa.
- **Câmera:** nova **visão do jogador** (primeira pessoa, sem ver a própria cabeça), além de TV, atrás do jogador, aérea e dinâmica.
- **Chuva e tempestade:** a chuva vem em ondas de intensidade, o gramado encharca com o tempo, há névoa de respingos rente ao chão, rastro d'água da bola, gotas na lente da câmera, raios visíveis no céu e torcida e apito sintetizados.
- **Controles da partida:** com a bola, ✕ = passe, △ = lançamento, ○/□ = chute. Sem a bola, ✕ = desarme, ○ = carrinho e L1 = trocar de jogador. Os botões de toque mudam de nome conforme a situação.
- O esqueleto antigo e não utilizado do modo 11 x 11 (`TeamAI`, `CollectiveMatchMode`, `FootballRulesEngine`, `RefereeSystem`, `PlayerAttributes`) foi substituído por esta implementação.

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
