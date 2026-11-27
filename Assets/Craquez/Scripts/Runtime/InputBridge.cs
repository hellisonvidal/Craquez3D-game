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
    /// Une teclado, controles físicos (PlayStation DualShock/DualSense, Xbox, Switch Pro e Bluetooth/USB)
    /// e toque no esquema oficial de controles estilo eFootball:
    /// - Ataque (Com a Bola):
    ///     * LS / WASD / Setas: Conduzir e Mirar
    ///     * R2 / RT (Shift): Correr (Dash)
    ///     * ✕ / A (Z / J): Passe Rasteiro (segurar = regula força/distância)
    ///     * △ / Y (Q / U): Passe em Profundidade (L1 + △ = Profundidade por Cobertura)
    ///     * ○ / B (E / L): Passe Alto / Cruzamento (no layout Padrão)
    ///     * □ / X (Espaço / K): Chute (R1 + □ = Chute Colocado; L1 + □ = Cavadinha)
    ///     * R1 / RB (F): Controle Fino / Finta
    ///     * L1 / LB (X / LeftAlt): Modificador (Cavadinha / Lançamento em profundidade / Adiantada)
    /// - Defesa (Sem a Bola):
    ///     * ✕ / A (Z / J): Pressão (segurar aproxima e desarma no alcance)
    ///     * L2 / LT ou R1 / RB (LeftCtrl / F): Cercar (Match-up)
    ///     * □ / X (Espaço / K): Bote / Jogo de corpo
    ///     * ○ / B (E / L): Carrinho (Sliding Tackle)
    ///     * L1 / LB (X / LeftAlt): Trocar Cursor (Trocar Jogador)
    ///     * △ / Y (Q / U): Saída do Goleiro (segurar)
    /// </summary>
    public static class InputBridge
    {
        // Valores escritos pelos controles de toque a cada quadro.
        public static Vector2 VirtualMove;
        public static bool VirtualSprint, VirtualShoot, VirtualFinesse, VirtualChip, VirtualLow, VirtualSkill, VirtualKnock;
        public static bool VirtualPass, VirtualThroughBall, VirtualCross, VirtualLoftedThrough;
        public static bool VirtualMatchUp, VirtualSlide, VirtualPressure, VirtualSwitch, VirtualKeeperRush;

        // ------------------------------------------------------------ gamepad

#if ENABLE_INPUT_SYSTEM
        static Gamepad Pad { get { return Gamepad.current; } }

        /// <summary>Um controle físico (Bluetooth/USB) foi mexido neste quadro: os controles de toque saem da tela.</summary>
        public static bool GamepadActive
        {
            get
            {
                var gp = Pad;
                if (gp == null) return false;
                if (gp.leftStick.ReadValue().sqrMagnitude > 0.09f || gp.rightStick.ReadValue().sqrMagnitude > 0.09f || gp.dpad.ReadValue().sqrMagnitude > 0.25f) return true;
                return gp.buttonSouth.isPressed || gp.buttonEast.isPressed || gp.buttonWest.isPressed || gp.buttonNorth.isPressed ||
                       gp.leftShoulder.isPressed || gp.rightShoulder.isPressed || gp.leftTrigger.isPressed || gp.rightTrigger.isPressed || gp.startButton.isPressed;
            }
        }

        static ButtonControl ShootButton(Gamepad gp)
        {
            return GameSettings.Layout == LayoutControle.ChuteNoCirculo ? gp.buttonEast : gp.buttonWest;
        }

        static ButtonControl LoftedOrSlideButton(Gamepad gp)
        {
            return GameSettings.Layout == LayoutControle.ChuteNoCirculo ? gp.buttonWest : gp.buttonEast;
        }
#else
        public static bool GamepadActive { get { return false; } }
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

        /// <summary>Nome do botão para exibir nos menus e na arena, conforme o controle conectado e o layout eFootball.</summary>
        public static string Glyph(string action)
        {
            var t = ControleConectado;
            if (t == TipoControle.Nenhum && Platform.IsTv)
            {
                switch (action)
                {
                    case "rasteiro":
                    case "passe":
                    case "desarme":
                    case "pressao":
                    case "confirmar": return "OK";
                    case "voltar":
                    case "pausa": return "Voltar";
                }
            }
            bool circle = GameSettings.Layout == LayoutControle.ChuteNoCirculo;
            if (t == TipoControle.Nintendo)
            {
                switch (action)
                {
                    case "chute":
                    case "bote": return circle ? "A" : "Y";
                    case "cruzamento":
                    case "passe_alto":
                    case "carrinho":
                    case "colocado": return circle ? "Y" : "A";
                    case "profundidade":
                    case "lancamento":
                    case "cavadinha":
                    case "goleiro_sair": return "X";
                    case "correr": return "ZR";
                    case "rasteiro":
                    case "passe":
                    case "pressao":
                    case "desarme":
                    case "confirmar": return "B";
                    case "drible": return "R";
                    case "cercar": return "ZL";
                    case "arrancada":
                    case "trocar": return "L";
                    case "voltar": return "A";
                    case "camera": return "RS";
                    case "novabola": return "-";
                    case "pausa": return "+";
                }
            }
            bool ps = t == TipoControle.PlayStation;
            switch (action)
            {
                case "chute":
                case "bote": return ps ? (circle ? "○" : "□") : (circle ? "B" : "X");
                case "cruzamento":
                case "passe_alto":
                case "carrinho": return ps ? (circle ? "□" : "○") : (circle ? "X" : "B");
                case "colocado": return ps ? ("R1+" + (circle ? "○" : "□")) : ("RB+" + (circle ? "B" : "X"));
                case "cavadinha": return ps ? ("L1+" + (circle ? "○" : "□")) : ("LB+" + (circle ? "B" : "X"));
                case "profundidade":
                case "lancamento":
                case "goleiro_sair": return ps ? "△" : "Y";
                case "correr": return ps ? "R2" : "RT";
                case "rasteiro":
                case "passe":
                case "pressao":
                case "desarme": return ps ? "✕" : "A";
                case "drible": return ps ? "R1" : "RB";
                case "cercar": return ps ? "L2" : "LT";
                case "arrancada":
                case "trocar": return ps ? "L1" : "LB";
                case "confirmar": return ps ? "✕" : "A";
                case "voltar": return ps ? "○" : "B";
                case "camera": return ps ? "R3" : "RS";
                case "novabola": return ps ? "Share/Create" : "View";
                case "pausa": return ps ? "Options" : "Menu";
            }
            return action;
        }

        // ------------------------------------------------------------ movimento e visão

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
                    if (mag > 0.11f)
                    {
                        float t = Mathf.Clamp01((mag - 0.11f) / 0.81f);
                        m += (s / mag) * Mathf.Pow(t, 1.12f);
                    }
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

        /// <summary>Analógico direito do controle físico: permite espiar o campo com a câmera.</summary>
        public static Vector2 Look
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var gp = Pad;
                if (gp != null)
                {
                    Vector2 s = gp.rightStick.ReadValue();
                    float mag = s.magnitude;
                    if (mag > 0.16f) return (s / mag) * Mathf.Clamp01((mag - 0.16f) / 0.78f);
                }
#endif
                return Vector2.zero;
            }
        }

        /// <summary>Correr (Dash eFootball): R2 / RT, Shift ou botão CORRER.</summary>
        public static bool Sprint
        {
            get
            {
                bool v = VirtualSprint;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)) v = true;
                var gp = Pad;
                if (gp != null && (gp.rightTrigger.ReadValue() > 0.30f || gp.leftStickButton.isPressed)) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) v = true;
