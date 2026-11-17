using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Efeitos de partículas leves (texturas do Kenney Particle Pack, CC0): poeira de chute, de passada, de carrinho
    /// e do mergulho do goleiro, tufo de grama no chute forte e estrelas na comemoração.
    /// Criado pelo <see cref="ArenaBootstrap"/> com materiais URP próprios (ligados pelo setup do editor):
    /// sem material o URP desenharia as partículas em rosa. Cada efeito tem um pequeno pool reaproveitado
    /// (nada de Instantiate/Destroy a cada passo) e a poeira some com o gramado molhado.
    /// </summary>
    public class ParticleEffectsManager : MonoBehaviour
    {
        public static ParticleEffectsManager Instance { get; private set; }

        /// <summary>Cada efeito: um sistema "molde" e cópias reaproveitadas em rodízio.</summary>
        class Fx
        {
            public ParticleSystem[] pool;
            public int next;
        }

        readonly Dictionary<string, Fx> fx = new Dictionary<string, Fx>();
        Material dust, smoke, star;

        /// <summary>Cria o gerenciador. Os materiais vêm do setup (Partículas/URP); sem eles os efeitos ficam desligados.</summary>
        public static ParticleEffectsManager Create(Transform parent, Material dustMat, Material smokeMat, Material starMat)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("EfeitosParticulas");
            if (parent != null) go.transform.SetParent(parent, false);
            var m = go.AddComponent<ParticleEffectsManager>();
            m.dust = dustMat; m.smoke = smokeMat; m.star = starMat;
            m.Build();
            return m;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Build()
        {
            if (dust != null)
            {
                Add("KickDust", 6, dust, 0.35f, new Vector2(0.25f, 0.55f), new Vector2(1.2f, 3.2f), new Vector2(0.12f, 0.3f),
                    new Color(0.55f, 0.47f, 0.36f, 0.55f), 0.4f, 8, 14, ParticleSystemShapeType.Cone, 35f, 0.08f);
                Add("FootstepDust", 10, dust, 0.2f, new Vector2(0.25f, 0.45f), new Vector2(0.3f, 0.9f), new Vector2(0.1f, 0.22f),
                    new Color(0.55f, 0.48f, 0.38f, 0.35f), 0.2f, 2, 4, ParticleSystemShapeType.Hemisphere, 0f, 0.06f);
                Add("SlideDust", 4, dust, 0.5f, new Vector2(0.4f, 0.9f), new Vector2(0.8f, 2.6f), new Vector2(0.2f, 0.45f),
                    new Color(0.5f, 0.44f, 0.36f, 0.5f), 0.3f, 14, 22, ParticleSystemShapeType.Box, 0f, 0.25f);
                Add("GoalkeeperDive", 3, dust, 0.5f, new Vector2(0.4f, 1f), new Vector2(0.8f, 2.4f), new Vector2(0.25f, 0.5f),
                    new Color(0.5f, 0.45f, 0.37f, 0.5f), 0.3f, 16, 24, ParticleSystemShapeType.Box, 0f, 0.6f);
                // chute muito forte rente ao chão: tufo de grama/terra (antes "queimado na grama")
                Add("GrassScorch", 3, dust, 0.3f, new Vector2(0.35f, 0.7f), new Vector2(2f, 4.5f), new Vector2(0.05f, 0.12f),
                    new Color(0.3f, 0.42f, 0.18f, 0.9f), 1.6f, 8, 12, ParticleSystemShapeType.Cone, 25f, 0.05f);
            }
            if (smoke != null)
                Add("KickImpact", 4, smoke, 0.2f, new Vector2(0.15f, 0.3f), new Vector2(0.5f, 1.5f), new Vector2(0.15f, 0.3f),
                    new Color(1f, 1f, 1f, 0.25f), 0f, 3, 5, ParticleSystemShapeType.Sphere, 0f, 0.08f);
            if (star != null)
                Add("GoalCelebration", 2, star, 1f, new Vector2(1.2f, 2.2f), new Vector2(3f, 7f), new Vector2(0.15f, 0.35f),
                    new Color(1f, 0.9f, 0.4f, 1f), 0.6f, 40, 60, ParticleSystemShapeType.Hemisphere, 0f, 0.8f);
        }

        void Add(string key, int count, Material mat, float duration, Vector2 life, Vector2 speed, Vector2 size, Color color,
                 float gravity, int burstMin, int burstMax, ParticleSystemShapeType shapeType, float angle, float radius)
        {
            var f = new Fx { pool = new ParticleSystem[count] };
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(key + "_" + i);
                go.transform.SetParent(transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = duration;
                main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
                main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = color;
                main.gravityModifier = gravity;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = burstMax * 2;

                var em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstMin, (short)burstMax) });

                var sh = ps.shape;
                sh.shapeType = shapeType;
                sh.angle = angle;
                sh.radius = radius;
                if (shapeType == ParticleSystemShapeType.Box) sh.scale = new Vector3(radius * 2f, 0.05f, radius);

                // some suavemente e cresce um pouco (poeira se espalhando)
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.4f));

                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = mat;
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                f.pool[i] = ps;
            }
            fx[key] = f;
        }

        /// <summary>Chance de sair poeira: some com o gramado encharcado e diminui na qualidade gráfica baixa.</summary>
        static float DustFactor()
        {
            var w = WeatherSystem.Instance;
            float dry = w != null ? Mathf.Clamp01(1f - w.Wetness * 1.6f) : 1f;
            return dry * GameSettings.Particulas; // qualidade gráfica: menos poeira no celular fraco
        }

        /// <param name="chance">0..1: com o gramado molhado a poeira sai menos vezes (e some quando encharcado).</param>
        ParticleSystem Play(string key, Vector3 position, Vector3 direction, float speedMul = 1f, float sizeMul = 1f, float chance = 1f)
        {
            Fx f;
            if (!fx.TryGetValue(key, out f) || chance < 0.05f) return null;
            if (chance < 1f && Random.value > chance) return null;
            var ps = f.pool[f.next];
            f.next = (f.next + 1) % f.pool.Length;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.position = position;
            Vector3 d = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.up;
            ps.transform.rotation = Quaternion.LookRotation(d, Mathf.Abs(Vector3.Dot(d, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up);
            var main = ps.main;
            main.startSpeedMultiplier = Mathf.Max(0.05f, speedMul);
            main.startSizeMultiplier = Mathf.Max(0.05f, sizeMul);
            ps.Play();
            return ps;
        }

        // =====================================================================
        // API usada pelos jogadores, goleiro e partida
        // =====================================================================

        /// <summary>Poeira ao chutar a bola.</summary>
        public void PlayKickDust(Vector3 position, Vector3 direction, float intensity = 1f)
        {
            Vector3 d = direction; d.y = 0f;
            Play("KickDust", position, d + Vector3.up * 0.8f, 0.6f + intensity * 0.6f, 0.8f + intensity * 0.4f, DustFactor());
        }

        /// <summary>Pequena nuvem no ponto de contato com a bola (só em chutes fortes).</summary>
        public void PlayKickImpact(Vector3 position, Vector3 direction, float power = 1f)
        {
            if (power < 0.55f) return;
            Play("KickImpact", position, direction, 0.5f + power * 0.5f, 0.8f + power * 0.4f);
        }

        /// <summary>Sem rastro na bola (o efeito antigo ficava tocando no centro do campo).</summary>
        public ParticleSystem GetBallTrailSystem() { return null; }

        /// <summary>Poeira ao deslizar/carrinho.</summary>
        public void PlaySlideDust(Vector3 position, Vector3 direction, float speed)
        {
            Play("SlideDust", position, direction + Vector3.up * 0.5f, Mathf.Lerp(0.5f, 1.4f, Mathf.Clamp01(speed / 10f)), 1f, DustFactor());
        }

        /// <summary>Estrelas douradas na comemoração do gol.</summary>
        public void PlayGoalCelebration(Vector3 position)
        {
            Play("GoalCelebration", position + Vector3.up, Vector3.up);
        }

        /// <summary>Poeira no mergulho do goleiro.</summary>
        public void PlayGoalkeeperDive(Vector3 position, Vector3 direction, float intensity = 1f)
        {
            Vector3 p = position; p.y = 0.05f;
            Play("GoalkeeperDive", p, direction + Vector3.up * 0.6f, 0.5f + intensity * 0.7f, 0.7f + intensity * 0.5f, DustFactor());
        }

        /// <summary>Pequena poeira na passada (só correndo; só perto da câmera para poupar o celular).</summary>
        public void PlayFootstepDust(Vector3 position, float speed)
        {
            if (speed < 4f || GameSettings.Qualidade == QualidadeGrafica.Baixa) return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - position).sqrMagnitude > 35f * 35f) return;
            Vector3 p = position; p.y = 0.03f;
            Play("FootstepDust", p, Vector3.up, Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(speed / 8f)), 1f, DustFactor() * 0.8f);
        }

        /// <summary>Tufo de grama no chute muito forte rente ao chão.</summary>
        public void PlayGrassScorch(Vector3 position)
        {
            Vector3 p = position; p.y = 0.03f;
            Play("GrassScorch", p, Vector3.up);
        }

        /// <summary>Para e limpa todos os efeitos (troca de modo, reinício).</summary>
        public void ClearAllEffects()
        {
            foreach (var f in fx.Values)
                foreach (var ps in f.pool)
                    if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
