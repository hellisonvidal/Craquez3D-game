using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Identidade visual da interface (cores do logo Craquez: azul-noite, azul elétrico, amarelo e vermelho)
    /// e primitivas de desenho IMGUI geradas em tempo de execução: retângulo arredondado (9-slice),
    /// círculo suave, anel, degradês e sombra.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Navy = new Color(0.03f, 0.07f, 0.18f);
        public static readonly Color NavyLight = new Color(0.07f, 0.14f, 0.32f);
        public static readonly Color Blue = new Color(0.12f, 0.48f, 1f);
        public static readonly Color Cyan = new Color(0.35f, 0.85f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.82f, 0.12f);
        public static readonly Color Red = new Color(0.92f, 0.2f, 0.17f);
        public static readonly Color Green = new Color(0.25f, 0.85f, 0.45f);
        public static readonly Color TextDim = new Color(0.78f, 0.84f, 0.95f);

        static Texture2D round, circle, ring, gradV, gradH, shadow, white;

        public static Texture2D White { get { if (white == null) white = Solid(Color.white); return white; } }

        /// <summary>Retângulo arredondado branco 64x64, raio 16 (usar com border 18 no GUIStyle ou DrawRound).</summary>
        public static Texture2D Round
        {
            get
            {
                if (round != null) return round;
                const int N = 64; const float r = 16f;
                round = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, N - r), cy = Mathf.Clamp(y + 0.5f, r, N - r);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
                    }
                round.SetPixels(px); round.Apply();
                return round;
            }
        }

        public static Texture2D Circle
        {
            get
            {
                if (circle != null) return circle;
                const int N = 128;
                circle = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N * 0.5f, N * 0.5f));
                        px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(N * 0.5f - d));
                    }
                circle.SetPixels(px); circle.Apply();
                return circle;
            }
        }

        public static Texture2D Ring
        {
            get
            {
                if (ring != null) return ring;
                const int N = 128; const float w = 5f;
                ring = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N * 0.5f, N * 0.5f));
                        float edge = N * 0.5f - 1f;
                        float a = Mathf.Clamp01(edge - d + 0.5f) * Mathf.Clamp01(d - (edge - w) + 0.5f);
                        px[y * N + x] = new Color(1f, 1f, 1f, a);
                    }
                ring.SetPixels(px); ring.Apply();
                return ring;
            }
        }

        /// <summary>Degradê vertical: transparente em cima, opaco embaixo.</summary>
        public static Texture2D GradV
        {
            get
            {
                if (gradV != null) return gradV;
                gradV = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 64; y++) gradV.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, y / 63f)));
                gradV.Apply();
                return gradV;
            }
        }

        /// <summary>Degradê horizontal: opaco à esquerda, transparente à direita.</summary>
        public static Texture2D GradH
        {
            get
            {
                if (gradH != null) return gradH;
                gradH = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < 64; x++) gradH.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, x / 63f)));
                gradH.Apply();
                return gradH;
            }
        }

        /// <summary>Sombra suave (retângulo arredondado borrado).</summary>
        public static Texture2D Shadow
        {
            get
            {
                if (shadow != null) return shadow;
                const int N = 64; const float r = 22f;
                shadow = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r + 4, N - r - 4), cy = Mathf.Clamp(y + 0.5f, r + 4, N - r - 4);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        float a = Mathf.Clamp01(1f - d / (r + 4f));
                        px[y * N + x] = new Color(0f, 0f, 0f, a * a);
                    }
                shadow.SetPixels(px); shadow.Apply();
                return shadow;
            }
        }

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.SetPixels(new[] { c, c, c, c }); t.Apply();
            return t;
        }

        // ------------------------------------------------------------ desenho

        public static void Fill(Rect r, Color c)
        {
            Color o = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = o;
        }

        /// <summary>Retângulo arredondado (raio em pixels de tela).</summary>
        public static void RoundRect(Rect r, Color c, float radius)
        {
            float b = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
            GUI.DrawTexture(r, White, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, b);
        }

        /// <summary>Contorno arredondado.</summary>
        public static void RoundBorder(Rect r, Color c, float radius, float width)
        {
            float b = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
            GUI.DrawTexture(r, White, ScaleMode.StretchToFill, true, 0f, c, new Vector4(width, width, width, width), b);
        }

        public static void ShadowRect(Rect r, float spread, float alpha)
        {
            Color o = GUI.color; GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(r.x - spread, r.y - spread * 0.6f, r.width + spread * 2f, r.height + spread * 2f), Shadow);
            GUI.color = o;
        }

        public static void Disc(Vector2 guiCenter, float radius, Color c)
        {
            Color o = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(guiCenter.x - radius, guiCenter.y - radius, radius * 2f, radius * 2f), Circle);
            GUI.color = o;
        }

        public static void RingAt(Vector2 guiCenter, float radius, Color c)
        {
            Color o = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(guiCenter.x - radius, guiCenter.y - radius, radius * 2f, radius * 2f), Ring);
            GUI.color = o;
        }

        public static void Gradient(Rect r, Color c, bool vertical)
        {
            Color o = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, vertical ? GradV : GradH);
            GUI.color = o;
        }

        public static Color A(Color c, float a) { c.a *= a; return c; }

        /// <summary>Texto com sombra.</summary>
        public static void Text(Rect r, string text, GUIStyle st, Color c, float shadow)
        {
            Color keep = st.normal.textColor;
            if (shadow > 0f)
            {
                st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.55f);
                GUI.Label(new Rect(r.x + shadow, r.y + shadow, r.width, r.height), text, st);
            }
            st.normal.textColor = c;
            GUI.Label(r, text, st);
            st.normal.textColor = keep;
        }

        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    }

    /// <summary>Estilos de texto IMGUI compartilhados (escalados pela altura da tela; base 1080p).</summary>
    public class Ui
    {
        public float Scale { get; private set; }
        public GUIStyle Title, H1, H2, Body, BodyCenter, Small, SmallCenter, Tiny, Big, Clear, Pill;
        float built = -1f;

        public void Begin()
        {
            float s = Screen.height / 1080f;
            if (Mathf.Approximately(s, built) && Title != null) return;
            built = s; Scale = s;
            Font f = Compat.DefaultFont();
            Title = Make(f, 66, FontStyle.Bold, TextAnchor.MiddleLeft);
            H1 = Make(f, 42, FontStyle.Bold, TextAnchor.MiddleLeft);
            H2 = Make(f, 31, FontStyle.Bold, TextAnchor.MiddleLeft);
            Body = Make(f, 27, FontStyle.Normal, TextAnchor.MiddleLeft);
            BodyCenter = Make(f, 27, FontStyle.Normal, TextAnchor.MiddleCenter);
            Small = Make(f, 23, FontStyle.Normal, TextAnchor.MiddleLeft);
            SmallCenter = Make(f, 23, FontStyle.Normal, TextAnchor.MiddleCenter);
            Tiny = Make(f, 19, FontStyle.Bold, TextAnchor.MiddleLeft);
            Big = Make(f, 110, FontStyle.Bold, TextAnchor.MiddleCenter);
            Pill = Make(f, 25, FontStyle.Bold, TextAnchor.MiddleCenter);
            Clear = new GUIStyle();
        }

        GUIStyle Make(Font f, float size, FontStyle fs, TextAnchor a)
        {
            var g = new GUIStyle(GUI.skin.label) { font = f, fontSize = R(size), fontStyle = fs, alignment = a, wordWrap = true, clipping = TextClipping.Overflow };
            g.normal.textColor = Color.white;
            g.padding = new RectOffset(0, 0, 0, 0);
            g.margin = new RectOffset(0, 0, 0, 0);
            return g;
        }

        public int R(float v) { return Mathf.Max(8, Mathf.RoundToInt(v * Scale)); }

        /// <summary>Botão "invisível" sobre uma área desenhada à mão (toque/mouse).</summary>
        public bool Hit(Rect r) { return GUI.Button(r, GUIContent.none, Clear); }
    }
}
