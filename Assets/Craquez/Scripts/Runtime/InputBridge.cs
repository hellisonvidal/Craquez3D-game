using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Craquez
{
    public struct PointerPoint
    {
        public int id;           // id do dedo (-1 = mouse)
        public Vector2 position; // coordenadas de tela (origem embaixo-esquerda)
    }

    public enum TipoControle { Nenhum, PlayStation, Xbox, Nintendo, Generico }

    /// <summary>
    /// Une teclado, controles (PlayStation DualShock/DualSense, Xbox, Switch Pro e controles genéricos
    /// Bluetooth/USB reconhecidos como gamepad) e toque numa única fonte de comandos.
    /// Funciona com o Input System novo (preferido) e com o Input Manager antigo.
    /// Os botões de ação respeitam o layout escolhido em Configurações.
    /// </summary>
    public static class InputBridge
    {
        // Valores escritos pelos controles de toque a cada quadro.
        public static Vector2 VirtualMove;
        public static bool VirtualSprint, VirtualShoot, VirtualFinesse, VirtualChip, VirtualLow, VirtualSkill, VirtualKnock;

        // ------------------------------------------------------------ gamepad

#if ENABLE_INPUT_SYSTEM
        static Gamepad Pad { get { return Gamepad.current; } }

        static ButtonControl ShootButton(Gamepad gp)
        {
            return GameSettings.Layout == LayoutControle.ChuteNoCirculo ? gp.buttonEast : gp.buttonWest;
        }

        static ButtonControl FinesseButton(Gamepad gp)
        {
            return GameSettings.Layout == LayoutControle.ChuteNoCirculo ? gp.buttonWest : gp.buttonEast;
        }
#endif

        public static TipoControle ControleConectado
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var gp = Pad;
                if (gp == null) return TipoControle.Nenhum;
                string n = ((gp.displayName ?? "") + " " + (gp.name ?? "") + " " + (gp.layout ?? "")).ToLowerInvariant();
                if (n.Contains("dualshock") || n.Contains("dualsense") || n.Contains("playstation") || n.Contains("sony") || n.Contains("wireless controller"))
                    return TipoControle.PlayStation;
                if (n.Contains("xbox") || n.Contains("xinput") || n.Contains("microsoft")) return TipoControle.Xbox;
                if (n.Contains("switch") || n.Contains("nintendo")) return TipoControle.Nintendo;
                return TipoControle.Generico;
#elif ENABLE_LEGACY_INPUT_MANAGER
                var names = Input.GetJoystickNames();
                foreach (var j in names)
                {
                    if (string.IsNullOrEmpty(j)) continue;
                    string n = j.ToLowerInvariant();
                    if (n.Contains("sony") || n.Contains("wireless controller") || n.Contains("dualsense")) return TipoControle.PlayStation;
                    if (n.Contains("xbox")) return TipoControle.Xbox;
                    return TipoControle.Generico;
                }
                return TipoControle.Nenhum;
#else
                return TipoControle.Nenhum;
#endif
            }
        }

        /// <summary>Nome do botão para exibir na tela, conforme o controle conectado. Ações: chute, colocado, cavadinha, correr, confirmar, voltar.</summary>
        public static string Glyph(string action)
        {
            var t = ControleConectado;
            bool circle = GameSettings.Layout == LayoutControle.ChuteNoCirculo;
            bool ps = t == TipoControle.PlayStation;
            switch (action)
            {
                case "chute": return ps ? (circle ? "○" : "□") : (circle ? "B" : "X");
                case "colocado": return ps ? (circle ? "□" : "○") : (circle ? "X" : "B");
                case "cavadinha": return ps ? "△" : "Y";
                case "correr": return ps ? "R2" : "RT";
                case "rasteiro": return ps ? "✕" : "A";
                case "drible": return ps ? "R1" : "RB";
                case "arrancada": return ps ? "L1" : "LB";
                case "confirmar": return ps ? "✕" : "A";
                case "voltar": return ps ? "○" : "B";
                case "camera": return ps ? "R3" : "RS";
                case "novabola": return ps ? "Share/Create" : "View";
                case "pausa": return ps ? "Options" : "Menu";
            }
            return action;
        }

        // ------------------------------------------------------------ movimento

        public static Vector2 Move
        {
            get
            {
                Vector2 m = VirtualMove;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) m.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) m.y -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) m.x += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) m.x -= 1f;
                }
                var gp = Pad;
                if (gp != null)
                {
                    Vector2 s = gp.leftStick.ReadValue();
                    float mag = s.magnitude;
                    if (mag > 0.12f) m += s / mag * Mathf.Clamp01((mag - 0.12f) / 0.8f); // zona morta radial reescalada
                    m += gp.dpad.ReadValue();
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) m.y += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) m.y -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) m.x += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) m.x -= 1f;
#endif
                return Vector2.ClampMagnitude(m, 1f);
            }
        }

        public static bool Sprint
        {
            get
            {
                bool v = VirtualSprint;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)) v = true;
                var gp = Pad;
                if (gp != null && (gp.rightTrigger.ReadValue() > 0.4f || gp.leftStickButton.isPressed)) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) v = true;
#endif
                return v;
            }
        }

        /// <summary>Chute forte (segurar para carregar a força). No modo goleiro: mergulhar.</summary>
        public static bool Shoot
        {
            get
            {
                bool v = VirtualShoot;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) v = true;
                var gp = Pad;
                if (gp != null && ShootButton(gp).isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.Space)) v = true;
                if (Input.GetKey(GameSettings.Layout == LayoutControle.ChuteNoCirculo ? KeyCode.JoystickButton1 : KeyCode.JoystickButton2)) v = true;
