# Changelog

Formato: [Versionamento Semântico](https://semver.org/lang/pt-BR/). A versão do app vem do arquivo `VERSION`.

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
