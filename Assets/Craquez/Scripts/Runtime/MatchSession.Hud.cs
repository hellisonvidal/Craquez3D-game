using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// HUD da partida: placar e relógio, nomes fixos sobre os jogadores, cartão do jogador controlado
    /// (nome, função, perfil de mentalidade e fôlego), minimapa central na parte de baixo, mensagens
    /// do árbitro e painel de fim de jogo. Nomes e minimapa podem ser desligados (Configurações / Pausa).
    /// </summary>
    public partial class MatchSession
    {
        string message;
        float messageUntil, messageStart;
        Color messageColor = Color.white;

        public void Show(string text, Color c, float seconds)
        {
            message = text; messageColor = c; messageStart = Time.time; messageUntil = Time.time + seconds;
        }

        void OnGUI()
        {
            if (!Active || Times == null) return;
            if (Time.timeScale == 0f && Phase != Fase.Fim) return;
            ui.Begin();
            float s = ui.Scale;
            float W = Screen.width, H = Screen.height;
            GUI.depth = 1;

            if (GameSettings.NomesJogadores) DrawNameTags(s);
            DrawScoreboard(s);
            DrawControlledCard(s);
            DrawControlsHint(s);
            if (GameSettings.MiniMapa) DrawMiniMap(s, W, H);

            // botões do topo (em celulares/PC com toque; na TV usa o controle)
            if (Phase != Fase.Fim && !Platform.IsTv)
            {
                float bw = 190 * s, bh = 68 * s, gap = 12 * s;
                float bx = W - bw - 20 * s;
                if (TopButton(new Rect(bx, 18 * s, bw, bh), "PAUSA", UiKit.Red, s)) { if (OnPauseRequested != null) OnPauseRequested(); return; }
                if (TopButton(new Rect(bx - (bw + gap), 18 * s, bw, bh), "CÂMERA", UiKit.Blue, s)) CycleCamera();
            }

            // força do chute
            var h = Human;
            if (h != null && h.pc != null && h.pc.Charging)
            {
                float pw = 520 * s, ph = 26 * s;
                Rect r = new Rect((W - pw) * 0.5f, H * 0.66f, pw, ph);
                UiKit.RoundRect(new Rect(r.x - 6 * s, r.y - 6 * s, r.width + 12 * s, r.height + 12 * s), UiKit.A(UiKit.Navy, 0.75f), (ph + 12 * s) * 0.5f);
                float c = h.pc.Charge;
                Color col = c < 0.6f ? Color.Lerp(UiKit.Green, UiKit.Yellow, c / 0.6f) : Color.Lerp(UiKit.Yellow, UiKit.Red, (c - 0.6f) / 0.4f);
                UiKit.RoundRect(new Rect(r.x, r.y, Mathf.Max(ph, r.width * c), r.height), col, ph * 0.5f);
            }

            DrawMessage(s, W, H);
            DrawRestartPrompt(s, W, H);
            if (Phase == Fase.Fim && phaseTime > 2.5f) DrawEndPanel(s, W, H);

            if (touch != null && touch.enabled && Phase != Fase.Fim) touch.Draw();
        }

        bool TopButton(Rect r, string text, Color accent, float s)
        {
            UiKit.RoundRect(r, UiKit.A(UiKit.Navy, 0.68f), r.height * 0.5f);
            UiKit.RoundBorder(r, UiKit.A(accent, 0.95f), r.height * 0.5f, 3 * s);
            UiKit.Text(r, text, ui.Pill, Color.white, 0f);
            return ui.Hit(r);
        }

        // ------------------------------------------------------------ placar

        void DrawScoreboard(float s)
        {
            var a = Times[0]; var b = Times[1];
            Rect r = new Rect(20 * s, 18 * s, 470 * s, 70 * s);
            UiKit.RoundRect(r, UiKit.A(UiKit.Navy, 0.82f), 16 * s);
            float cw = 120 * s;
            Rect ca = new Rect(r.x + 10 * s, r.y + 10 * s, cw, r.height - 20 * s);
            Rect cb = new Rect(r.x + 10 * s + cw + 110 * s, r.y + 10 * s, cw, r.height - 20 * s);
            TeamChip(ca, a, s);
            TeamChip(cb, b, s);
            UiKit.Text(new Rect(ca.xMax, r.y, 110 * s, r.height), a.gols + "  x  " + b.gols, new GUIStyle(ui.H1) { alignment = TextAnchor.MiddleCenter }, Color.white, 2 * s);
            string clock = MatchMinute.ToString("00") + "'";
            string half = Phase == Fase.Intervalo ? "INT" : (Phase == Fase.Fim ? "FIM" : Half + "ºT");
            Rect cr = new Rect(cb.xMax + 8 * s, r.y + 8 * s, r.xMax - cb.xMax - 16 * s, r.height - 16 * s);
            UiKit.RoundRect(cr, UiKit.A(UiKit.NavyLight, 0.9f), 10 * s);
            UiKit.Text(new Rect(cr.x, cr.y, cr.width, cr.height * 0.6f), clock, new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleCenter }, UiKit.Yellow, 0f);
            UiKit.Text(new Rect(cr.x, cr.y + cr.height * 0.52f, cr.width, cr.height * 0.48f), half, new GUIStyle(ui.Tiny) { alignment = TextAnchor.MiddleCenter }, UiKit.TextDim, 0f);
            // posse
            float pa = a.tempoPosse + b.tempoPosse > 1f ? a.tempoPosse / (a.tempoPosse + b.tempoPosse) : 0.5f;
            Rect pr = new Rect(r.x + 12 * s, r.yMax + 6 * s, r.width - 24 * s, 6 * s);
            UiKit.Fill(new Rect(pr.x, pr.y, pr.width * pa, pr.height), a.cor);
            UiKit.Fill(new Rect(pr.x + pr.width * pa, pr.y, pr.width * (1f - pa), pr.height), b.cor);
        }

        void TeamChip(Rect r, TimeInfo t, float s)
        {
            UiKit.RoundRect(r, t.cor, 10 * s);
            UiKit.Fill(new Rect(r.x, r.yMax - 6 * s, r.width, 6 * s), t.corSecundaria);
            UiKit.Text(r, t.sigla + (t.humano ? " ●" : ""), new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleCenter }, Color.white, 2 * s);
        }

        // ------------------------------------------------------------ nomes sobre os jogadores

        void DrawNameTags(float s)
        {
            var cam = mainCamera;
            if (cam == null) return;
            var st = new GUIStyle(ui.Tiny) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = ui.R(19) };
            bool pov = cameraRig.mode == CameraRig.Modo.VisaoJogador;
            foreach (var m in all)
            {
                if (m == null || m.expulso || !m.gameObject.activeInHierarchy) continue;
                if (pov && m == Human) continue;
                Vector3 head = m.Pos + Vector3.up * (2.05f * m.transform.localScale.y + (m.IsKeeper ? 0.12f : 0f));
                Vector3 sp = cam.WorldToScreenPoint(head);
                if (sp.z < 0.5f || sp.z > 75f) continue;
                float k = Mathf.Clamp(26f / sp.z, 0.55f, 1.1f);
                Vector2 g = new Vector2(sp.x, Screen.height - sp.y);
                string label = m.numero + " " + m.nome + (m.lesao > 0f ? " ✚" : "") + (m.amarelos > 0 ? " ▮" : "");
                float w = (label.Length * 10.5f + 22f) * s * k, hh = 30 * s * k;
                Rect r = new Rect(g.x - w * 0.5f, g.y - hh, w, hh);
                bool me = m == Human;
                UiKit.RoundRect(r, UiKit.A(me ? UiKit.Yellow : UiKit.Navy, me ? 0.92f : 0.6f), hh * 0.5f);
                UiKit.Fill(new Rect(r.x + hh * 0.35f, r.yMax - 3 * s * k, r.width - hh * 0.7f, 3 * s * k), m.time.cor);
                int keep = st.fontSize;
                st.fontSize = Mathf.Max(9, Mathf.RoundToInt(keep * k));
                UiKit.Text(r, label, st, me ? UiKit.Navy : Color.white, 0f);
                st.fontSize = keep;
                if (me)
                {
                    // seta do jogador controlado
                    float aw = 16 * s * k;
                    UiKit.Text(new Rect(g.x - aw, r.yMax - 2 * s, aw * 2f, aw * 1.4f), "▼", new GUIStyle(st) { fontSize = Mathf.RoundToInt(22 * s * k) }, UiKit.Yellow, 1f);
                }
            }
        }

        // ------------------------------------------------------------ cartão fixo do jogador controlado

        void DrawControlledCard(float s)
        {
            var h = Human;
            if (h == null) return;
            Rect r = new Rect(20 * s, 108 * s, 420 * s, 122 * s);
            UiKit.RoundRect(r, UiKit.A(UiKit.Navy, 0.72f), 14 * s);
            UiKit.Fill(new Rect(r.x, r.y + 12 * s, 6 * s, r.height - 24 * s), h.time.cor);
            UiKit.Text(new Rect(r.x + 20 * s, r.y + 6 * s, 80 * s, 50 * s), h.numero.ToString(), new GUIStyle(ui.H1) { alignment = TextAnchor.MiddleLeft }, UiKit.Yellow, 2 * s);
            UiKit.Text(new Rect(r.x + 86 * s, r.y + 6 * s, r.width - 96 * s, 34 * s), h.nome.ToUpper(), ui.H2, Color.white, 1 * s);
            string sub = GeradorJogador.NomeFuncao(h.funcao) + "  •  " + h.men.Perfil(h.funcao) + (h.lesao > 0f ? "  •  contundido" : "");
            UiKit.Text(new Rect(r.x + 86 * s, r.y + 38 * s, r.width - 96 * s, 26 * s), sub, ui.Tiny, UiKit.TextDim, 0f);
            if (h.aparencia != null)
                UiKit.Text(new Rect(r.x + 86 * s, r.y + 62 * s, r.width - 96 * s, 24 * s), h.aparencia.Descricao(), new GUIStyle(ui.Tiny) { fontSize = ui.R(16) }, UiKit.A(UiKit.TextDim, 0.85f), 0f);
            // fôlego
            Rect bar = new Rect(r.x + 86 * s, r.y + 96 * s, r.width - 110 * s, 10 * s);
            UiKit.RoundRect(bar, UiKit.A(Color.black, 0.5f), 5 * s);
            Color sc = Color.Lerp(UiKit.Red, UiKit.Green, h.stamina);
            UiKit.RoundRect(new Rect(bar.x, bar.y, Mathf.Max(10 * s, bar.width * h.stamina), bar.height), sc, 5 * s);
            UiKit.Text(new Rect(r.x + 20 * s, r.y + 88 * s, 70 * s, 26 * s), "FÔLEGO", new GUIStyle(ui.Tiny) { fontSize = ui.R(15) }, UiKit.TextDim, 0f);
        }

        // ------------------------------------------------------------ comandos na tela

        bool hintHadBall;
        float hintChanged = -9f;

        /// <summary>Nome do comando conforme o aparelho: botão do controle, tecla ou botão de toque.</summary>
        static string Key(string action, string tecla, string toque)
        {
            if (InputBridge.ControleConectado != TipoControle.Nenhum || Platform.IsTv) return InputBridge.Glyph(action);
            return Application.isMobilePlatform ? toque : tecla;
        }

        /// <summary>
        /// Lista curta dos comandos do momento, embaixo do cartão do jogador: muda (com destaque) quando
        /// o seu jogador ganha ou perde a bola.
        /// </summary>
        void DrawControlsHint(float s)
        {
            var h = Human;
            if (h == null || Phase != Fase.Jogo) return;
            bool hasBall = Owner == h;
            if (hasBall != hintHadBall) { hintHadBall = hasBall; hintChanged = Time.time; }
            float since = Time.time - hintChanged;
            string[] linhas = hasBall
                ? new[]
                {
                    Key("rasteiro", "Z", "PASSE") + "  passe",
                    Key("cavadinha", "Q", "LANÇAR") + "  lançamento",
                    Key("chute", "Espaço", "CHUTE") + "  chute (segure)",
                    Key("colocado", "E", "COLOCADO") + "  colocado",
                    Key("drible", "F", "DRIBLE") + "  drible",
                    Key("arrancada", "X", "ARRANCADA") + "  arrancada",
                    Key("correr", "Shift", "CORRER") + "  correr",
                }
                : new[]
                {
                    Key("rasteiro", "Z", "DESARME") + "  desarme",
                    Key("chute", "Espaço", "CARRINHO") + "  carrinho",
                    Key("arrancada", "X", "TROCAR") + "  trocar jogador",
                    Key("correr", "Shift", "CORRER") + "  correr",
                };
            Color accent = hasBall ? UiKit.Green : UiKit.Red;
            float lh = 27 * s, w = 300 * s;
            Rect r = new Rect(20 * s, 242 * s, w, 46 * s + lh * linhas.Length);
            // ao trocar, o painel pisca forte e depois fica discreto
            float pulse = Mathf.Clamp01(1f - since / 1.2f);
            float alpha = Mathf.Lerp(0.45f, 0.85f, pulse);
            UiKit.RoundRect(r, UiKit.A(UiKit.Navy, alpha), 12 * s);
            UiKit.RoundBorder(r, UiKit.A(accent, 0.35f + 0.6f * pulse), 12 * s, (2f + 2f * pulse) * s);
            UiKit.Text(new Rect(r.x + 14 * s, r.y + 6 * s, w - 28 * s, 34 * s), hasBall ? "COM A BOLA" : "SEM A BOLA", ui.Pill, accent, 1 * s);
            if (hintStyle == null || hintStyle.fontSize != ui.R(17)) hintStyle = new GUIStyle(ui.Tiny) { fontSize = ui.R(17), alignment = TextAnchor.MiddleLeft };
            for (int i = 0; i < linhas.Length; i++)
                UiKit.Text(new Rect(r.x + 16 * s, r.y + 40 * s + i * lh, w - 24 * s, lh), linhas[i], hintStyle, UiKit.A(Color.white, Mathf.Lerp(0.8f, 1f, pulse)), 1 * s);
        }
        GUIStyle hintStyle;

        // ------------------------------------------------------------ minimapa (centro, embaixo)

        void DrawMiniMap(float s, float W, float H)
        {
            float mw = Mathf.Min(W * 0.3f, 460 * s);
            float mh = mw * (Arena.Width / Arena.Length);
            Rect r = new Rect((W - mw) * 0.5f, H - mh - 18 * s, mw, mh);
            UiKit.RoundRect(new Rect(r.x - 8 * s, r.y - 8 * s, r.width + 16 * s, r.height + 16 * s), UiKit.A(UiKit.Navy, 0.22f), 12 * s);
            UiKit.Fill(r, new Color(0.12f, 0.42f, 0.18f, 0.22f));
            Color line = new Color(1f, 1f, 1f, 0.35f);
            float lw = Mathf.Max(1f, 1.5f * s);
            // linhas: contorno, meio de campo, círculo e áreas
            UiKit.Fill(new Rect(r.x, r.y, r.width, lw), line); UiKit.Fill(new Rect(r.x, r.yMax - lw, r.width, lw), line);
            UiKit.Fill(new Rect(r.x, r.y, lw, r.height), line); UiKit.Fill(new Rect(r.xMax - lw, r.y, lw, r.height), line);
            UiKit.Fill(new Rect(r.center.x - lw * 0.5f, r.y, lw, r.height), line);
            UiKit.RingAt(r.center, 9.15f / Arena.Length * r.width, line);
            float bxw = 16.5f / Arena.Length * r.width, bxh = 40.32f / Arena.Width * r.height;
            Box(new Rect(r.x, r.center.y - bxh * 0.5f, bxw, bxh), line, lw);
            Box(new Rect(r.xMax - bxw, r.center.y - bxh * 0.5f, bxw, bxh), line, lw);

            float dot = Mathf.Max(5f, 9f * s);
            foreach (var m in all)
            {
                if (m == null || m.expulso || !m.gameObject.activeInHierarchy) continue;
                Vector2 p = MapPoint(r, m.Pos);
                if (m == Human)
                {
                    UiKit.Disc(p, dot * 0.95f, UiKit.Yellow);
                    UiKit.Disc(p, dot * 0.6f, m.time.cor);
                }
                else
                {
                    UiKit.Disc(p, dot * 0.62f, Color.black * 0.6f);
                    UiKit.Disc(p, dot * 0.5f, m.IsKeeper ? Color.Lerp(m.time.cor, Color.white, 0.35f) : m.time.cor);
                }
            }
            foreach (var rf in referees)
                if (rf != null) UiKit.Disc(MapPoint(r, rf.transform.position), dot * 0.35f, new Color(0.85f, 0.95f, 0.2f));
            Vector2 bp = MapPoint(r, BallPos);
            UiKit.Disc(bp, dot * 0.55f, Color.black);
            UiKit.Disc(bp, dot * 0.4f, Color.white);

            // sentido do ataque do seu time
            var ut = UserTeam;
            if (ut != null)
            {
                string arrow = ut.attackDir > 0f ? "ATAQUE  ►" : "◄  ATAQUE";
                UiKit.Text(new Rect(r.x, r.y - 30 * s, r.width, 24 * s), arrow, new GUIStyle(ui.Tiny) { alignment = TextAnchor.MiddleCenter }, UiKit.A(ut.cor, 1f), 1f);
            }
        }

        static void Box(Rect r, Color c, float w)
        {
            UiKit.Fill(new Rect(r.x, r.y, r.width, w), c); UiKit.Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            UiKit.Fill(new Rect(r.x, r.y, w, r.height), c); UiKit.Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        /// <summary>Mundo → minimapa (x para a direita, z para cima, como vê a câmera de TV).</summary>
        static Vector2 MapPoint(Rect r, Vector3 p)
        {
            float u = Mathf.Clamp01((p.x + Arena.HalfLength) / Arena.Length);
            float v = Mathf.Clamp01((p.z + Arena.HalfWidth) / Arena.Width);
            return new Vector2(r.x + u * r.width, r.yMax - v * r.height);
        }

        // ------------------------------------------------------------ mensagens e avisos

        void DrawMessage(float s, float W, float H)
        {
            if (string.IsNullOrEmpty(message) || Time.time >= messageUntil) return;
            float a = Mathf.Clamp01((messageUntil - Time.time) / 0.4f) * Mathf.Clamp01((Time.time - messageStart) / 0.12f);
            float pop = 1f + 0.2f * Mathf.Exp(-(Time.time - messageStart) * 10f);
            var st = new GUIStyle(ui.Big) { fontSize = Mathf.RoundToInt(ui.Big.fontSize * 0.62f * pop) };
            Rect band = new Rect(0, H * 0.2f, W, 120 * s);
            UiKit.Fill(new Rect(0, band.y + 14 * s, W, band.height - 28 * s), UiKit.A(UiKit.Navy, 0.5f * a));
            UiKit.Fill(new Rect(0, band.y + 14 * s, W, 4 * s), UiKit.A(messageColor, 0.85f * a));
            UiKit.Fill(new Rect(0, band.yMax - 18 * s, W, 4 * s), UiKit.A(messageColor, 0.85f * a));
            UiKit.Text(band, message, st, new Color(messageColor.r, messageColor.g, messageColor.b, a), 4 * s);
        }

        void DrawRestartPrompt(float s, float W, float H)
        {
            if (Phase != Fase.BolaParada || Restart == null || !Restart.waitingHuman || Restart.taker != Human) return;
            string tipo;
            switch (Restart.tipo)
            {
                case TipoReinicio.Penalti: tipo = "PÊNALTI: " + InputBridge.Glyph("chute") + " chute (segure p/ força) + direção"; break;
                case TipoReinicio.Falta: tipo = "FALTA: " + InputBridge.Glyph("chute") + "/" + InputBridge.Glyph("colocado") + " chute   ✕ passe   △ lançamento"; break;
                case TipoReinicio.Lateral: tipo = "LATERAL: direção + ✕ para cobrar"; break;
                case TipoReinicio.Escanteio: tipo = "ESCANTEIO: △ cruzamento   ✕ passe curto"; break;
                case TipoReinicio.Saida: tipo = "SAÍDA DE BOLA: ✕ passe"; break;
                default: tipo = "✕ passe   △ lançamento"; break;
            }
            if (InputBridge.ControleConectado == TipoControle.Nenhum)
                tipo = tipo.Replace("✕", "Z/PASSE").Replace("△", "Q/LANÇAR");
            Rect r = new Rect((W - 900 * s) * 0.5f, H * 0.36f, 900 * s, 56 * s);
            UiKit.RoundRect(r, UiKit.A(UiKit.Navy, 0.75f), 28 * s);
            UiKit.Text(r, tipo, ui.Pill, UiKit.Yellow, 0f);
        }

        // ------------------------------------------------------------ fim de jogo

        void DrawEndPanel(float s, float W, float H)
        {
            var a = Times[0]; var b = Times[1];
            float pw = Mathf.Min(980 * s, W * 0.86f), ph = 620 * s;
            Rect p = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f, pw, ph);
            UiKit.Fill(new Rect(0, 0, W, H), UiKit.A(UiKit.Navy, 0.55f));
            UiKit.ShadowRect(p, 30 * s, 0.7f);
            UiKit.RoundRect(p, UiKit.A(UiKit.Navy, 0.96f), 28 * s);
            UiKit.RoundBorder(p, UiKit.A(UiKit.Blue, 0.8f), 28 * s, 2 * s);
            UiKit.Text(new Rect(p.x, p.y + 20 * s, pw, 60 * s), "FIM DE JOGO", new GUIStyle(ui.Title) { alignment = TextAnchor.MiddleCenter }, Color.white, 3 * s);
            UiKit.Text(new Rect(p.x, p.y + 90 * s, pw, 70 * s), a.nome + "   " + a.gols + "  x  " + b.gols + "   " + b.nome,
                       new GUIStyle(ui.H1) { alignment = TextAnchor.MiddleCenter }, UiKit.Yellow, 2 * s);
            float tp = a.tempoPosse + b.tempoPosse;
            int posA = tp > 1f ? Mathf.RoundToInt(a.tempoPosse / tp * 100f) : 50;
            string[] rows =
            {
                "Posse de bola|" + posA + "%|" + (100 - posA) + "%",
                "Finalizações|" + a.chutes + "|" + b.chutes,
                "No gol|" + a.chutesNoGol + "|" + b.chutesNoGol,
                "Escanteios|" + a.escanteios + "|" + b.escanteios,
                "Faltas|" + a.faltas + "|" + b.faltas,
                "Impedimentos|" + a.impedimentos + "|" + b.impedimentos,
                "Cartões (A/V)|" + a.amarelos + " / " + a.vermelhos + "|" + b.amarelos + " / " + b.vermelhos,
            };
            float y = p.y + 170 * s;
            foreach (var row in rows)
            {
                var c = row.Split('|');
                UiKit.Text(new Rect(p.x + 60 * s, y, 200 * s, 40 * s), c[1], new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleCenter }, Color.white, 0f);
                UiKit.Text(new Rect(p.x, y, pw, 40 * s), c[0], new GUIStyle(ui.Body) { alignment = TextAnchor.MiddleCenter }, UiKit.TextDim, 0f);
                UiKit.Text(new Rect(p.xMax - 260 * s, y, 200 * s, 40 * s), c[2], new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleCenter }, Color.white, 0f);
                y += 44 * s;
            }
            float bw = 360 * s, bh = 80 * s;
            Rect again = new Rect(p.center.x - bw - 12 * s, p.yMax - bh - 26 * s, bw, bh);
            Rect menu = new Rect(p.center.x + 12 * s, p.yMax - bh - 26 * s, bw, bh);
            UiKit.RoundRect(again, UiKit.Red, bh * 0.5f);
            UiKit.Text(again, "JOGAR DE NOVO", new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleCenter }, Color.white, 0f);
            UiKit.RoundRect(menu, UiKit.A(UiKit.NavyLight, 0.95f), bh * 0.5f);
            UiKit.RoundBorder(menu, UiKit.A(Color.white, 0.5f), bh * 0.5f, 2 * s);
            UiKit.Text(menu, "MENU PRINCIPAL", new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleCenter }, Color.white, 0f);
            if (ui.Hit(again) || (phaseTime > 3.5f && InputBridge.ConfirmPressed)) { RequestRestartMatch(); return; }
            if (ui.Hit(menu) || (phaseTime > 3.5f && InputBridge.BackPressed)) RequestExit();
        }
    }
}