#endif
                return v;
            }
        }

        /// <summary>Chute colocado (com curva). No modo goleiro: defesa parada/encaixe.</summary>
        public static bool Finesse
        {
            get
            {
                bool v = VirtualFinesse;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.eKey.isPressed) v = true;
                var gp = Pad;
                if (gp != null && FinesseButton(gp).isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.E)) v = true;
                if (Input.GetKey(GameSettings.Layout == LayoutControle.ChuteNoCirculo ? KeyCode.JoystickButton2 : KeyCode.JoystickButton1)) v = true;
#endif
                return v;
            }
        }

        /// <summary>Cavadinha. No modo goleiro: saltar (bola alta).</summary>
        public static bool Chip
        {
            get
            {
                bool v = VirtualChip;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.qKey.isPressed) v = true;
                if (Pad != null && Pad.buttonNorth.isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.JoystickButton3)) v = true;
#endif
                return v;
            }
        }

        /// <summary>Chute rasteiro (segurar para força): ✕ / A, tecla Z.</summary>
        public static bool LowShot
        {
            get
            {
                bool v = VirtualLow;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.zKey.isPressed) v = true;
                if (Pad != null && Pad.buttonSouth.isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.JoystickButton0)) v = true;
#endif
                return v;
            }
        }

        /// <summary>Drible: com direção = corte/finta; para trás = puxada; sem direção = parar a bola. R1 / RB, tecla F.</summary>
        public static bool Skill
        {
            get
            {
                bool v = VirtualSkill;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.fKey.isPressed) v = true;
                if (Pad != null && Pad.rightShoulder.isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.JoystickButton5)) v = true;
#endif
                return v;
            }
        }

        /// <summary>Arrancada (toque longo para frente e disparada). L1 / LB, tecla X.</summary>
        public static bool KnockOn
        {
            get
            {
                bool v = VirtualKnock;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.xKey.isPressed) v = true;
                if (Pad != null && Pad.leftShoulder.isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.JoystickButton4)) v = true;
#endif
                return v;
            }
        }

        public static bool ResetPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) return true;
                if (Pad != null && Pad.selectButton.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton6)) return true;
#endif
                return false;
            }
        }

        public static bool CameraPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) return true;
                if (Pad != null && Pad.rightStickButton.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.JoystickButton9)) return true;
#endif
                return false;
            }
        }

        public static bool MenuPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
                if (Pad != null && Pad.startButton.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7)) return true;
#endif
                return false;
            }
        }

        public static bool ToggleTouchPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.T);
#else
                return false;
#endif
            }
        }

        // ------------------------------------------------------------ navegação de menus (controle/teclado)

        static float navRepeat;

        /// <summary>-1 = cima, +1 = baixo, 0 = nada (com repetição ao segurar).</summary>
        public static int NavVertical()
        {
            float y = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed) y += 1f;
                if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) y -= 1f;
            }
            if (Pad != null) y += Pad.leftStick.ReadValue().y + Pad.dpad.ReadValue().y;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) y -= 1f;
#endif
            if (Mathf.Abs(y) < 0.5f) { navRepeat = 0f; return 0; }
            navRepeat -= Time.unscaledDeltaTime;
            if (navRepeat > 0f) return 0;
            navRepeat = 0.22f;
            return y > 0f ? -1 : 1;
        }

        static float navRepeatH;

        public static int NavHorizontal()
        {
            float x = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) x += 1f;
                if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) x -= 1f;
            }
            if (Pad != null) x += Pad.leftStick.ReadValue().x + Pad.dpad.ReadValue().x;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) x -= 1f;
#endif
            if (Mathf.Abs(x) < 0.5f) { navRepeatH = 0f; return 0; }
            navRepeatH -= Time.unscaledDeltaTime;
            if (navRepeatH > 0f) return 0;
            navRepeatH = 0.25f;
            return x > 0f ? 1 : -1;
        }

        /// <summary>Confirmar no menu: Enter/Espaço, ✕ (PlayStation) / A (Xbox).</summary>
        public static bool ConfirmPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) return true;
                if (Pad != null && Pad.buttonSouth.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0)) return true;
#endif
                return false;
            }
        }

        /// <summary>Voltar no menu: Esc/Backspace, ○ (PlayStation) / B (Xbox), botão voltar do Android.</summary>
        public static bool BackPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)) return true;
                if (Pad != null && Pad.buttonEast.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1)) return true;
#endif
                return false;
            }
        }

        /// <summary>Qualquer toque/clique/tecla/botão (tela "toque para iniciar").</summary>
        public static bool AnyPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
                var gp = Pad;
                if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame ||
                                   gp.buttonNorth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.anyKeyDown) return true;
                for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
#endif
                return false;
            }
        }

        // ------------------------------------------------------------ toque

        /// <summary>Lista todos os toques ativos (e o mouse pressionado, para testar no editor).</summary>
        public static void GetPointers(List<PointerPoint> list)
        {
            list.Clear();
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null)
            {
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    TouchControl t = touches[i];
                    if (!t.press.isPressed) continue;
                    list.Add(new PointerPoint { id = t.touchId.ReadValue(), position = t.position.ReadValue() });
                }
            }
            if (list.Count == 0 && Mouse.current != null && Mouse.current.leftButton.isPressed)
                list.Add(new PointerPoint { id = -1, position = Mouse.current.position.ReadValue() });
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                list.Add(new PointerPoint { id = t.fingerId, position = t.position });
            }
            if (list.Count == 0 && Input.GetMouseButton(0))
                list.Add(new PointerPoint { id = -1, position = Input.mousePosition });
#endif
        }
    }
}
