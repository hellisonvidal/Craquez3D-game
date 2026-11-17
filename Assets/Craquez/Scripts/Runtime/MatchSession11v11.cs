using System;
using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Sessão de partida 11x11 completa: usa CollectiveMatchMode, TeamAI, FootballRulesEngine, RefereeSystem.
    /// Gerencia 22 jogadores de linha + 2 goleiros + árbitro, placar, tempo, substituições, estatísticas.
    /// </summary>
    public class MatchSession11v11 : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject playerPrefab;
        public GameObject keeperPrefab;
        public GameObject ballPrefab;
        public GameObject refereePrefab;

        [Header("Configurações")]
        public float matchDurationMinutes = 90f;
        public bool enableExtraTime = false;
        public bool enablePenalties = false;
        public int maxSubstitutions = 5;
        public bool useVAR = true;

        [Header("Times")]
        public CollectiveMatchMode.TeamData homeTeamData;
        public CollectiveMatchMode.TeamData awayTeamData;

        // Componentes do sistema
        private CollectiveMatchMode _collectiveMatchMode;
        private FootballRulesEngine _rulesEngine;
        private RefereeSystem _refereeSystem;
        private BallPhysics _ball;
        private CameraRig _cameraRig;
        private WeatherSystem _weather;
        private TouchControls _touch;
        private PuddleField _puddleField;

        // Jogadores instanciados
        private Dictionary<int, PlayerController> _homePlayers = new Dictionary<int, PlayerController>();
        private Dictionary<int, PlayerController> _awayPlayers = new Dictionary<int, PlayerController>();
        private Dictionary<int, GoalkeeperAI> _homeGK = new Dictionary<int, GoalkeeperAI>();
        private Dictionary<int, GoalkeeperAI> _awayGK = new Dictionary<int, GoalkeeperAI>();

        // Estado
        public bool Active { get; private set; }
        public CollectiveMatchMode.MatchState MatchState => _collectiveMatchMode?.CurrentState ?? CollectiveMatchMode.MatchState.NotStarted;
        public float MatchTime => _rulesEngine?.CurrentMatchTime ?? 0f;
        public int HomeScore => _rulesEngine?.HomeTeam?.score ?? 0;
        public int AwayScore => _rulesEngine?.AwayTeam?.score ?? 0;

        // HUD
        private Ui ui;
        private string message;
        private float messageUntil, messageStart;
        private Color messageColor = Color.white;

        public event Action OnPauseRequested;

        void Awake()
        {
            ErrorOverlay.Ensure();
        }

        void Start()
        {
            ui = new Ui();
            InitializeSystems();
        }

        void InitializeSystems()
        {
            // Obter ou criar sistemas singleton
            _rulesEngine = FootballRulesEngine.Instance;
            _refereeSystem = RefereeSystem.Instance;
            _ball = BallPhysics.Instance;
            _collectiveMatchMode = CollectiveMatchMode.Instance;

            if (_collectiveMatchMode != null)
            {
                _collectiveMatchMode.OnStateChanged += OnMatchStateChanged;
                _collectiveMatchMode.OnGoal += OnGoal;
                _collectiveMatchMode.OnCard += OnCard;
                _collectiveMatchMode.OnSubstitution += OnSubstitution;
                _collectiveMatchMode.OnMatchEnd += OnMatchEnd;
            }

            if (_rulesEngine != null)
            {
                _rulesEngine.OnGoalScored += OnGoalScoredRules;
                _rulesEngine.OnCardShown += OnCardShownRules;
                _rulesEngine.OnSubstitution += OnSubstitutionRules;
                _rulesEngine.OnMatchEvent += OnMatchEventRules;
                _rulesEngine.OnPhaseChanged += OnPhaseChangedRules;
            }
        }

        void OnDestroy()
        {
            if (_collectiveMatchMode != null)
            {
                _collectiveMatchMode.OnStateChanged -= OnMatchStateChanged;
                _collectiveMatchMode.OnGoal -= OnGoal;
                _collectiveMatchMode.OnCard -= OnCard;
                _collectiveMatchMode.OnSubstitution -= OnSubstitution;
                _collectiveMatchMode.OnMatchEnd -= OnMatchEnd;
            }

            if (_rulesEngine != null)
            {
                _rulesEngine.OnGoalScored -= OnGoalScoredRules;
                _rulesEngine.OnCardShown -= OnCardShownRules;
                _rulesEngine.OnSubstitution -= OnSubstitutionRules;
                _rulesEngine.OnMatchEvent -= OnMatchEventRules;
                _rulesEngine.OnPhaseChanged -= OnPhaseChangedRules;
            }
        }

        /// <summary>
        /// Inicia a partida 11x11 com configurações padrão ou personalizadas.
        /// </summary>
        public void Begin(Clima c, CollectiveMatchMode.TeamData home = null, CollectiveMatchMode.TeamData away = null)
        {
            if (home != null) homeTeamData = home;
            if (away != null) awayTeamData = away;

            if (_weather != null) _weather.Apply(c);

            // Configurar times se não foram passados
            if (homeTeamData == null || awayTeamData == null)
            {
                CreateDefaultTeams();
            }

            // Passar dados para CollectiveMatchMode
            if (_collectiveMatchMode != null)
            {
                _collectiveMatchMode.homeTeamData = homeTeamData;
                _collectiveMatchMode.awayTeamData = awayTeamData;
                _collectiveMatchMode.matchDurationMinutes = matchDurationMinutes;
                _collectiveMatchMode.enableExtraTime = enableExtraTime;
                _collectiveMatchMode.enablePenalties = enablePenalties;
                _collectiveMatchMode.maxSubstitutions = maxSubstitutions;
                _collectiveMatchMode.useVAR = useVAR;
            }

            Active = true;
            Show($"PARTIDA 11x11 - {homeTeamData.teamName} vs {awayTeamData.teamName}", Color.white, 3f);
            Show($"Clima: {Arena.Nome(c).ToUpper()}", Color.white, 2f);

            // A partida começará via CollectiveMatchMode (TeamSelection -> FormationSetup -> PreMatch -> InProgress)
        }

        void CreateDefaultTeams()
        {
            if (homeTeamData == null)
            {
                homeTeamData = _collectiveMatchMode?.CreateDefaultTeamData("CRAQUEZ FC", "CRQ", Color.blue, Color.white, 0);
            }
            if (awayTeamData == null)
            {
                awayTeamData = _collectiveMatchMode?.CreateDefaultTeamData("OPOSIÇÃO FC", "OPO", Color.red, Color.white, 1);
            }
        }

        // ------------------------------------------------------------ Event Handlers

        void OnMatchStateChanged(CollectiveMatchMode.MatchState newState)
        {
            Show($"FASE: {newState}", UiKit.Cyan, 2f);
        }

        void OnGoal(int teamId, int playerId)
        {
            string teamName = teamId == 0 ? homeTeamData?.teamName : awayTeamData?.teamName;
            string playerName = GetPlayerName(teamId, playerId);
            Show($"GOOOOL! {playerName} ({teamName})", UiKit.Yellow, 3f);
        }

        void OnCard(int teamId, int playerId, FootballRulesEngine.CardType cardType)
        {
            string teamName = teamId == 0 ? homeTeamData?.teamName : awayTeamData?.teamName;
            string playerName = GetPlayerName(teamId, playerId);
            string cardText = cardType == FootballRulesEngine.CardType.Yellow ? "AMARELO" : "VERMELHO";
            Show($"{cardText}: {playerName} ({teamName})", cardType == FootballRulesEngine.CardType.Red ? UiKit.Red : UiKit.Yellow, 3f);
        }

        void OnSubstitution(int teamId, int playerOut, int playerIn)
        {
            string teamName = teamId == 0 ? homeTeamData?.teamName : awayTeamData?.teamName;
            string outName = GetPlayerName(teamId, playerOut);
            string inName = GetPlayerName(teamId, playerIn);
            Show($"Substituição {teamName}: {outName} sai, {inName} entra", UiKit.Cyan, 2.5f);
        }

        void OnMatchEnd()
        {
            Active = false;
            string result = $"{homeTeamData?.teamName} {HomeScore} x {AwayScore} {awayTeamData?.teamName}";
            Show($"FIM DE JOGO: {result}", UiKit.Yellow, 5f);
        }

        // FootballRulesEngine events
        void OnGoalScoredRules(int teamId, int playerId) { }
        void OnCardShownRules(int teamId, int playerId, FootballRulesEngine.CardType card) { }
        void OnSubstitutionRules(int teamId, int playerOut, int playerIn) { }
        void OnMatchEventRules(FootballRulesEngine.MatchEvent evt) { }
        void OnPhaseChangedRules(FootballRulesEngine.MatchPhase phase) { }

        string GetPlayerName(int teamId, int playerId)
        {
            var team = teamId == 0 ? homeTeamData : awayTeamData;
            if (team != null && playerId < team.squad.Count)
                return team.squad[playerId].playerName;
            return $"Jogador #{playerId}";
        }

        public void Show(string text, Color c, float seconds)
        {
            message = text; messageColor = c; messageStart = Time.time; messageUntil = Time.time + seconds;
        }

        public void SetPaused(bool paused)
        {
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        void Update()
        {
            if (!Active || Time.timeScale == 0f) return;

            if (InputBridge.MenuPressed) { if (OnPauseRequested != null) OnPauseRequested(); return; }
            if (InputBridge.CameraPressed) _cameraRig?.Cycle();

            // Atalhos para substituições (teclas numéricas para testes)
            if (Input.GetKeyDown(KeyCode.Alpha1)) RequestSub(0, 0, 11);
            if (Input.GetKeyDown(KeyCode.Alpha2)) RequestSub(1, 0, 11);
        }

        void RequestSub(int teamId, int outId, int inId)
        {
            _collectiveMatchMode?.RequestSubstitution(teamId, outId, inId);
        }

        // ------------------------------------------------------------ HUD

        void OnGUI()
        {
            if (!Active || Time.timeScale == 0f) return;
            ui.Begin();
            float s = ui.Scale;
            float W = Screen.width, H = Screen.height;
            GUI.depth = 1;

            // Painel de informações (placar, tempo, clima)
            DrawMatchInfo(W, H, s);

            // Botões do topo
            float bw = 190 * s, bh = 68 * s, gap = 12 * s;
            float bx = W - bw - 20 * s;
            if (TopButton(new Rect(bx, 18 * s, bw, bh), "PAUSA", UiKit.Red, s)) { if (OnPauseRequested != null) OnPauseRequested(); return; }
            if (TopButton(new Rect(bx - (bw + gap), 18 * s, bw, bh), "CÂMERA", UiKit.Blue, s)) _cameraRig?.Cycle();
            if (TopButton(new Rect(bx - 2 * (bw + gap), 18 * s, bw, bh), "ESTATÍSTICAS", UiKit.Green, s)) { /* TODO: show stats */ }

            // Mensagem central
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

            // Dicas de controles
            Rect hr = new Rect(20 * s, H - 80 * s, W - 40 * s, 60 * s);
            UiKit.RoundRect(hr, UiKit.A(UiKit.Navy, 0.55f), 25 * s);
            UiKit.Text(hr, GetControlsHint(), ui.SmallCenter, UiKit.A(Color.white, 0.9f), 0f);
        }

        void DrawMatchInfo(float W, float H, float s)
        {
            Rect info = new Rect(20 * s, 18 * s, 550 * s, 140 * s);
            UiKit.RoundRect(info, UiKit.A(UiKit.Navy, 0.62f), 18 * s);
            UiKit.Fill(new Rect(info.x, info.y + 16 * s, 6 * s, info.height - 32 * s), WeatherColor());

            // Placar e nomes dos times
            string homeName = homeTeamData?.shortName ?? "CASA";
            string awayName = awayTeamData?.shortName ?? "FORA";
            UiKit.Text(new Rect(info.x + 24 * s, info.y + 8 * s, 500 * s, 50 * s),
                $"{homeName}  {HomeScore}  -  {AwayScore}  {awayName}", ui.H1, Color.white, 3 * s);

            // Tempo de jogo
            int minutes = Mathf.FloorToInt(MatchTime / 60f);
            int seconds = Mathf.FloorToInt(MatchTime % 60f);
            string phaseName = GetPhaseName(MatchState);
            UiKit.Text(new Rect(info.x + 24 * s, info.y + 58 * s, 300 * s, 34 * s),
                $"{phaseName}  {minutes:00}:{seconds:00}", ui.Body, UiKit.TextDim, 0f);

            // Vento
            if (_weather != null && _weather.P != null)
            {
                float kmh = _weather.WindSpeed * 3.6f;
                UiKit.Text(new Rect(info.x + 350 * s, info.y + 58 * s, 200 * s, 34 * s),
                    $"Vento {kmh:0} km/h", ui.Body, UiKit.TextDim, 0f);
            }

            // Clima
            UiKit.Text(new Rect(info.x + 24 * s, info.y + 90 * s, 500 * s, 28 * s),
                $"Clima: {Arena.Nome(_weather?.Current ?? Clima.Sol).ToUpper()}  •  Câmera: {(_cameraRig != null ? CameraRig.Nome(_cameraRig.mode) : "-")}",
                ui.Tiny, UiKit.A(UiKit.TextDim, 0.85f), 0f);
        }

        string GetPhaseName(CollectiveMatchMode.MatchState state)
        {
            switch (state)
            {
                case CollectiveMatchMode.MatchState.NotStarted: return "AGUARDANDO";
                case CollectiveMatchMode.MatchState.TeamSelection: return "ESCALAÇÃO";
                case CollectiveMatchMode.MatchState.FormationSetup: return "FORMAÇÃO";
                case CollectiveMatchMode.MatchState.PreMatch: return "PRÉ-JOGO";
                case CollectiveMatchMode.MatchState.InProgress: return "EM JOGO";
                case CollectiveMatchMode.MatchState.Paused: return "PAUSADO";
                case CollectiveMatchMode.MatchState.HalfTime: return "INTERVALO";
                case CollectiveMatchMode.MatchState.ExtraTime: return "PRORROGAÇÃO";
                case CollectiveMatchMode.MatchState.Penalties: return "PÊNALTIS";
                case CollectiveMatchMode.MatchState.Finished: return "ENCERRADO";
                default: return state.ToString();
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
            if (_weather?.Current == Clima.Chuva) return UiKit.Cyan;
            if (_weather?.Current == Clima.Tempestade) return new Color(0.6f, 0.45f, 1f);
            return UiKit.Yellow;
        }

        string GetControlsHint()
        {
            var t = InputBridge.ControleConectado;
            if (t != TipoControle.Nenhum)
            {
                return "Analógico: mover   " + InputBridge.Glyph("chute") + " chute   " + InputBridge.Glyph("colocado") + " colocado   " +
                       InputBridge.Glyph("cavadinha") + " cavadinha   " + InputBridge.Glyph("rasteiro") + " rasteiro   " +
                       InputBridge.Glyph("drible") + " drible   " + InputBridge.Glyph("arrancada") + " arrancada   " +
                       InputBridge.Glyph("correr") + " correr   " + InputBridge.Glyph("camera") + " câmera   " + InputBridge.Glyph("pausa") + " pausa";
            }
            return "WASD mover   Shift correr   Espaço chute   E colocado   Q cavadinha   Z rasteiro   F drible   X arrancada   C câmera   Esc pausa";
        }

        // ------------------------------------------------------------ Injeção de dependências (chamado pelo bootstrap)

        public void InjectDependencies(CameraRig cameraRig, WeatherSystem weather, TouchControls touch, PuddleField puddleField)
        {
            _cameraRig = cameraRig;
            _weather = weather;
            _touch = touch;
            _puddleField = puddleField;
        }

        public void SetCollectiveMatchMode(CollectiveMatchMode mode)
        {
            _collectiveMatchMode = mode;
        }
    }
}