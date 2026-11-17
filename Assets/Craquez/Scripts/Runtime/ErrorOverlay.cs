using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Mostra na tela os erros de execução (exceções) para facilitar testes no celular:
    /// basta tirar um print e enviar. Toque na caixa para escondê-la.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ErrorOverlay : MonoBehaviour
    {
        static ErrorOverlay instance;
        readonly List<string> lines = new List<string>();
        bool hidden;
        GUIStyle style;

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("ErrosNaTela");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ErrorOverlay>();
        }

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }

        readonly HashSet<string> seen = new HashSet<string>();
        int total;
        bool expanded;
        GUIStyle badge;

        void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            string first = "";
            if (!string.IsNullOrEmpty(stack))
            {
                var parts = stack.Split('\n');
                first = parts[0];
                if (parts.Length > 1) first += " | " + parts[1];
            }
            string key = msg + first;
            if (!seen.Add(key)) return;          // não repete o mesmo erro a cada quadro
            total++;
            if (lines.Count < 6) lines.Add(msg + (first.Length == 0 ? "" : "\n   " + first));
            hidden = false;
        }

        /// <summary>
        /// Só um selo pequeno e translúcido no canto (não cobre o jogo); tocando nele abre o detalhe
        /// para tirar print, e tocando de novo fecha.
        /// </summary>
        void OnGUI()
        {
            if (hidden || lines.Count == 0) return;
            GUI.depth = -1000;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true, fontSize = Mathf.Max(12, Screen.height / 60) };
                style.normal.textColor = Color.white;
                style.normal.background = Tex(new Color(0.1f, 0.1f, 0.12f, 0.8f));
                badge = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(11, Screen.height / 70), fontStyle = FontStyle.Bold };
                badge.normal.textColor = new Color(1f, 0.85f, 0.85f, 0.9f);
                badge.normal.background = Tex(new Color(0.6f, 0.1f, 0.1f, 0.35f));
            }
            float bw = Mathf.Max(70f, Screen.height * 0.11f), bh = Mathf.Max(22f, Screen.height * 0.035f);
            var b = new Rect(Screen.width - bw - 8f, Screen.height - bh - 8f, bw, bh);
            if (GUI.Button(b, "! " + total, badge)) expanded = !expanded;
            if (!expanded) return;
            var r = new Rect(Screen.width * 0.25f, Screen.height * 0.25f, Screen.width * 0.5f, Screen.height * 0.5f);
            string text = "ERROS v" + Application.version + " (toque para fechar)\n" + string.Join("\n", lines.ToArray());
            if (GUI.Button(r, text, style)) expanded = false;
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
        }
    }
}
