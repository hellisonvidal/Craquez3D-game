# Changelog

Formato: [Versionamento Semântico](https://semver.org/lang/pt-BR/). A versão do app vem do arquivo `VERSION`.

## [0.1.3] — 2026-11-14

### Corrigido
- Vídeo de abertura não entrava no APK gerado pelo GitHub: convertido para WebM (VP8 + Vorbis), formato que a Unity
  importa em Linux, Windows e macOS e que o Android reproduz. O build automático agora falha se o vídeo não for importado.
- Setup do URP no Unity 6: sombras suaves configuradas pelo campo serializado (a propriedade é somente leitura no URP 17).

## [0.1.2] — 2026-11-14

### Adicionado
- Build automático no GitHub: a cada versão nova no arquivo `VERSION` (na main), o GitHub Actions gera o APK assinado,
  cria a tag e publica a Release com as notas deste CHANGELOG — o atualizador do app baixa dali.
- Execução manual do build pela aba Actions, com opção de só gerar o APK.
- Setup do projeto sem janelas (`CraquezSetup.Configure`) e build por linha de comando (`CraquezCI.BuildAndroid`).

## [0.1.1] — 2026-11-14

### Corrigido
- Goleiro "torto": substituído pelo novo modelo do rascunho (em pé, simétrico, pesos 100% nos ossos Humanoid),
  colocado em T-pose, 1,90 m e 32 mil triângulos. O modelo anterior tinha o rosto e o cabelo presos a ossos
  extras do peito (a cabeça não acompanhava as animações) e o tronco inclinado 20° na pose de repouso.
- Jogador: parte da mão esquerda estava presa a ossos extras do antebraço; pesos transferidos para as mãos.

## [0.1.0] — 2026-11-14

Primeira versão: Arena de Treino de finalização.

### Adicionado
- Projeto Unity 6 (URP) configurado automaticamente para Android (IL2CPP, ARM64, Vulkan/GLES3, paisagem) e Windows.
- Aviso "jogo totalmente programado por IA", vídeo de abertura e tela "toque na tela para iniciar".
- Menu principal (Arena de Treino, Configurações) e menu de pausa, navegáveis por toque, teclado e controle.
- Arena: estádio do rascunho em escala real, gramado oficial com marcações, gols oficiais com rede deformável.
- Modos: controlar o atacante (goleiro com IA) ou controlar o goleiro (atacante com IA).
- Clima Sol / Chuva / Tempestade / Aleatório: chuva, respingos, poças com marcas de pingos, áreas encharcadas,
  vento com rajadas, raios e trovões, refletores, gramado molhado, pós-processamento por clima.
- Física da bola: arrasto, efeito Magnus, bola sem efeito, quique com atrito/rotação, rolamento por superfície,
  poças que param a bola, derrapagem no encharcado.
- Chutes forte, colocado e cavadinha com carga de força e imprecisão realista.
- Goleiro: posicionamento, reação, mergulho, salto, IK das mãos, encaixe/espalmada/ponta dos dedos/rebote.
- Seis câmeras (padrão: atrás do atacante).
- Controles PlayStation, Xbox e genéricos; layout "chute no ○/B" ou "chute no □/X".
- Configurações: layout, câmera padrão, qualidade gráfica, volume, vibração, atualizações.
- Atualização obrigatória dentro do app via GitHub Releases (download + instalador do Android).
- Rig Humanoid no jogador e no goleiro; animações Mixamo dos rascunhos.
