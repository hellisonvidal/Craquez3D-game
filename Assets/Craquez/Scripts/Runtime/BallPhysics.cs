using System;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Física realista da bola (FIFA tamanho 5: 22 cm, 430 g):
    /// - arrasto aerodinâmico com "crise de arrasto" (Cd cai de ~0,47 para ~0,22 acima de ~12 m/s);
    /// - efeito Magnus (curva) proporcional a ω × v;
    /// - vento (ar relativo) com rajadas;
    /// - resistência ao rolamento dependente do gramado (seco/molhado);
    /// - poças d'água que freiam a bola fortemente (podem pará-la por completo);
    /// - rede do gol que absorve a bola.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class BallPhysics : MonoBehaviour
    {
        public static BallPhysics Instance { get; private set; }

        public const float AirDensity = 1.225f;
        public static readonly float Area = Mathf.PI * Arena.BallRadius * Arena.BallRadius;
        public const float MagnusK = 0.0021f;

        public struct ShotInfo
        {
            public Vector3 origin;
            public Vector3 velocity;
            public Vector3 spin;
            public float time;
        }

        /// <summary>Disparado sempre que a bola é chutada.</summary>
        public static event Action<ShotInfo> OnShot;
        /// <summary>Disparado quando a bola bate em algo com força (posição, velocidade de impacto, collider).</summary>
        public event Action<Vector3, float, Collider> OnImpact;

        public Rigidbody Body { get; private set; }
        public SphereCollider Sphere { get; private set; }
        public float LastKickTime { get; private set; } = -10f;
        public bool IsHeld { get { return holder != null; } }
        public Transform Holder { get { return holder; } }
        public Vector3 Velocity { get { return Body.isKinematic ? Vector3.zero : Compat.GetVelocity(Body); } }
        public bool Grounded { get { return transform.position.y <= Arena.BallRadius + 0.025f; } }
        public bool InPuddle { get; private set; }

        Transform holder;
        Vector3 holderOffset;
        float splashCooldown;
        float bounceTarget = 0.62f;
        float knuckleSeed;

        // Particle & Audio integration
        ParticleSystem ballTrail;
        bool trailActive;
        float lastImpactTime;
        float lastBounceTime;

        void Awake()
        {
            Instance = this;
            Arena.ConfigurePhysicsLayers();
            gameObject.layer = Arena.LayerBall;
            Body = GetComponent<Rigidbody>();
            Sphere = GetComponent<SphereCollider>();
            Body.mass = Arena.BallMass;
            Body.useGravity = true;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 200f;
            Compat.SetDamping(Body, 0f, 0.02f);
            Sphere.radius = Arena.BallRadius;
            Compat.SetPhysicsMaterial(Sphere, "Bola", 0.45f, 0.55f, bounceTarget, true);

            // Initialize ball trail from ParticleEffectsManager
            if (ParticleEffectsManager.Instance != null)
            {
                ballTrail = ParticleEffectsManager.Instance.GetBallTrailSystem();
                if (ballTrail != null)
                {
                    ballTrail.transform.SetParent(transform);
                    ballTrail.transform.localPosition = Vector3.zero;
                    ballTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    trailActive = false;
                }
            }
        }

        public void ApplyWeather(float bounciness)
        {
            bounceTarget = bounciness;
            Compat.SetBounciness(Sphere, bounciness);
        }

        // ---------------------------------------------------------------- controle

        public void PlaceAt(Vector3 pos)
        {
            Release();
            Body.isKinematic = false;
            Body.position = pos;
            transform.position = pos;
            Compat.SetVelocity(Body, Vector3.zero);
            Body.angularVelocity = Vector3.zero;
            InPuddle = false;
        }

        public void Kick(Vector3 velocity, Vector3 spin)
        {
            Release();
            Body.isKinematic = false;
            Compat.SetVelocity(Body, velocity);
            Body.angularVelocity = spin;
            LastKickTime = Time.time;
            knuckleSeed = UnityEngine.Random.value * 50f;
            if (OnShot != null)
                OnShot(new ShotInfo { origin = Body.position, velocity = velocity, spin = spin, time = Time.time });
        }

        /// <summary>Desvio (espalmada do goleiro) — não conta como chute.</summary>
        public void Deflect(Vector3 velocity, Vector3 spin)
        {
            Release();
            Body.isKinematic = false;
            Compat.SetVelocity(Body, velocity);
            Body.angularVelocity = spin;
        }

        public void Hold(Transform t, Vector3 localOffset)
        {
            holder = t;
            holderOffset = localOffset;
            Compat.SetVelocity(Body, Vector3.zero);
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;

            // Stop ball trail when held
            if (ballTrail != null && trailActive)
            {
                ballTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                trailActive = false;
            }
        }

        public void HoldAtSocket(Transform socketTransform)
        {
            Hold(socketTransform, Vector3.zero);
        }

        public void Release()
        {
            if (holder == null) return;
            holder = null;
            Body.isKinematic = false;

            // Start ball trail when released with velocity
            if (ballTrail != null && !trailActive)
            {
                Vector3 v = Compat.GetVelocity(Body);
                if (v.sqrMagnitude > 0.5f)
                {
                    ballTrail.Play();
                    trailActive = true;
                }
            }
        }

        void LateUpdate()
        {
            if (holder != null)
            {
                Vector3 p = holder.TransformPoint(holderOffset);
                Body.position = p;
                transform.position = p;

                // Sync ball trail position when held (trail should be stopped but positioned)
                if (ballTrail != null)
                {
                    ballTrail.transform.position = transform.position;
                }
            }
            else if (ballTrail != null && trailActive)
            {
                // Sync ball trail position when free and active
                ballTrail.transform.position = transform.position;

                // Auto-stop trail when ball is nearly stationary
                Vector3 v = Compat.GetVelocity(Body);
                if (v.sqrMagnitude < 0.25f)
                {
                    ballTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    trailActive = false;
                }
            }
        }

        // ---------------------------------------------------------------- forças

        void FixedUpdate()
        {
            if (Body.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            Vector3 v = Compat.GetVelocity(Body);
            Vector3 w = Body.angularVelocity;
            bool grounded = Grounded;

            WeatherSystem ws = WeatherSystem.Instance;
            Vector3 wind = ws != null ? ws.Wind : Vector3.zero;

            // --- aerodinâmica (+ "bola sem efeito": chutes fortes quase sem rotação oscilam no ar)
            Vector3 vr = v - wind * (grounded ? 0.3f : 1f);
            Vector3 f = AeroForce(vr, w, grounded);
            float spd = vr.magnitude;
            if (!grounded && spd > 19f && w.magnitude < 6f)
            {
                float t = Time.time * 2.3f;
                Vector3 side = Vector3.Cross(vr / spd, Vector3.up).normalized;
                float k = (Mathf.PerlinNoise(knuckleSeed, t) - 0.5f) * 2f;
                float u = (Mathf.PerlinNoise(knuckleSeed + 3.1f, t) - 0.5f) * 2f;
                f += (side * k + Vector3.up * u * 0.6f) * Arena.BallMass * 3.2f * Mathf.InverseLerp(19f, 32f, spd);
            }
            Body.AddForce(f, ForceMode.Force);

            // --- superfície sob a bola
            PuddleField pf = PuddleField.Instance;
            float depth = 0f;
            Superficie surf = pf != null ? pf.SurfaceAt(Body.position, out depth) : Superficie.Grama;
            float wet = ws != null && ws.P != null ? ws.P.wetness : 0f;

            // --- quique (resolvido aqui; o collider do chão não tem atrito nem restituição)
            if (v.y < -0.35f && Body.position.y + v.y * dt <= Arena.BallRadius + 0.004f)
            {
                v = Bounce(v, ref w, surf, depth, wet);
                Compat.SetVelocity(Body, v);
                Body.angularVelocity = w;
                grounded = true;
            }

            InPuddle = surf == Superficie.Poca && grounded;
            if (grounded && Mathf.Abs(v.y) < 1.2f)
            {
                Vector3 vh = new Vector3(v.x, 0f, v.z);
                float sp = vh.magnitude;
                float decel = ws != null ? ws.RollingDeceleration : 1.1f;
                float perMeter = 0f;
                if (surf == Superficie.Encharcado)
                {
                    decel *= Mathf.Lerp(0.6f, 0.4f, depth);   // película d'água: a bola desliza mais
                }
                else if (surf == Superficie.Poca)
                {
                    decel += 3.5f * depth;
                    perMeter = 1.7f * depth;                   // v(x) ≈ v0·e^(-k·x): a água "segura" a bola
                    w *= Mathf.Exp(-6f * dt);
                    splashCooldown -= dt;
                    if (sp > 0.8f && splashCooldown <= 0f && ws != null)
                    {
                        ws.Splash(Body.position, Mathf.Clamp01(sp / 12f));
                        splashCooldown = Mathf.Lerp(0.25f, 0.07f, Mathf.Clamp01(sp / 15f));
                    }
                }

                if (sp > 1e-4f)
                {
                    // atrito de deslizamento até atingir rolamento puro (bola com efeito "morde" o gramado)
                    float mu = SlideFriction(surf, wet);
                    Vector3 rollW = Vector3.Cross(Vector3.up, vh) / Arena.BallRadius;
                    Vector3 contactV = vh + Vector3.Cross(w, -Vector3.up * Arena.BallRadius);
                    float slip = contactV.magnitude;
                    if (slip > 0.05f)
                    {
                        float dv = Mathf.Min(mu * 9.81f * dt, slip * 0.4f);
                        Vector3 d = contactV / slip;
                        vh -= d * dv;
                        w += Vector3.Cross(-Vector3.up * Arena.BallRadius, -d * dv) * 1.5f / (Arena.BallRadius * Arena.BallRadius);
                    }
                    else w = Vector3.Lerp(w, rollW, 1f - Mathf.Exp(-12f * dt));

                    sp = vh.magnitude;
                    float newSp = Mathf.Max(0f, sp - (decel + perMeter * sp * sp) * dt);
                    Vector3 nh = sp > 1e-5f ? vh * (newSp / sp) : Vector3.zero;
                    v = new Vector3(nh.x, v.y, nh.z);
                    Compat.SetVelocity(Body, v);
                    Body.angularVelocity = w;
                    if (newSp < 0.03f && Mathf.Abs(v.y) < 0.05f)
                    {
                        Compat.SetVelocity(Body, new Vector3(0f, v.y, 0f));
                        Body.angularVelocity = Vector3.zero;
                    }
                }
            }
            else if (!grounded)
            {
                Body.angularVelocity = w * Mathf.Exp(-0.05f * dt);
            }

            GoalNet.ConstrainAll(this, dt);
        }


        /// <summary>Atrito de deslizamento bola–superfície.</summary>
        static float SlideFriction(Superficie s, float wet)
        {
            switch (s)
            {
                case Superficie.Encharcado: return 0.1f;
                case Superficie.Poca: return 0.45f;
                default: return Mathf.Lerp(0.55f, 0.32f, wet);
            }
        }

        /// <summary>
        /// Quique realista de uma bola oca (I = 2/3·m·R²) no gramado:
        /// restituição cai com a velocidade de impacto e com a umidade; o atrito no contato troca
        /// velocidade por rotação até o rolamento (backspin freia/"segura", topspin acelera).
        /// No encharcado a película d'água reduz o atrito e a bola sai mais rápida (derrapa).
        /// </summary>
        Vector3 Bounce(Vector3 v, ref Vector3 w, Superficie s, float depth, float wet)
        {
            float vn = -v.y;
            float e = Mathf.Lerp(0.7f, 0.5f, Mathf.Clamp01(vn / 14f));
            e *= Mathf.Lerp(1f, 0.86f, wet) * (bounceTarget / 0.62f);
            float mu = SlideFriction(s, wet);
            if (s == Superficie.Encharcado) e *= 0.85f;
            if (s == Superficie.Poca) { e = Mathf.Lerp(0.35f, 0.08f, depth); }

            Vector3 vt = new Vector3(v.x, 0f, v.z);
            Vector3 r = -Vector3.up * Arena.BallRadius;
            Vector3 vc = vt + Vector3.Cross(w, r);
            float jn = (1f + e) * vn;
            float slip = vc.magnitude;
            if (slip > 1e-4f)
            {
                float jt = Mathf.Min(mu * jn, 0.4f * slip);     // 0,4 = 1/(1 + m·R²/I) para casca esférica
                Vector3 d = vc / slip;
                vt -= d * jt;
                w += Vector3.Cross(r, -d * jt) * 1.5f / (Arena.BallRadius * Arena.BallRadius);
            }
            if (s == Superficie.Encharcado) vt *= 1f + 0.07f * Mathf.Clamp01(vn / 6f); // aquaplanagem: sai "voando" rasteira

            var ws = WeatherSystem.Instance;
            if (s == Superficie.Poca && ws != null && vn > 1.5f) ws.Splash(Body.position, Mathf.Clamp01(vn / 8f));
            if (OnImpact != null && vn > 1.5f) OnImpact(Body.position - Vector3.up * Arena.BallRadius, vn, null);
            return vt + Vector3.up * (e * vn);
        }

        /// <summary>Força aerodinâmica total (arrasto + Magnus) para ar relativo vr e rotação w.</summary>
        public static Vector3 AeroForce(Vector3 vr, Vector3 w, bool grounded)
        {
            float s = vr.magnitude;
            if (s < 0.01f) return Vector3.zero;
            float t = Mathf.Clamp01((s - 9f) / 8f);
            float cd = Mathf.Lerp(0.47f, 0.22f, t * t * (3f - 2f * t));
            Vector3 drag = -0.5f * AirDensity * cd * Area * s * vr;
            Vector3 magnus = grounded ? Vector3.zero : MagnusK * Vector3.Cross(w, vr);
            // limita o Magnus (coeficiente de sustentação real satura ~0,35)
            float maxLift = 0.5f * AirDensity * Area * 0.35f * s * s;
            if (magnus.magnitude > maxLift) magnus = magnus.normalized * maxLift;
            return drag + magnus;
        }

        /// <summary>
        /// Simula a trajetória (sem poças) e retorna o ponto/tempo onde a bola cruza o plano x = planeX.
        /// Usado pela mira do jogador e pela leitura do goleiro.
        /// </summary>
        public static bool PredictPlaneCrossing(Vector3 p, Vector3 v, Vector3 w, Vector3 wind, float planeX, float maxTime,
                                                out Vector3 hit, out float time)
        {
            const float dt = 0.01f;
            float g = Physics.gravity.y;
            float m = Arena.BallMass;
            int steps = Mathf.CeilToInt(maxTime / dt);
            float dir = Mathf.Sign(planeX - p.x);
            for (int i = 0; i < steps; i++)
            {
                bool grounded = p.y <= Arena.BallRadius + 0.02f;
                Vector3 vr = v - wind * (grounded ? 0.3f : 1f);
                Vector3 a = AeroForce(vr, w, grounded) / m;
                a.y += g;
                v += a * dt;
                Vector3 np = p + v * dt;
                if (np.y < Arena.BallRadius)
                {
                    np.y = Arena.BallRadius;
                    if (v.y < -1f) v.y = -v.y * 0.55f; else v.y = 0f;
                    v.x *= 0.92f; v.z *= 0.92f;
                }
                if (Mathf.Sign(planeX - np.x) != dir)
                {
                    float k = (planeX - p.x) / (np.x - p.x);
                    hit = Vector3.Lerp(p, np, k);
                    time = (i + k) * dt;
                    return true;
                }
                w *= 0.9995f;
                p = np;
                if (v.sqrMagnitude < 0.01f) break;
            }
            hit = p;
            time = maxTime;
            return false;
        }

        void OnCollisionEnter(Collision c)
        {
            float speed = c.relativeVelocity.magnitude;
            if (speed < 1.5f) return;
            Vector3 point = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            GoalNetPanel panel = c.collider.GetComponent<GoalNetPanel>();
            if (panel != null && panel.net != null) panel.net.Bulge(point, c.relativeVelocity);
            if (OnImpact != null) OnImpact(point, speed, c.collider);

            // Particle & Audio: Impact effects
            if (Time.time - lastImpactTime > 0.1f) // Throttle impact effects
            {
                lastImpactTime = Time.time;

                bool isGround = c.collider.CompareTag("Ground") || c.collider.CompareTag("Pitch");
                bool isGoalNet = panel != null;
                bool isPlayer = c.collider.GetComponent<PlayerController>() != null || c.collider.GetComponent<GoalkeeperAI>() != null;
                bool isPost = c.collider.CompareTag("GoalPost") || c.collider.CompareTag("GoalCrossbar");

                // Particles
                if (ParticleEffectsManager.Instance != null)
                {
                    // Impact particles at collision point
                    Vector3 impactDir = c.relativeVelocity.normalized;
                    ParticleEffectsManager.Instance.PlayKickImpact(point, impactDir, Mathf.Clamp01(speed / 25f));

                    // Grass scorch for high-speed ground impacts
                    if (isGround && speed > 20f && point.y < 0.5f)
                        ParticleEffectsManager.Instance.PlayGrassScorch(point);
                }

                // Audio
                if (AudioManager.Instance != null)
                {
                    if (isGoalNet)
                    {
                        AudioManager.Instance.PlayNetHit(point, Mathf.Clamp01(speed / 30f));
                    }
                    else if (isGround && Grounded)
                    {
                        AudioManager.Instance.PlayBallBounce(point, speed);
                    }
                    else
                    {
                        AudioManager.Instance.PlayImpact(point, Mathf.Clamp01(speed / 25f), isPost);
                    }
                }
            }
        }
    }
}
