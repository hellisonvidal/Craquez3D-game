using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Craquez
{
    /// <summary>
    /// Telas do jogo:
    /// Aviso (programado por IA) → Vídeo de abertura → "Toque na tela para iniciar" →
    /// [Atualização obrigatória, se houver versão nova] → Menu principal (Arena de Treino, Configurações) → Jogo / Pausa.
    /// Navegação por toque, mouse, teclado e controles (PlayStation, Xbox e genéricos).
    /// </summary>
    public class AppFlow : MonoBehaviour
    {
        public TrainingSession session;
        public AppUpdater updater;
        public VideoClip introClip;
        public CameraRig cameraRig;

        enum Tela { Aviso, Video, ToqueIniciar, Atualizacao, Principal, Arena, Config, Pausa, Jogando }

        class Row
        {
            public string label;
            public Func<string> value;
            public Action<int> change;   // -1 / +1
            public Action activate;
            public bool info;            // somente leitura
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

            // atualização obrigatória: assim que detectada, bloqueia os menus
            if (updater != null && updater.UpdateRequired &&
                (tela == Tela.Principal || tela == Tela.Arena || tela == Tela.Config))
                Go(Tela.Atualizacao);

            switch (tela)
            {
                case Tela.Aviso:
                    if (telaTime > 4.5f || (telaTime > 1.2f && InputBridge.AnyPressed)) { Go(Tela.Video); StartVideo(); }
                    break;
                case Tela.Video:
                    if (videoDone || (!videoStarted && telaTime > 8f) || (telaTime > 1.5f && InputBridge.AnyPressed))
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
                default:
                    NavigateList();
                    break;
            }
        }

        void NavigateList()
        {
            var rows = Rows();
            if (rows.Count == 0) return;
            int dv = InputBridge.NavVertical();
            if (dv != 0)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    sel = (sel + dv + rows.Count) % rows.Count;
                    if (!rows[sel].info) break;
                }
            }
            sel = Mathf.Clamp(sel, 0, rows.Count - 1);
            int dh = InputBridge.NavHorizontal();
            var r = rows[sel];
            if (dh != 0 && r.change != null) r.change(dh);
            if (InputBridge.ConfirmPressed)
            {
                if (r.activate != null) r.activate();
                else if (r.change != null) r.change(1);
            }
            else if (InputBridge.BackPressed) Back();
        }

        void Back()
        {
            switch (tela)
            {
                case Tela.Arena: Go(Tela.Principal); break;
                case Tela.Config: Go(configBack); break;
                case Tela.Pausa: Resume(); break;
            }
        }

        void Resume()
        {
            session.SetPaused(false);
            Go(Tela.Jogando);
        }

        static string[] Climas = { "Sol", "Chuva", "Tempestade", "Aleatório" };

        List<Row> Rows()
        {
            var list = new List<Row>();
            switch (tela)
            {
                case Tela.Principal:
                    list.Add(new Row { label = "ARENA DE TREINO", activate = () => Go(Tela.Arena) });
                    list.Add(new Row { label = "CONFIGURAÇÕES", activate = () => { configBack = Tela.Principal; Go(Tela.Config); } });
                    if (!Application.isMobilePlatform && !Application.isEditor)
                        list.Add(new Row { label = "SAIR", activate = Application.Quit });
                    break;

                case Tela.Arena:
                    list.Add(new Row { label = "Clima", value = () => Climas[climaSel], change = d => climaSel = (climaSel + d + 4) % 4 });
                    list.Add(new Row { label = "Você controla", value = () => GameSettings.Papel == PapelControle.Atacante ? "Atacante" : "Goleiro",
                                       change = d => GameSettings.Papel = GameSettings.Papel == PapelControle.Atacante ? PapelControle.Goleiro : PapelControle.Atacante });
                    list.Add(new Row { label = "INICIAR TREINO", activate = StartTraining });
                    list.Add(new Row { label = "VOLTAR", activate = () => Go(Tela.Principal) });
                    break;

                case Tela.Pausa:
                    list.Add(new Row { label = "CONTINUAR", activate = Resume });
                    list.Add(new Row { label = "NOVA BOLA", activate = () => { session.ResetDrill(); Resume(); } });
                    list.Add(new Row { label = "Câmera", value = () => CameraRig.Nome(cameraRig.mode), change = d => cameraRig.Cycle() });
                    list.Add(new Row { label = "CONFIGURAÇÕES", activate = () => { configBack = Tela.Pausa; Go(Tela.Config); } });
                    list.Add(new Row { label = "SAIR PARA O MENU", activate = () => { session.SetPaused(false); session.Preview(); Go(Tela.Principal); } });
                    break;

                case Tela.Config:
                    list.Add(new Row
                    {
                        label = "Layout do controle",
                        value = () => GameSettings.Layout == LayoutControle.ChuteNoCirculo ? "Chute no ○ / B" : "Chute no □ / X",
                        change = d => GameSettings.Layout = GameSettings.Layout == LayoutControle.ChuteNoCirculo ? LayoutControle.ChuteNoQuadrado : LayoutControle.ChuteNoCirculo
                    });
                    list.Add(new Row
                    {
                        label = "Câmera padrão (atacante)",
                        value = () => CameraRig.Nome(CameraRig.ModosAtacante[Mathf.Clamp(GameSettings.CameraPadrao, 0, CameraRig.ModosAtacante.Length - 1)]),
                        change = d => GameSettings.CameraPadrao = (GameSettings.CameraPadrao + d + CameraRig.ModosAtacante.Length) % CameraRig.ModosAtacante.Length
                    });
                    list.Add(new Row
                    {
                        label = "Qualidade gráfica",
                        value = () => GameSettings.Qualidade == QualidadeGrafica.Baixa ? "Baixa" : (GameSettings.Qualidade == QualidadeGrafica.Media ? "Média" : "Alta"),
                        change = d => GameSettings.Qualidade = (QualidadeGrafica)(((int)GameSettings.Qualidade + d + 3) % 3)
                    });
                    list.Add(new Row
                    {
                        label = "Volume",
                        value = () => Mathf.RoundToInt(GameSettings.Volume * 100f) + "%",
                        change = d => GameSettings.Volume = Mathf.Repeat(Mathf.Round(GameSettings.Volume * 10f) / 10f + d * 0.1f + 0.0001f, 1.1f)
                    });
                    list.Add(new Row { label = "Vibração", value = () => GameSettings.Vibracao ? "Ligada" : "Desligada", change = d => GameSettings.Vibracao = !GameSettings.Vibracao });
                    list.Add(new Row { label = "Atualizações", value = UpdateStatus, activate = () => { if (updater == null) return; if (updater.UpdateRequired) Go(Tela.Atualizacao); else updater.Check(); } });
                    list.Add(new Row { label = "Controle conectado", value = ControllerName, info = true });
                    list.Add(new Row { label = "Versão instalada", value = () => "v" + Application.version, info = true });
                    list.Add(new Row { label = "VOLTAR", activate = () => Go(configBack) });
                    break;
            }
            return list;
        }

        string UpdateStatus()
        {
            if (updater == null) return "-";
            switch (updater.State)
            {
                case AppUpdater.Estado.Verificando: return "Verificando...";
                case AppUpdater.Estado.EmDia: return "Em dia (toque p/ verificar)";
                case AppUpdater.Estado.Disponivel: return "Nova: v" + updater.LatestVersion;
                case AppUpdater.Estado.SemConexao: return "Sem conexão (tentar)";
                case AppUpdater.Estado.Erro: return "Erro (tentar)";
                case AppUpdater.Estado.Baixando: return "Baixando " + Mathf.RoundToInt(updater.Progress * 100f) + "%";
                default: return "Verificar agora";
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
            session.Begin(c, GameSettings.Papel);
        }

        // ------------------------------------------------------------ desenho

        void OnGUI()
        {
            ui.Begin();
            GUI.depth = 0;
            float s = ui.Scale, W = Screen.width, H = Screen.height;

            switch (tela)
            {
                case Tela.Aviso:
                    {
                        Fill(Color.black, 1f);
                        float a = Mathf.Clamp01(telaTime / 0.6f) * Mathf.Clamp01((4.5f - telaTime) / 0.6f);
                        var t = new GUIStyle(ui.Title); t.normal.textColor = new Color(1f, 1f, 1f, a);
                        var l = new GUIStyle(ui.LabelCenter); l.normal.textColor = new Color(1f, 1f, 1f, a);
                        GUI.Label(new Rect(0, H * 0.32f, W, 100 * s), "CRAQUEZ 3D", t);
                        GUI.Label(new Rect(W * 0.1f, H * 0.48f, W * 0.8f, 120 * s), "Este jogo foi totalmente programado por Inteligência Artificial.", l);
                        return;
                    }
                case Tela.Video:
                    Fill(Color.black, 1f);
                    if (rt != null && videoStarted) GUI.DrawTexture(new Rect(0, 0, W, H), rt, ScaleMode.ScaleAndCrop);
                    return;
                case Tela.ToqueIniciar:
                    {
                        Fill(Color.black, 1f);
                        if (rt != null && videoStarted) GUI.DrawTexture(new Rect(0, 0, W, H), rt, ScaleMode.ScaleAndCrop);
                        else GUI.Label(new Rect(0, H * 0.35f, W, 100 * s), "CRAQUEZ 3D", ui.Title);
                        float a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                        var st = new GUIStyle(ui.Title) { fontSize = Mathf.RoundToInt(46 * s) };
                        st.normal.textColor = new Color(0f, 0f, 0f, a * 0.7f);
                        string txt = InputBridge.ControleConectado != TipoControle.Nenhum && !Application.isMobilePlatform
                            ? "PRESSIONE QUALQUER BOTÃO" : "TOQUE NA TELA PARA INICIAR";
                        GUI.Label(new Rect(3 * s, H * 0.84f + 3 * s, W, 70 * s), txt, st);
                        st.normal.textColor = new Color(1f, 1f, 1f, a);
                        GUI.Label(new Rect(0, H * 0.84f, W, 70 * s), txt, st);
                        return;
                    }
                case Tela.Atualizacao:
                    DrawUpdate(s, W, H);
                    return;
                case Tela.Jogando:
                    return;
            }

            // menus em lista
            Fill(Color.black, tela == Tela.Pausa ? 0.55f : 0.45f);
            string title = tela == Tela.Principal ? "CRAQUEZ 3D" : tela == Tela.Arena ? "ARENA DE TREINO" : tela == Tela.Config ? "CONFIGURAÇÕES" : "PAUSA";
            GUI.Label(new Rect(0, H * 0.06f, W, 100 * s), title, ui.Title);
            if (tela == Tela.Principal)
                GUI.Label(new Rect(0, H * 0.06f + 90 * s, W, 50 * s), "Treino de finalização contra o goleiro", ui.LabelCenter);

            var rows = Rows();
            float rw = Mathf.Min(1000f * s, W * 0.86f), rh = (tela == Tela.Config ? 74f : 96f) * s, gap = 14f * s;
            float total = rows.Count * rh + (rows.Count - 1) * gap;
            float y0 = Mathf.Max(H * 0.24f, (H - total) * 0.5f + 40 * s);
            float x0 = (W - rw) * 0.5f;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                Rect rect = new Rect(x0, y0 + i * (rh + gap), rw, rh);
                bool selected = i == sel && !r.info;
                if (GUI.Button(rect, r.label, selected ? ui.RowSel : ui.Row))
                {
                    sel = i;
                    if (r.activate != null) r.activate();
                    else if (r.change != null) r.change(1);
                }
                if (r.value != null)
                    GUI.Label(new Rect(rect.x, rect.y, rect.width - 30 * s, rect.height), r.change != null ? "‹  " + r.value() + "  ›" : r.value(), ui.Value);
            }

            if (tela == Tela.Config)
            {
                string hint = "Layout atual — Chute: " + InputBridge.Glyph("chute") + "   Colocado: " + InputBridge.Glyph("colocado") +
                              "   Cavadinha: " + InputBridge.Glyph("cavadinha") + "   Correr: " + InputBridge.Glyph("correr");
                GUI.Label(new Rect(0, H - 90 * s, W, 40 * s), hint, ui.Small);
            }
            GUI.Label(new Rect(0, H - 46 * s, W - 24 * s, 40 * s), "v" + Application.version, new GUIStyle(ui.Small) { alignment = TextAnchor.LowerRight });
        }

        void DrawUpdate(float s, float W, float H)
        {
            Fill(new Color(0.02f, 0.03f, 0.06f), 0.92f);
            GUI.Label(new Rect(0, H * 0.12f, W, 100 * s), "ATUALIZAÇÃO OBRIGATÓRIA", ui.Title);
            string info = "Uma nova versão do Craquez 3D está disponível: v" + updater.LatestVersion +
                          "  (instalada: v" + updater.CurrentVersion + ").\nAtualize para continuar jogando.";
            GUI.Label(new Rect(W * 0.1f, H * 0.28f, W * 0.8f, 130 * s), info, ui.LabelCenter);
            if (!string.IsNullOrEmpty(updater.ReleaseNotes))
                GUI.Label(new Rect(W * 0.15f, H * 0.42f, W * 0.7f, H * 0.16f), updater.ReleaseNotes, new GUIStyle(ui.Small) { alignment = TextAnchor.UpperCenter });

            float bw = 640 * s, bh = 120 * s;
            Rect b = new Rect((W - bw) * 0.5f, H * 0.62f, bw, bh);
            if (updater.State == AppUpdater.Estado.Baixando)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(b, Texture2D.whiteTexture);
                GUI.color = new Color(0.85f, 0.12f, 0.1f, 0.95f);
                GUI.DrawTexture(new Rect(b.x, b.y, b.width * updater.Progress, b.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(b, "BAIXANDO " + Mathf.RoundToInt(updater.Progress * 100f) + "%", ui.LabelCenter);
            }
            else
            {
                string label = updater.State == AppUpdater.Estado.Instalando ? "INSTALANDO..." : "ATUALIZAR AGORA (" + InputBridge.Glyph("confirmar") + ")";
                if (GUI.Button(b, label, ui.ButtonSel)) updater.DownloadAndInstall();
            }
            if (!string.IsNullOrEmpty(updater.ErrorMessage))
                GUI.Label(new Rect(W * 0.1f, H * 0.78f, W * 0.8f, 80 * s), updater.ErrorMessage, ui.LabelCenter);
            GUI.Label(new Rect(0, H - 60 * s, W, 50 * s),
                "O Android mostrará a confirmação de instalação do sistema — confirme para concluir.", ui.Small);
        }

        static void Fill(Color c, float alpha)
        {
            GUI.color = new Color(c.r, c.g, c.b, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
