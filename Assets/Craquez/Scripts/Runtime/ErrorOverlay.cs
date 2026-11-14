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

        void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (lines.Count >= 6) return;
            string first = stack;
            if (!string.IsNullOrEmpty(stack))
            {
                var parts = stack.Split('\n');
                first = parts.Length > 0 ? parts[0] : stack;
                if (parts.Length > 1) first += " | " + parts[1];
            }
            lines.Add(msg + (string.IsNullOrEmpty(first) ? "" : "\n   " + first));
            hidden = false;
        }

        void OnGUI()
        {
            if (hidden || lines.Count == 0) return;
            GUI.depth = -1000;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true,
                    fontSize = Mathf.Max(14, Screen.height / 50)
                };
                style.normal.textColor = Color.white;
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, new Color(0.55f, 0f, 0f, 0.85f));
                tex.Apply();
                style.normal.background = tex;
            }
            var r = new Rect(10, 10, Screen.width - 20, Screen.height * 0.4f);
            string text = "ERRO v" + Application.version + " (toque para fechar)\n" + string.Join("\n", lines.ToArray());
            if (GUI.Button(r, text, style)) hidden = true;
        }
    }
}
