using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Controles de toque para Android (multitoque): analógico flutuante à esquerda,
    /// botões CHUTE / COLOCADO / CAVADINHA / CORRER à direita. Desenhado com IMGUI,
    /// lógica de toque feita à mão (não depende de EventSystem).
    /// Ativo automaticamente em celulares; tecla T alterna no editor/PC para testar com o mouse.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        public Texture2D circle;
        public bool visible;
        /// <summary>No modo goleiro os botões viram MERGULHAR / DEFESA / SALTAR / SAIR.</summary>
        public bool goalkeeperMode;

        class Button
        {
            public string label;
            public Vector2 center; // normalizado (0..1) a partir do canto inferior direito
            public float radius;   // fração da altura da tela
            public int finger = int.MinValue;
            public bool held;
            public Color color;
        }

        readonly List<PointerPoint> pointers = new List<PointerPoint>();
        readonly Button shoot = new Button { label = "CHUTE", center = new Vector2(0.11f, 0.2f), radius = 0.1f, color = new Color(0.9f, 0.25f, 0.2f) };
        readonly Button finesse = new Button { label = "COLOCADO", center = new Vector2(0.26f, 0.13f), radius = 0.075f, color = new Color(0.2f, 0.55f, 0.95f) };
        readonly Button chip = new Button { label = "CAVADINHA", center = new Vector2(0.08f, 0.44f), radius = 0.07f, color = new Color(0.95f, 0.75f, 0.15f) };
        readonly Button sprint = new Button { label = "CORRER", center = new Vector2(0.25f, 0.36f), radius = 0.07f, color = new Color(0.3f, 0.8f, 0.4f) };
        Button[] buttons;

        int stickFinger = int.MinValue;
        Vector2 stickOrigin, stickPos;

        void Awake()
        {
            buttons = new[] { shoot, finesse, chip, sprint };
            visible = Application.isMobilePlatform;
        }

        void OnDisable() { ClearVirtual(); }

        static void ClearVirtual()
        {
            InputBridge.VirtualMove = Vector2.zero;
            InputBridge.VirtualShoot = InputBridge.VirtualFinesse = InputBridge.VirtualChip = InputBridge.VirtualSprint = false;
        }

        Vector2 ButtonScreen(Button b)
        {
            return new Vector2(Screen.width - b.center.x * Screen.height * 1.6f, b.center.y * Screen.height);
        }

        float StickRadius { get { return Screen.height * 0.11f; } }

        void Update()
        {
            if (InputBridge.ToggleTouchPressed) visible = !visible;
            if (!visible) { ClearVirtual(); stickFinger = int.MinValue; return; }

            InputBridge.GetPointers(pointers);
            // libera dedos que saíram
            bool stickAlive = false;
            foreach (var b in buttons) b.held = false;
            var seen = new HashSet<int>();
            foreach (var p in pointers)
            {
                seen.Add(p.id);
                if (p.id == stickFinger) { stickAlive = true; stickPos = p.position; continue; }
                bool claimed = false;
                foreach (var b in buttons)
                {
                    if (b.finger == p.id) { b.held = true; claimed = true; break; }
                }
                if (claimed) continue;
                // novo toque
                foreach (var b in buttons)
                {
                    if (b.finger != int.MinValue) continue;
                    if (Vector2.Distance(p.position, ButtonScreen(b)) < b.radius * Screen.height * 1.25f)
                    {
                        b.finger = p.id; b.held = true; claimed = true; break;
                    }
                }
                if (claimed) continue;
                if (stickFinger == int.MinValue && p.position.x < Screen.width * 0.45f && p.position.y < Screen.height * 0.8f)
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
                if (d.magnitude > r) { stickOrigin = stickPos - d.normalized * r; d = stickPos - stickOrigin; }
                move = d / r;
                if (move.magnitude < 0.12f) move = Vector2.zero;
            }
            InputBridge.VirtualMove = move;
            InputBridge.VirtualShoot = shoot.held;
            InputBridge.VirtualFinesse = finesse.held;
            InputBridge.VirtualChip = chip.held;
            InputBridge.VirtualSprint = sprint.held || move.magnitude > 0.95f;
        }

        /// <summary>Retorna true se o ponto (tela, origem embaixo) está sobre um controle de toque.</summary>
        public bool Covers(Vector2 screenPos)
        {
            if (!visible) return false;
            foreach (var b in buttons) if (Vector2.Distance(screenPos, ButtonScreen(b)) < b.radius * Screen.height * 1.25f) return true;
            return false;
        }

        public void Draw()
        {
            if (!visible || circle == null) return;
            Color old = GUI.color;
            // analógico
            if (stickFinger != int.MinValue)
            {
                DrawCircle(stickOrigin, StickRadius, new Color(1f, 1f, 1f, 0.18f));
                DrawCircle(stickPos, StickRadius * 0.45f, new Color(1f, 1f, 1f, 0.55f));
            }
            else
            {
                Vector2 hint = new Vector2(Screen.height * 0.22f, Screen.height * 0.22f);
                DrawCircle(hint, StickRadius, new Color(1f, 1f, 1f, 0.1f));
                DrawCircle(hint, StickRadius * 0.45f, new Color(1f, 1f, 1f, 0.25f));
            }
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(Screen.height * 0.022f)
            };
            style.normal.textColor = Color.white;
            foreach (var b in buttons)
            {
                Vector2 c = ButtonScreen(b);
                float r = b.radius * Screen.height;
                Color col = b.color; col.a = b.held ? 0.85f : 0.5f;
                DrawCircle(c, r, col);
                GUI.color = Color.white;
                GUI.Label(new Rect(c.x - r * 1.5f, Screen.height - c.y - r, r * 3f, r * 2f), Label(b), style);
            }
            GUI.color = old;
        }

        string Label(Button b)
        {
            if (!goalkeeperMode) return b.label;
            if (b == shoot) return "MERGULHAR";
            if (b == finesse) return "DEFESA";
            if (b == chip) return "SALTAR";
            return "SAIR";
        }

        void DrawCircle(Vector2 screenCenter, float r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(screenCenter.x - r, Screen.height - screenCenter.y - r, r * 2f, r * 2f), circle);
        }
    }
}