#endif
                return v;
            }
        }

        /// <summary>Chute (□ / X no Padrão eFootball, ○ / B no Alternativo, Espaço ou K). Na defesa: Bote / Jogo de corpo.</summary>
        public static bool Shoot
        {
            get
            {
                bool v = VirtualShoot;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.isPressed || kb.kKey.isPressed)) v = true;
                var gp = Pad;
                if (gp != null && ShootButton(gp).isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.K)) v = true;
                if (Input.GetKey(GameSettings.Layout == LayoutControle.ChuteNoCirculo ? KeyCode.JoystickButton1 : KeyCode.JoystickButton2)) v = true;
#endif
                return v;
            }
        }

        /// <summary>
        /// Passe Alto / Cruzamento na partida (○ / B no Padrão eFootball, □ / X no Alternativo, E ou L).
        /// Na defesa: Carrinho (Sliding Tackle). Na arena de treino individual: Chute Colocado.
        /// </summary>
        public static bool Finesse
        {
            get
            {
                bool v = VirtualFinesse;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null && (kb.eKey.isPressed || kb.lKey.isPressed)) v = true;
                var gp = Pad;
                if (gp != null && LoftedOrSlideButton(gp).isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.L)) v = true;
                if (Input.GetKey(GameSettings.Layout == LayoutControle.ChuteNoCirculo ? KeyCode.JoystickButton2 : KeyCode.JoystickButton1)) v = true;
