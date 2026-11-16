using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Gerenciador centralizado de efeitos de partículas usando assets gratuitos do Kenney.
    /// Inclui: poeira ao correr, fumaça no chute, rastro da bola, celebração de gol, etc.
    /// </summary>
    public class ParticleEffectsManager : MonoBehaviour
    {
        public static ParticleEffectsManager Instance { get; private set; }

        [Header("Particle Textures (Kenney Particle Pack)")]
        [SerializeField] private Texture2D[] dirtTextures;       // dirt_01-03 - poeira corrida/deslize
        [SerializeField] private Texture2D[] smokeTextures;      // smoke_01-10 - fumaça impacto
        [SerializeField] private Texture2D[] sparkTextures;      // spark_01-07 - faíscas chute forte
        [SerializeField] private Texture2D[] traceTextures;      // trace_01-07 - rastro bola
        [SerializeField] private Texture2D[] flameTextures;      // flame_01-06 - fogo celebração
        [SerializeField] private Texture2D[] fireTextures;       // fire_01-02 - fogo intenso
        [SerializeField] private Texture2D[] circleTextures;     // circle_01-05 - anéis impacto
        [SerializeField] private Texture2D[] lightTextures;      // light_01-03 - luzes estádio
        [SerializeField] private Texture2D flareTexture;         // flare_01 - flare
        [SerializeField] private Texture2D[] scorchTextures;     // scorch_01-03 - marcas grama
        [SerializeField] private Texture2D[] starTextures;       // star_01-09 - estrelas celebração

        [Header("Prefab References")]
        [SerializeField] private GameObject kickDustPrefab;
        [SerializeField] private GameObject kickImpactPrefab;
        [SerializeField] private GameObject ballTrailPrefab;
        [SerializeField] private GameObject slideDustPrefab;
        [SerializeField] private GameObject goalCelebrationPrefab;
        [SerializeField] private GameObject goalkeeperDivePrefab;
        [SerializeField] private GameObject footstepDustPrefab;
        [SerializeField] private GameObject grassScorchPrefab;

        [Header("Settings")]
        [SerializeField] private LayerMask groundLayer;

        private readonly Queue<ParticleSystem> _pool = new();
        private readonly Dictionary<string, ParticleSystem> _prefabs = new();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePrefabs();
        }

        void InitializePrefabs()
        {
            // Criar prefabs programaticamente se não existirem
            CreateKickDustPrefab();
            CreateKickImpactPrefab();
            CreateBallTrailPrefab();
            CreateSlideDustPrefab();
            CreateGoalCelebrationPrefab();
            CreateGoalkeeperDivePrefab();
            CreateFootstepDustPrefab();
            CreateGrassScorchPrefab();
        }

        // =====================================================================
        // CRIAÇÃO DE PREFABS DE PARTÍCULAS
        // =====================================================================

        void CreateKickDustPrefab()
        {
            var go = new GameObject("KickDustEffect");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.6f, 0.5f, 0.4f, 0.8f),
                new Color(0.4f, 0.35f, 0.3f, 0.3f)
            );
            main.gravityModifier = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 25) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.1f;
            shape.position = new Vector3(0, 0.05f, 0);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 2f);

            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;
            if (dirtTextures != null && dirtTextures.Length > 0)
            {
                for (int i = 0; i < dirtTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(dirtTextures[i], new Rect(0, 0, dirtTextures[i].width, dirtTextures[i].height), Vector2.one * 0.5f));
            }

            _prefabs["KickDust"] = ps;
        }

        void CreateKickImpactPrefab()
        {
            var go = new GameObject("KickImpactEffect");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.9f),
                new Color(0.8f, 0.7f, 0.6f, 0.2f)
            );
            main.gravityModifier = 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 35) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-1f, 3f);

            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;
            if (smokeTextures != null && smokeTextures.Length > 0)
            {
                for (int i = 0; i < smokeTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(smokeTextures[i], new Rect(0, 0, smokeTextures[i].width, smokeTextures[i].height), Vector2.one * 0.5f));
            }
            if (sparkTextures != null && sparkTextures.Length > 0)
            {
                for (int i = 0; i < sparkTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(sparkTextures[i], new Rect(0, 0, sparkTextures[i].width, sparkTextures[i].height), Vector2.one * 0.5f));
            }
            if (circleTextures != null && circleTextures.Length > 0)
            {
                for (int i = 0; i < circleTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(circleTextures[i], new Rect(0, 0, circleTextures[i].width, circleTextures[i].height), Vector2.one * 0.5f));
            }

            _prefabs["KickImpact"] = ps;
        }

        void CreateBallTrailPrefab()
        {
            var go = new GameObject("BallTrailEffect");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.6f),
                new Color(1f, 0.9f, 0.7f, 0.1f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 30f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;
            if (traceTextures != null && traceTextures.Length > 0)
            {
                for (int i = 0; i < traceTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(traceTextures[i], new Rect(0, 0, traceTextures[i].width, traceTextures[i].height), Vector2.one * 0.5f));
            }
            if (smokeTextures != null && smokeTextures.Length > 0)
            {
                for (int i = 0; i < Mathf.Min(3, smokeTextures.Length); i++)
                    textureSheet.AddSprite(Sprite.Create(smokeTextures[i], new Rect(0, 0, smokeTextures[i].width, smokeTextures[i].height), Vector2.one * 0.5f));
            }

            _prefabs["BallTrail"] = ps;
        }

        void CreateSlideDustPrefab()
        {
            var go = new GameObject("SlideDustEffect");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.5f, 0.45f, 0.4f, 0.7f),
                new Color(0.3f, 0.28f, 0.25f, 0.1f)
            );
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.5f, 0.05f, 0.2f);
            shape.position = new Vector3(0, 0.02f, 0);

            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;
            if (dirtTextures != null && dirtTextures.Length > 0)
            {
                for (int i = 0; i < dirtTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(dirtTextures[i], new Rect(0, 0, dirtTextures[i].width, dirtTextures[i].height), Vector2.one * 0.5f));
            }
            if (smokeTextures != null && smokeTextures.Length > 0)
            {
                for (int i = 0; i < Mathf.Min(3, smokeTextures.Length); i++)
                    textureSheet.AddSprite(Sprite.Create(smokeTextures[i], new Rect(0, 0, smokeTextures[i].width, smokeTextures[i].height), Vector2.one * 0.5f));
            }

            _prefabs["SlideDust"] = ps;
        }

        void CreateGoalCelebrationPrefab()
        {
            var go = new GameObject("GoalCelebrationEffect");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.9f, 0.3f, 1f),
                new Color(1f, 0.5f, 0.1f, 0.2f)
            );
            main.gravityModifier = -0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50, 80) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 1f;
            shape.position = new Vector3(0, 0.5f, 0);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(1f, 5f);

            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;
            if (flameTextures != null && flameTextures.Length > 0)
            {
                for (int i = 0; i < flameTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(flameTextures[i], new Rect(0, 0, flameTextures[i].width, flameTextures[i].height), Vector2.one * 0.5f));
            }
            if (fireTextures != null && fireTextures.Length > 0)
            {
                for (int i = 0; i < fireTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(fireTextures[i], new Rect(0, 0, fireTextures[i].width, fireTextures[i].height), Vector2.one * 0.5f));
            }
            if (starTextures != null && starTextures.Length > 0)
            {
                for (int i = 0; i < starTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(starTextures[i], new Rect(0, 0, starTextures[i].width, starTextures[i].height), Vector2.one * 0.5f));
            }
            if (circleTextures != null && circleTextures.Length > 0)
            {
                for (int i = 0; i < circleTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(circleTextures[i], new Rect(0, 0, circleTextures[i].width, circleTextures[i].height), Vector2.one * 0.5f));
            }

            _prefabs["GoalCelebration"] = ps;
        }

        void CreateGoalkeeperDivePrefab()
        {
            var go = new GameObject("GoalkeeperDiveEffect");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.8f),
                new Color(0.7f, 0.7f, 0.7f, 0.1f)
            );
            main.gravityModifier = 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 50) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.5f, 0.1f, 0.3f);
            shape.position = new Vector3(0, 0.05f, 0);

            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;
            if (dirtTextures != null && dirtTextures.Length > 0)
            {
                for (int i = 0; i < dirtTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(dirtTextures[i], new Rect(0, 0, dirtTextures[i].width, dirtTextures[i].height), Vector2.one * 0.5f));
            }
            if (smokeTextures != null && smokeTextures.Length > 0)
            {
                for (int i = 0; i < Mathf.Min(4, smokeTextures.Length); i++)
                    textureSheet.AddSprite(Sprite.Create(smokeTextures[i], new Rect(0, 0, smokeTextures[i].width, smokeTextures[i].height), Vector2.one * 0.5f));
            }

            _prefabs["GoalkeeperDive"] = ps;
        }

        void CreateFootstepDustPrefab()
        {
            var go = new GameObject("FootstepDustEffect");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.15f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.5f, 0.45f, 0.5f),
                new Color(0.35f, 0.32f, 0.28f, 0.1f)
            );
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 6) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.03f;
            shape.position = new Vector3(0, 0.02f, 0);

            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;
            if (dirtTextures != null && dirtTextures.Length > 0)
            {
                for (int i = 0; i < dirtTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(dirtTextures[i], new Rect(0, 0, dirtTextures[i].width, dirtTextures[i].height), Vector2.one * 0.5f));
            }
            if (smokeTextures != null && smokeTextures.Length > 0)
            {
                for (int i = 0; i < Mathf.Min(2, smokeTextures.Length); i++)
                    textureSheet.AddSprite(Sprite.Create(smokeTextures[i], new Rect(0, 0, smokeTextures[i].width, smokeTextures[i].height), Vector2.one * 0.5f));
            }

            _prefabs["FootstepDust"] = ps;
        }

        void CreateGrassScorchPrefab()
        {
            var go = new GameObject("GrassScorchEffect");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = 10f; // Marcas duram bastante
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.2f, 0.15f, 0.1f, 0.6f),
                new Color(0.1f, 0.08f, 0.05f, 0.3f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1, 2) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;
            shape.position = new Vector3(0, 0.01f, 0);
            shape.rotation = new Vector3(90, 0, 0);
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;
            if (scorchTextures != null && scorchTextures.Length > 0)
            {
                for (int i = 0; i < scorchTextures.Length; i++)
                    textureSheet.AddSprite(Sprite.Create(scorchTextures[i], new Rect(0, 0, scorchTextures[i].width, scorchTextures[i].height), Vector2.one * 0.5f));
            }

            _prefabs["GrassScorch"] = ps;
        }

        // =====================================================================
        // MÉTODOS PÚBLICOS PARA TRIGGER EFEITOS
        // =====================================================================

        /// <summary>Poeira ao chutar a bola</summary>
        public void PlayKickDust(Vector3 position, Vector3 direction, float intensity = 1f)
        {
            var ps = GetOrCreateParticleSystem("KickDust");
            if (ps == null) return;

            ps.transform.position = position;
            ps.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            var main = ps.main;
            main.startSpeedMultiplier = intensity;
            ps.Play();
        }

        /// <summary>Impacto visual no chute (fumaça + faíscas + anel)</summary>
        public void PlayKickImpact(Vector3 position, Vector3 direction, float power = 1f)
        {
            var ps = GetOrCreateParticleSystem("KickImpact");
            if (ps == null) return;

            ps.transform.position = position;
            ps.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            var main = ps.main;
            main.startSpeedMultiplier = 0.5f + power * 0.5f;
            main.startSizeMultiplier = 0.8f + power * 0.4f;
            ps.Play();
        }

        /// <summary>Ativa/desativa rastro da bola</summary>
        public ParticleSystem GetBallTrailSystem()
        {
            return GetOrCreateParticleSystem("BallTrail");
        }

        /// <summary>Poeira ao deslizar/carrinho</summary>
        public void PlaySlideDust(Vector3 position, Vector3 direction, float speed)
        {
            var ps = GetOrCreateParticleSystem("SlideDust");
            if (ps == null) return;

            ps.transform.position = position;
            ps.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            var main = ps.main;
            main.startSpeedMultiplier = Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(speed / 10f));
            ps.Play();
        }

        /// <summary>Celebração de gol com fogo/estrelas</summary>
        public void PlayGoalCelebration(Vector3 position)
        {
            var ps = GetOrCreateParticleSystem("GoalCelebration");
            if (ps == null) return;

            ps.transform.position = position + Vector3.up * 1f;
            ps.Play();
        }

        /// <summary>Poeira no mergulho do goleiro</summary>
        public void PlayGoalkeeperDive(Vector3 position, Vector3 direction, float intensity = 1f)
        {
            var ps = GetOrCreateParticleSystem("GoalkeeperDive");
            if (ps == null) return;

            ps.transform.position = position;
            ps.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            var main = ps.main;
            main.startSpeedMultiplier = intensity;
            main.startSizeMultiplier = intensity;
            ps.Play();
        }

        /// <summary>Pequena poeira no passo (corrida)</summary>
        public void PlayFootstepDust(Vector3 position, float speed)
        {
            if (speed < 2f) return; // Só em corrida

            var ps = GetOrCreateParticleSystem("FootstepDust");
            if (ps == null) return;

            ps.transform.position = position;
            var main = ps.main;
            main.startSpeedMultiplier = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(speed / 8f));
            ps.Play();
        }

        /// <summary>Marca de queimado na grama (chute muito forte)</summary>
        public void PlayGrassScorch(Vector3 position)
        {
            var ps = GetOrCreateParticleSystem("GrassScorch");
            if (ps == null) return;

            ps.transform.position = position;
            ps.Play();
        }

        // =====================================================================
        // POOLING
        // =====================================================================

        ParticleSystem GetOrCreateParticleSystem(string key)
        {
            if (_prefabs.TryGetValue(key, out var prefab) && prefab != null)
            {
                // Para efeitos únicos, instanciar novo
                if (key != "BallTrail")
                {
                    var instance = Instantiate(prefab, transform);
                    instance.Play();
                    // Auto-destruir após terminar
                    Destroy(instance.gameObject, prefab.main.duration + prefab.main.startLifetime.constantMax + 1f);
                    return instance;
                }
                return prefab;
            }
            return null;
        }

        /// <summary>Limpar todos os efeitos ativos</summary>
        public void ClearAllEffects()
        {
            foreach (var ps in _prefabs.Values)
            {
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}