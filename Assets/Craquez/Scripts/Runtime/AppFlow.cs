using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Craquez
{
    /// <summary>
    /// Telas do jogo:
    /// Aviso (programado por IA) → Vídeo de abertura → "Toque na tela para iniciar" →
    /// [Atualização obrigatória, se houver versão nova] → Menu principal → Preparar treino / Configurações → Jogo / Pausa.
    /// Visual com a identidade do logo (azul-noite, azul elétrico, amarelo, vermelho), cartões grandes para toque,
    /// estádio ao vivo ao fundo (o clima escolhido aparece na hora) e navegação completa por controle e teclado.
    /// </summary>
    public class AppFlow : MonoBehaviour
    {
        public TrainingSession session;
        public AppUpdater updater;
        public VideoClip introClip;
        public CameraRig cameraRig;
        public Texture2D logo;
        public ArenaBootstrap arenaBootstrap;

        enum Tela { Aviso, Video, ToqueIniciar, Atualizacao, Principal, Arena, Partida11v11, ConfigEquipes, Formacao, Config, Pausa, Jogando }
        enum Tipo { Botao, Lista, Alternar, Volume, Info }

        /// <summary>Item navegável de uma tela.</summary>
        class Item
        {
            public string label, desc, section;
            public Tipo tipo;
            public Func<string> value;
            public Func<bool> on;
            public Action<int> change;
            public Action activate;
            public int column;
            public Rect rect; // preenchido no desenho (para toque)
        }

        Tela tela;
        Tela configBack;
        float telaTime;
        int sel;
        Ui ui;
        VideoPlayer vp;
        RenderTexture rt;
        bool videoStarted, videoDone;
        int climaSel; // 0..2 = clima, 3 = aleatório
        int arenaRow; // 0 = clima, 1 = papel, 2 = iniciar, 3 = voltar
        readonly List<Item> items = new List<Item>();
        bool usingPointer;

        // altura (fração da tela) onde o texto aparece, logo abaixo do logo no último quadro do vídeo
        const float LogoTextY = 0.72f;

        static readonly string[] Climas = { "SOL", "CHUVA", "TEMPESTADE", "ALEATÓRIO" };
        static readonly string[] ClimaDesc =
        {
            "Céu limpo, gramado seco e rápido. Vento fraco.",
            "Gramado molhado, poças que freiam a bola, vento moderado.",
            "Chuva forte, raios, rajadas e muitas poças. Goleiro escorrega.",
            "Surpresa: o clima é sorteado ao entrar."
        };

        void Start()
        {
            ui = new Ui();
            GameSettings.Aplicar();
            climaSel = Selecao.Aleatorio ? 3 : (int)Selecao.Clima;
            session.OnPauseRequested += Pause;
            session.Preview();
            if (updater != null) updater.Check();
            Go(Tela.Aviso);
        }

        void OnDestroy()
        {
            if (session != null) session.OnPauseRequested -= Pause;
            if (rt != null) rt.Release();
        }

        void Go(Tela t)
        {
            tela = t;
            telaTime = 0f;
            sel = 0;
            arenaRow = 0;
        }

        void Pause()
        {
            session.SetPaused(true);
            Go(Tela.Pausa);
        }

        // ------------------------------------------------------------ vídeo

        void StartVideo()
        {
            if (introClip == null) { videoDone = true; return; }
            rt = new RenderTexture(1280, 720, 0);
            vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.clip = introClip;
            vp.isLooping = false;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.skipOnDrop = true;
            vp.loopPointReached += p => videoDone = true;
            vp.errorReceived += (p, msg) => { Debug.LogWarning("Vídeo de abertura: " + msg); videoDone = true; };
            vp.prepareCompleted += p => { p.Play(); videoStarted = true; };
            vp.Prepare();
        }

        // ------------------------------------------------------------ lógica

        void Update()
        {
            telaTime += Time.unscaledDeltaTime;

            if (updater != null && updater.UpdateRequired &&
                (tela == Tela.Principal || tela == Tela.Arena || tela == Tela.Config))
                Go(Tela.Atualizacao);

            switch (tela)
            {
                case Tela.Aviso:
                    if (telaTime > 4.2f || (telaTime > 1.2f && InputBridge.AnyPressed)) { Go(Tela.Video); StartVideo(); }
                    break;
                case Tela.Video:
                    // toque durante o vídeo: pula direto para o quadro final (logo) em vez de parar no meio
                    if (!videoDone && vp != null && videoStarted && telaTime > 1.0f && InputBridge.AnyPressed && vp.frameCount > 2)
                    {
                        long last = (long)vp.frameCount - 2;
                        if (vp.frame < last) vp.frame = last;
                    }
                    if (videoDone || (!videoStarted && telaTime > 8f))
                    {
                        if (vp != null && vp.isPlaying) vp.Pause();
                        Go(Tela.ToqueIniciar);
                    }
                    break;
                case Tela.ToqueIniciar:
                    if (telaTime > 0.5f && InputBridge.AnyPressed)
                        Go(updater != null && updater.UpdateRequired ? Tela.Atualizacao : Tela.Principal);
                    break;
                case Tela.Atualizacao:
                    if (updater == null || (!updater.UpdateRequired && updater.State != AppUpdater.Estado.Verificando)) { Go(Tela.Principal); break; }
                    if (InputBridge.ConfirmPressed) updater.DownloadAndInstall();
                    break;
                case Tela.Jogando:
                    break;
                case Tela.Arena:
                    NavigateArena();
                    break;
                case Tela.Partida11v11:
                    NavigatePartida11v11();
                    break;
                case Tela.ConfigEquipes:
                    NavigateConfigEquipes();
                    break;
                case Tela.Formacao:
                    NavigateFormacao();
                    break;
                default:
                    NavigateList();
                    break;
            }
        }

        void NavigateList()
        {
            BuildItems();
            if (items.Count == 0) return;
            int dv = InputBridge.NavVertical();
            int dh = InputBridge.NavHorizontal();
            if (dv != 0 || dh != 0) usingPointer = false;
            if (dv != 0)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    sel = (sel + dv + items.Count) % items.Count;
                    if (items[sel].tipo != Tipo.Info) break;
                }
            }
            sel = Mathf.Clamp(sel, 0, items.Count - 1);
            var it = items[sel];
            if (dh != 0)
            {
                if (it.change != null) it.change(dh);
                else if (tela == Tela.Config)
                {
                    // muda de coluna
                    int col = it.column + dh;
                    for (int i = 0; i < items.Count; i++)
                        if (items[i].column == col && items[i].tipo != Tipo.Info) { sel = i; break; }
                }
            }
            if (InputBridge.ConfirmPressed) Activate(it);
            else if (InputBridge.BackPressed) Back();
        }

        static void Activate(Item it)
        {
            if (it.activate != null) it.activate();
            else if (it.change != null) it.change(1);
        }

        void NavigateArena()
        {
            int dv = InputBridge.NavVertical();
            int dh = InputBridge.NavHorizontal();
            if (dv != 0 || dh != 0) usingPointer = false;
            if (dv != 0) arenaRow = Mathf.Clamp(arenaRow + dv, 0, 3);
            if (dh != 0)
            {
                if (arenaRow == 0) SetClima((climaSel + dh + 4) % 4);
                else if (arenaRow == 1) TogglePapel();
                else if (arenaRow >= 2) arenaRow = arenaRow == 2 ? 3 : 2;
            }
            if (InputBridge.ConfirmPressed)
            {
                if (arenaRow == 0 || arenaRow == 1) arenaRow++;
                else if (arenaRow == 2) StartTraining();
                else Go(Tela.Principal);
            }
            else if (InputBridge.BackPressed) Go(Tela.Principal);
        }

        void NavigatePartida11v11()
        {
            int dv = InputBridge.NavVertical();
            int dh = InputBridge.NavHorizontal();
            if (dv != 0 || dh != 0) usingPointer = false;
            // Navigate list for Partida11v11
            NavigateList();
        }

        void NavigateConfigEquipes()
        {
            int dv = InputBridge.NavVertical();
            int dh = InputBridge.NavHorizontal();
            if (dv != 0 || dh != 0) usingPointer = false;
            // Navigate list for ConfigEquipes
            NavigateList();
        }

        void NavigateFormacao()
        {
            int dv = InputBridge.NavVertical();
            int dh = InputBridge.NavHorizontal();
            if (dv != 0 || dh != 0) usingPointer = false;
            // Navigate list for Formacao
            NavigateList();
        }

        void SetClima(int c)
        {
            climaSel = c;
            // o estádio ao fundo já mostra o clima escolhido
            if (c < 3 && session.weather != null && session.weather.Current != (Clima)c) session.weather.Apply((Clima)c);
        }

        static void TogglePapel()
        {
            GameSettings.Papel = GameSettings.Papel == PapelControle.Atacante ? PapelControle.Goleiro : PapelControle.Atacante;
        }

        void Back()
        {
            switch (tela)
            {
                case Tela.Arena: Go(Tela.Principal); break;
                case Tela.Partida11v11: Go(Tela.Principal); break;
                case Tela.ConfigEquipes: Go(Tela.Partida11v11); break;
                case Tela.Formacao: Go(Tela.ConfigEquipes); break;
                case Tela.Config: Go(configBack); break;
                case Tela.Pausa: Resume(); break;
            }
        }

        void Resume()
        {
            session.SetPaused(false);
            Go(Tela.Jogando);
        }

        void BuildItems()
        {
            items.Clear();
            switch (tela)
            {
                case Tela.Principal:
                    items.Add(new Item { label = "JOGO-TREINO", desc = "Finalização contra o goleiro • escolha o clima e quem você controla", activate = () => Go(Tela.Arena) });
                    items.Add(new Item { label = "PARTIDA 11x11", desc = "Jogo coletivo completo • escolha times, formações e táticas", activate = () => Go(Tela.Partida11v11) });
                    items.Add(new Item { label = "CONFIGURAÇÕES", desc = "Controles, câmera, gráficos, som e atualizações", activate = () => { configBack = Tela.Principal; Go(Tela.Config); } });
                    items.Add(new Item { label = "SAIR DO JOGO", desc = "Fechar o Craquez 3D", activate = Application.Quit });
                    break;

                case Tela.Partida11v11:
                    items.Add(new Item { label = "CONFIGURAR EQUIPES", desc = "Escolha times, escudos e cores", activate = () => Go(Tela.ConfigEquipes) });
                    items.Add(new Item { label = "CONFIGURAR FORMAÇÃO", desc = "Defina táticas e posições iniciais", activate = () => Go(Tela.Formacao) });
                    items.Add(new Item { label = "INICIAR PARTIDA", desc = "Começar o jogo 11x11", activate = () => StartMatch11v11() });
                    items.Add(new Item { label = "VOLTAR", activate = () => Go(Tela.Principal) });
                    break;

                case Tela.ConfigEquipes:
                    items.Add(new Item { label = "TIME MANDANTE", desc = "Configure o time da casa", activate = () => { /* TODO: team editor */ } });
                    items.Add(new Item { label = "TIME VISITANTE", desc = "Configure o time visitante", activate = () => { /* TODO: team editor */ } });
                    items.Add(new Item { label = "VOLTAR", activate = () => Go(Tela.Partida11v11) });
                    break;

                case Tela.Formacao:
                    items.Add(new Item { label = "FORMAÇÃO MANDANTE", desc = "Escolha a formação do time da casa", activate = () => { /* TODO: formation editor */ } });
                    items.Add(new Item { label = "FORMAÇÃO VISITANTE", desc = "Escolha a formação do time visitante", activate = () => { /* TODO: formation editor */ } });
                    items.Add(new Item { label = "MENTALIDADE MANDANTE", desc = "Define a postura tática do time da casa", activate = () => { /* TODO: mentality editor */ } });
                    items.Add(new Item { label = "MENTALIDADE VISITANTE", desc = "Define a postura tática do time visitante", activate = () => { /* TODO: mentality editor */ } });
                    items.Add(new Item { label = "VOLTAR", activate = () => Go(Tela.ConfigEquipes) });
                    break;

                case Tela.Pausa:
                    items.Add(new Item { label = "CONTINUAR", activate = Resume });
                    items.Add(new Item { label = "CÂMERA", tipo = Tipo.Lista, value = () => CameraRig.Nome(cameraRig.mode), change = d => cameraRig.Cycle() });
                    items.Add(new Item { label = "CONFIGURAÇÕES", activate = () => { configBack = Tela.Pausa; Go(Tela.Config); } });
                    items.Add(new Item { label = "SAIR PARA O MENU", activate = () => { session.SetPaused(false); session.Preview(); Go(Tela.Principal); } });
                    items.Add(new Item { label = "SAIR DO JOGO", activate = Application.Quit });
                    break;

                case Tela.Config:
                    items.Add(new Item
                    {
                        section = "CONTROLES", label = "Layout do controle", tipo = Tipo.Lista,
                        value = () => GameSettings.Layout == LayoutControle.ChuteNoCirculo ? "Chute no ○ / B" : "Chute no □ / X",
                        change = d => GameSettings.Layout = GameSettings.Layout == LayoutControle.ChuteNoCirculo ? LayoutControle.ChuteNoQuadrado : LayoutControle.ChuteNoCirculo
                    });
                    items.Add(new Item { label = "Controle conectado", tipo = Tipo.Info, value = ControllerName });
                    items.Add(new Item
                    {
                        section = "CÂMERA", label = "Câmera padrão", tipo = Tipo.Lista,
                        value = () => CameraRig.Nome(CameraRig.ModosAtacante[Mathf.Clamp(GameSettings.CameraPadrao, 0, CameraRig.ModosAtacante.Length - 1)]),
                        change = d => GameSettings.CameraPadrao = (GameSettings.CameraPadrao + d + CameraRig.ModosAtacante.Length) % CameraRig.ModosAtacante.Length
                    });
                    items.Add(new Item
                    {
                        section = "GRÁFICOS E SOM", column = 1, label = "Qualidade gráfica", tipo = Tipo.Lista,
                        value = () => GameSettings.Qualidade == QualidadeGrafica.Baixa ? "Baixa" : (GameSettings.Qualidade == QualidadeGrafica.Media ? "Média" : "Alta"),
                        change = d => GameSettings.Qualidade = (QualidadeGrafica)(((int)GameSettings.Qualidade + d + 3) % 3)
                    });
                    items.Add(new Item
                    {
                        column = 1, label = "Volume", tipo = Tipo.Volume,
                        value = () => Mathf.RoundToInt(GameSettings.Volume * 100f) + "%",
                        change = d => GameSettings.Volume = Mathf.Clamp01(Mathf.Round(GameSettings.Volume * 10f) / 10f + d * 0.1f)
                    });
                    items.Add(new Item { column = 1, label = "Vibração", tipo = Tipo.Alternar, on = () => GameSettings.Vibracao, change = d => GameSettings.Vibracao = !GameSettings.Vibracao });
                    items.Add(new Item
                    {
                        section = "SISTEMA", column = 1, label = "Atualizações", tipo = Tipo.Botao, value = UpdateStatus,
                        activate = () => { if (updater == null) return; if (updater.UpdateRequired) Go(Tela.Atualizacao); else updater.Check(); }
                    });
                    items.Add(new Item { column = 1, label = "Versão instalada", tipo = Tipo.Info, value = () => "v" + Application.version });
                    items.Add(new Item { column = 0, label = "VOLTAR", tipo = Tipo.Botao, activate = () => Go(configBack) });
                    break;
            }
        }

        string UpdateStatus()
        {
            if (updater == null) return "-";
            switch (updater.State)
            {
                case AppUpdater.Estado.Verificando: return "Verificando...";
                case AppUpdater.Estado.EmDia: return "Em dia";
                case AppUpdater.Estado.Disponivel: return "Nova: v" + updater.LatestVersion;
                case AppUpdater.Estado.SemConexao: return "Sem conexão";
                case AppUpdater.Estado.Erro: return "Erro • tentar";
                case AppUpdater.Estado.Baixando: return "Baixando " + Mathf.RoundToInt(updater.Progress * 100f) + "%";
                default: return "Verificar";
            }
        }

        static string ControllerName()
        {
            switch (InputBridge.ControleConectado)
            {
                case TipoControle.PlayStation: return "PlayStation";
                case TipoControle.Xbox: return "Xbox";
                case TipoControle.Nintendo: return "Nintendo";
                case TipoControle.Generico: return "Genérico";
                default: return "Nenhum";
            }
        }

        void StartTraining()
        {
            Selecao.Aleatorio = climaSel == 3;
            if (climaSel < 3) Selecao.Clima = (Clima)climaSel;
            Clima c = Selecao.Resolver();
            Go(Tela.Jogando);
            if (arenaBootstrap != null)
            {
                arenaBootstrap.StartTraining(c, GameSettings.Papel);
            }
            else if (session != null)
            {
                session.Begin(c, GameSettings.Papel);
            }
        }

        void StartMatch11v11()
        {
            Selecao.Aleatorio = climaSel == 3;
            if (climaSel < 3) Selecao.Clima = (Clima)climaSel;
            Clima c = Selecao.Resolver();
            Go(Tela.Jogando);
            if (arenaBootstrap != null)
            {
                arenaBootstrap.StartMatch11v11(c);
            }
            // Fallback: if no arenaBootstrap, just start training session
            else if (session != null)
            {
                session.Begin(c, GameSettings.Papel);
            }
        }

        // ------------------------------------------------------------ desenho

        float S { get { return ui.Scale; } }

        void OnGUI()
        {
            ui.Begin();
            GUI.depth = 0;
            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.TouchDown) usingPointer = true;
            float W = Screen.width, H = Screen.height;

            switch (tela)
            {
                case Tela.Aviso: DrawAviso(W, H); return;
                case Tela.Video:
                    UiKit.Fill(new Rect(0, 0, W, H), Color.black);
                    if (rt != null && videoStarted) GUI.DrawTexture(new Rect(0, 0, W, H), rt, ScaleMode.ScaleAndCrop);
                    return;
                case Tela.ToqueIniciar: DrawToque(W, H); return;
                case Tela.Atualizacao: DrawUpdate(W, H); return;
                case Tela.Jogando: return;
                case Tela.Principal: DrawPrincipal(W, H); break;
                case Tela.Arena: DrawArena(W, H); break;
                case Tela.Partida11v11: DrawPartida11v11(W, H); break;
                case Tela.ConfigEquipes: DrawConfigEquipes(W, H); break;
                case Tela.Formacao: DrawFormacao(W, H); break;
                case Tela.Config: DrawConfig(W, H); break;
                case Tela.Pausa: DrawPausa(W, H); break;
            }
            DrawFooter(W, H);
        }

        // --- fundo comum: estádio ao vivo + degradê azul-noite à esquerda e embaixo
        void Backdrop(float W, float H, float leftWidth)
        {
            UiKit.Fill(new Rect(0, 0, W, H), UiKit.A(UiKit.Navy, 0.25f));
            UiKit.Fill(new Rect(0, 0, leftWidth * 0.55f, H), UiKit.A(UiKit.Navy, 0.82f));
            UiKit.Gradient(new Rect(leftWidth * 0.55f, 0, leftWidth * 0.75f, H), UiKit.A(UiKit.Navy, 0.82f), false);
            UiKit.Gradient(new Rect(0, H * 0.62f, W, H * 0.38f), UiKit.A(UiKit.Navy, 0.9f), true);
            // faixa diagonal nas cores do logo
            UiKit.Fill(new Rect(0, 0, W, 6 * S), UiKit.A(UiKit.Blue, 0.9f));
            UiKit.Fill(new Rect(0, 6 * S, W * 0.33f, 4 * S), UiKit.Yellow);
            UiKit.Fill(new Rect(W * 0.33f, 6 * S, W * 0.2f, 4 * S), UiKit.Red);
        }

        void DrawLogo(Rect r, float alpha)
        {
            if (logo != null)
            {
                Color o = GUI.color; GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(r, logo, ScaleMode.ScaleToFit);
                GUI.color = o;
            }
            else UiKit.Text(r, "CRAQUEZ 3D", ui.Title, new Color(1f, 1f, 1f, alpha), 3 * S);
        }

        void DrawAviso(float W, float H)
        {
            UiKit.Fill(new Rect(0, 0, W, H), Color.black);
            float a = Mathf.Clamp01(telaTime / 0.6f) * Mathf.Clamp01((4.2f - telaTime) / 0.6f);
            float lw = Mathf.Min(560 * S, W * 0.5f);
            DrawLogo(new Rect((W - lw) * 0.5f, H * 0.18f, lw, lw * 0.53f), a);
            float y = H * 0.18f + lw * 0.53f + 40 * S;
            UiKit.Fill(new Rect(W * 0.5f - 60 * S, y, 120 * S, 3 * S), UiKit.A(UiKit.Yellow, a));
            var st = ui.BodyCenter;
            UiKit.Text(new Rect(W * 0.1f, y + 24 * S, W * 0.8f, 90 * S), "Este jogo foi totalmente programado por Inteligência Artificial.", st, new Color(1f, 1f, 1f, a), 0f);
        }

        void DrawToque(float W, float H)
        {
            UiKit.Fill(new Rect(0, 0, W, H), Color.black);
            if (rt != null && videoStarted) GUI.DrawTexture(new Rect(0, 0, W, H), rt, ScaleMode.ScaleAndCrop);
            else
            {
                UiKit.Fill(new Rect(0, 0, W, H), UiKit.Navy);
                float lw = Mathf.Min(760 * S, W * 0.6f);
                DrawLogo(new Rect((W - lw) * 0.5f, H * 0.16f, lw, lw * 0.53f), 1f);
            }
            // só texto (sem moldura), aparece depois que o vídeo termina no logo
            float fade = Mathf.Clamp01((telaTime - 0.15f) / 0.6f);
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.2f);
            string txt = InputBridge.ControleConectado != TipoControle.Nenhum && !Application.isMobilePlatform
                ? "PRESSIONE QUALQUER BOTÃO" : "TOQUE NA TELA PARA INICIAR";
            Rect tr = new Rect(0, H * LogoTextY, W, 70 * S);
            UiKit.Text(tr, txt, new GUIStyle(ui.Pill) { alignment = TextAnchor.MiddleCenter }, new Color(1f, 1f, 1f, fade * (0.55f + 0.45f * pulse)), 2 * S);
        }

        // --- menu principal: logo + cartões grandes
        void DrawPrincipal(float W, float H)
        {
            BuildItems();
            Backdrop(W, H, W * 0.62f);
            float lw = Mathf.Min(620 * S, W * 0.42f);
            float intro = UiKit.EaseOut(telaTime / 0.5f);
            DrawLogo(new Rect(70 * S, 40 * S - (1f - intro) * 30 * S, lw, lw * 0.53f), intro);

            float cw = Mathf.Min(700 * S, W * 0.5f), ch = 132 * S, gap = 22 * S;
            float y0 = 40 * S + lw * 0.53f + 30 * S;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                float t = UiKit.EaseOut((telaTime - 0.1f - i * 0.08f) / 0.35f);
                Rect r = new Rect(70 * S - (1f - t) * 80 * S, y0 + i * (ch + gap), cw, ch);
                bool selected = i == sel && !usingPointer;
                DrawCard(r, it.label, it.desc, selected, i == 0 ? UiKit.Red : (it.activate == Application.Quit ? new Color(0.45f, 0.5f, 0.6f) : UiKit.Blue), t);
                if (ui.Hit(r)) { sel = i; Activate(it); }
            }
        }

        void DrawCard(Rect r, string title, string desc, bool selected, Color accent, float alpha)
        {
            float rad = 20 * S;
            if (selected) { r.x += 10 * S; }
            UiKit.ShadowRect(r, 14 * S, 0.5f * alpha);
            Color fill = selected ? Color.Lerp(UiKit.NavyLight, accent, 0.55f) : UiKit.A(UiKit.NavyLight, 0.88f);
            UiKit.RoundRect(r, UiKit.A(fill, alpha), rad);
            UiKit.RoundRect(new Rect(r.x, r.y, 10 * S, r.height), UiKit.A(selected ? UiKit.Yellow : accent, alpha), 5 * S);
            if (selected) UiKit.RoundBorder(r, UiKit.A(UiKit.Yellow, alpha), rad, 3 * S);
            float px = r.x + 38 * S;
            UiKit.Text(new Rect(px, r.y + 16 * S, r.width - 110 * S, 56 * S), title, ui.H1, new Color(1f, 1f, 1f, alpha), 2 * S);
            if (!string.IsNullOrEmpty(desc))
                UiKit.Text(new Rect(px, r.y + 70 * S, r.width - 110 * S, 50 * S), desc, ui.Small, UiKit.A(UiKit.TextDim, alpha), 0f);
            // seta
            UiKit.Text(new Rect(r.xMax - 70 * S, r.y, 50 * S, r.height), "›", new GUIStyle(ui.Title) { alignment = TextAnchor.MiddleCenter },
                       UiKit.A(selected ? UiKit.Yellow : Color.white, alpha * (selected ? 1f : 0.5f)), 0f);
        }

        // --- preparar treino: cartões de clima e de papel
        void DrawArena(float W, float H)
        {
            Backdrop(W, H, W * 0.75f);
            float x0 = 70 * S;
            float intro = UiKit.EaseOut(telaTime / 0.4f);
            UiKit.Text(new Rect(x0, 34 * S, W, 80 * S), "ARENA DE TREINO", ui.Title, new Color(1f, 1f, 1f, intro), 3 * S);
            UiKit.Text(new Rect(x0, 104 * S, W, 40 * S), "Finalização: você contra o goleiro. Sem placar, sem tempo.", ui.Body, UiKit.A(UiKit.TextDim, intro), 0f);

            // clima
            float y = 170 * S;
            SectionLabel(new Rect(x0, y, 600 * S, 40 * S), "CLIMA", arenaRow == 0 && !usingPointer);
            y += 50 * S;
            float avail = Mathf.Min(W - x0 * 2f, 1500 * S);
            float gap = 20 * S;
            float cw = (avail - gap * 3f) / 4f, ch = 210 * S;
            for (int i = 0; i < 4; i++)
            {
                Rect r = new Rect(x0 + i * (cw + gap), y, cw, ch);
                bool chosen = climaSel == i;
                bool focus = chosen && arenaRow == 0 && !usingPointer;
                DrawWeatherCard(r, i, chosen, focus);
                if (ui.Hit(r)) { SetClima(i); arenaRow = 0; }
            }

            // papel
            y += ch + 36 * S;
            SectionLabel(new Rect(x0, y, 600 * S, 40 * S), "VOCÊ CONTROLA", arenaRow == 1 && !usingPointer);
            y += 50 * S;
            float pw = (Mathf.Min(avail, 1100 * S) - gap) / 2f, ph = 130 * S;
            for (int i = 0; i < 2; i++)
            {
                var papel = (PapelControle)i;
                Rect r = new Rect(x0 + i * (pw + gap), y, pw, ph);
                bool chosen = GameSettings.Papel == papel;
                bool focus = chosen && arenaRow == 1 && !usingPointer;
                string t = i == 0 ? "ATACANTE" : "GOLEIRO";
                string d = i == 0 ? "Conduza, drible e finalize. O goleiro é controlado pela IA." : "Defenda os chutes da IA: mergulhe, salte e saia do gol.";
                DrawOption(r, t, d, chosen, focus, i == 0 ? UiKit.Red : UiKit.Green);
                if (ui.Hit(r)) { GameSettings.Papel = papel; arenaRow = 1; }
            }

            // ações
            y += ph + 40 * S;
            float bh = 100 * S, bw = 520 * S;
            Rect start = new Rect(x0, Mathf.Min(y, H - bh - 90 * S), bw, bh);
            bool fStart = arenaRow == 2 && !usingPointer;
            float glow = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            UiKit.ShadowRect(start, 16 * S, 0.6f);
            UiKit.RoundRect(start, fStart ? Color.Lerp(UiKit.Red, new Color(1f, 0.35f, 0.2f), glow) : UiKit.Red, bh * 0.5f);
            if (fStart) UiKit.RoundBorder(start, UiKit.Yellow, bh * 0.5f, 4 * S);
            UiKit.Text(start, "INICIAR TREINO  ›", new GUIStyle(ui.H1) { alignment = TextAnchor.MiddleCenter }, Color.white, 2 * S);
            if (ui.Hit(start)) StartTraining();

            Rect back = new Rect(start.xMax + 24 * S, start.y + 10 * S, 260 * S, bh - 20 * S);
            bool fBack = arenaRow == 3 && !usingPointer;
            UiKit.RoundRect(back, UiKit.A(UiKit.NavyLight, 0.9f), back.height * 0.5f);
            UiKit.RoundBorder(back, fBack ? UiKit.Yellow : UiKit.A(Color.white, 0.35f), back.height * 0.5f, (fBack ? 4 : 2) * S);
            UiKit.Text(back, "VOLTAR", new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleCenter }, Color.white, 0f);
            if (ui.Hit(back)) Go(Tela.Principal);
        }

        // --- PARTIDA 11x11: menu principal do modo coletivo
        void DrawPartida11v11(float W, float H)
        {
            BuildItems();
            Backdrop(W, H, W * 0.62f);
            float lw = Mathf.Min(620 * S, W * 0.42f);
            float intro = UiKit.EaseOut(telaTime / 0.5f);
            DrawLogo(new Rect(70 * S, 40 * S - (1f - intro) * 30 * S, lw, lw * 0.53f), intro);

            float cw = Mathf.Min(700 * S, W * 0.5f), ch = 132 * S, gap = 22 * S;
            float y0 = 40 * S + lw * 0.53f + 30 * S;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                float t = UiKit.EaseOut((telaTime - 0.1f - i * 0.08f) / 0.35f);
                Rect r = new Rect(70 * S - (1f - t) * 80 * S, y0 + i * (ch + gap), cw, ch);
                bool selected = i == sel && !usingPointer;
                DrawCard(r, it.label, it.desc, selected, i == 0 ? UiKit.Red : (i == 2 ? UiKit.Green : (it.activate == Application.Quit ? new Color(0.45f, 0.5f, 0.6f) : UiKit.Blue)), t);
                if (ui.Hit(r)) { sel = i; Activate(it); }
            }
        }

        // --- CONFIGURAR EQUIPES
        void DrawConfigEquipes(float W, float H)
        {
            BuildItems();
            Backdrop(W, H, W * 0.62f);
            float x0 = 70 * S;
            float intro = UiKit.EaseOut(telaTime / 0.4f);
            UiKit.Text(new Rect(x0, 34 * S, W, 80 * S), "CONFIGURAR EQUIPES", ui.Title, new Color(1f, 1f, 1f, intro), 3 * S);
            UiKit.Text(new Rect(x0, 104 * S, W, 40 * S), "Personalize os times da partida 11x11.", ui.Body, UiKit.A(UiKit.TextDim, intro), 0f);

            float cw = Mathf.Min(700 * S, W * 0.5f), ch = 132 * S, gap = 22 * S;
            float y0 = 180 * S;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                float t = UiKit.EaseOut((telaTime - 0.1f - i * 0.08f) / 0.35f);
                Rect r = new Rect(70 * S - (1f - t) * 80 * S, y0 + i * (ch + gap), cw, ch);
                bool selected = i == sel && !usingPointer;
                DrawCard(r, it.label, it.desc, selected, it.activate == null ? new Color(0.45f, 0.5f, 0.6f) : UiKit.Blue, t);
                if (ui.Hit(r)) { sel = i; Activate(it); }
            }
        }

        // --- CONFIGURAR FORMAÇÃO
        void DrawFormacao(float W, float H)
        {
            BuildItems();
            Backdrop(W, H, W * 0.62f);
            float x0 = 70 * S;
            float intro = UiKit.EaseOut(telaTime / 0.4f);
            UiKit.Text(new Rect(x0, 34 * S, W, 80 * S), "CONFIGURAR FORMAÇÃO", ui.Title, new Color(1f, 1f, 1f, intro), 3 * S);
            UiKit.Text(new Rect(x0, 104 * S, W, 40 * S), "Defina formações, mentalidades e táticas para ambos os times.", ui.Body, UiKit.A(UiKit.TextDim, intro), 0f);

            float cw = Mathf.Min(700 * S, W * 0.5f), ch = 132 * S, gap = 22 * S;
            float y0 = 180 * S;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                float t = UiKit.EaseOut((telaTime - 0.1f - i * 0.08f) / 0.35f);
                Rect r = new Rect(70 * S - (1f - t) * 80 * S, y0 + i * (ch + gap), cw, ch);
                bool selected = i == sel && !usingPointer;
                DrawCard(r, it.label, it.desc, selected, it.activate == null ? new Color(0.45f, 0.5f, 0.6f) : UiKit.Blue, t);
                if (ui.Hit(r)) { sel = i; Activate(it); }
            }
        }

        void SectionLabel(Rect r, string text, bool focus)
        {
            UiKit.Fill(new Rect(r.x, r.y + 8 * S, 6 * S, r.height - 16 * S), focus ? UiKit.Yellow : UiKit.Blue);
            UiKit.Text(new Rect(r.x + 18 * S, r.y, r.width, r.height), text, ui.H2, focus ? UiKit.Yellow : Color.white, 2 * S);
        }

        void DrawWeatherCard(Rect r, int i, bool chosen, bool focus)
        {
            Color top, bottom;
            switch (i)
            {
                case 0: top = new Color(1f, 0.72f, 0.2f); bottom = new Color(0.2f, 0.55f, 1f); break;
                case 1: top = new Color(0.45f, 0.55f, 0.7f); bottom = new Color(0.12f, 0.2f, 0.35f); break;
                case 2: top = new Color(0.35f, 0.25f, 0.6f); bottom = new Color(0.06f, 0.06f, 0.16f); break;
                default: top = UiKit.Red; bottom = UiKit.Blue; break;
            }
            float lift = chosen ? 8 * S : 0f;
            r.y -= lift;
            UiKit.ShadowRect(r, 12 * S, chosen ? 0.65f : 0.4f);
            UiKit.RoundRect(r, bottom, 18 * S);
            Color o = GUI.color; GUI.color = top;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * 0.75f), UiKit.GradV, ScaleMode.StretchToFill, true, 0f, top, Vector4.zero, new Vector4(18 * S, 18 * S, 0f, 0f));
            GUI.color = o;
            if (i == 3)
            {
                // listras com as cores do logo
                float sw = r.width / 3f;
                UiKit.Fill(new Rect(r.x + 18 * S, r.y + 20 * S, sw - 18 * S, 8 * S), UiKit.Yellow);
                UiKit.Fill(new Rect(r.x + sw, r.y + 20 * S, sw, 8 * S), UiKit.Red);
                UiKit.Fill(new Rect(r.x + sw * 2f, r.y + 20 * S, sw - 18 * S, 8 * S), UiKit.Blue);
            }
            DrawWeatherIcon(new Vector2(r.x + r.width * 0.5f, r.y + 62 * S), 34 * S, i);
            UiKit.RoundRect(new Rect(r.x, r.yMax - 98 * S, r.width, 98 * S), UiKit.A(UiKit.Navy, 0.7f), 18 * S);
            UiKit.Text(new Rect(r.x + 16 * S, r.yMax - 96 * S, r.width - 32 * S, 40 * S), Climas[i], new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleCenter }, Color.white, 2 * S);
            UiKit.Text(new Rect(r.x + 12 * S, r.yMax - 58 * S, r.width - 24 * S, 54 * S), ClimaDesc[i], new GUIStyle(ui.Tiny) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Normal }, UiKit.TextDim, 0f);
            if (chosen) UiKit.RoundBorder(r, focus ? UiKit.Yellow : UiKit.A(Color.white, 0.85f), 18 * S, (focus ? 5 : 3) * S);
        }

        void DrawWeatherIcon(Vector2 c, float r, int i)
        {
            float t = Time.unscaledTime;
            switch (i)
            {
                case 0:
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f + t * 0.5f;
                        Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 1.25f;
                        UiKit.Disc(p, r * 0.16f, new Color(1f, 0.95f, 0.6f, 0.9f));
                    }
                    UiKit.Disc(c, r * 0.8f, new Color(1f, 0.9f, 0.35f));
                    break;
                case 1:
                case 2:
                    Cloud(c, r, i == 2 ? new Color(0.55f, 0.58f, 0.68f) : new Color(0.9f, 0.93f, 1f));
                    for (int k = 0; k < (i == 2 ? 6 : 4); k++)
                    {
                        float x = c.x - r + k * r * (i == 2 ? 0.4f : 0.6f);
                        float y = c.y + r * 0.6f + Mathf.Repeat(t * 60f * S + k * 17f, r * 0.9f);
                        UiKit.Fill(new Rect(x, y, 3 * S, r * 0.3f), new Color(0.7f, 0.85f, 1f, 0.85f));
                    }
                    if (i == 2 && Mathf.Repeat(t, 2.6f) < 0.12f)
                        UiKit.Fill(new Rect(c.x - r * 0.1f, c.y + r * 0.3f, r * 0.2f, r * 0.9f), new Color(1f, 1f, 0.7f, 0.95f));
                    break;
                default:
                    var st = new GUIStyle(ui.Title) { alignment = TextAnchor.MiddleCenter, fontSize = ui.R(70) };
                    UiKit.Text(new Rect(c.x - r * 2f, c.y - r * 1.4f, r * 4f, r * 2.8f), "?", st, Color.white, 3 * S);
                    break;
            }
        }

        static void Cloud(Vector2 c, float r, Color col)
        {
            UiKit.Disc(c + new Vector2(-r * 0.55f, r * 0.1f), r * 0.55f, col);
            UiKit.Disc(c + new Vector2(r * 0.1f, -r * 0.2f), r * 0.75f, col);
            UiKit.Disc(c + new Vector2(r * 0.7f, r * 0.15f), r * 0.5f, col);
            UiKit.RoundRect(new Rect(c.x - r * 1.1f, c.y + r * 0.05f, r * 2.3f, r * 0.6f), col, r * 0.3f);
        }

        void DrawOption(Rect r, string title, string desc, bool chosen, bool focus, Color accent)
        {
            float rad = 18 * S;
            UiKit.ShadowRect(r, 12 * S, 0.45f);
            UiKit.RoundRect(r, chosen ? Color.Lerp(UiKit.NavyLight, accent, 0.45f) : UiKit.A(UiKit.NavyLight, 0.85f), rad);
            // marcador de seleção
            Vector2 c = new Vector2(r.x + 44 * S, r.y + r.height * 0.5f);
            UiKit.RingAt(c, 18 * S, Color.white);
            if (chosen) UiKit.Disc(c, 10 * S, UiKit.Yellow);
            UiKit.Text(new Rect(r.x + 80 * S, r.y + 14 * S, r.width - 100 * S, 50 * S), title, ui.H1, Color.white, 2 * S);
            UiKit.Text(new Rect(r.x + 80 * S, r.y + 64 * S, r.width - 100 * S, 56 * S), desc, ui.Small, UiKit.TextDim, 0f);
            if (chosen) UiKit.RoundBorder(r, focus ? UiKit.Yellow : UiKit.A(Color.white, 0.8f), rad, (focus ? 5 : 3) * S);
        }

        // --- configurações em duas colunas com seções
        void DrawConfig(float W, float H)
        {
            BuildItems();
            Backdrop(W, H, W);
            UiKit.Fill(new Rect(0, 0, W, H), UiKit.A(UiKit.Navy, 0.45f));
            float x0 = 70 * S;
            UiKit.Text(new Rect(x0, 34 * S, W, 80 * S), "CONFIGURAÇÕES", ui.Title, Color.white, 3 * S);

            float colGap = 40 * S;
            float colW = Mathf.Min((W - x0 * 2f - colGap) / 2f, 820 * S);
            float rowH = 84 * S, rowGap = 12 * S;
            float[] y = { 150 * S, 150 * S };
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.label == "VOLTAR") continue;
                int c = it.column;
                float cx = x0 + c * (colW + colGap);
                if (!string.IsNullOrEmpty(it.section))
                {
                    if (y[c] > 160 * S) y[c] += 14 * S;
                    UiKit.Text(new Rect(cx + 4 * S, y[c], colW, 40 * S), it.section, ui.Tiny, UiKit.Cyan, 0f);
                    y[c] += 42 * S;
                }
                Rect r = new Rect(cx, y[c], colW, rowH);
                it.rect = r;
                DrawSettingRow(r, it, i == sel && !usingPointer);
                if (it.tipo != Tipo.Info && ui.Hit(r)) { sel = i; Activate(it); }
                y[c] += rowH + rowGap;
            }

            // voltar + legenda dos botões
            int bi = items.FindIndex(it => it.label == "VOLTAR");
            float by = Mathf.Max(y[0], y[1]) + 16 * S;
            Rect back = new Rect(x0, Mathf.Min(by, H - 170 * S), 300 * S, 78 * S);
            bool fb = bi == sel && !usingPointer;
            UiKit.RoundRect(back, fb ? UiKit.Red : UiKit.A(UiKit.NavyLight, 0.95f), back.height * 0.5f);
            UiKit.RoundBorder(back, fb ? UiKit.Yellow : UiKit.A(Color.white, 0.4f), back.height * 0.5f, (fb ? 4 : 2) * S);
            UiKit.Text(back, "‹  VOLTAR", new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleCenter }, Color.white, 0f);
            if (bi >= 0 && ui.Hit(back)) { sel = bi; Activate(items[bi]); }

            string legend = "Chute " + InputBridge.Glyph("chute") + "   Colocado " + InputBridge.Glyph("colocado") + "   Cavadinha " + InputBridge.Glyph("cavadinha") +
                            "   Rasteiro " + InputBridge.Glyph("rasteiro") + "   Drible " + InputBridge.Glyph("drible") + "   Arrancada " + InputBridge.Glyph("arrancada") +
                            "   Correr " + InputBridge.Glyph("correr");
            UiKit.Text(new Rect(back.xMax + 30 * S, back.y, W - back.xMax - 100 * S, back.height), legend, ui.Small, UiKit.TextDim, 0f);
        }

        void DrawSettingRow(Rect r, Item it, bool focus)
        {
            float rad = 16 * S;
            UiKit.RoundRect(r, focus ? Color.Lerp(UiKit.NavyLight, UiKit.Blue, 0.45f) : UiKit.A(UiKit.NavyLight, it.tipo == Tipo.Info ? 0.55f : 0.9f), rad);
            if (focus) UiKit.RoundBorder(r, UiKit.Yellow, rad, 3 * S);
            UiKit.Text(new Rect(r.x + 28 * S, r.y, r.width * 0.5f, r.height), it.label, ui.H2, it.tipo == Tipo.Info ? UiKit.TextDim : Color.white, 0f);

            float vw = r.width * 0.46f, vh = r.height - 26 * S;
            Rect v = new Rect(r.xMax - vw - 16 * S, r.y + 13 * S, vw, vh);
            switch (it.tipo)
            {
                case Tipo.Lista:
                    UiKit.RoundRect(v, UiKit.A(UiKit.Navy, 0.8f), vh * 0.5f);
                    UiKit.Text(new Rect(v.x + 10 * S, v.y, 40 * S, v.height), "‹", new GUIStyle(ui.H1) { alignment = TextAnchor.MiddleCenter }, UiKit.Yellow, 0f);
                    UiKit.Text(new Rect(v.xMax - 50 * S, v.y, 40 * S, v.height), "›", new GUIStyle(ui.H1) { alignment = TextAnchor.MiddleCenter }, UiKit.Yellow, 0f);
                    UiKit.Text(new Rect(v.x + 50 * S, v.y, v.width - 100 * S, v.height), it.value(), ui.Pill, Color.white, 0f);
                    break;
                case Tipo.Volume:
                    {
                        float vol = GameSettings.Volume;
                        Rect bar = new Rect(v.x + 10 * S, v.center.y - 7 * S, v.width - 120 * S, 14 * S);
                        UiKit.RoundRect(bar, UiKit.A(UiKit.Navy, 0.9f), 7 * S);
                        UiKit.RoundRect(new Rect(bar.x, bar.y, Mathf.Max(14 * S, bar.width * vol), bar.height), UiKit.Cyan, 7 * S);
                        UiKit.Disc(new Vector2(bar.x + bar.width * vol, bar.center.y), 15 * S, Color.white);
                        UiKit.Text(new Rect(bar.xMax + 10 * S, v.y, 100 * S, v.height), it.value(), ui.Pill, Color.white, 0f);
                        // toque na barra ajusta direto
                        Event e = Event.current;
                        Rect hit = new Rect(bar.x - 10 * S, v.y, bar.width + 20 * S, v.height);
                        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && hit.Contains(e.mousePosition))
                        {
                            GameSettings.Volume = Mathf.Clamp01((e.mousePosition.x - bar.x) / bar.width);
                            e.Use();
                        }
                        break;
                    }
                case Tipo.Alternar:
                    {
                        bool on = it.on();
                        Rect sw = new Rect(v.xMax - 120 * S, v.center.y - 22 * S, 110 * S, 44 * S);
                        UiKit.RoundRect(sw, on ? UiKit.Green : UiKit.A(Color.white, 0.25f), 22 * S);
                        UiKit.Disc(new Vector2(on ? sw.xMax - 22 * S : sw.x + 22 * S, sw.center.y), 18 * S, Color.white);
                        UiKit.Text(new Rect(v.x, v.y, v.width - 140 * S, v.height), on ? "Ligada" : "Desligada", new GUIStyle(ui.Body) { alignment = TextAnchor.MiddleRight }, Color.white, 0f);
                        break;
                    }
                case Tipo.Botao:
                    if (it.value != null)
                    {
                        UiKit.RoundRect(v, UiKit.A(UiKit.Blue, 0.85f), vh * 0.5f);
                        UiKit.Text(v, it.value(), ui.Pill, Color.white, 0f);
                    }
                    break;
                case Tipo.Info:
                    UiKit.Text(new Rect(v.x, v.y, v.width - 10 * S, v.height), it.value(), new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleRight }, Color.white, 0f);
                    break;
            }
        }

        // --- pausa: painel central
        void DrawPausa(float W, float H)
        {
            BuildItems();
            UiKit.Fill(new Rect(0, 0, W, H), UiKit.A(UiKit.Navy, 0.72f));
            float pw = Mathf.Min(720 * S, W * 0.6f);
            float bh = 92 * S, gap = 16 * S;
            float ph = 150 * S + items.Count * (bh + gap) + 20 * S;
            float intro = UiKit.EaseOut(telaTime / 0.25f);
            Rect p = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f + (1f - intro) * 40 * S, pw, ph);
            UiKit.ShadowRect(p, 30 * S, 0.7f);
            UiKit.RoundRect(p, UiKit.A(UiKit.Navy, 0.96f), 28 * S);
            UiKit.RoundBorder(p, UiKit.A(UiKit.Blue, 0.8f), 28 * S, 2 * S);
            UiKit.Fill(new Rect(p.x + 40 * S, p.y + 112 * S, 100 * S, 4 * S), UiKit.Yellow);
            UiKit.Text(new Rect(p.x + 40 * S, p.y + 26 * S, pw, 80 * S), "PAUSA", ui.Title, Color.white, 3 * S);
            string info = Arena.Nome(session.weather.Current) + "  •  " + (session.Role == PapelControle.Goleiro ? "Goleiro" : "Atacante");
            UiKit.Text(new Rect(p.x, p.y + 40 * S, pw - 40 * S, 60 * S), info, new GUIStyle(ui.Body) { alignment = TextAnchor.MiddleRight }, UiKit.TextDim, 0f);

            float y = p.y + 140 * S;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                Rect r = new Rect(p.x + 40 * S, y, pw - 80 * S, bh);
                bool focus = i == sel && !usingPointer;
                UiKit.RoundRect(r, focus ? Color.Lerp(UiKit.NavyLight, i == 0 ? UiKit.Red : UiKit.Blue, 0.7f) : UiKit.A(UiKit.NavyLight, 0.95f), 18 * S);
                if (focus) UiKit.RoundBorder(r, UiKit.Yellow, 18 * S, 3 * S);
                UiKit.Text(new Rect(r.x + 30 * S, r.y, r.width, r.height), it.label, ui.H2, Color.white, 0f);
                if (it.value != null)
                    UiKit.Text(new Rect(r.x, r.y, r.width - 30 * S, r.height), "‹  " + it.value() + "  ›", new GUIStyle(ui.Body) { alignment = TextAnchor.MiddleRight }, UiKit.Yellow, 0f);
                if (ui.Hit(r)) { sel = i; Activate(it); }
                y += bh + gap;
            }
        }

        // --- rodapé: dicas de botões + versão
        void DrawFooter(float W, float H)
        {
            float h = 56 * S;
            string hint;
            if (InputBridge.ControleConectado != TipoControle.Nenhum || !Application.isMobilePlatform)
                hint = InputBridge.Glyph("confirmar") + " Selecionar     " + InputBridge.Glyph("voltar") + " Voltar     ‹ › Alterar";
            else hint = "Toque para escolher";
            UiKit.Text(new Rect(70 * S, H - h - 14 * S, W * 0.6f, h), hint, ui.Small, UiKit.A(Color.white, 0.75f), 0f);
            string right = "v" + Application.version;
            var ctl = InputBridge.ControleConectado;
            if (ctl != TipoControle.Nenhum) right = "Controle " + ControllerName() + " conectado     " + right;
            UiKit.Text(new Rect(W * 0.4f, H - h - 14 * S, W * 0.6f - 50 * S, h), right, new GUIStyle(ui.Small) { alignment = TextAnchor.MiddleRight }, UiKit.A(Color.white, 0.6f), 0f);
        }

        void DrawUpdate(float W, float H)
        {
            UiKit.Fill(new Rect(0, 0, W, H), UiKit.Navy);
            float lw = Mathf.Min(420 * S, W * 0.3f);
            DrawLogo(new Rect((W - lw) * 0.5f, 30 * S, lw, lw * 0.53f), 1f);
            float y = 40 * S + lw * 0.53f;
            UiKit.Text(new Rect(0, y, W, 70 * S), "ATUALIZAÇÃO DISPONÍVEL", new GUIStyle(ui.Title) { alignment = TextAnchor.MiddleCenter, fontSize = ui.R(52) }, Color.white, 3 * S);
            string info = "Nova versão v" + updater.LatestVersion + "  (instalada: v" + updater.CurrentVersion + "). Atualize para continuar jogando.";
            UiKit.Text(new Rect(W * 0.1f, y + 76 * S, W * 0.8f, 50 * S), info, ui.BodyCenter, UiKit.TextDim, 0f);
            if (!string.IsNullOrEmpty(updater.ReleaseNotes))
            {
                Rect notes = new Rect(W * 0.2f, y + 140 * S, W * 0.6f, H * 0.2f);
                UiKit.RoundRect(notes, UiKit.A(UiKit.NavyLight, 0.9f), 18 * S);
                UiKit.Text(new Rect(notes.x + 24 * S, notes.y + 16 * S, notes.width - 48 * S, notes.height - 32 * S), updater.ReleaseNotes,
                           new GUIStyle(ui.Small) { alignment = TextAnchor.UpperLeft }, Color.white, 0f);
            }

            float bw = 640 * S, bh = 110 * S;
            Rect b = new Rect((W - bw) * 0.5f, H * 0.68f, bw, bh);
            if (updater.State == AppUpdater.Estado.Baixando)
            {
                UiKit.RoundRect(b, UiKit.A(Color.black, 0.5f), bh * 0.5f);
                UiKit.RoundRect(new Rect(b.x, b.y, Mathf.Max(bh, b.width * updater.Progress), b.height), UiKit.Blue, bh * 0.5f);
                UiKit.Text(b, "BAIXANDO " + Mathf.RoundToInt(updater.Progress * 100f) + "%", new GUIStyle(ui.H1) { alignment = TextAnchor.MiddleCenter }, Color.white, 2 * S);
            }
            else
            {
                string label = updater.State == AppUpdater.Estado.Instalando ? "INSTALANDO..." : "ATUALIZAR AGORA";
                UiKit.ShadowRect(b, 16 * S, 0.6f);
                UiKit.RoundRect(b, UiKit.Red, bh * 0.5f);
                UiKit.RoundBorder(b, UiKit.Yellow, bh * 0.5f, 4 * S);
                UiKit.Text(b, label, new GUIStyle(ui.H1) { alignment = TextAnchor.MiddleCenter }, Color.white, 2 * S);
                if (ui.Hit(b)) updater.DownloadAndInstall();
            }
            if (!string.IsNullOrEmpty(updater.ErrorMessage))
                UiKit.Text(new Rect(W * 0.1f, b.yMax + 20 * S, W * 0.8f, 60 * S), updater.ErrorMessage, ui.BodyCenter, new Color(1f, 0.6f, 0.55f), 0f);
            UiKit.Text(new Rect(0, H - 70 * S, W, 50 * S), "O Android mostrará a confirmação de instalação do sistema — confirme para concluir.", ui.SmallCenter, UiKit.TextDim, 0f);
        }
    }
}
