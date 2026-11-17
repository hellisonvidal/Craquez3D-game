using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Craquez
{
    /// <summary>
    /// Modo de jogo coletivo 11x11 - primeiro modo de jogo implementado.
    /// Gerencia: escalações, substituições, táticas, UI de partida, replay, estatísticas.
    /// </summary>
    public class CollectiveMatchMode : MonoBehaviour
    {
        public static CollectiveMatchMode Instance { get; private set; }

        [Header("Configurações da Partida")]
        public float matchDurationMinutes = 90f;
        public bool enableExtraTime = false;
        public bool enablePenalties = false;
        public int maxSubstitutions = 5;
        public bool useVAR = true;

        [Header("Prefabs")]
        public GameObject playerPrefab;
        public GameObject keeperPrefab;
        public GameObject ballPrefab;

        [Header("Times")]
        public TeamData homeTeamData;
        public TeamData awayTeamData;

        [Header("UI References")]
        public GameObject matchUIPrefab;
        public GameObject pauseMenuPrefab;
        public GameObject matchEndPrefab;
        public GameObject formationUIPrefab;
        public GameObject playerStatsPrefab;

        [Header("Câmeras")]
        public Camera mainCamera;
        public Camera tacticalCamera;
        // Cinemachine cameras - commented out if Cinemachine not installed
        // public Cinemachine.CinemachineVirtualCamera broadcastCamera;
        // public Cinemachine.CinemachineVirtualCamera tacticalCam;

        // Estado da partida
        public enum MatchState
        {
            NotStarted,
            TeamSelection,
            FormationSetup,
            PreMatch,
            InProgress,
            Paused,
            HalfTime,
            ExtraTime,
            Penalties,
            Finished
        }

        public MatchState CurrentState { get; private set; } = MatchState.NotStarted;

        // Componentes
        private FootballRulesEngine _rulesEngine;
        private RefereeSystem _refereeSystem;
        private TeamAI _homeTeamAI;
        private TeamAI _awayTeamAI;
        private BallPhysics _ball;
        // UI components - use MonoBehaviour instead of custom types
        private MonoBehaviour _matchUI;
        private MonoBehaviour _formationUI;

        // Jogadores em campo
        private Dictionary<int, PlayerController> _homePlayers = new Dictionary<int, PlayerController>();
        private Dictionary<int, PlayerController> _awayPlayers = new Dictionary<int, PlayerController>();
        private Dictionary<int, GoalkeeperAI> _homeGK = new Dictionary<int, GoalkeeperAI>();
        private Dictionary<int, GoalkeeperAI> _awayGK = new Dictionary<int, GoalkeeperAI>();

        // Substituições
        private int _homeSubsUsed = 0;
        private int _awaySubsUsed = 0;
        private List<int> _homeBench = new List<int>();
        private List<int> _awayBench = new List<int>();

        // Estatísticas da partida
        private MatchStatistics _matchStats = new MatchStatistics();

        // Replay system
        private List<MatchSnapshot> _matchHistory = new List<MatchSnapshot>();
        private float _snapshotInterval = 0.1f;
        private float _lastSnapshotTime = 0f;
        private bool _recordingReplay = true;

        // Eventos
        public event Action<MatchState> OnStateChanged;
        public event Action<int, int> OnGoal; // teamId, playerId
        public event Action<int, int, FootballRulesEngine.CardType> OnCard;
        public event Action<int, int, int> OnSubstitution; // teamId, out, in
        public event Action OnMatchEnd;

        [Serializable]
        public class TeamData
        {
            public string teamName;
            public string shortName;
            public Color primaryColor;
            public Color secondaryColor;
            public Sprite crest;
            public FootballRulesEngine.Formation preferredFormation = FootballRulesEngine.Formation.FourFourTwo;
            public FootballRulesEngine.TeamMentality mentality = FootballRulesEngine.TeamMentality.Balanced;
            public List<PlayerData> squad = new List<PlayerData>(); // 23 players (11 starters + 12 subs)

            // Home/Away Kits
            public Color homeKitPrimary = Color.white;
            public Color homeKitSecondary = Color.blue;
            public Color homeKitNumber = Color.black;
            public Color awayKitPrimary = Color.blue;
            public Color awayKitSecondary = Color.white;
            public Color awayKitNumber = Color.white;
            public Color thirdKitPrimary = Color.red;
            public Color thirdKitSecondary = Color.black;
            public Color thirdKitNumber = Color.white;
        }

        [Serializable]
        public class PlayerData
        {
            public string playerName;
            public int shirtNumber;
            public PlayerPosition position; // PlayerPosition is in Craquez namespace
            public int overall;
            public PlayerAttributes attributes;
            public PlayerMentality mentality;
            public bool isStarter = false;
            public bool isCaptain = false;
            public bool isViceCaptain = false;
        }

        [Serializable]
        public class MatchStatistics
        {
            public int homeScore = 0;
            public int awayScore = 0;
            public int homeShots = 0;
            public int awayShots = 0;
            public int homeShotsOnTarget = 0;
            public int awayShotsOnTarget = 0;
            public int homePossession = 0;
            public int awayPossession = 0;
            public int homePasses = 0;
            public int awayPasses = 0;
            public int homePassAccuracy = 0;
            public int awayPassAccuracy = 0;
            public int homeCorners = 0;
            public int awayCorners = 0;
            public int homeFouls = 0;
            public int awayFouls = 0;
            public int homeOffsides = 0;
            public int awayOffsides = 0;
            public int homeYellowCards = 0;
            public int awayYellowCards = 0;
            public int homeRedCards = 0;
            public int awayRedCards = 0;
            public Dictionary<int, PlayerStats> playerStats = new Dictionary<int, PlayerStats>();

            public int HomePossessionPct => homePossession + awayPossession > 0 ?
                (homePossession * 100) / (homePossession + awayPossession) : 50;
            public int AwayPossessionPct => 100 - HomePossessionPct;
        }

        [Serializable]
        public class PlayerStats
        {
            public int playerId;
            public string playerName;
            public int teamId;
            public int minutesPlayed = 0;
            public int goals = 0;
            public int assists = 0;
            public int shots = 0;
            public int shotsOnTarget = 0;
            public int passesAttempted = 0;
            public int passesCompleted = 0;
            public int keyPasses = 0;
            public int crosses = 0;
            public int crossesCompleted = 0;
            public int dribblesAttempted = 0;
            public int dribblesCompleted = 0;
            public int tacklesWon = 0;
            public int interceptions = 0;
            public int clearances = 0;
            public int blocks = 0;
            public int foulsCommitted = 0;
            public int foulsSuffered = 0;
            public int offsides = 0;
            public int yellowCards = 0;
            public int redCards = 0;
            public int saves = 0; // GK
            public int goalsConceded = 0; // GK
            public float distanceCovered = 0f;
            public float topSpeed = 0f;
            public float avgSpeed = 0f;
            public int rating = 6; // 1-10
        }

        [Serializable]
        public class MatchSnapshot
        {
            public float time;
            public Vector3 ballPosition;
            public Vector3 ballVelocity;
            public int ballHolderTeam = -1;
            public int ballHolderId = -1;
            public Dictionary<int, PlayerSnapshot> playerSnapshots = new Dictionary<int, PlayerSnapshot>();
            public int homeScore;
            public int awayScore;
        }

        [Serializable]
        public class PlayerSnapshot
        {
            public int playerId;
            public int teamId;
            public Vector3 position;
            public Vector3 rotation;
            public Vector3 velocity;
            public string animationState;
            public float stamina;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            InitializeSystems();
            CreateDefaultTeamsIfNeeded();
            EnterTeamSelection();
        }

        void InitializeSystems()
        {
            _rulesEngine = FootballRulesEngine.Instance;
            _refereeSystem = RefereeSystem.Instance;
            _ball = BallPhysics.Instance;

            // Find or create TeamAI components
            _homeTeamAI = FindOrCreateTeamAI(0);
            _awayTeamAI = FindOrCreateTeamAI(1);

            // Subscribe to rules engine events
            if (_rulesEngine != null)
            {
                _rulesEngine.OnGoalScored += OnGoalScored;
                _rulesEngine.OnCardShown += OnCardShown;
                _rulesEngine.OnSubstitution += OnSubstitutionMade;
                _rulesEngine.OnMatchEvent += OnMatchEvent;
                _rulesEngine.OnPhaseChanged += OnPhaseChanged;
            }
        }

        TeamAI FindOrCreateTeamAI(int teamId)
        {
            var existing = FindObjectsByType<TeamAI>(FindObjectsSortMode.None);
            foreach (var ai in existing)
            {
                if (ai.teamId == teamId) return ai;
            }

            var obj = new GameObject($"TeamAI_Team{teamId}");
            obj.transform.SetParent(transform);
            var ai2 = obj.AddComponent<TeamAI>();
            ai2.teamId = teamId;
            return ai2;
        }

        void CreateDefaultTeamsIfNeeded()
        {
            if (homeTeamData == null)
            {
                homeTeamData = CreateDefaultTeamData("CRAQUEZ FC", "CRQ", Color.blue, Color.white, 0);
            }
            if (awayTeamData == null)
            {
                awayTeamData = CreateDefaultTeamData("OPOSIÇÃO FC", "OPO", Color.red, Color.white, 1);
            }
        }

        TeamData CreateDefaultTeamData(string name, string shortName, Color primary, Color secondary, int teamId)
        {
            var data = new TeamData
            {
                teamName = name,
                shortName = shortName,
                primaryColor = primary,
                secondaryColor = secondary,
                squad = new List<PlayerData>(),

                // Generate random kit colors based on primary/secondary
                homeKitPrimary = primary,
                homeKitSecondary = secondary,
                homeKitNumber = GetContrastingColor(primary),
                awayKitPrimary = Color.white,
                awayKitSecondary = primary,
                awayKitNumber = GetContrastingColor(Color.white),
                thirdKitPrimary = GetThirdKitColor(primary),
                thirdKitSecondary = GetThirdKitColor(secondary),
                thirdKitNumber = GetContrastingColor(GetThirdKitColor(primary))
            };

            // Generate 23 players (11 starters + 12 subs)
            var positions = new[]
            {
                PlayerPosition.GK,
                PlayerPosition.CB,
                PlayerPosition.CB,
                PlayerPosition.FB,
                PlayerPosition.FB,
                PlayerPosition.CDM,
                PlayerPosition.CM,
                PlayerPosition.CM,
                PlayerPosition.WM,
                PlayerPosition.ST,
                PlayerPosition.ST
            };

            string[] firstNames = { "João", "Pedro", "Lucas", "Mateus", "Gabriel", "Rafael", "Felipe", "Gustavo", "Bruno", "Diego", "Alex", "Carlos", "Roberto", "Fernando", "Ricardo", "Marcelo", "André", "Tiago", "Vinicius", "Thiago", "Leonardo", "Rodrigo", "Eduardo" };
            string[] lastNames = { "Silva", "Santos", "Oliveira", "Souza", "Pereira", "Costa", "Rodrigues", "Almeida", "Nascimento", "Lima", "Araújo", "Fernandes", "Carvalho", "Gomes", "Martins", "Rocha", "Ribeiro", "Alves", "Monteiro", "Mendes", "Barbosa", "Cavalcanti", "Freitas" };

            for (int i = 0; i < 23; i++)
            {
                var pos = i < positions.Length ? positions[i] : PlayerPosition.CM;
                var playerData = new PlayerData
                {
                    playerName = $"{firstNames[UnityEngine.Random.Range(0, firstNames.Length)]} {lastNames[UnityEngine.Random.Range(0, lastNames.Length)]}",
                    shirtNumber = i + 1,
                    position = pos,
                    overall = UnityEngine.Random.Range(55, 85),
                    attributes = PlayerAttributes.GenerateRandom(pos, UnityEngine.Random.Range(55, 85)),
                    mentality = PlayerMentality.GenerateForPosition(pos, PlayerAttributes.GenerateRandom(pos, UnityEngine.Random.Range(55, 85))),
                    isStarter = i < 11,
                    isCaptain = i == 0,
                    isViceCaptain = i == 1
                };
                data.squad.Add(playerData);
            }

            return data;
        }

        // Helper methods for kit color generation
        static Color GetContrastingColor(Color baseColor)
        {
            // Return black for light colors, white for dark colors
            float luminance = 0.299f * baseColor.r + 0.587f * baseColor.g + 0.114f * baseColor.b;
            return luminance > 0.5f ? Color.black : Color.white;
        }

        static Color GetThirdKitColor(Color baseColor)
        {
            // Generate a complementary/contrasting color for third kit
            float h, s, v;
            Color.RGBToHSV(baseColor, out h, out s, out v);
            float newH = (h + 0.5f) % 1f; // Opposite hue
            return Color.HSVToRGB(newH, s * 0.8f, v);
        }

        // =====================================================================
        // ESTADOS DO JOGO
        // =====================================================================

        void EnterTeamSelection()
        {
            CurrentState = MatchState.TeamSelection;
            OnStateChanged?.Invoke(CurrentState);

            // Show team selection UI
            ShowTeamSelectionUI();
        }

        void EnterFormationSetup()
        {
            CurrentState = MatchState.FormationSetup;
            OnStateChanged?.Invoke(CurrentState);

            ShowFormationUI();
        }

        void EnterPreMatch()
        {
            CurrentState = MatchState.PreMatch;
            OnStateChanged?.Invoke(CurrentState);

            // Initialize match in rules engine
            var homeState = new FootballRulesEngine.TeamState
            {
                teamId = 0,
                teamName = homeTeamData.teamName,
                formation = homeTeamData.preferredFormation,
                mentality = homeTeamData.mentality,
                playersOnField = new List<int>(),
                substitutes = new List<int>()
            };

            var awayState = new FootballRulesEngine.TeamState
            {
                teamId = 1,
                teamName = awayTeamData.teamName,
                formation = awayTeamData.preferredFormation,
                mentality = awayTeamData.mentality,
                playersOnField = new List<int>(),
                substitutes = new List<int>()
            };

            // Assign starters and subs
            for (int i = 0; i < homeTeamData.squad.Count; i++)
            {
                var pd = homeTeamData.squad[i];
                if (pd.isStarter) homeState.playersOnField.Add(i);
                else homeState.substitutes.Add(i);
            }

            for (int i = 0; i < awayTeamData.squad.Count; i++)
            {
                var pd = awayTeamData.squad[i];
                if (pd.isStarter) awayState.playersOnField.Add(i);
                else awayState.substitutes.Add(i);
            }

            _homeBench = new List<int>(homeState.substitutes);
            _awayBench = new List<int>(awayState.substitutes);

            _rulesEngine.InitializeMatch(homeState, awayState, matchDurationMinutes);

            // Spawn players
            SpawnTeamPlayers(0, homeState.playersOnField, homeTeamData);
            SpawnTeamPlayers(1, awayState.playersOnField, awayTeamData);

            // Show pre-match UI (lineups, etc.)
            ShowPreMatchUI();
        }

        void SpawnTeamPlayers(int teamId, List<int> starterIndices, TeamData teamData)
        {
            var teamAI = teamId == 0 ? _homeTeamAI : _awayTeamAI;
            var playersDict = teamId == 0 ? _homePlayers : _awayPlayers;
            var gkDict = teamId == 0 ? _homeGK : _awayGK;

            foreach (int squadIndex in starterIndices)
            {
                var pd = teamData.squad[squadIndex];
                var playerObj = CreatePlayerObject(pd, teamId, teamData);
                if (playerObj == null) continue;

                var pc = playerObj.GetComponent<PlayerController>();
                var gk = playerObj.GetComponent<GoalkeeperAI>();

                if (pc != null)
                {
                    playersDict[squadIndex] = pc;
                    pc.aiControlled = true;
                }
                if (gk != null)
                {
                    gkDict[squadIndex] = gk;
                }

                // Register with systems
                if (_rulesEngine != null)
                {
                    _rulesEngine.RegisterPlayer(squadIndex, pc, pd.attributes, pd.mentality);
                }

                if (teamAI != null)
                {
                    teamAI.RegisterPlayer(squadIndex, pc, pd.attributes, pd.mentality);
                }
            }
        }

        GameObject CreatePlayerObject(PlayerData pd, int teamId, TeamData teamData)
        {
            // Use appropriate prefab based on position
            GameObject prefab = pd.position == PlayerPosition.GK ? keeperPrefab : playerPrefab;

            if (prefab == null)
            {
                Debug.LogError($"[CollectiveMatchMode] Prefab não configurado para {pd.position}");
                return null;
            }

            // Position on field
            Vector3 pos = GetFormationPosition(pd.position, teamId, starterIndices.IndexOf(_homePlayers.Count + _awayPlayers.Count));
            Quaternion rot = teamId == 0 ? Quaternion.LookRotation(Vector3.right) : Quaternion.LookRotation(Vector3.left);

            // Instantiate from prefab
            var playerObj = Instantiate(prefab, pos, rot);
            playerObj.name = $"Player_{pd.shirtNumber}_{pd.playerName}";
            playerObj.transform.SetParent(transform);

            // Configure for AI control
            var pc = playerObj.GetComponent<PlayerController>();
            var gk = playerObj.GetComponent<GoalkeeperAI>();

            if (pc != null)
            {
                pc.InputEnabled = false;
                pc.aiControlled = true;
            }

            if (gk != null)
            {
                gk.humanControlled = false;
            }

            // Ensure layer is correct
            playerObj.layer = Arena.LayerPlayerBody;

            return playerObj;
        }

        Vector3 GetFormationPosition(PlayerPosition pos, int teamId, int index)
        {
            // Simple positioning based on position
            float x, z;

            switch (pos)
            {
                case PlayerPosition.GK:
                    x = teamId == 0 ? -Arena.HalfLength + 2f : Arena.HalfLength - 2f;
                    z = 0f;
                    break;
                case PlayerPosition.CB:
                    x = teamId == 0 ? -Arena.HalfLength + 15f : Arena.HalfLength - 15f;
                    z = index % 2 == 0 ? -10f : 10f;
                    break;
                case PlayerPosition.FB:
                case PlayerPosition.WB:
                    x = teamId == 0 ? -Arena.HalfLength + 20f : Arena.HalfLength - 20f;
                    z = index % 2 == 0 ? -25f : 25f;
                    break;
                case PlayerPosition.CDM:
                    x = teamId == 0 ? -Arena.HalfLength + 30f : Arena.HalfLength - 30f;
                    z = 0f;
                    break;
                case PlayerPosition.CM:
                    x = teamId == 0 ? -Arena.HalfLength + 35f : Arena.HalfLength - 35f;
                    z = index % 2 == 0 ? -15f : 15f;
                    break;
                case PlayerPosition.AM:
                    x = teamId == 0 ? -Arena.HalfLength + 45f : Arena.HalfLength - 45f;
                    z = 0f;
                    break;
                case PlayerPosition.WM:
                case PlayerPosition.WF:
                    x = teamId == 0 ? -Arena.HalfLength + 40f : Arena.HalfLength - 40f;
                    z = index % 2 == 0 ? -20f : 20f;
                    break;
                case PlayerPosition.ST:
                case PlayerPosition.CF:
                    x = teamId == 0 ? -Arena.HalfLength + 50f : Arena.HalfLength - 50f;
                    z = index % 2 == 0 ? -5f : 5f;
                    break;
                default:
                    x = 0f; z = 0f;
                    break;
            }

            return new Vector3(x, 0f, z);
        }

        List<int> starterIndices = new List<int>();

        void EnterMatch()
        {
            CurrentState = MatchState.InProgress;
            OnStateChanged?.Invoke(CurrentState);

            _matchStats = new MatchStatistics();
            _matchHistory.Clear();
            _recordingReplay = true;
            _lastSnapshotTime = 0f;

            // Start recording
            InvokeRepeating(nameof(RecordSnapshot), 0f, _snapshotInterval);

            // Show match UI
            ShowMatchUI();

            // Kick off
            _ball.PlaceAt(Vector3.zero);
        }

        void RecordSnapshot()
        {
            if (!_recordingReplay) return;

            var snapshot = new MatchSnapshot
            {
                time = _rulesEngine.CurrentMatchTime,
                ballPosition = _ball.transform.position,
                ballVelocity = _ball.Velocity,
                homeScore = _matchStats.homeScore,
                awayScore = _matchStats.awayScore
            };

            // Ball holder
            if (_ball.IsHeld && _ball.Holder != null)
            {
                snapshot.ballHolderTeam = GetPlayerTeam(_ball.Holder);
                snapshot.ballHolderId = GetPlayerId(_ball.Holder);
            }

            // Player snapshots
            foreach (var kvp in _homePlayers)
            {
                if (kvp.Value != null)
                {
                    snapshot.playerSnapshots[kvp.Key] = CreatePlayerSnapshot(kvp.Key, 0, kvp.Value);
                }
            }
            foreach (var kvp in _awayPlayers)
            {
                if (kvp.Value != null)
                {
                    snapshot.playerSnapshots[kvp.Key] = CreatePlayerSnapshot(kvp.Key, 1, kvp.Value);
                }
            }

            _matchHistory.Add(snapshot);
        }

        PlayerSnapshot CreatePlayerSnapshot(int playerId, int teamId, PlayerController pc)
        {
            return new PlayerSnapshot
            {
                playerId = playerId,
                teamId = teamId,
                position = pc.transform.position,
                rotation = pc.transform.eulerAngles,
                velocity = pc.Velocity,
                animationState = pc.Anim != null ? pc.Anim.GetCurrentAnimatorStateInfo(0).IsName("") ? "" : pc.Anim.GetCurrentAnimatorStateInfo(0).shortNameHash.ToString() : "",
                stamina = _rulesEngine.PlayerAttributes.ContainsKey(playerId) ? _rulesEngine.PlayerAttributes[playerId].currentStamina : 100f
            };
        }

        int GetPlayerTeam(Transform holder)
        {
            // Check if holder belongs to home or away player
            foreach (var kvp in _homePlayers)
                if (kvp.Value != null && kvp.Value.transform == holder) return 0;
            foreach (var kvp in _awayPlayers)
                if (kvp.Value != null && kvp.Value.transform == holder) return 1;
            return -1;
        }

        int GetPlayerId(Transform holder)
        {
            foreach (var kvp in _homePlayers)
                if (kvp.Value != null && kvp.Value.transform == holder) return kvp.Key;
            foreach (var kvp in _awayPlayers)
                if (kvp.Value != null && kvp.Value.transform == holder) return kvp.Key;
            return -1;
        }

        // =====================================================================
        // EVENT HANDLERS
        // =====================================================================

        void OnGoalScored(int teamId, int playerId)
        {
            if (teamId == 0) _matchStats.homeScore++; else _matchStats.awayScore++;

            OnGoal?.Invoke(teamId, playerId);

            // Update player stats
            if (playerId >= 0)
            {
                UpdatePlayerStat(teamId, playerId, "goals", 1);
            }

            // Check for assist
            // (would need to track last passer)

            _recordingReplay = false; // Pause replay during celebration
            Invoke(nameof(ResumeReplay), 5f);
        }

        void ResumeReplay()
        {
            _recordingReplay = true;
        }

        void OnCardShown(int teamId, int playerId, FootballRulesEngine.CardType card)
        {
            OnCard?.Invoke(teamId, playerId, card);

            if (card == FootballRulesEngine.CardType.Yellow)
                UpdatePlayerStat(teamId, playerId, "yellowCards", 1);
            else
                UpdatePlayerStat(teamId, playerId, "redCards", 1);
        }

        void OnSubstitutionMade(int teamId, int playerOut, int playerIn)
        {
            OnSubstitution?.Invoke(teamId, playerOut, playerIn);

            if (teamId == 0)
            {
                _homeSubsUsed++;
                _homePlayers.Remove(playerOut);
                _homeBench.Remove(playerIn);
                // Spawn new player
                var pd = homeTeamData.squad[playerIn];
                var obj = CreatePlayerObject(pd, 0, homeTeamData);
                var pc = obj.GetComponent<PlayerController>();
                _homePlayers[playerIn] = pc;
                _rulesEngine.RegisterPlayer(playerIn, pc, pd.attributes, pd.mentality);
                _homeTeamAI.RegisterPlayer(playerIn, pc, pd.attributes, pd.mentality);
            }
            else
            {
                _awaySubsUsed++;
                _awayPlayers.Remove(playerOut);
                _awayBench.Remove(playerIn);
                var pd = awayTeamData.squad[playerIn];
                var obj = CreatePlayerObject(pd, 1, awayTeamData);
                var pc = obj.GetComponent<PlayerController>();
                _awayPlayers[playerIn] = pc;
                _rulesEngine.RegisterPlayer(playerIn, pc, pd.attributes, pd.mentality);
                _awayTeamAI.RegisterPlayer(playerIn, pc, pd.attributes, pd.mentality);
            }
        }

        void OnMatchEvent(FootballRulesEngine.MatchEvent evt)
        {
            switch (evt.type)
            {
                case FootballRulesEngine.MatchEventType.Corner:
                    if (evt.teamId == 0) _matchStats.homeCorners++; else _matchStats.awayCorners++;
                    break;
                case FootballRulesEngine.MatchEventType.Foul:
                    if (evt.teamId == 0) _matchStats.homeFouls++; else _matchStats.awayFouls++;
                    break;
                case FootballRulesEngine.MatchEventType.Offside:
                    if (evt.teamId == 0) _matchStats.homeOffsides++; else _matchStats.awayOffsides++;
                    break;
            }
        }

        void OnPhaseChanged(FootballRulesEngine.MatchPhase phase)
        {
            switch (phase)
            {
                case FootballRulesEngine.MatchPhase.HalfTime:
                    CurrentState = MatchState.HalfTime;
                    OnStateChanged?.Invoke(CurrentState);
                    ShowHalfTimeUI();
                    break;
                case FootballRulesEngine.MatchPhase.SecondHalf:
                    CurrentState = MatchState.InProgress;
                    OnStateChanged?.Invoke(CurrentState);
                    break;
                case FootballRulesEngine.MatchPhase.PostMatch:
                    EndMatch();
                    break;
            }
        }

        void UpdatePlayerStat(int teamId, int playerId, string statName, int value)
        {
            if (!_matchStats.playerStats.ContainsKey(playerId))
            {
                _matchStats.playerStats[playerId] = new PlayerStats
                {
                    playerId = playerId,
                    playerName = _rulesEngine.PlayerAttributes.ContainsKey(playerId) ?
                        _rulesEngine.PlayerAttributes[playerId].playerName : $"Player {playerId}",
                    teamId = teamId
                };
            }

            var stats = _matchStats.playerStats[playerId];
            var prop = stats.GetType().GetField(statName);
            if (prop != null)
            {
                int current = (int)prop.GetValue(stats);
                prop.SetValue(stats, current + value);
            }
        }

        // =====================================================================
        // SUBSTITUIÇÕES (UI)
        // =====================================================================

        public bool RequestSubstitution(int teamId, int playerOut, int playerIn)
        {
            if (teamId == 0)
            {
                if (_homeSubsUsed >= maxSubstitutions) return false;
                if (!_homePlayers.ContainsKey(playerOut)) return false;
                if (!_homeBench.Contains(playerIn)) return false;
            }
            else
            {
                if (_awaySubsUsed >= maxSubstitutions) return false;
                if (!_awayPlayers.ContainsKey(playerOut)) return false;
                if (!_awayBench.Contains(playerIn)) return false;
            }

            return _rulesEngine.RequestSubstitution(teamId, playerOut, playerIn);
        }

        // =====================================================================
        // PAUSE / MENU
        // =====================================================================

        public void TogglePause()
        {
            if (CurrentState == MatchState.InProgress)
            {
                CurrentState = MatchState.Paused;
                Time.timeScale = 0f;
                OnStateChanged?.Invoke(CurrentState);
                ShowPauseMenu();
            }
            else if (CurrentState == MatchState.Paused)
            {
                ResumeMatch();
            }
        }

        public void ResumeMatch()
        {
            CurrentState = MatchState.InProgress;
            Time.timeScale = 1f;
            OnStateChanged?.Invoke(CurrentState);
            HidePauseMenu();
        }

        // =====================================================================
        // FIM DE JOGO
        // =====================================================================

        void EndMatch()
        {
            CurrentState = MatchState.Finished;
            OnStateChanged?.Invoke(CurrentState);

            _recordingReplay = false;
            CancelInvoke(nameof(RecordSnapshot));

            // Calculate final ratings
            CalculatePlayerRatings();

            // Show match end screen
            ShowMatchEndUI();

            OnMatchEnd?.Invoke();
        }

        void CalculatePlayerRatings()
        {
            foreach (var kvp in _matchStats.playerStats)
            {
                var stats = kvp.Value;
                // Simple rating calculation based on stats
                float rating = 6f; // Base

                rating += stats.goals * 1.5f;
                rating += stats.assists * 1f;
                rating += stats.shotsOnTarget * 0.3f;
                rating += stats.keyPasses * 0.2f;
                rating += stats.tacklesWon * 0.3f;
                rating += stats.interceptions * 0.2f;
                rating -= stats.foulsCommitted * 0.1f;
                rating -= stats.yellowCards * 0.5f;
                rating -= stats.redCards * 2f;

                if (stats.saves > 0) // GK
                {
                    rating += stats.saves * 0.5f;
                    rating -= stats.goalsConceded * 0.5f;
                }

                stats.rating = Mathf.Clamp(Mathf.RoundToInt(rating * 2f) / 2, 1, 10); // Round to 0.5
            }
        }

        // =====================================================================
        // REPLAY SYSTEM
        // =====================================================================

        public void PlayReplay(float startTime, float duration)
        {
            // Find snapshots in range
            var relevant = _matchHistory.FindAll(s => s.time >= startTime && s.time <= startTime + duration);
            if (relevant.Count == 0) return;

            StartCoroutine(PlayReplayCoroutine(relevant));
        }

        System.Collections.IEnumerator PlayReplayCoroutine(List<MatchSnapshot> snapshots)
        {
            _recordingReplay = false;
            Time.timeScale = 0.5f; // Slow motion

            foreach (var snap in snapshots)
            {
                // Restore ball
                _ball.transform.position = snap.ballPosition;
                _ball.Body.linearVelocity = snap.ballVelocity;

                // Restore players
                foreach (var psnap in snap.playerSnapshots)
                {
                    var snapshot = psnap.Value;
                    PlayerController pc = null;
                    if (snapshot.teamId == 0 && _homePlayers.ContainsKey(snapshot.playerId))
                        pc = _homePlayers[snapshot.playerId];
                    else if (snapshot.teamId == 1 && _awayPlayers.ContainsKey(snapshot.playerId))
                        pc = _awayPlayers[snapshot.playerId];

                    if (pc != null)
                    {
                        pc.transform.position = snapshot.position;
                        pc.transform.eulerAngles = snapshot.rotation;
                    }
                }

                yield return new WaitForSecondsRealtime(_snapshotInterval);
            }

            Time.timeScale = 1f;
            _recordingReplay = true;
        }

        // =====================================================================
        // UI METHODS (Stubs - implement with actual UI system)
        // =====================================================================

        void ShowTeamSelectionUI() { /* Implement UI */ }
        void ShowFormationUI() { /* Implement UI */ }
        void ShowPreMatchUI() { /* Implement UI */ }
        void ShowMatchUI() { /* Implement UI */ }
        void ShowPauseMenu() { /* Implement UI */ }
        void HidePauseMenu() { /* Implement UI */ }
        void ShowHalfTimeUI() { /* Implement UI */ }
        void ShowMatchEndUI() { /* Implement UI */ }

        // =====================================================================
        // PUBLIC API
        // </summary>
        public MatchStatistics GetMatchStats() => _matchStats;
        public List<MatchSnapshot> GetMatchHistory() => new List<MatchSnapshot>(_matchHistory);
        public MatchState GetCurrentState() => CurrentState;
        public TeamData GetHomeTeamData() => homeTeamData;
        public TeamData GetAwayTeamData() => awayTeamData;
        public Dictionary<int, PlayerController> GetHomePlayers() => new Dictionary<int, PlayerController>(_homePlayers);
        public Dictionary<int, PlayerController> GetAwayPlayers() => new Dictionary<int, PlayerController>(_awayPlayers);
    }
}