#endif
                return v;
            }
        }

        /// <summary>Passe Alto / Cruzamento na partida (○ / B no Padrão eFootball, E/L ou deslizar PASSE p/ cima no toque).</summary>
        public static bool LoftedPass { get { return VirtualCross || Finesse; } }

        /// <summary>Carrinho na defesa (○ / B no Padrão eFootball, E/L ou botão CARRINHO no toque).</summary>
        public static bool SlideTackle { get { return VirtualSlide || Finesse; } }

        /// <summary>Passe em Profundidade (△ / Y, Q / U ou botão ENFIADA). Na defesa: Saída do Goleiro (segurar).</summary>
        public static bool Chip
        {
            get
            {
                bool v = VirtualChip;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null && (kb.qKey.isPressed || kb.uKey.isPressed)) v = true;
                if (Pad != null && Pad.buttonNorth.isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.U) || Input.GetKey(KeyCode.JoystickButton3)) v = true;
#endif
                return v;
            }
        }

        /// <summary>Passe Rasteiro (com bola) / Pressão (sem bola): ✕ / A, Z ou J, ou OK na TV.</summary>
        public static bool LowShot
        {
            get
            {
                bool v = VirtualLow;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null && (kb.zKey.isPressed || kb.jKey.isPressed || (Platform.IsTv && (kb.enterKey.isPressed || kb.numpadEnterKey.isPressed)))) v = true;
                if (Pad != null && Pad.buttonSouth.isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.JoystickButton0) || (Platform.IsTv && (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)))) v = true;
#endif
                return v;
            }
        }

        /// <summary>Passe Rasteiro (✕ / A).</summary>
        public static bool Pass { get { return VirtualPass || LowShot; } }

        /// <summary>Passe em Profundidade (△ / Y).</summary>
        public static bool ThroughBall { get { return VirtualThroughBall || Chip; } }

        /// <summary>Passe em Profundidade por Cobertura (L1 + △ ou deslizar ENFIADA p/ cima).</summary>
        public static bool LoftedThroughBall { get { return VirtualLoftedThrough || (KnockOn && ThroughBall); } }

        /// <summary>Cruzamento / Passe Alto (○ / B).</summary>
        public static bool Cross { get { return LoftedPass; } }

        /// <summary>Pressão defensiva eFootball (segurar ✕ / A ou botão PRESSÃO aproxima e dá o bote no alcance).</summary>
        public static bool Pressure { get { return VirtualPressure || LowShot; } }

        /// <summary>Saída do Goleiro na defesa (segurar △ / Y ou deslizar p/ cima).</summary>
        public static bool KeeperRush { get { return VirtualKeeperRush || Chip; } }

        /// <summary>Controle Colocado / Finta (R1 / RB, tecla F): R1 + Chute = Chute Colocado.</summary>
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

        /// <summary>Cercar defensivo (Match-up no eFootball): L2 / LT, R1 / RB, LeftCtrl ou deslizar PRESSÃO p/ cima.</summary>
        public static bool MatchUp
        {
            get
            {
                bool v = VirtualMatchUp || Skill;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed)) v = true;
                if (Pad != null && Pad.leftTrigger.ReadValue() > 0.30f) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) v = true;
#endif
                return v;
            }
        }

        /// <summary>Trocar Cursor / Modificador L1 (L1 / LB, tecla X ou LeftAlt): L1 + Chute = Cavadinha; L1 + △ = Profundidade Alta.</summary>
        public static bool KnockOn
        {
            get
            {
                bool v = VirtualKnock || VirtualSwitch;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.xKey.isPressed || Keyboard.current.leftAltKey.isPressed)) v = true;
                if (Pad != null && Pad.leftShoulder.isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.JoystickButton4)) v = true;
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
                if (Keyboard.current != null && (Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.vKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)) return true;
                if (Pad != null && Pad.rightStickButton.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton9)) return true;
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

        /// <summary>Confirmar no menu: Enter/Espaço, OK do controle remoto da TV, ✕ (PlayStation) / A (Xbox).</summary>
        public static bool ConfirmPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) return true;
                if (Pad != null && Pad.buttonSouth.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0)) return true;
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
                    if (!t.press.isPressed && !t.press.wasPressedThisFrame) continue;
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
