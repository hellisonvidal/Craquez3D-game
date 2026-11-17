using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Craquez
{
    /// <summary>
    /// Modos de jogo disponíveis
    /// </summary>
    public enum GameMode
    {
        Treino,       // Jogo-treino 1x1 (atacante vs goleiro)
        Partida11v11  // Partida coletiva 11x11
    }

    /// <summary>
    /// Monta a arena em tempo de execução a partir dos assets ligados pelo setup do editor
    /// (menu Craquez 3D > Configurar Projeto). Suporta modo Treino e Partida 11x11.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ArenaBootstrap : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject playerPrefab;
        public GameObject keeperPrefab;
        public GameObject ballPrefab;
        public GameObject refereePrefab;

        [Header("Materiais")]
        public Material grassLight;
        public Material grassDark;
        public Material lines;
        public Material puddle;
        public Material soaked;
        public Material ripple;
        public Material net;
        public Material post;
        public Material rain;
        public Material splash;
        public Material droplet;
        public Material clouds;
        public Material skybox;

        [Header("Cena")]
        public Light sun;
        public Camera mainCamera;
        public Transform postProcessRoot;
        public ReflectionProbe reflectionProbe;
        public Texture2D uiCircle;
        public Texture2D logo;
        public VideoClip introClip;

        [Header("Jogador")]
        [Tooltip("Altura do jogador em metros (o modelo é escalado para esta altura no setup).")]
        public float playerHeight = 1.78f;

        // Componentes comuns da cena
        public BallPhysics Ball { get; private set; }
        public WeatherSystem Weather { get; private set; }
        public CameraRig CameraRig { get; private set; }
        public PuddleField PuddleField { get; private set; }
        public TouchControls TouchControls { get; private set; }
        public AppUpdater AppUpdater { get; private set; }
        public AppFlow AppFlow { get; private set; }
        public GameObject WorldRoot { get; private set; }
        public Material GrassLightMat { get; private set; }
        public Material GrassDarkMat { get; private set; }

        // Estado
        private GameMode _currentMode = GameMode.Treino;
        private TrainingSession _trainingSession;
        private MatchSession11v11 _matchSession11v11;
        private PlayerController _player;
        private GoalkeeperAI _keeper;
        private ShooterAI _shooterAI;
        private GameObject _uiRoot;

        void Awake()
        {
            ErrorOverlay.Ensure();
            Application.targetFrameRate = 60;
            Time.timeScale = 1f;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 0.01f;            // 100 Hz: física da bola precisa
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            Physics.bounceThreshold = 0.6f;
            Arena.ConfigurePhysicsLayers();

            WorldRoot = new GameObject("Mundo").transform;

            // Materiais com instância própria (o clima altera em tempo de execução)
            GrassLightMat = new Material(grassLight);
            GrassDarkMat = new Material(grassDark);
            var pm = new Material(puddle);
            var cm = clouds != null ? new Material(clouds) : null;
            var sm = soaked != null ? new Material(soaked) : null;

            // Campo e gols
            Renderer[] grassRenderers;
            PitchBuilder.Build(WorldRoot, GrassLightMat, GrassDarkMat, lines, out grassRenderers);
            Safe("gols", () =>
            {
                GoalNet.Build(WorldRoot, "GolTreino", new Vector3(Arena.HalfLength - PitchBuilder.LineWidth * 0.5f, 0f, 0f), 0f, post, net);
                GoalNet.Build(WorldRoot, "GolOposto", new Vector3(-Arena.HalfLength + PitchBuilder.LineWidth * 0.5f, 0f, 0f), 180f, post, net);
            });

            // Poças
            var pfGo = new GameObject("Pocas");
            pfGo.transform.SetParent(WorldRoot, false);
            PuddleField = pfGo.AddComponent<PuddleField>();
            Safe("poças", () => PuddleField.Init(pm, sm, ripple));

            // Áudio procedural
            var audioGo = new GameObject("AudioProcedural");
            audioGo.transform.SetParent(WorldRoot, false);
            Safe("áudio", () => audioGo.AddComponent<ProceduralAudio>());

            // Bola
            var ballGo = Instantiate(ballPrefab, new Vector3(30f, Arena.BallRadius, 0f), Quaternion.identity);
            ballGo.name = "Bola";
            Ball = ballGo.GetComponent<BallPhysics>();
            if (Ball == null) Ball = ballGo.AddComponent<BallPhysics>();

            // Clima
            var wGo = new GameObject("Clima");
            wGo.transform.SetParent(WorldRoot, false);
            Weather = wGo.AddComponent<WeatherSystem>();
            Weather.sun = sun;
            Weather.skyboxTemplate = skybox;
            Weather.rainMaterial = rain;
            Weather.splashMaterial = splash;
            Weather.dropletMaterial = droplet;
            Weather.cloudMaterial = cm;
            Weather.postProcessRoot = postProcessRoot;
            Weather.probe = reflectionProbe;
            Weather.puddleField = PuddleField;
            Weather.followCamera = mainCamera.transform;
            Safe("clima", () => Weather.Build());

            // Câmera
            CameraRig = mainCamera.GetComponent<CameraRig>();
            if (CameraRig == null) CameraRig = mainCamera.gameObject.AddComponent<CameraRig>();
            CameraRig.ball = Ball;
            mainCamera.farClipPlane = 1500f;

            // Interface comum
            _uiRoot = new GameObject("Interface");
            _uiRoot.transform.SetParent(WorldRoot, false);
            TouchControls = _uiRoot.AddComponent<TouchControls>();
            TouchControls.circle = uiCircle;
            TouchControls.enabled = false;

            AppUpdater = _uiRoot.AddComponent<AppUpdater>();
            AppFlow = _uiRoot.AddComponent<AppFlow>();
            AppFlow.arenaBootstrap = this;

            // Criar TrainingSession mínima para preview/menus
            _trainingSession = _uiRoot.AddComponent<TrainingSession>();
            _trainingSession.cameraRig = CameraRig;
            _trainingSession.weather = Weather;
            _trainingSession.touch = TouchControls;

            AppFlow.session = _trainingSession;

            // WeatherBallLink
            WeatherBallLink link = _uiRoot.AddComponent<WeatherBallLink>();
            link.ball = Ball;
            link.weather = Weather;

            // Iniciar no modo Treino por padrão (AppFlow vai chamar Begin)
            _currentMode = GameMode.Treino;
        }

        /// <summary>
        /// Inicia o modo Jogo-Treino (1x1: atacante vs goleiro)
        /// </summary>
        public void StartTraining(Clima clima, PapelControle papel)
        {
            _currentMode = GameMode.Treino;

            // Limpar sessão anterior se houver
            CleanupCurrentSession();

            // Criar jogador
            var playerGo = Instantiate(playerPrefab, new Vector3(28f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            playerGo.name = "Jogador";
            _player = playerGo.GetComponent<PlayerController>();
            if (_player == null) _player = playerGo.AddComponent<PlayerController>();
            _player.ball = Ball;
            _player.cameraTransform = mainCamera.transform;

            // Shooter AI (para modo goleiro)
            _shooterAI = playerGo.GetComponent<ShooterAI>();
            if (_shooterAI == null) _shooterAI = playerGo.AddComponent<ShooterAI>();
            _shooterAI.ball = Ball;
            _shooterAI.enabled = false;

            // Criar goleiro
            var keeperGo = Instantiate(keeperPrefab, Arena.AttackGoalCenter + new Vector3(-0.6f, 0f, 0f), Quaternion.LookRotation(-Vector3.right));
            keeperGo.name = "Goleiro";
            _keeper = keeperGo.GetComponent<GoalkeeperAI>();
            if (_keeper == null) _keeper = keeperGo.AddComponent<GoalkeeperAI>();
            _keeper.ball = Ball;
            _keeper.cameraTransform = mainCamera.transform;

            // Coletar materiais para o clima
            if (Weather != null)
            {
                Weather.grassMaterials.Clear();
                Weather.grassBaseColors.Clear();
                Weather.grassMaterials.Add(GrassLightMat); Weather.grassBaseColors.Add(GrassLightMat.GetColor("_BaseColor"));
                Weather.grassMaterials.Add(GrassDarkMat); Weather.grassBaseColors.Add(GrassDarkMat.GetColor("_BaseColor"));
                CollectCharacterMaterials(playerGo, Weather.characterMaterials);
                CollectCharacterMaterials(keeperGo, Weather.characterMaterials);
                CollectCharacterMaterials(Ball.gameObject, Weather.characterMaterials);
            }

            // Configurar câmera
            CameraRig.player = playerGo.transform;
            CameraRig.keeper = keeperGo.transform;
            CameraRig.ball = Ball;

            // Criar sessão de treino
            _trainingSession = _uiRoot.GetComponent<TrainingSession>();
            if (_trainingSession == null)
            {
                _trainingSession = _uiRoot.AddComponent<TrainingSession>();
            }
            _trainingSession.player = _player;
            _trainingSession.shooterAI = _shooterAI;
            _trainingSession.keeper = _keeper;
            _trainingSession.ball = Ball;
            _trainingSession.cameraRig = CameraRig;
            _trainingSession.weather = Weather;
            _trainingSession.touch = TouchControls;

            // Atualizar AppFlow
            if (AppFlow != null)
            {
                AppFlow.session = _trainingSession;
                AppFlow.cameraRig = CameraRig;
            }

            // Iniciar sessão
            Weather.Apply(clima);
            _trainingSession.Begin(clima, papel);
        }

        /// <summary>
        /// Inicia o modo Partida 11x11
        /// </summary>
        public void StartMatch11v11(Clima clima, CollectiveMatchMode.TeamData homeTeam = null, CollectiveMatchMode.TeamData awayTeam = null)
        {
            _currentMode = GameMode.Partida11v11;

            // Limpar sessão anterior se houver
            CleanupCurrentSession();

            // Configurar prefabs no CollectiveMatchMode (singleton)
            var collectiveMode = CollectiveMatchMode.Instance;
            if (collectiveMode != null)
            {
                collectiveMode.playerPrefab = playerPrefab;
                collectiveMode.keeperPrefab = keeperPrefab;
                collectiveMode.ballPrefab = ballPrefab;
            }

            // Criar sessão 11v11
            if (_uiRoot != null)
            {
                _matchSession11v11 = _uiRoot.AddComponent<MatchSession11v11>();
                _matchSession11v11.playerPrefab = playerPrefab;
                _matchSession11v11.keeperPrefab = keeperPrefab;
                _matchSession11v11.ballPrefab = ballPrefab;
                _matchSession11v11.refereePrefab = refereePrefab;
                _matchSession11v11.InjectDependencies(CameraRig, Weather, TouchControls, PuddleField);
            }

            // Configurar câmera (a bola já está configurada)
            CameraRig.ball = Ball;

            // Coletar materiais para o clima (a bola já existe)
            if (Weather != null)
            {
                Weather.grassMaterials.Clear();
                Weather.grassBaseColors.Clear();
                Weather.grassMaterials.Add(GrassLightMat); Weather.grassBaseColors.Add(GrassLightMat.GetColor("_BaseColor"));
                Weather.grassMaterials.Add(GrassDarkMat); Weather.grassBaseColors.Add(GrassDarkMat.GetColor("_BaseColor"));
                CollectCharacterMaterials(Ball.gameObject, Weather.characterMaterials);
                // Os jogadores serão adicionados pelo MatchSession11v11 / CollectiveMatchMode
            }

            // Atualizar AppFlow para usar a nova sessão
            if (AppFlow != null && _matchSession11v11 != null)
            {
                // AppFlow espera TrainingSession, mas podemos adaptar ou criar um wrapper
                // Por enquanto, deixamos o AppFlow com a TrainingSession para menus
                // O MatchSession11v11 gerencia sua própria UI
            }

            // Iniciar partida
            Weather.Apply(clima);
            _matchSession11v11?.Begin(clima, homeTeam, awayTeam);
        }

        void CleanupCurrentSession()
        {
            // Não destrói _trainingSession pois é reutilizado
            // Apenas limpa referências dos jogadores
            if (_player != null)
            {
                Destroy(_player.gameObject);
                _player = null;
            }
            if (_keeper != null)
            {
                Destroy(_keeper.gameObject);
                _keeper = null;
            }
            if (_shooterAI != null)
            {
                Destroy(_shooterAI);
                _shooterAI = null;
            }
            CameraRig.player = null;
            CameraRig.keeper = null;
            if (_matchSession11v11 != null)
            {
                Destroy(_matchSession11v11);
                _matchSession11v11 = null;
            }
            if (_player != null)
            {
                Destroy(_player.gameObject);
                _player = null;
            }
            if (_keeper != null)
            {
                Destroy(_keeper.gameObject);
                _keeper = null;
            }
            if (_shooterAI != null)
            {
                Destroy(_shooterAI);
                _shooterAI = null;
            }
            CameraRig.player = null;
            CameraRig.keeper = null;
        }

        static void Safe(string step, System.Action action)
        {
            try { action(); }
            catch (System.Exception e) { Debug.LogError("[Craquez 3D] Falha ao montar " + step + ": " + e); }
        }

        static void CollectCharacterMaterials(GameObject go, List<Material> list)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials) // cria instâncias por personagem
                    if (m != null && m.HasProperty("_Smoothness")) list.Add(m);
        }
    }

    /// <summary>Aplica à bola os parâmetros físicos do clima atual.</summary>
    public class WeatherBallLink : MonoBehaviour
    {
        public BallPhysics ball;
        public WeatherSystem weather;
        Clima? last;

        void Update()
        {
            if (weather.P == null) return;
            if (last.HasValue && last.Value == weather.Current) return;
            last = weather.Current;
            ball.ApplyWeather(weather.P.bounce);
        }
    }
}
