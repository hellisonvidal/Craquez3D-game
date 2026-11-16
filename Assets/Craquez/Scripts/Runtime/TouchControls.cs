using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Controles de toque para Android (multitoque), desenhados com IMGUI e lógica de toque própria:
    /// analógico flutuante à esquerda (aparece onde o dedo encosta) e botões à direita em dois arcos:
    /// CHUTE (grande) com COLOCADO, CAVADINHA e RASTEIRO em volta; CORRER, DRIBLE e ARRANCADA no arco externo.
    /// No modo goleiro ficam só MERGULHAR / DEFESA / SALTAR / SAIR.
    /// Ativo automaticamente em celulares; tecla T alterna no editor/PC para testar com o mouse.
    /// </summary>
    [DefaultExecutionOrder(-200)] // lê o toque antes da partida e do jogador (sem um quadro de atraso)
    public class TouchControls : MonoBehaviour
    {
        public Texture2D circle; // mantido por compatibilidade (o desenho usa UiKit)
        public bool visible;
        public bool goalkeeperMode;
        [Tooltip("Partida 11 x 11: rótulos de ataque (com a bola) e de defesa (sem a bola).")]
        public bool matchMode;
        public System.Func<bool> matchHasBall;

        class Button
        {
            public string label, gkLabel, atkLabel, defLabel;
            public Vector2 center; // em alturas de tela, a partir do canto inferior direito
            public float radius;   // fração da altura da tela
            public int finger = int.MinValue;
            public bool held;
            public float pressAnim;
            public Color color;
            public bool attackerOnly;
        }

        readonly List<PointerPoint> pointers = new List<PointerPoint>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly Button shoot = new Button { label = "CHUTE", gkLabel = "MERGULHAR", atkLabel = "CHUTE", defLabel = "CARRINHO", center = new Vector2(0.17f, 0.2f), radius = 0.105f, color = UiKit.Red };
        readonly Button finesse = new Button { label = "COLOCADO", gkLabel = "DEFESA", atkLabel = "COLOCADO", defLabel = "", center = new Vector2(0.39f, 0.13f), radius = 0.07f, color = UiKit.Blue };
        readonly Button low = new Button { label = "RASTEIRO", gkLabel = "", atkLabel = "PASSE", defLabel = "DESARME", center = new Vector2(0.38f, 0.32f), radius = 0.066f, color = new Color(0.55f, 0.35f, 0.95f), attackerOnly = true };
        readonly Button chip = new Button { label = "CAVADINHA", gkLabel = "SALTAR", atkLabel = "LANÇAR", defLabel = "", center = new Vector2(0.2f, 0.44f), radius = 0.066f, color = UiKit.Yellow };
        readonly Button sprint = new Button { label = "CORRER", gkLabel = "SAIR", atkLabel = "CORRER", defLabel = "CORRER", center = new Vector2(0.6f, 0.11f), radius = 0.075f, color = UiKit.Green };
        readonly Button skill = new Button { label = "DRIBLE", gkLabel = "", atkLabel = "DRIBLE", defLabel = "", center = new Vector2(0.58f, 0.3f), radius = 0.062f, color = new Color(1f, 0.55f, 0.15f), attackerOnly = true };
        readonly Button knock = new Button { label = "ARRANCADA", gkLabel = "", atkLabel = "ARRANCADA", defLabel = "TROCAR", center = new Vector2(0.43f, 0.5f), radius = 0.058f, color = new Color(0.2f, 0.8f, 0.85f), attackerOnly = true };
        Button[] buttons;

        int stickFinger = int.MinValue;
        Vector2 stickOrigin, stickPos;
        GUIStyle labelStyle;
        int styleH;

        void Awake()
        {
            buttons = new[] { shoot, finesse, low, chip, sprint, skill, knock };
            visible = Application.isMobilePlatform;
        }

        void OnDisable() { ClearVirtual(); }

        static void ClearVirtual()
        {
            InputBridge.VirtualMove = Vector2.zero;
            InputBridge.VirtualShoot = InputBridge.VirtualFinesse = InputBridge.VirtualChip = InputBridge.VirtualSprint = false;
            InputBridge.VirtualLow = InputBridge.VirtualSkill = InputBridge.VirtualKnock = false;
        }

        bool Active(Button b)
        {
            if (matchMode) return !string.IsNullOrEmpty(Label(b));
            return !(goalkeeperMode && b.attackerOnly);
        }

        string Label(Button b)
        {
            if (matchMode) return matchHasBall != null && matchHasBall() ? b.atkLabel : b.defLabel;
            return goalkeeperMode ? b.gkLabel : b.label;
        }

        Vector2 ButtonScreen(Button b)
        {
            float h = Screen.height;
            return new Vector2(Screen.width - b.center.x * h, b.center.y * h);
        }

        float StickRadius { get { return Screen.height * 0.12f; } }

        void Update()
        {
            if (InputBridge.ToggleTouchPressed) visible = !visible;
            if (!visible) { ClearVirtual(); stickFinger = int.MinValue; return; }

            InputBridge.GetPointers(pointers);
            bool stickAlive = false;
            foreach (var b in buttons) b.held = false;
            seen.Clear();
            foreach (var p in pointers)
            {
                seen.Add(p.id);
                if (p.id == stickFinger) { stickAlive = true; stickPos = p.position; continue; }
                bool claimed = false;
                foreach (var b in buttons)
                    if (b.finger == p.id) { b.held = true; claimed = true; break; }
                if (claimed) continue;
                // novo toque: botão mais próximo dentro da área (área de toque 35% maior que o desenho)
                Button best = null; float bestD = float.MaxValue;
                foreach (var b in buttons)
                {
                    if (b.finger != int.MinValue || !Active(b)) continue;
                    float d = Vector2.Distance(p.position, ButtonScreen(b));
                    if (d < b.radius * Screen.height * 1.5f && d < bestD) { best = b; bestD = d; }
                }
                if (best != null) { best.finger = p.id; best.held = true; continue; }
                if (stickFinger == int.MinValue && p.position.x < Screen.width * 0.45f && p.position.y < Screen.height * 0.82f)
                {
                    stickFinger = p.id; stickOrigin = p.position; stickPos = p.position; stickAlive = true;
                }
            }
            foreach (var b in buttons) if (b.finger != int.MinValue && !seen.Contains(b.finger)) b.finger = int.MinValue;
            if (!stickAlive) stickFinger = int.MinValue;

            Vector2 move = Vector2.zero;
            if (stickFinger != int.MinValue)
            {
                Vector2 d = stickPos - stickOrigin;
                float r = StickRadius;
                // a base acompanha o dedo quando ele passa do limite (mudanças de direção rápidas)
                if (d.magnitude > r) { stickOrigin = stickPos - d.normalized * r; d = stickPos - stickOrigin; }
                float m = d.magnitude / r;
                const float dead = 0.05f;
                // resposta um pouco mais rápida no começo do curso (curva suave), máximo antes da borda
                move = m > dead ? d.normalized * Mathf.Pow(Mathf.Clamp01((m - dead) / (0.85f - dead)), 0.8f) : Vector2.zero;
            }
            float dt = Time.unscaledDeltaTime;
            foreach (var b in buttons) b.pressAnim = Mathf.MoveTowards(b.pressAnim, b.held ? 1f : 0f, dt * 12f);

            InputBridge.VirtualMove = move;
            InputBridge.VirtualShoot = shoot.held;
            InputBridge.VirtualFinesse = finesse.held;
            InputBridge.VirtualChip = chip.held;
            InputBridge.VirtualSprint = sprint.held;
            InputBridge.VirtualLow = low.held && Active(low);
            InputBridge.VirtualSkill = skill.held && Active(skill);
            InputBridge.VirtualKnock = knock.held && Active(knock);
        }

        /// <summary>Retorna true se o ponto (tela, origem embaixo) está sobre um controle de toque.</summary>
        public bool Covers(Vector2 screenPos)
        {
            if (!visible) return false;
            foreach (var b in buttons) if (Active(b) && Vector2.Distance(screenPos, ButtonScreen(b)) < b.radius * Screen.height * 1.5f) return true;
            return false;
        }

        public void Draw()
        {
            if (!visible) return;
            float H = Screen.height;
            if (labelStyle == null || styleH != Screen.height)
            {
                styleH = Screen.height;
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    font = Compat.DefaultFont(),
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = Mathf.RoundToInt(H * 0.021f),
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };
            }

            // analógico
            float R = StickRadius;
            Vector2 o = stickFinger != int.MinValue ? stickOrigin : new Vector2(H * 0.26f, H * 0.24f);
            Vector2 k = stickFinger != int.MinValue ? stickPos : o;
            Vector2 og = new Vector2(o.x, H - o.y), kg = new Vector2(k.x, H - k.y);
            float act = stickFinger != int.MinValue ? 1f : 0.55f;
            UiKit.Disc(og, R, new Color(0.02f, 0.05f, 0.14f, 0.35f * act));
            UiKit.RingAt(og, R, new Color(1f, 1f, 1f, 0.35f * act));
            UiKit.RingAt(og, R * 0.62f, new Color(1f, 1f, 1f, 0.12f * act));
            UiKit.Disc(kg, R * 0.42f, new Color(1f, 1f, 1f, 0.75f * act));
            UiKit.RingAt(kg, R * 0.42f, UiKit.A(UiKit.Cyan, 0.9f * act));

            foreach (var b in buttons)
            {
                if (!Active(b)) continue;
                Vector2 c = ButtonScreen(b);
                Vector2 g = new Vector2(c.x, H - c.y);
                float r = b.radius * H * (1f - 0.08f * b.pressAnim);
                UiKit.Disc(g + new Vector2(0f, r * 0.08f), r * 1.04f, new Color(0f, 0f, 0f, 0.3f));
                Color fill = Color.Lerp(UiKit.A(b.color, 0.42f), UiKit.A(b.color, 0.9f), b.pressAnim);
                UiKit.Disc(g, r, fill);
                UiKit.Disc(g - new Vector2(0f, r * 0.25f), r * 0.6f, new Color(1f, 1f, 1f, 0.08f + 0.06f * b.pressAnim)); // brilho
                UiKit.RingAt(g, r, new Color(1f, 1f, 1f, 0.55f + 0.4f * b.pressAnim));
                string text = Label(b);
                var st = labelStyle;
                int keep = st.fontSize;
                if (b == shoot) st.fontSize = Mathf.RoundToInt(keep * 1.25f);
                UiKit.Text(new Rect(g.x - r * 1.6f, g.y - r, r * 3.2f, r * 2f), text, st, Color.white, Mathf.Max(1f, H * 0.002f));
                st.fontSize = keep;
            }
        }
    }
}
