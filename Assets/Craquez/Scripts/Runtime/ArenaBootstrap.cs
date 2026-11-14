using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Craquez
{
    /// <summary>
    /// Monta a arena de treino em tempo de execução a partir dos assets ligados pelo setup do editor
    /// (menu Craquez 3D > Configurar Projeto). Mantém a cena simples e robusta.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ArenaBootstrap : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject playerPrefab;
        public GameObject keeperPrefab;
        public GameObject ballPrefab;

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
        public VideoClip introClip;

        [Header("Jogador")]
        [Tooltip("Altura do jogador em metros (o modelo é escalado para esta altura no setup).")]
        public float playerHeight = 1.78f;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Time.timeScale = 1f;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 0.01f;            // 100 Hz: física da bola precisa
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            Physics.bounceThreshold = 0.6f;

            var world = new GameObject("Mundo").transform;

            // materiais com instância própria (o clima altera em tempo de execução)
            var gl = new Material(grassLight); var gd = new Material(grassDark);
            var pm = new Material(puddle); var cm = clouds != null ? new Material(clouds) : null;
            var sm = soaked != null ? new Material(soaked) : null;

            Renderer[] grassRenderers;
            PitchBuilder.Build(world, gl, gd, lines, out grassRenderers);
            GoalNet.Build(world, "GolTreino", new Vector3(Arena.HalfLength - PitchBuilder.LineWidth * 0.5f, 0f, 0f), 0f, post, net);
            GoalNet.Build(world, "GolOposto", new Vector3(-Arena.HalfLength + PitchBuilder.LineWidth * 0.5f, 0f, 0f), 180f, post, net);

            var pfGo = new GameObject("Pocas");
            pfGo.transform.SetParent(world, false);
            var puddles = pfGo.AddComponent<PuddleField>();
            puddles.Init(pm, sm, ripple);

            var audioGo = new GameObject("AudioProcedural");
            audioGo.transform.SetParent(world, false);
            audioGo.AddComponent<ProceduralAudio>();

            // personagens e bola
            var ballGo = Instantiate(ballPrefab, new Vector3(30f, Arena.BallRadius, 0f), Quaternion.identity);
            ballGo.name = "Bola";
            var ball = ballGo.GetComponent<BallPhysics>();
            if (ball == null) ball = ballGo.AddComponent<BallPhysics>();

            var playerGo = Instantiate(playerPrefab, new Vector3(28f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            playerGo.name = "Jogador";
            var player = playerGo.GetComponent<PlayerController>();
            if (player == null) player = playerGo.AddComponent<PlayerController>();
            player.ball = ball;
            player.cameraTransform = mainCamera.transform;
            var shooter = playerGo.GetComponent<ShooterAI>();
            if (shooter == null) shooter = playerGo.AddComponent<ShooterAI>();
            shooter.ball = ball;
            shooter.enabled = false;

            var keeperGo = Instantiate(keeperPrefab, Arena.AttackGoalCenter + new Vector3(-0.6f, 0f, 0f), Quaternion.LookRotation(-Vector3.right));
            keeperGo.name = "Goleiro";
            var keeper = keeperGo.GetComponent<GoalkeeperAI>();
            if (keeper == null) keeper = keeperGo.AddComponent<GoalkeeperAI>();
            keeper.ball = ball;
            keeper.cameraTransform = mainCamera.transform;

            // clima
            var wGo = new GameObject("Clima");
            wGo.transform.SetParent(world, false);
            var weather = wGo.AddComponent<WeatherSystem>();
            weather.sun = sun;
            weather.skyboxTemplate = skybox;
            weather.rainMaterial = rain;
            weather.splashMaterial = splash;
            weather.dropletMaterial = droplet;
            weather.cloudMaterial = cm;
            weather.postProcessRoot = postProcessRoot;
            weather.probe = reflectionProbe;
            weather.puddleField = puddles;
            weather.followCamera = mainCamera.transform;
            weather.grassMaterials.Add(gl); weather.grassBaseColors.Add(gl.GetColor("_BaseColor"));
            weather.grassMaterials.Add(gd); weather.grassBaseColors.Add(gd.GetColor("_BaseColor"));
            CollectCharacterMaterials(playerGo, weather.characterMaterials);
            CollectCharacterMaterials(keeperGo, weather.characterMaterials);
            CollectCharacterMaterials(ballGo, weather.characterMaterials);
            weather.Build();

            // câmera
            var rig = mainCamera.GetComponent<CameraRig>();
            if (rig == null) rig = mainCamera.gameObject.AddComponent<CameraRig>();
            rig.player = playerGo.transform;
            rig.keeper = keeperGo.transform;
            rig.ball = ball;
            mainCamera.farClipPlane = 1500f;

            // controles e sessão
            var ui = new GameObject("Interface");
            ui.transform.SetParent(world, false);
            var touch = ui.AddComponent<TouchControls>();
            touch.circle = uiCircle;
            touch.enabled = false;
            var session = ui.AddComponent<TrainingSession>();
            session.player = player;
            session.shooterAI = shooter;
            session.keeper = keeper;
            session.ball = ball;
            session.cameraRig = rig;
            session.weather = weather;
            session.touch = touch;

            var updater = ui.AddComponent<AppUpdater>();
            var flow = ui.AddComponent<AppFlow>();
            flow.session = session;
            flow.updater = updater;
            flow.introClip = introClip;
            flow.cameraRig = rig;

            // bola quica menos com o gramado molhado
            WeatherBallLink link = ui.AddComponent<WeatherBallLink>();
            link.ball = ball;
            link.weather = weather;
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
