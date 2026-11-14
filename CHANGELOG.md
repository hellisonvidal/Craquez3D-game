# Changelog

Formato: [Versionamento Semântico](https://semver.org/lang/pt-BR/). A versão do app vem do arquivo `VERSION`.

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
