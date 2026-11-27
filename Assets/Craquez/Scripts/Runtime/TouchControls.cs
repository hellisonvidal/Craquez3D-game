using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Controles de toque estilo eFootball Mobile Clássico (4 botões em diamante ergonômico + gestos de deslizar):
    /// - Esquerda: Analógico virtual (Flutuante, Fixo ou Ancorado).
    /// - Direita (Com a Bola - Ataque):
    ///     * CHUTE (baixo/direita): tocar/segurar = Chute; deslizar p/ baixo = Chute Colocado; deslizar p/ cima = Cavadinha.
    ///     * PASSE (esquerda): tocar/segurar = Passe Rasteiro; deslizar p/ cima = Passe Alto / Cruzamento.
    ///     * ENFIADA (topo): tocar/segurar = Passe em Profundidade; deslizar p/ cima = Profundidade Alta.
    ///     * CORRER (direita/topo): segurar = Correr (Dash); toque duplo = Finta / Drible.
    /// - Direita (Sem a Bola - Defesa):
    ///     * PRESSÃO (esquerda): segurar = Pressão e bote; deslizar p/ cima = Cercar (Match-up).
    ///     * CARRINHO (baixo/direita): Carrinho; deslizar p/ cima = Saída do Goleiro.
    ///     * TROCAR (topo): Trocar Jogador (Cursor).
    ///     * CORRER (direita/topo): Correr (Dash).
    /// - Modo Goleiro (Treino): MERGULHO, DEFESA, SALTAR, SAIR.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class TouchControls : MonoBehaviour
    {
        public Texture2D circle;
        public bool visible;
        public bool goalkeeperMode;
        [Tooltip("Partida 11 x 11: alterna dinamicamente os 4 botões clássicos do eFootball entre Ataque e Defesa.")]
        public bool matchMode;
        public System.Func<bool> matchHasBall;

        enum SwipeDir { None = 0, Up = 1, Down = 2 }

        class Button
        {
            public string trainLabel, gkLabel, atkLabel, defLabel;
            public string atkUpHint, atkDownHint, defUpHint;
            public Vector2 center; // em frações de altura da tela a partir do canto inferior direito
            public float radius;
            public int finger = int.MinValue;
            public Vector2 pressStart, curPos;
            public SwipeDir swipe;
            public bool held;
            public bool lockedAtk;
            public bool wasModeLocked;
            public float pressAnim;
            public Color atkColor, defColor;
        }

        readonly List<PointerPoint> pointers = new List<PointerPoint>();
        readonly HashSet<int> seen = new HashSet<int>();

        // Diamante de 4 botões estilo eFootball Mobile Clássico
        readonly Button btnBottomRight = new Button
        {
            trainLabel = "CHUTE", gkLabel = "MERGULHAR", atkLabel = "CHUTE", defLabel = "CARRINHO",
            atkUpHint = "▲ CAVADINHA", atkDownHint = "▼ COLOCADO", defUpHint = "▲ GOLEIRO",
            center = new Vector2(0.18f, 0.17f), radius = 0.095f,
            atkColor = UiKit.Red, defColor = new Color(0.92f, 0.35f, 0.25f)
        };
        readonly Button btnLeft = new Button
        {
            trainLabel = "RASTEIRO", gkLabel = "DEFESA", atkLabel = "PASSE", defLabel = "PRESSÃO",
            atkUpHint = "▲ CRUZAR", atkDownHint = "", defUpHint = "▲ CERCAR",
            center = new Vector2(0.42f, 0.19f), radius = 0.084f,
            atkColor = UiKit.Blue, defColor = UiKit.Green
        };
        readonly Button btnTop = new Button
        {
            trainLabel = "COLOCADO", gkLabel = "SALTAR", atkLabel = "ENFIADA", defLabel = "TROCAR",
            atkUpHint = "▲ COBERTURA", atkDownHint = "", defUpHint = "",
            center = new Vector2(0.33f, 0.40f), radius = 0.080f,
            atkColor = UiKit.Yellow, defColor = new Color(0.20f, 0.78f, 0.90f)
        };
        readonly Button btnRight = new Button
        {
            trainLabel = "CORRER", gkLabel = "SAIR", atkLabel = "CORRER", defLabel = "CORRER",
            atkUpHint = "", atkDownHint = "", defUpHint = "",
            center = new Vector2(0.14f, 0.41f), radius = 0.082f,
            atkColor = UiKit.Green, defColor = UiKit.Green
        };
        Button[] buttons;

        int stickFinger = int.MinValue;
        Vector2 stickOrigin, stickPos;
        bool stickSprint;
        bool padHidden;
        float lastDashTapTime = -10f;
        float skillPulseUntil = -10f;

        float Scale { get { return GameSettings.EscalaControles; } }
        Vector2 StickHome { get { float H = Screen.height; return new Vector2(H * 0.26f * Scale, H * 0.24f * Scale); } }
        public bool Showing { get { return visible && !padHidden; } }
        GUIStyle labelStyle, subStyle;
        int styleH;

        void Awake()
        {
            buttons = new[] { btnBottomRight, btnLeft, btnTop, btnRight };
            visible = Application.isMobilePlatform && !Platform.IsTv;
        }

        void OnDisable() { ClearVirtual(); }

        static void ClearVirtual()
        {
            InputBridge.VirtualMove = Vector2.zero;
            InputBridge.VirtualShoot = InputBridge.VirtualFinesse = InputBridge.VirtualChip = InputBridge.VirtualSprint = false;
            InputBridge.VirtualLow = InputBridge.VirtualSkill = InputBridge.VirtualKnock = false;
            InputBridge.VirtualPass = InputBridge.VirtualThroughBall = InputBridge.VirtualCross = InputBridge.VirtualLoftedThrough = false;
            InputBridge.VirtualMatchUp = InputBridge.VirtualSlide = InputBridge.VirtualPressure = InputBridge.VirtualSwitch = InputBridge.VirtualKeeperRush = false;
        }

        bool IsAttackingNow()
        {
            return matchMode && matchHasBall != null && matchHasBall();
        }

        bool ButtonAttacking(Button b)
        {
            if (!matchMode) return true;
            return b.wasModeLocked ? b.lockedAtk : IsAttackingNow();
        }

        string Label(Button b)
        {
            if (matchMode)
            {
                bool atk = ButtonAttacking(b);
                if (b.held && b.swipe == SwipeDir.Up)
                {
                    if (atk && b == btnBottomRight) return "CAVADINHA";
                    if (atk && b == btnLeft) return "CRUZAR";
                    if (atk && b == btnTop) return "ALTO △";
                    if (!atk && b == btnLeft) return "CERCAR";
                    if (!atk && b == btnBottomRight) return "GOLEIRO";
                }
                else if (b.held && b.swipe == SwipeDir.Down && atk && b == btnBottomRight)
                {
                    return "COLOCADO";
                }
                return atk ? b.atkLabel : b.defLabel;
            }
            if (goalkeeperMode) return b.gkLabel;
            if (b.held && b == btnBottomRight)
            {
                if (b.swipe == SwipeDir.Up) return "CAVADINHA";
                if (b.swipe == SwipeDir.Down) return "COLOCADO";
            }
            return b.trainLabel;
        }

        Vector2 ButtonScreen(Button b)
        {
            float h = Screen.height * Scale;
            return new Vector2(Screen.width - b.center.x * h, b.center.y * h);
        }

        float ButtonRadius(Button b) { return b.radius * Screen.height * Scale; }
        float StickRadius { get { return Screen.height * 0.12f * Scale; } }

        static bool InTopHudBar(Vector2 pos)
        {
            float s = Screen.height / 1080f;
            return pos.y > Screen.height - 96f * s && pos.x > Screen.width - 440f * s;
        }

        void Update()
        {
            if (InputBridge.ToggleTouchPressed) visible = !visible;
            if (!visible) { ClearVirtual(); stickFinger = int.MinValue; return; }

            InputBridge.GetPointers(pointers);
            if (InputBridge.GamepadActive) padHidden = true;
            if (pointers.Count > 0) padHidden = false;
            if (padHidden)
            {
                ClearVirtual();
                stickFinger = int.MinValue;
                foreach (var b in buttons) { b.finger = int.MinValue; b.wasModeLocked = false; b.swipe = SwipeDir.None; }
                return;
            }

            bool liveAtk = IsAttackingNow();
            bool stickAlive = false;
            foreach (var b in buttons) b.held = false;
            seen.Clear();

            float swipeThreshold = Screen.height * 0.045f * Scale;

            foreach (var p in pointers)
            {
                seen.Add(p.id);
                if (p.id == stickFinger) { stickAlive = true; stickPos = p.position; continue; }
                bool claimed = false;
                foreach (var b in buttons)
                {
                    if (b.finger == p.id)
                    {
                        b.held = true;
                        b.curPos = p.position;
                        float dy = p.position.y - b.pressStart.y;
                        if (dy > swipeThreshold) b.swipe = SwipeDir.Up;
                        else if (dy < -swipeThreshold) b.swipe = SwipeDir.Down;
                        else b.swipe = SwipeDir.None;
                        claimed = true;
                        break;
                    }
                }
                if (claimed) continue;
                if (InTopHudBar(p.position)) continue;

                Button best = null; float bestD = float.MaxValue;
                foreach (var b in buttons)
                {
                    if (b.finger != int.MinValue) continue;
                    float d = Vector2.Distance(p.position, ButtonScreen(b));
                    if (d < ButtonRadius(b) * 1.45f && d < bestD) { best = b; bestD = d; }
                }
                if (best != null)
                {
                    best.finger = p.id;
                    best.held = true;
                    best.pressStart = p.position;
                    best.curPos = p.position;
                    best.swipe = SwipeDir.None;
                    best.lockedAtk = liveAtk;
                    best.wasModeLocked = true;
                    if (best == btnRight)
                    {
                        float now = Time.unscaledTime;
                        if (now - lastDashTapTime < 0.28f) skillPulseUntil = now + 0.12f;
                        lastDashTapTime = now;
                    }
                    continue;
                }
                if (stickFinger == int.MinValue && p.position.x < Screen.width * 0.48f && p.position.y < Screen.height * 0.84f)
                {
                    stickFinger = p.id; stickPos = p.position; stickAlive = true; stickSprint = false;
                    stickOrigin = GameSettings.Joystick == ModoJoystick.Fixo ? StickHome : p.position;
                }
            }

            foreach (var b in buttons)
            {
                if (b.finger != int.MinValue && !seen.Contains(b.finger))
                {
                    b.finger = int.MinValue;
                    b.wasModeLocked = false;
                    b.swipe = SwipeDir.None;
                }
            }
            if (!stickAlive) { stickFinger = int.MinValue; stickSprint = false; }

            Vector2 move = Vector2.zero;
            if (stickFinger != int.MinValue)
            {
                Vector2 d = stickPos - stickOrigin;
                float r = StickRadius;
                float raw = d.magnitude / r;
                if (GameSettings.CorrerNoAnalogico)
                {
                    if (raw > 1.12f) stickSprint = true;
                    else if (raw < 0.8f) stickSprint = false;
                }
                else stickSprint = false;
                if (d.magnitude > r)
                {
                    if (GameSettings.Joystick == ModoJoystick.Flutuante) { stickOrigin = stickPos - d.normalized * r; d = stickPos - stickOrigin; }
                    else d = d.normalized * r;
                }
                float m = d.magnitude / r;
                const float dead = 0.05f;
                move = m > dead ? d.normalized * Mathf.Pow(Mathf.Clamp01((m - dead) / (0.85f - dead)), 0.8f) : Vector2.zero;
            }
            float dt = Time.unscaledDeltaTime;
            foreach (var b in buttons) b.pressAnim = Mathf.MoveTowards(b.pressAnim, b.held ? 1f : 0f, dt * 12f);

            ClearVirtual();
            InputBridge.VirtualMove = move;
            InputBridge.VirtualSprint = btnRight.held || stickSprint;
            if (Time.unscaledTime < skillPulseUntil && (!matchMode || liveAtk))
                InputBridge.VirtualSkill = true;

            if (matchMode)
            {
                // Botão principal inferior direito: CHUTE (ataque) / CARRINHO (defesa)
                if (btnBottomRight.held)
                {
                    if (ButtonAttacking(btnBottomRight))
                    {
                        InputBridge.VirtualShoot = true;
                        if (btnBottomRight.swipe == SwipeDir.Down) InputBridge.VirtualSkill = true; // R1 + Chute = Colocado
                        else if (btnBottomRight.swipe == SwipeDir.Up) InputBridge.VirtualKnock = true; // L1 + Chute = Cavadinha
                    }
                    else
                    {
                        if (btnBottomRight.swipe == SwipeDir.Up) InputBridge.VirtualKeeperRush = true;
                        else InputBridge.VirtualSlide = true;
                    }
                }
                // Botão esquerdo: PASSE (ataque) / PRESSÃO (defesa)
                if (btnLeft.held)
                {
                    if (ButtonAttacking(btnLeft))
                    {
                        if (btnLeft.swipe == SwipeDir.Up) InputBridge.VirtualCross = true;
                        else { InputBridge.VirtualPass = true; InputBridge.VirtualLow = true; }
                    }
                    else
                    {
                        if (btnLeft.swipe == SwipeDir.Up) InputBridge.VirtualMatchUp = true;
                        else { InputBridge.VirtualPressure = true; InputBridge.VirtualLow = true; }
                    }
                }
                // Botão topo: ENFIADA (ataque) / TROCAR (defesa)
                if (btnTop.held)
                {
                    if (ButtonAttacking(btnTop))
                    {
                        InputBridge.VirtualThroughBall = true;
                        InputBridge.VirtualChip = true;
                        if (btnTop.swipe == SwipeDir.Up) InputBridge.VirtualLoftedThrough = true;
                    }
                    else
                    {
                        InputBridge.VirtualSwitch = true;
                        InputBridge.VirtualKnock = true;
                    }
                }
            }
            else if (goalkeeperMode)
            {
                InputBridge.VirtualShoot = btnBottomRight.held;
                InputBridge.VirtualFinesse = btnLeft.held;
                InputBridge.VirtualChip = btnTop.held;
            }
            else
            {
                // Arena de Treino (Atacante)
                if (btnBottomRight.held)
                {
                    if (btnBottomRight.swipe == SwipeDir.Down) InputBridge.VirtualFinesse = true;
                    else if (btnBottomRight.swipe == SwipeDir.Up) InputBridge.VirtualChip = true;
                    else InputBridge.VirtualShoot = true;
                }
                InputBridge.VirtualLow = btnLeft.held;
                InputBridge.VirtualFinesse = InputBridge.VirtualFinesse || btnTop.held;
            }
        }

        /// <summary>Retorna true se o ponto (tela, origem embaixo) está sobre um controle de toque.</summary>
        public bool Covers(Vector2 screenPos)
        {
            if (!Showing) return false;
            foreach (var b in buttons) if (Vector2.Distance(screenPos, ButtonScreen(b)) < ButtonRadius(b) * 1.45f) return true;
            return false;
        }

        public void Draw()
        {
            if (!Showing) return;
            float H = Screen.height;
            if (labelStyle == null || styleH != Screen.height)
            {
                styleH = Screen.height;
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    font = Compat.DefaultFont(),
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = Mathf.RoundToInt(H * 0.022f),
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };
                subStyle = new GUIStyle(labelStyle)
                {
                    fontSize = Mathf.RoundToInt(H * 0.014f),
                };
            }

            // Analógico esquerdo
            float R = StickRadius;
            Vector2 o = stickFinger != int.MinValue ? stickOrigin : StickHome;
            Vector2 k = o;
            if (stickFinger != int.MinValue)
            {
                Vector2 dk = stickPos - o;
                k = o + Vector2.ClampMagnitude(dk, R);
            }
            Vector2 og = new Vector2(o.x, H - o.y), kg = new Vector2(k.x, H - k.y);
            float act = stickFinger != int.MinValue ? 1f : 0.55f;
            UiKit.Disc(og, R, new Color(0.02f, 0.05f, 0.14f, 0.35f * act));
            UiKit.RingAt(og, R, new Color(1f, 1f, 1f, 0.35f * act));
            UiKit.RingAt(og, R * 0.62f, new Color(1f, 1f, 1f, 0.12f * act));
            if (GameSettings.CorrerNoAnalogico)
                UiKit.RingAt(og, R * 1.12f, stickSprint ? UiKit.A(UiKit.Green, 0.95f) : new Color(1f, 1f, 1f, 0.1f * act));
            UiKit.Disc(kg, R * 0.42f, new Color(1f, 1f, 1f, 0.75f * act));
            UiKit.RingAt(kg, R * 0.42f, UiKit.A(UiKit.Cyan, 0.9f * act));

            // 4 botões eFootball à direita
            foreach (var b in buttons)
            {
                bool atk = ButtonAttacking(b);
                Color baseCol = (!matchMode || atk) ? b.atkColor : b.defColor;
                Vector2 c = ButtonScreen(b);
                Vector2 g = new Vector2(c.x, H - c.y);
                if (b.held && b.swipe != SwipeDir.None)
                    g.y += (b.swipe == SwipeDir.Up ? -1f : 1f) * H * 0.012f;

                float r = ButtonRadius(b) * (1f - 0.07f * b.pressAnim);
                UiKit.Disc(g + new Vector2(0f, r * 0.08f), r * 1.04f, new Color(0f, 0f, 0f, 0.32f));
                Color fill = Color.Lerp(UiKit.A(baseCol, 0.46f), UiKit.A(baseCol, 0.92f), b.pressAnim);
                UiKit.Disc(g, r, fill);
                UiKit.Disc(g - new Vector2(0f, r * 0.25f), r * 0.58f, new Color(1f, 1f, 1f, 0.08f + 0.06f * b.pressAnim));
                UiKit.RingAt(g, r, new Color(1f, 1f, 1f, 0.58f + 0.4f * b.pressAnim));

                string text = Label(b);
                int keep = labelStyle.fontSize;
                if (b == btnBottomRight) labelStyle.fontSize = Mathf.RoundToInt(keep * 1.15f);
                UiKit.Text(new Rect(g.x - r * 1.6f, g.y - r, r * 3.2f, r * 2f), text, labelStyle, Color.white, Mathf.Max(1f, H * 0.002f));
                labelStyle.fontSize = keep;

                // Indicador sutil de deslizar (estilo eFootball Mobile)
                if (!b.held && matchMode)
                {
                    string upHint = atk ? b.atkUpHint : b.defUpHint;
                    if (!string.IsNullOrEmpty(upHint))
                        UiKit.Text(new Rect(g.x - r * 1.5f, g.y - r * 1.30f, r * 3f, r * 0.4f), "▲", subStyle, new Color(1f, 1f, 1f, 0.45f), 0f);
                    if (atk && !string.IsNullOrEmpty(b.atkDownHint))
                        UiKit.Text(new Rect(g.x - r * 1.5f, g.y + r * 0.90f, r * 3f, r * 0.4f), "▼", subStyle, new Color(1f, 1f, 1f, 0.45f), 0f);
                }
            }
        }
    }
}
