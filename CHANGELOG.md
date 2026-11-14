# Changelog

Formato: [Versionamento Semântico](https://semver.org/lang/pt-BR/). A versão do app vem do arquivo `VERSION`.

## [0.3.0] — 2026-11-14

### Corrigido
- Pés "tortos para trás" e corpo desalinhado: a pose de repouso do jogador e do goleiro tinha pernas inclinadas para trás
  (até 11°), coluna e braços fora da T-pose; a Unity repassava esse erro a todas as animações. Os dois esqueletos foram
  endireitados para a T-pose canônica do Mixamo (pernas retas, pés para frente, coluna e braços alinhados).

### Goleiro
- Animações novas de verdade: postura de espera (agachado, mãos à frente), deslocamento lateral em base,
  mergulho com o corpo estendido e braços acima da cabeça, queda e levantar do lado certo, salto com as mãos no alto
  e defesa no peito (encaixe). Antes ele usava animações genéricas sem defesa.

### Jogabilidade e visual
- Condução: depois do toque a bola acompanha a direção pedida e não abre demais à frente (controle bem mais firme).
- Câmera padrão mais próxima e mais baixa — jogador maior na tela, gol visível ao fundo.
- Brilho do gramado molhado sem "estourar" sob os refletores; poças e áreas encharcadas mais naturais.
- Botão **SAIR DO JOGO** no menu principal e na pausa.

## [0.2.0] — 2026-11-14

### Novo visual
- Menus refeitos com a identidade do logo (azul-noite, azul elétrico, amarelo e vermelho): cartões grandes para toque,
  animações de entrada, logo na tela inicial, estádio ao vivo ao fundo.
- "Arena de treino" virou uma tela de preparação: cartões de clima com ícones animados (o estádio ao fundo já muda
  para o clima escolhido) e cartões Atacante / Goleiro.
- Configurações em duas colunas com seções, chaves liga/desliga e barra de volume; pausa em painel central.
- HUD novo (painel de clima/vento, botões arredondados, barra de força com faixa de risco, nome do drible na tela).
- Botões de toque redesenhados, com área de toque maior e resposta visual ao apertar.

### Jogabilidade
- Condução de bola por toques (antes a bola ficava "presa" ao pé): toque curto no trote, longo no sprint,
  o jogador ajusta a passada para alcançar a bola, domínio que amortece bolas rápidas, mudança brusca de direção com toque curto.
- Novos comandos: chute rasteiro, drible (corte lateral, puxada para trás, parar a bola) e arrancada.
- Resposta mais rápida: aceleração e giro maiores, freada e pivô mais firmes, zona morta menor no analógico
  (toque e controle) e fim do sprint automático ao empurrar o analógico até o fim.
- Botões que estavam apertados ao sair de um menu não disparam chute sem querer.

### Corrigido
- Tela muito escura na chuva e na tempestade: luz e ambiente mais fortes, céu sem o tom laranja,
  refletores que iluminam de verdade, pós-processamento mais claro, poças e áreas encharcadas menos escuras.
- Goleiro com os braços para trás: a animação de espera deixava os cotovelos atrás do corpo;
  agora ele fica na postura de defesa (mãos à frente na altura da cintura, cotovelos abertos).

## [0.1.4] — 2026-11-14

### Corrigido
- Jogo travava logo após a tela da Unity, só com o gramado e uma trave: o build Android removia o colisor das traves
  (componente criado só em tempo de execução) e a montagem da arena parava ali, sem jogadores, gols nem menus.
  Agora o colisor é garantido no código, a remoção de código da engine foi desligada e há um `link.xml` de proteção.
- A montagem da arena continua mesmo se uma parte decorativa (gols, poças, áudio, clima) falhar.

### Adicionado
- Ícone do app a partir da capa do vídeo de abertura (logo Craquez), com ícone adaptativo para Android 8+.
- Caixa vermelha de erro na tela quando algo falha no celular (toque para fechar) — tire um print e envie.

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
