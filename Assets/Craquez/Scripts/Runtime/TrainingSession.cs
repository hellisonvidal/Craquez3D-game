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
            if (weather != null && weather.P == null) weather.Apply(Clima.Sol);
            Active = false;

            // Safe null checks for lazy-initialized components
            if (player != null)
            {
                player.aiControlled = false;
                player.InputEnabled = false;
            }
            if (shooterAI != null) shooterAI.enabled = false;
            if (keeper != null) keeper.humanControlled = false;

            if (cameraRig != null) cameraRig.mode = CameraRig.Modo.Menu;
            if (touch != null) touch.enabled = false;

            // Only reset drill if all required components exist
            if (player != null && keeper != null && ball != null)
            {
                ResetDrill();
            }
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
            player.BlockInput(0.4f);
            ResetDrill();
            cameraRig.Snap();
            Show("CLIMA: " + Arena.Nome(c).ToUpper(), Color.white, 2.2f);
        }

        void SetControl(bool on, PapelControle role)
        {
            bool gk = role == PapelControle.Goleiro;
            if (player != null)
            {
                player.InputEnabled = on && !gk;
                player.aiControlled = on && gk;
            }
            if (keeper != null)
            {
                keeper.humanControlled = on && gk;
            }
            if (shooterAI != null) shooterAI.enabled = on && gk;
        }

        public void SetPaused(bool paused)
        {
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (touch != null) touch.enabled = !paused && Active;
            if (paused) SetControl(false, Role); else if (Active) { SetControl(true, Role); player.BlockInput(0.35f); }
        }

        public void ResetDrill()
        {
            if (ball == null || player == null || keeper == null) return;

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
                resultLocked = true;
                resetAt = Time.time + 3.6f; // o goleiro repõe a bola com as mãos ou com um chutão
            }
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
                resultLocked = true; resetAt = Time.time + 4.2f;
                keeper.RetrieveAndDistribute();
            }
            else if (b.x > L + R || b.x < -L - R || Mathf.Abs(b.z) > Arena.HalfWidth + R)
            {
                resultLocked = true; resetAt = Time.time + 1.6f;
            }
            else
            {
                bool still = ball.Velocity.magnitude < 0.15f;
                float dp = Vector3.Distance(player.transform.position, b);
                bool aiStuck = gk && Time.time - lastShotTime > 2f && lastShotTime > 0f;
                ballStillTimer = (still && (dp > 12f || aiStuck) && Time.time - lastShotTime > 2f) ? ballStillTimer + Time.deltaTime : 0f;
                if (ballStillTimer > (gk ? 2.5f : 6f)) ResetDrill();
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

            // painel de informações (clima, vento, câmera)
            Rect info = new Rect(20 * s, 18 * s, 430 * s, 112 * s);
            UiKit.RoundRect(info, UiKit.A(UiKit.Navy, 0.62f), 18 * s);
            UiKit.Fill(new Rect(info.x, info.y + 16 * s, 6 * s, info.height - 32 * s), WeatherColor());
            UiKit.Text(new Rect(info.x + 24 * s, info.y + 8 * s, 400 * s, 44 * s),
                       Arena.Nome(weather.Current).ToUpper() + (Role == PapelControle.Goleiro ? "  •  GOLEIRO" : ""), ui.H2, Color.white, 2 * s);
            float kmh = weather.WindSpeed * 3.6f;
            UiKit.Text(new Rect(info.x + 24 * s, info.y + 50 * s, 300 * s, 34 * s), "Vento " + kmh.ToString("0") + " km/h", ui.Body, UiKit.TextDim, 0f);
            DrawWindArrow(new Vector2(info.x + 330 * s, info.y + 66 * s), 34 * s);
            UiKit.Text(new Rect(info.x + 24 * s, info.y + 80 * s, 400 * s, 28 * s), "Câmera: " + CameraRig.Nome(cameraRig.mode), ui.Tiny, UiKit.A(UiKit.TextDim, 0.85f), 0f);

            // botões do topo
            float bw = 190 * s, bh = 68 * s, gap = 12 * s;
            float bx = W - bw - 20 * s;
            if (TopButton(new Rect(bx, 18 * s, bw, bh), "PAUSA", UiKit.Red, s)) { if (OnPauseRequested != null) OnPauseRequested(); return; }
            if (TopButton(new Rect(bx - (bw + gap), 18 * s, bw, bh), "CÂMERA", UiKit.Blue, s)) cameraRig.Cycle();

            // barra de força
            if (player.Charging && Role == PapelControle.Atacante)
            {
                float pw = 560 * s, ph = 28 * s;
                Rect r = new Rect((W - pw) * 0.5f, H * 0.74f, pw, ph);
                UiKit.RoundRect(new Rect(r.x - 6 * s, r.y - 6 * s, r.width + 12 * s, r.height + 12 * s), UiKit.A(UiKit.Navy, 0.75f), (ph + 12 * s) * 0.5f);
                float c = player.Charge;
                Color col = c < 0.6f ? Color.Lerp(UiKit.Green, UiKit.Yellow, c / 0.6f) : Color.Lerp(UiKit.Yellow, UiKit.Red, (c - 0.6f) / 0.4f);
                UiKit.RoundRect(new Rect(r.x, r.y, Mathf.Max(ph, r.width * c), r.height), col, ph * 0.5f);
                UiKit.Fill(new Rect(r.x + r.width * 0.85f, r.y - 4 * s, 3 * s, r.height + 8 * s), UiKit.A(Color.white, 0.7f)); // acima disso: chute descontrolado
                string tipo;
                switch (player.ChargingType)
                {
                    case TipoChute.Colocado: tipo = "CHUTE COLOCADO"; break;
                    case TipoChute.Cavadinha: tipo = "CAVADINHA"; break;
                    case TipoChute.Rasteiro: tipo = "CHUTE RASTEIRO"; break;
                    default: tipo = "CHUTE FORTE"; break;
                }
                UiKit.Text(new Rect(r.x, r.y - 50 * s, r.width, 40 * s), tipo, new GUIStyle(ui.H2) { alignment = TextAnchor.MiddleCenter }, Color.white, 2 * s);
            }

            // nome do drible feito
            if (Role == PapelControle.Atacante && !string.IsNullOrEmpty(player.LastMove) && Time.time - player.LastMoveTime < 0.9f)
            {
                float a = Mathf.Clamp01((0.9f - (Time.time - player.LastMoveTime)) / 0.3f);
                Rect m = new Rect((W - 340 * s) * 0.5f, H * 0.62f, 340 * s, 56 * s);
                UiKit.RoundRect(m, UiKit.A(UiKit.Navy, 0.6f * a), 28 * s);
                UiKit.Text(m, player.LastMove, ui.Pill, UiKit.A(UiKit.Yellow, a), 0f);
            }

            // mensagem central
            if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
            {
                float a = Mathf.Clamp01((messageUntil - Time.time) / 0.4f) * Mathf.Clamp01((Time.time - messageStart) / 0.12f);
                float pop = 1f + 0.25f * Mathf.Exp(-(Time.time - messageStart) * 10f);
                var st = new GUIStyle(ui.Big) { fontSize = Mathf.RoundToInt(ui.Big.fontSize * pop) };
                Rect band = new Rect(0, H * 0.24f, W, 170 * s);
                UiKit.Fill(new Rect(0, band.y + 20 * s, W, band.height - 40 * s), UiKit.A(UiKit.Navy, 0.45f * a));
                UiKit.Fill(new Rect(0, band.y + 20 * s, W, 4 * s), UiKit.A(messageColor, 0.8f * a));
                UiKit.Fill(new Rect(0, band.yMax - 24 * s, W, 4 * s), UiKit.A(messageColor, 0.8f * a));
                UiKit.Text(band, message, st, new Color(messageColor.r, messageColor.g, messageColor.b, a), 5 * s);
            }

            if (touch != null && touch.enabled) touch.Draw();
            if (touch == null || !touch.visible)
            {
                Rect hr = new Rect(20 * s, H - 64 * s, W - 40 * s, 50 * s);
                UiKit.RoundRect(hr, UiKit.A(UiKit.Navy, 0.55f), 25 * s);
                UiKit.Text(hr, Hint(), ui.SmallCenter, UiKit.A(Color.white, 0.9f), 0f);
            }
        }

        bool TopButton(Rect r, string text, Color accent, float s)
        {
            UiKit.RoundRect(r, UiKit.A(UiKit.Navy, 0.68f), r.height * 0.5f);
            UiKit.RoundBorder(r, UiKit.A(accent, 0.95f), r.height * 0.5f, 3 * s);
            UiKit.Text(r, text, ui.Pill, Color.white, 0f);
            return ui.Hit(r);
        }

        Color WeatherColor()
        {
            switch (weather.Current)
            {
                case Clima.Sol: return UiKit.Yellow;
                case Clima.Chuva: return UiKit.Cyan;
                default: return new Color(0.6f, 0.45f, 1f);
            }
        }

        string Hint()
        {
            var t = InputBridge.ControleConectado;
            if (t != TipoControle.Nenhum)
            {
                if (Role == PapelControle.Goleiro)
                    return "Analógico: mover   " + InputBridge.Glyph("chute") + " + direção: mergulhar   " + InputBridge.Glyph("colocado") + ": defesa   " +
                           InputBridge.Glyph("cavadinha") + ": saltar   " + InputBridge.Glyph("correr") + ": sair do gol   " + InputBridge.Glyph("camera") + ": câmera   " + InputBridge.Glyph("pausa") + ": pausa";
                return InputBridge.Glyph("correr") + " correr   " + InputBridge.Glyph("chute") + " chute   " + InputBridge.Glyph("colocado") + " colocado   " +
                       InputBridge.Glyph("cavadinha") + " cavadinha   " + InputBridge.Glyph("rasteiro") + " rasteiro (segure = força)   " +
                       InputBridge.Glyph("drible") + " drible   " + InputBridge.Glyph("arrancada") + " arrancada   " + InputBridge.Glyph("camera") + " câmera";
            }
            if (Role == PapelControle.Goleiro)
                return "WASD mover   Espaço + direção mergulhar   E defesa   Q saltar   Shift sair do gol   C câmera   Esc pausa";
            return "WASD mover   Shift correr   Espaço chute   E colocado   Q cavadinha   Z rasteiro   F drible   X arrancada   C câmera   Esc pausa";
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
}
