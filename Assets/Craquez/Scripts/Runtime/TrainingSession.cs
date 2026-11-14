using System;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Jogabilidade da arena de treino de finalização: reposicionamento, detecção de gol / defesa / bola fora
    /// (apenas mensagens — sem placar e sem tempo de jogo), modo atacante ou goleiro, e o HUD.
    /// Menus ficam em <see cref="AppFlow"/>.
    /// </summary>
    public class TrainingSession : MonoBehaviour
    {
        public PlayerController player;
        public ShooterAI shooterAI;
        public GoalkeeperAI keeper;
        public BallPhysics ball;
        public CameraRig cameraRig;
        public WeatherSystem weather;
        public TouchControls touch;

        public event Action OnPauseRequested;

        public bool Active { get; private set; }
        public PapelControle Role { get; private set; }

        string message;
        float messageUntil, messageStart;
        Color messageColor = Color.white;
        float resetAt = -1f;
        bool resultLocked;
        float lastShotTime = -100f;
        float ballStillTimer;
        Ui ui;

        void Start()
        {
            BallPhysics.OnShot += OnShot;
            if (keeper != null) keeper.OnSave += OnSave;
            if (ball != null) ball.OnImpact += OnImpact;
            ui = new Ui();
        }

        void OnDestroy()
        {
            BallPhysics.OnShot -= OnShot;
            if (keeper != null) keeper.OnSave -= OnSave;
            if (ball != null) ball.OnImpact -= OnImpact;
        }

        // ------------------------------------------------------------ ciclo

        /// <summary>Prévia de fundo para os menus (sem controle do usuário).</summary>
        public void Preview()
        {
            if (weather.P == null) weather.Apply(Clima.Sol);
            Active = false;
            SetControl(false, PapelControle.Atacante);
            player.aiControlled = false;
            if (shooterAI != null) shooterAI.enabled = false;
            cameraRig.mode = CameraRig.Modo.Menu;
            if (touch != null) touch.enabled = false;
            ResetDrill();
        }

        public void Begin(Clima c, PapelControle role)
        {
            Role = role;
            weather.Apply(c);
            Active = true;
            SetControl(true, role);
            cameraRig.goalkeeperRole = role == PapelControle.Goleiro;
            if (role == PapelControle.Goleiro) cameraRig.mode = CameraRig.Modo.Goleiro;
            else
            {
                int idx = Mathf.Clamp(GameSettings.CameraPadrao, 0, CameraRig.ModosAtacante.Length - 1);
                cameraRig.mode = CameraRig.ModosAtacante[idx];
            }
            if (touch != null) { touch.enabled = true; touch.goalkeeperMode = role == PapelControle.Goleiro; }
            ResetDrill();
            cameraRig.Snap();
            Show("CLIMA: " + Arena.Nome(c).ToUpper(), Color.white, 2.2f);
        }

        void SetControl(bool on, PapelControle role)
        {
            bool gk = role == PapelControle.Goleiro;
            player.InputEnabled = on && !gk;
            player.aiControlled = on && gk;
            keeper.humanControlled = on && gk;
            if (shooterAI != null) shooterAI.enabled = on && gk;
        }

        public void SetPaused(bool paused)
        {
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (touch != null) touch.enabled = !paused && Active;
            if (paused) SetControl(false, Role); else if (Active) SetControl(true, Role);
        }

        public void ResetDrill()
        {
            resultLocked = false;
            resetAt = -1f;
            ballStillTimer = 0f;
            Vector3 g = Arena.AttackGoalCenter;
            Vector3 spot = g + new Vector3(-20f, 0f, 0f);
            for (int i = 0; i < 40; i++)
            {
                float dist = Role == PapelControle.Goleiro ? UnityEngine.Random.Range(26f, 34f) : UnityEngine.Random.Range(16f, 27f);
                float ang = UnityEngine.Random.Range(-32f, 32f);
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * -Vector3.right;
                spot = g + dir * dist;
                if (PuddleField.Instance == null || !PuddleField.Instance.IsNearPuddle(spot, 1.8f)) break;
            }
            spot.y = Arena.BallRadius;
            ball.PlaceAt(spot);
            Vector3 toGoal = (g - spot); toGoal.y = 0f; toGoal.Normalize();
            player.Teleport(spot - toGoal * 1.4f, g);
            keeper.ResetTo(g + new Vector3(-0.6f, 0f, 0f));
            if (shooterAI != null && shooterAI.enabled) shooterAI.Restart();
        }

        void OnShot(BallPhysics.ShotInfo s) { lastShotTime = Time.time; }

        void OnSave(bool caught)
        {
            if (resultLocked || !Active) return;
            bool gk = Role == PapelControle.Goleiro;
            if (caught)
            {
                Show(gk ? "QUE DEFESA!" : "DEFESA DO GOLEIRO!", new Color(1f, 0.85f, 0.3f), 2.4f);
                resultLocked = true;
                resetAt = Time.time + 2.6f;
            }
            else Show(gk ? "ESPALMOU! ATENÇÃO AO REBOTE" : "ESPALMOU!", new Color(1f, 0.85f, 0.3f), 1.6f);
        }

        void OnImpact(Vector3 p, float speed, Collider c)
        {
            var au = ProceduralAudio.Instance;
            if (au == null) return;
            if (c == null) { if (speed > 2.5f) au.Bounce(p, speed / 15f); return; }
            string n = c.name;
            if (n.StartsWith("Trave") || n.StartsWith("Travessao"))
            {
                au.Post(p, speed / 20f);
                GameSettings.Vibrar();
                if (Active && !resultLocked && speed > 4f) Show(n.StartsWith("Travessao") ? "NO TRAVESSÃO!" : "NA TRAVE!", new Color(1f, 0.6f, 0.3f), 1.5f);
            }
            else if (c.GetComponent<GoalNetPanel>() != null) au.Net(p, speed / 15f);
        }

        public void Show(string text, Color c, float seconds)
        {
            message = text; messageColor = c; messageStart = Time.time; messageUntil = Time.time + seconds;
        }

        void Update()
        {
            if (!Active || Time.timeScale == 0f) return;
            if (InputBridge.MenuPressed) { if (OnPauseRequested != null) OnPauseRequested(); return; }
            if (InputBridge.ResetPressed) ResetDrill();
            if (InputBridge.CameraPressed) cameraRig.Cycle();

            if (resetAt > 0f && Time.time >= resetAt) { ResetDrill(); return; }
            if (resultLocked || ball.IsHeld) return;

            Vector3 b = ball.transform.position;
            float L = Arena.HalfLength, R = Arena.BallRadius;
            bool gk = Role == PapelControle.Goleiro;
            if (b.x > L + R && Mathf.Abs(b.z) < Arena.GoalHalfWidth && b.y < Arena.GoalHeight)
            {
                Show(gk ? "GOL... TENTE DE NOVO" : "GOOOL!", gk ? new Color(1f, 0.45f, 0.4f) : new Color(0.4f, 1f, 0.45f), 2.6f);
                resultLocked = true; resetAt = Time.time + 3.2f;
            }
            else if (b.x > L + R || b.x < -L - R || Mathf.Abs(b.z) > Arena.HalfWidth + R)
            {
                Show("PRA FORA", gk ? new Color(0.4f, 1f, 0.45f) : new Color(1f, 0.45f, 0.4f), 1.8f);
                resultLocked = true; resetAt = Time.time + 2.2f;
            }
            else
            {
                bool still = ball.Velocity.magnitude < 0.15f;
                float dp = Vector3.Distance(player.transform.position, b);
                bool aiStuck = gk && Time.time - lastShotTime > 2f && lastShotTime > 0f;
                ballStillTimer = (still && (dp > 12f || aiStuck) && Time.time - lastShotTime > 2f) ? ballStillTimer + Time.deltaTime : 0f;
                if (ballStillTimer > (gk ? 2.5f : 6f)) { Show("NOVA BOLA", Color.white, 1.2f); ResetDrill(); }
            }
        }

        // ------------------------------------------------------------ HUD

        void OnGUI()
        {
            if (!Active || Time.timeScale == 0f) return;
            ui.Begin();
            float s = ui.Scale;
            float W = Screen.width, H = Screen.height;
            GUI.depth = 1;

            // clima e vento
            GUI.Label(new Rect(24 * s, 18 * s, 700 * s, 44 * s), "Clima: " + Arena.Nome(weather.Current) + (Role == PapelControle.Goleiro ? "   •   Você é o GOLEIRO" : ""), ui.Label);
            float kmh = weather.WindSpeed * 3.6f;
            GUI.Label(new Rect(24 * s, 60 * s, 700 * s, 44 * s), "Vento: " + kmh.ToString("0") + " km/h", ui.Label);
            DrawWindArrow(new Vector2(330 * s, 82 * s), 34 * s);
            GUI.Label(new Rect(24 * s, 102 * s, 700 * s, 40 * s), "Câmera: " + CameraRig.Nome(cameraRig.mode), ui.Small);

            // botões
            float bw = 210 * s, bh = 74 * s, gap = 14 * s;
            float bx = W - bw - 20 * s;
            if (GUI.Button(new Rect(bx, 18 * s, bw, bh), "PAUSA", ui.Button)) { if (OnPauseRequested != null) OnPauseRequested(); return; }
            if (GUI.Button(new Rect(bx - (bw + gap), 18 * s, bw, bh), "CÂMERA", ui.Button)) cameraRig.Cycle();
            if (GUI.Button(new Rect(bx - (bw + gap) * 2f, 18 * s, bw, bh), "NOVA BOLA", ui.Button)) ResetDrill();

            // barra de força
            if (player.Charging && Role == PapelControle.Atacante)
            {
                float pw = 520 * s, ph = 26 * s;
                Rect r = new Rect((W - pw) * 0.5f, H * 0.78f, pw, ph);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(r.x - 4 * s, r.y - 4 * s, r.width + 8 * s, r.height + 8 * s), Texture2D.whiteTexture);
                float c = player.Charge;
                GUI.color = Color.Lerp(new Color(0.3f, 0.9f, 0.3f), new Color(1f, 0.2f, 0.15f), Mathf.InverseLerp(0.6f, 1f, c));
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * c, r.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                string tipo = player.ChargingType == TipoChute.Forte ? "CHUTE FORTE" : (player.ChargingType == TipoChute.Colocado ? "CHUTE COLOCADO" : "CAVADINHA");
                GUI.Label(new Rect(r.x, r.y - 46 * s, r.width, 40 * s), tipo, ui.LabelCenter);
            }

            // mensagem central
            if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
            {
                float a = Mathf.Clamp01((messageUntil - Time.time) / 0.4f) * Mathf.Clamp01((Time.time - messageStart) / 0.12f);
                float pop = 1f + 0.25f * Mathf.Exp(-(Time.time - messageStart) * 10f);
                var st = new GUIStyle(ui.Big) { fontSize = Mathf.RoundToInt(ui.Big.fontSize * pop) };
                st.normal.textColor = new Color(0f, 0f, 0f, a * 0.6f);
                GUI.Label(new Rect(4 * s, H * 0.25f + 4 * s, W, 160 * s), message, st);
                st.normal.textColor = new Color(messageColor.r, messageColor.g, messageColor.b, a);
                GUI.Label(new Rect(0, H * 0.25f, W, 160 * s), message, st);
            }

            if (touch != null && touch.enabled) touch.Draw();
            if (touch == null || !touch.visible) GUI.Label(new Rect(0, H - 50 * s, W, 40 * s), Hint(), ui.Small);
        }

        string Hint()
        {
            var t = InputBridge.ControleConectado;
            if (t != TipoControle.Nenhum)
            {
                if (Role == PapelControle.Goleiro)
                    return "Analógico: mover   " + InputBridge.Glyph("chute") + " + direção: mergulhar   " + InputBridge.Glyph("colocado") + ": defesa parada   " +
                           InputBridge.Glyph("cavadinha") + ": saltar   " + InputBridge.Glyph("correr") + ": sair do gol   " + InputBridge.Glyph("camera") + ": câmera   " + InputBridge.Glyph("pausa") + ": pausa";
                return "Analógico: mover   " + InputBridge.Glyph("correr") + ": correr   " + InputBridge.Glyph("chute") + ": chute   " + InputBridge.Glyph("colocado") + ": colocado   " +
                       InputBridge.Glyph("cavadinha") + ": cavadinha (segure para força)   " + InputBridge.Glyph("camera") + ": câmera   " + InputBridge.Glyph("pausa") + ": pausa";
            }
            if (Role == PapelControle.Goleiro)
                return "WASD: mover   Espaço + direção: mergulhar   E: defesa parada   Q: saltar   Shift: sair do gol   C: câmera   R: nova bola   Esc: pausa";
            return "WASD: mover   Shift: correr   Espaço: chute   E: colocado   Q: cavadinha (segure para força)   R: nova bola   C: câmera   T: toque   Esc: pausa";
        }

        void DrawWindArrow(Vector2 center, float len)
        {
            Vector3 w = weather.Wind;
            if (w.sqrMagnitude < 0.01f) return;
            Transform cam = cameraRig.transform;
            Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 r = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            Vector2 sd = new Vector2(Vector3.Dot(w, r), Vector3.Dot(w, f));
            float ang = Mathf.Atan2(sd.x, sd.y) * Mathf.Rad2Deg;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, center);
            GUI.color = new Color(0.6f, 0.85f, 1f, 0.95f);
            float th = Mathf.Max(3f, len * 0.14f);
            GUI.DrawTexture(new Rect(center.x - th * 0.5f, center.y - len * 0.5f, th, len), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(45f, new Vector2(center.x, center.y - len * 0.5f));
            float hs = len * 0.35f;
            GUI.DrawTexture(new Rect(center.x - th * 0.5f, center.y - len * 0.5f, th, hs), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - th * 0.5f, center.y - len * 0.5f, hs, th), Texture2D.whiteTexture);
            GUI.matrix = m;
            GUI.color = Color.white;
        }
    }

    /// <summary>Estilos IMGUI compartilhados (escalados pela altura da tela).</summary>
    public class Ui
    {
        public float Scale { get; private set; }
        public GUIStyle Title, Label, LabelCenter, Small, Big, Button, ButtonSel, Row, RowSel, Value;
        float built = -1f;

        public void Begin()
        {
            float s = Screen.height / 1080f;
            if (Mathf.Approximately(s, built) && Title != null) return;
            built = s; Scale = s;
            Font f = Compat.DefaultFont();
            Title = new GUIStyle(GUI.skin.label) { font = f, fontSize = R(72), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Title.normal.textColor = Color.white;
            Label = new GUIStyle(GUI.skin.label) { font = f, fontSize = R(30), fontStyle = FontStyle.Bold };
            Label.normal.textColor = Color.white;
            LabelCenter = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            Small = new GUIStyle(GUI.skin.label) { font = f, fontSize = R(22), alignment = TextAnchor.LowerCenter, wordWrap = true };
            Small.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            Big = new GUIStyle(GUI.skin.label) { font = f, fontSize = R(96), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Button = new GUIStyle(GUI.skin.button) { font = f, fontSize = R(32), fontStyle = FontStyle.Bold };
            Button.normal.background = Tex(new Color(0f, 0f, 0f, 0.55f));
            Button.hover.background = Tex(new Color(0.15f, 0.15f, 0.15f, 0.7f));
            Button.active.background = Tex(new Color(0.3f, 0.3f, 0.3f, 0.8f));
            Button.normal.textColor = Button.hover.textColor = Button.active.textColor = Color.white;
            ButtonSel = new GUIStyle(Button);
            ButtonSel.normal.background = ButtonSel.hover.background = Tex(new Color(0.85f, 0.12f, 0.1f, 0.92f));
            Row = new GUIStyle(Button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(R(30), R(30), 0, 0), fontSize = R(34) };
            RowSel = new GUIStyle(ButtonSel) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(R(30), R(30), 0, 0), fontSize = R(34) };
            Value = new GUIStyle(Label) { alignment = TextAnchor.MiddleRight, fontSize = R(32) };
        }

        int R(float v) { return Mathf.Max(8, Mathf.RoundToInt(v * Scale)); }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(2, 2);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }
    }
}
