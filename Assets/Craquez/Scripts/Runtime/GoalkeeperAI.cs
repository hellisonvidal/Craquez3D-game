using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Craquez
{
    /// <summary>
    /// Sistema Completo de Física, Posicionamento, Motion Warping e Defesa dos Goleiros (GK System):
    /// - Camada 1: Root com CapsuleCollider na Layer PlayerBody (ignora colisão direta com a Bola para não rebater de forma artificial;
    ///   ajusta a cápsula horizontalmente durante o voo para não flutuar em pé nem afundar no chão).
    /// - Camada 2 & 3: Rig Mixamo com MixamoBoneSockets (`Foot_R`, `Foot_L`, `Hand_R`, `Hand_L`, `Chest`, `GK_CatchSocket`).
    /// - Posicionamento em Arco (GK_Alert): Strafe/Sidestep sobre a bissetriz entre o centro do gol e a bola, encarando a bola.
    /// - Predição Matemática do Ponto de Interceptação (`TimeToImpact`, `DeltaX`, `HeightY`).
    /// - Seleção Automática de Animação Mixamo por Quadrante:
    ///     * Perto do corpo (`|DeltaX| < 0.6m`): `GK_Catch` (peito/mãos) ou `GK_FootSave` (rasteira).
    ///     * Distância média/longa (`0.6m <= |DeltaX| <= 2.8m`): `Dive_Low` (`Y < 0.6m`), `Dive_Mid` (`0.6m <= Y <= 1.4m`) ou `Dive_High` (`Y > 1.4m`).
    /// - Física do Salto (Dive Impulse + Motion Warping): Impulso controlado na janela de propulsão (`0.10` a `0.45` do tempo normalizado).
    /// - Resolução da Defesa (Encaixe no `GK_CatchSocket` vs. Espalmada direcionada para as laterais/linha de fundo com redução de 50%–70% da energia cinética).
    /// </summary>
    public class GoalkeeperAI : MonoBehaviour
    {
        public enum Estado
        {
            Posicionando = 0, // GK_Alert (Strafe/Sidestep sobre o arco da bissetriz)
            Defendendo = 1,   // GK_Diving / GK_Saving (Janela ativa de salto e interceptação)
            Recuperando = 2,  // GK_GettingUp (Retorno à base em pé)
            ComBola = 3       // GK_HoldingBall (Bola encaixada no GK_CatchSocket)
        }

        public enum QuadranteDefesa
        {
            Nenhum = 0,
            GK_Catch,
            GK_FootSave,
            Dive_Low,
            Dive_Mid,
            Dive_High
        }

        public BallPhysics ball;

        [Header("Lado do campo")]
        [Tooltip("+1 = defende o gol em +X (treino); -1 = defende o gol em -X.")]
        public float side = 1f;
        [Tooltip("Distância do gol em que sai para pegar bola solta e lenta.")]
        public float sweepRange = 9.2f;
        /// <summary>Partida: distância do adversário mais próximo de um ponto (para decidir sair do gol).</summary>
        [NonSerialized] public Func<Vector3, float> opponentDistance;
        /// <summary>Partida: companheiro para quem repor a bola (null = direção aleatória para o campo).</summary>
        [NonSerialized] public Func<Vector3?> distributionTarget;
        public Vector3 Velocity { get { return velocity; } }
        public bool HoldingBall { get { return State == Estado.ComBola && ball != null && ball.IsHeld; } }
        Vector3 Out { get { return new Vector3(-Mathf.Sign(side), 0f, 0f); } }

        [Header("Física de Locomoção e Arco (GK_Alert)")]
        public float maxShuffleSpeed = 4.5f;
        public float shuffleAcceleration = 18f;
        public float shuffleDeceleration = 25f;

        [Header("Física do Salto (Dive Impulse + Motion Warping)")]
        public float maxDiveTravel = 2.8f;      // Alcance lateral máximo (0.6m <= |DeltaX| <= 2.8m)
        public float maxDiveSpeed = 7.4f;       // Explosão/velocidade máxima no impulso (m/s)
        [Tooltip("Raio da Esfera de Interceptação das Luvas centrada no ponto médio entre Hand_L e Hand_R.")]
        public float handReach = 0.28f;

        [Header("Controle Humano (Modo Goleiro)")]
        public bool humanControlled;
        public Transform cameraTransform;
        bool prevDive, prevJump, prevBlock;
        bool shotInFlight;

        /// <summary>true = encaixou (GK_HoldingBall), false = espalmou/rebateu para lateral ou linha de fundo.</summary>
        public event Action<bool> OnSave;

        public Estado State { get; private set; }
        public QuadranteDefesa ActiveQuadrant { get; private set; }
        public MixamoBoneSockets Sockets { get { return sockets; } }

        Animator anim;
        MixamoBoneSockets sockets;
        CapsuleCollider bodyCapsule;
        Rigidbody rootBody;
        FootGrounder grounder;

        Transform lHand, rHand, lFore, rFore, chest, hips, lFoot, rFoot;
        Vector3 velocity;
        float reactAt = -1f;
        float stateTime;
        bool resolved;
        float ikWeight;
        float recoverTime = 0.6f;
        Vector3 ikTarget;

        // Dados de Predição e Motion Warping do Salto
        Vector3 diveFrom, diveTo, predictedHitPoint;
        float timeToImpact;
        float diveStart, diveDuration, diveHeight;
        bool diving;
        float diveSide; // +1 = Direita do goleiro, -1 = Esquerda do goleiro
        float gkSplashTimer;
        bool gkSplashLeft;
        float lastFootstepTime;

        static readonly int LateralHash = Animator.StringToHash("Lateral");
        static readonly int ForwardHash = Animator.StringToHash("Forward");
        static readonly int MirrorHash = Animator.StringToHash("Mirror");
        static readonly int DiveSpeedHash = Animator.StringToHash("DiveSpeed");

        Vector3 GoalCenter { get { return new Vector3(Mathf.Sign(side) * Arena.HalfLength, 0f, 0f); } }

        void Awake()
        {
            Arena.ConfigurePhysicsLayers();
            ConfigureLayer1Capsule();

            anim = GetComponentInChildren<Animator>();
            sockets = GetComponent<MixamoBoneSockets>();
            if (sockets == null) sockets = gameObject.AddComponent<MixamoBoneSockets>();
            sockets.InitializeSockets();

            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var relay = anim.GetComponent<IKRelay>();
                if (relay == null) relay = anim.gameObject.AddComponent<IKRelay>();
                relay.onIK = OnIK;

                // Remove quaisquer SphereColliders brutos residuais nos ossos (evita "mãos de alface" e rebotes aleatórios)
                foreach (var c in anim.GetComponentsInChildren<Collider>(true))
                {
                    if (c.gameObject != gameObject) Destroy(c);
                }
            }

            lHand = sockets.Hand_L;
            rHand = sockets.Hand_R;
            lFore = sockets.ForeArm_L;
            rFore = sockets.ForeArm_R;
            chest = sockets.Chest;
            hips = sockets.Hips;
            lFoot = sockets.Toe_L != null ? sockets.Toe_L : sockets.Foot_L;
            rFoot = sockets.Toe_R != null ? sockets.Toe_R : sockets.Foot_R;
        }

        /// <summary>
        /// Camada 1 - Root / Capsule Collider:
        /// Ignora colisão física direta com a Bola (Layer: PlayerBody x Layer: Ball) e ajusta sua orientação durante o voo.
        /// Usa as dimensões do prefab (baseadas na altura real do modelo escalonado).
        /// </summary>
        void ConfigureLayer1Capsule()
        {
            gameObject.layer = Arena.LayerPlayerBody;
            bodyCapsule = GetComponent<CapsuleCollider>();
            if (bodyCapsule == null) bodyCapsule = gameObject.AddComponent<CapsuleCollider>();

            // Usa as dimensões já configuradas no prefab
            if (bodyCapsule.height <= 0.1f || bodyCapsule.center.y <= 0.1f)
            {
                // Fallback: calcula baseado no FootGrounder
                var grounder = GetComponent<FootGrounder>();
                float h = grounder != null ? grounder.height : 1.90f;
                bodyCapsule.direction = 1;
                bodyCapsule.height = h;
                bodyCapsule.center = new Vector3(0f, h * 0.5f, 0f);
                bodyCapsule.radius = 0.25f;
            }

            // Guarda as dimensões originais em pé para restaurar depois do mergulho
            originalCapsuleHeight = bodyCapsule.height;
            originalCapsuleCenter = bodyCapsule.center;
            originalCapsuleRadius = bodyCapsule.radius;

            rootBody = GetComponent<Rigidbody>();
            if (rootBody == null) rootBody = gameObject.AddComponent<Rigidbody>();
            rootBody.isKinematic = true;
            rootBody.useGravity = false;
            rootBody.interpolation = RigidbodyInterpolation.None;

            if (ball != null && ball.Sphere != null)
                Physics.IgnoreCollision(bodyCapsule, ball.Sphere, true);
        }

        float originalCapsuleHeight = 1.90f;
        float originalCapsuleRadius = 0.25f;
        Vector3 originalCapsuleCenter = new Vector3(0f, 0.95f, 0f);

        /// <summary>
        /// Alterna a cápsula entre vertical (em pé) e horizontalizada durante a ponte (mergulho)
        /// para que o corpo horizontalizado não flutue em pé nem afunde no chão.
        /// Usa as dimensões originais do prefab como base.
        /// </summary>
        void SetCapsuleStanding(bool standing)
        {
            if (bodyCapsule == null) return;
            if (standing)
            {
                bodyCapsule.direction = 1; // Vertical (Y)
                bodyCapsule.radius = originalCapsuleRadius;
                bodyCapsule.height = originalCapsuleHeight;
                bodyCapsule.center = originalCapsuleCenter;
            }
            else
            {
                bodyCapsule.direction = 0; // Horizontal (X - eixo lateral do mergulho)
                // Cápsula horizontalizada: altura = diâmetro original, centro ajustado para a altura do quadril durante o mergulho
                bodyCapsule.radius = originalCapsuleRadius * 0.95f;
                bodyCapsule.height = originalCapsuleHeight * 0.92f; // Ligeiramente menor para não afundar
                bodyCapsule.center = new Vector3(0f, originalCapsuleCenter.y * 0.48f, 0f); // Centro mais baixo durante o mergulho
            }
        }

        void OnEnable() { BallPhysics.OnShot += HandleShot; }
        void OnDisable() { BallPhysics.OnShot -= HandleShot; }

        public void ResetTo(Vector3 pos)
        {
            pos.y = 0f;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(Out);
            velocity = Vector3.zero;
            State = Estado.Posicionando;
            ActiveQuadrant = QuadranteDefesa.Nenhum;
            reactAt = -1f;
            shotInFlight = false;
            diving = false;
            resolved = false;
            repo = Reposicao.Nenhuma;
            retrieveAt = -1f;
            ikWeight = 0f;
            SetCapsuleStanding(true);
            if (sockets != null)
            {
                sockets.lockHipsHorizontalInPlace = true;
                sockets.UpdateSockets(0f);
            }
            if (anim != null)
            {
                anim.SetBool(MirrorHash, false);
                anim.Play("Locomocao", 0, 0f);
            }
        }

        // ------------------------------------------------------------ Predição da Trajetória e Seleção de Quadrante

        void HandleShot(BallPhysics.ShotInfo info)
        {
            shotInFlight = true;
            if (State == Estado.ComBola || humanControlled) return;
            var ws = WeatherSystem.Instance;
            Vector3 wind = ws != null ? ws.Wind : Vector3.zero;
            Vector3 hit; float t;
            float planeX = transform.position.x;
            if (!BallPhysics.PredictPlaneCrossing(info.origin, info.velocity, info.spin, wind, planeX, 3.5f, out hit, out t)) return;
            if (Mathf.Abs(hit.z) > Arena.GoalHalfWidth + 2.5f || hit.y > Arena.GoalHeight + 1.5f) return;

            float reaction = Random.Range(0.14f, 0.22f);
            if (ws != null && ws.P != null) reaction += ws.P.gkReaction * 0.75f;
            float absDx = Mathf.Abs(Vector3.Dot(hit - transform.position, transform.right));
            float waitBeforeDive = (t > 0.68f && absDx >= 0.80f)
                ? Mathf.Max(reaction, t - 0.56f)
                : reaction;
            reactAt = Time.time + Mathf.Min(waitBeforeDive, Mathf.Max(0.06f, t - 0.14f));
            resolved = false;
        }

        /// <summary>
        /// Calcula matematicamente onde e em quantos segundos (TimeToImpact) a bola cruzará o plano de defesa,
        /// e seleciona a animação do Mixamo pelo quadrante (DeltaX e HeightY).
        /// </summary>
        void React()
        {
            reactAt = -1f;
            if (ball == null || ball.IsHeld) return;
            var ws = WeatherSystem.Instance;
            Vector3 wind = ws != null ? ws.Wind : Vector3.zero;
            Vector3 hit; float tImpact;
            Vector3 pos = transform.position;

            if (!BallPhysics.PredictPlaneCrossing(ball.Body.position, ball.Velocity, ball.Body.angularVelocity, wind, pos.x, 3.2f, out hit, out tImpact))
                return;
            if (Mathf.Abs(hit.z) > Arena.GoalHalfWidth + 1.2f || hit.y > Arena.GoalHeight + 0.85f)
                return; // Bola claramente para fora: apenas acompanha

            timeToImpact = Mathf.Max(0.08f, tImpact);
            predictedHitPoint = hit;
            ikTarget = hit;

            // Deslocamento lateral no referencial do goleiro (DeltaX) e altura prevista (HeightY)
            float deltaX = Vector3.Dot(hit - pos, transform.right);
            float absDeltaX = Mathf.Abs(deltaX);
            float heightY = hit.y;
            float side = Mathf.Sign(deltaX == 0f ? 1f : deltaX);

            // --- Seleção Automática de Animação Mixamo por Quadrante:
            // 1. Perto do corpo (|DeltaX| < 0.42m): GK_Catch (peito/mãos) ou GK_FootSave (rasteira)
            if (absDeltaX < 0.42f)
            {
                if (heightY < 0.42f)
                {
                    ActiveQuadrant = QuadranteDefesa.GK_FootSave;
                    StartQuadrantSave(ActiveQuadrant, side, Mathf.Clamp(absDeltaX, 0.1f, 0.45f), Mathf.Clamp(timeToImpact, 0.20f, 0.45f), 0f);
                }
                else if (heightY <= 1.65f)
                {
                    ActiveQuadrant = QuadranteDefesa.GK_Catch;
                    BeginDefend();
                    diving = false;
                    SetCapsuleStanding(true);
                    PlayCatchBlock();
                }
                else
                {
                    ActiveQuadrant = QuadranteDefesa.Dive_High;
                    StartJump(deltaX * 0.65f, Mathf.Clamp(timeToImpact, 0.24f, 0.55f), Mathf.Clamp(heightY - 1.65f, 0.25f, 0.75f));
                }
                return;
            }

            // 2. Distância média/longa (0.6m <= |DeltaX| <= 2.8m+): Dive_Low, Dive_Mid ou Dive_High
            if (heightY < 0.6f)
                ActiveQuadrant = QuadranteDefesa.Dive_Low;
            else if (heightY <= 1.4f)
                ActiveQuadrant = QuadranteDefesa.Dive_Mid;
            else
                ActiveQuadrant = QuadranteDefesa.Dive_High;

            // Bola alta com deslocamento lateral moderado (<= 1.35m): usa salto vertical com impulsão (GK_Salto)
            if (ActiveQuadrant == QuadranteDefesa.Dive_High && absDeltaX <= 1.35f)
            {
                float jumpLateral = Mathf.Clamp(deltaX * 0.78f, -1.1f, 1.1f);
                StartJump(jumpLateral, Mathf.Clamp(timeToImpact, 0.28f, 0.58f), Mathf.Clamp(heightY - 1.85f, 0.08f, 0.48f));
                return;
            }

            // No voo as mãos alcançam ~0.78m além do centro do quadril; o Motion Warping desloca a cápsula até o alcance físico
            float wantedTravel = Mathf.Clamp(absDeltaX - 0.72f, 0.25f, maxDiveTravel);
            float maxReachableInTime = maxDiveSpeed * Mathf.Max(timeToImpact, 0.18f);
            float travel = Mathf.Min(wantedTravel, maxReachableInTime);

            float targetApexY = ActiveQuadrant == QuadranteDefesa.Dive_Low
                ? Mathf.Clamp(heightY * 0.25f, 0.02f, 0.16f)
                : (ActiveQuadrant == QuadranteDefesa.Dive_Mid
                    ? Mathf.Clamp(heightY - 0.55f, 0.16f, 0.58f)
                    : Mathf.Clamp(heightY - 0.65f, 0.48f, 0.92f));

            StartQuadrantSave(ActiveQuadrant, side, travel, Mathf.Clamp(timeToImpact, 0.24f, 0.65f), targetApexY);
        }

        void PlayCatchBlock()
        {
            if (anim != null)
            {
                anim.SetBool(MirrorHash, false);
                anim.CrossFadeInFixedTime("GK_Defesa", 0.06f);
            }
        }

        void BeginDefend()
        {
            State = Estado.Defendendo;
            stateTime = 0f;
            resolved = false;
            if (sockets != null) sockets.lockHipsHorizontalInPlace = false;
        }

        void StartQuadrantSave(QuadranteDefesa quadrant, float side, float travel, float duration, float apexHeight)
        {
            BeginDefend();
            diving = true;
            diveSide = side;
            diveFrom = transform.position;
            diveTo = diveFrom + transform.right * (side * travel);
            // Garante que o goleiro não atravesse as traves durante o Motion Warping
            diveTo.z = Mathf.Clamp(diveTo.z, -Arena.GoalHalfWidth + 0.35f, Arena.GoalHalfWidth - 0.35f);
            diveDuration = Mathf.Max(0.20f, duration);
            diveHeight = apexHeight;
            diveStart = Time.time;

            SetCapsuleStanding(quadrant == QuadranteDefesa.GK_FootSave);

            // Particle: Dive dust
            if (ParticleEffectsManager.Instance != null)
            {
                ParticleEffectsManager.Instance.PlayGoalkeeperDive(diveFrom, transform.right * side, Mathf.Clamp01(travel / maxDiveTravel));
            }

            if (anim != null)
            {
                anim.SetBool(MirrorHash, side < 0f);
                if (quadrant == QuadranteDefesa.GK_FootSave)
                {
                    anim.CrossFadeInFixedTime("GK_Carrinho", 0.05f, 0, 0.02f);
                }
                else
                {
                    // Ajusta a velocidade da animação para que a janela de extensão máxima (0.35..0.45) coincida com TimeToImpact
                    float animSpeed = Mathf.Clamp(0.42f / Mathf.Max(0.16f, duration), 0.85f, 2.1f);
                    anim.SetFloat(DiveSpeedHash, animSpeed);
                    anim.CrossFadeInFixedTime("GK_Mergulho", 0.05f, 0, 0.02f);
                }
            }
        }

        void StartJump(float lateralOffset, float duration, float height)
        {
            BeginDefend();
            diving = true;
            diveSide = Mathf.Sign(lateralOffset == 0f ? 1f : lateralOffset);
            diveFrom = transform.position;
            diveTo = diveFrom + transform.right * lateralOffset;
            diveDuration = Mathf.Max(0.22f, duration);
            diveHeight = height;
            diveStart = Time.time;
            SetCapsuleStanding(true);

            // Particle: Jump/Dive dust
            if (ParticleEffectsManager.Instance != null)
            {
                ParticleEffectsManager.Instance.PlayGoalkeeperDive(diveFrom, transform.right * diveSide, Mathf.Clamp01(Mathf.Abs(lateralOffset) / maxDiveTravel));
            }

            if (anim != null)
            {
                anim.SetBool(MirrorHash, lateralOffset < 0f);
                string jumpState = Mathf.Abs(lateralOffset) < 0.45f ? "GK_Soco" : "GK_Salto";
                anim.CrossFadeInFixedTime(jumpState, 0.07f, 0, 0.05f);
            }
        }

        // ------------------------------------------------------------ Controle Humano (Modo Goleiro)

        Vector3 HumanStickWorld()
        {
            Vector2 mv = InputBridge.Move;
            Vector3 f = Vector3.forward, r = Vector3.right;
            if (cameraTransform != null)
            {
                f = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                r = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }
            return f * mv.y + r * mv.x;
        }

        void UpdateHuman(float dt)
        {
            ikWeight = Mathf.MoveTowards(ikWeight, 0f, dt * 3f);
            Vector3 stick = HumanStickWorld();
            bool dive = InputBridge.Shoot, jump = InputBridge.Chip, block = InputBridge.Finesse;
            bool diveDown = dive && !prevDive, jumpDown = jump && !prevJump, blockDown = block && !prevBlock;
            prevDive = dive; prevJump = jump; prevBlock = block;

            float predictedY = 0.8f;
            float predictedTime = 0.40f;
            if (ball != null && shotInFlight && !ball.IsHeld)
            {
                var ws = WeatherSystem.Instance;
                Vector3 hit; float t;
                if (BallPhysics.PredictPlaneCrossing(ball.Body.position, ball.Velocity, ball.Body.angularVelocity,
                        ws != null ? ws.Wind : Vector3.zero, transform.position.x, 2.5f, out hit, out t))
                {
                    predictedY = hit.y;
                    predictedTime = Mathf.Clamp(t, 0.24f, 0.55f);
                    ikTarget = hit;
                }
            }

            if (diveDown)
            {
                float lat = Vector3.Dot(stick, transform.right);
                if (Mathf.Abs(lat) < 0.22f)
                {
                    ActiveQuadrant = QuadranteDefesa.GK_Catch;
                    BeginDefend();
                    diving = false;
                    PlayCatchBlock();
                    if (ball != null) ikTarget = ball.transform.position;
                }
                else
                {
                    ActiveQuadrant = predictedY < 0.6f ? QuadranteDefesa.Dive_Low : (predictedY <= 1.4f ? QuadranteDefesa.Dive_Mid : QuadranteDefesa.Dive_High);
                    float travel = Mathf.Lerp(0.75f, maxDiveTravel, Mathf.Clamp01(Mathf.Abs(lat)));
                    float apex = Mathf.Clamp(predictedY - 0.5f, 0.1f, 1.05f);
                    StartQuadrantSave(ActiveQuadrant, Mathf.Sign(lat), travel, predictedTime, apex);
                }
                return;
            }
            if (jumpDown)
            {
                ActiveQuadrant = QuadranteDefesa.Dive_High;
                StartJump(Vector3.Dot(stick, transform.right) * 0.7f, 0.42f, 0.55f);
                return;
            }
            if (blockDown)
            {
                ActiveQuadrant = QuadranteDefesa.GK_Catch;
                BeginDefend();
                diving = false;
                PlayCatchBlock();
                if (ball != null) ikTarget = ball.transform.position;
                return;
            }

            float max = InputBridge.Sprint ? 6.2f : maxShuffleSpeed;
            Vector3 desired = stick * max;
            float accel = desired.magnitude < velocity.magnitude ? shuffleDeceleration : shuffleAcceleration;
            velocity = Vector3.MoveTowards(velocity, desired, accel * dt);

            Vector3 p = transform.position + velocity * dt;
            Vector3 g = GoalCenter;
            float sg = Mathf.Sign(side);
            p.x = g.x - sg * Mathf.Clamp((g.x - p.x) * sg, 0.3f, 16.5f);
            p.z = Mathf.Clamp(p.z, -20f, 20f);
            p.y = 0f;

            // Evita sobreposição com outros jogadores
            p = ResolvePlayerCollisions(p);

            transform.position = p;

            // Footstep sounds/particles for human-controlled goalkeeper
            float gkSpeed = velocity.magnitude;
            if (gkSpeed > 1.0f && Time.time - lastFootstepTime > 0.35f)
            {
                float stepInterval = gkSpeed > maxShuffleSpeed * 0.8f ? 0.3f : 0.4f;
                if (Time.time - lastFootstepTime > stepInterval)
                {
                    Vector3 footPos = gkSplashLeft
                        ? (sockets != null ? sockets.LeftFootContactPosition : transform.position - transform.right * 0.14f)
                        : (sockets != null ? sockets.RightFootContactPosition : transform.position + transform.right * 0.14f);

                    if (ParticleEffectsManager.Instance != null)
                        ParticleEffectsManager.Instance.PlayFootstepDust(footPos, gkSpeed);

                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayFootstep(footPos, gkSpeed, gkSpeed > maxShuffleSpeed * 0.8f);

                    lastFootstepTime = Time.time;
                    gkSplashLeft = !gkSplashLeft;
                }
            }

            if (ball != null)
            {
                Vector3 look = ball.transform.position - p; look.y = 0f;
                if (look.sqrMagnitude > 0.01f && Vector3.Dot(look.normalized, Out) > 0.1f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 420f * dt);
            }

            if (ball != null && !ball.IsHeld &&
                Vector3.Distance(ball.transform.position, transform.position + transform.forward * 0.4f) < 0.75f &&
                ball.Velocity.magnitude < 5.5f && ball.transform.position.y < 1.6f)
            {
                Grab();
            }
        }

        // ------------------------------------------------------------ Máquina de Estados Principal

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;

            if (ball != null && ball.Sphere != null && bodyCapsule != null)
                Physics.IgnoreCollision(bodyCapsule, ball.Sphere, true);

            if (reactAt > 0f && Time.time >= reactAt) React();
            UpdateDistribution();

            switch (State)
            {
                case Estado.Posicionando:
                    if (humanControlled) UpdateHuman(dt);
                    else UpdateArcPositioning(dt);
                    break;

                case Estado.Defendendo:
                    UpdateDefending(dt);
                    break;

                case Estado.Recuperando:
                    velocity = Vector3.zero;
                    ikWeight = Mathf.MoveTowards(ikWeight, 0f, dt * 3.5f);
                    if (stateTime > recoverTime)
                    {
                        State = Estado.Posicionando;
                        ActiveQuadrant = QuadranteDefesa.Nenhum;
                        stateTime = 0f;
                        SetCapsuleStanding(true);
                        if (sockets != null) sockets.lockHipsHorizontalInPlace = true;
                        if (anim != null) anim.SetBool(MirrorHash, false);
                    }
                    break;

                case Estado.ComBola:
                    velocity = Vector3.zero;
                    ikWeight = Mathf.MoveTowards(ikWeight, 0f, dt * 6f);
                    if (diving && stateTime > 0.95f && repo == Reposicao.Nenhuma)
                    {
                        diving = false;
                        SetCapsuleStanding(true);
                        if (anim != null) anim.CrossFadeInFixedTime("GK_Levantar", 0.25f);
                    }
                    if (sockets != null) sockets.UpdateSockets(0f);
                    if (ball != null && ball.IsHeld && sockets != null && sockets.GK_CatchSocket != null)
                        ikTarget = sockets.GK_CatchSocket.position;
                    break;
            }

            if (anim != null)
            {
                float latVel = Vector3.Dot(velocity, transform.right);
                float fwdVel = Vector3.Dot(velocity, transform.forward);
                anim.SetFloat(LateralHash, latVel * 1.25f, 0.12f, dt);
                anim.SetFloat(ForwardHash, fwdVel, 0.12f, dt);
            }

            bool ready = State == Estado.Posicionando && !diving;
            if (grounder == null) grounder = GetComponent<FootGrounder>();
            if (grounder != null)
                grounder.grounding = ready || (State == Estado.Defendendo && !diving) || (State == Estado.ComBola && !diving);

            // Não força IK estático nos braços durante o posicionamento normal para preservar GK_Pronto/DeslocarEsq/Dir

            // Passadas e respingos do goleiro em poças d'água
            gkSplashTimer -= dt;
            var pfGk = PuddleField.Instance;
            float gkDepth;
            if (pfGk != null && velocity.magnitude > 0.8f && gkSplashTimer <= 0f && pfGk.SurfaceAt(transform.position, out gkDepth) != Superficie.Grama)
            {
                gkSplashLeft = !gkSplashLeft;
                Vector3 foot = gkSplashLeft
                    ? (sockets != null ? sockets.LeftFootContactPosition : transform.position - transform.right * 0.14f)
                    : (sockets != null ? sockets.RightFootContactPosition : transform.position + transform.right * 0.14f);
                pfGk.FootStep(foot, Mathf.Clamp01(velocity.magnitude / maxShuffleSpeed) * Mathf.Max(0.4f, gkDepth));
                gkSplashTimer = Mathf.Lerp(0.38f, 0.20f, Mathf.Clamp01(velocity.magnitude / maxShuffleSpeed));
            }
        }

        /// <summary>
        /// Posicionamento em Arco (Bissetriz do Gol - GK_Alert):
        /// Move-se em Strafe/Sidestep sobre um arco entre o centro do gol e a bola, encarando sempre a bola.
        /// </summary>
        void UpdateArcPositioning(float dt)
        {
            ikWeight = Mathf.MoveTowards(ikWeight, 0f, dt * 3f);
            if (ball == null) return;

            Vector3 bp = ball.transform.position;
            Vector3 g = GoalCenter;
            Vector3 toBall = bp - g; toBall.y = 0f;
            float dist = toBall.magnitude;

            // Calcula o arco da bissetriz entre o centro do gol e os postes
            Vector3 leftPost = g + new Vector3(0f, 0f, Arena.GoalHalfWidth);
            Vector3 rightPost = g + new Vector3(0f, 0f, -Arena.GoalHalfWidth);
            Vector3 dirCenter = dist > 0.01f ? toBall / dist : Out;
            Vector3 dirBisector = dirCenter;
            if (dist > 1.0f)
            {
                Vector3 toL = (bp - leftPost).normalized;
                Vector3 toR = (bp - rightPost).normalized;
                Vector3 bis = (toL + toR);
                if (bis.sqrMagnitude > 1e-4f) dirBisector = Vector3.Lerp(dirCenter, bis.normalized, 0.5f).normalized;
            }

            Vector3 target;
            float sgn = Mathf.Sign(side);
            if (toBall.x * sgn > -0.5f || dist < 0.5f)
            {
                target = g + new Vector3(-0.55f * sgn, 0f, Mathf.Clamp(bp.z, -2.6f, 2.6f));
            }
            else
            {
                float arcRadius = Mathf.Clamp(dist * 0.115f, 0.65f, 3.65f);
                target = g + dirBisector * arcRadius;
            }
            if ((g.x - target.x) * sgn < 0.42f) target.x = g.x - sgn * 0.42f;
            target.z = Mathf.Clamp(target.z, -Arena.GoalHalfWidth + 0.45f, Arena.GoalHalfWidth - 0.45f);
            target.y = 0f;

            // Saída do gol em bola solta e lenta dentro da pequena/grande área
            bool looseBall = !ball.IsHeld && ball.Grounded && ball.Velocity.magnitude < 4.2f && dist < sweepRange && Time.time - ball.LastKickTime > 0.6f;
            if (looseBall)
            {
                float nearest;
                if (opponentDistance != null) nearest = opponentDistance(bp);
                else { PlayerController pc = FindPlayer(); nearest = pc == null ? 99f : Vector3.Distance(pc.transform.position, bp); }
                // só sai se chegar antes do atacante
                float myTime = Vector3.Distance(transform.position, bp) / 6.5f;
                if (nearest > 2.2f && myTime < nearest / 7.5f + 0.35f)
                {
                    target = new Vector3(bp.x, 0f, bp.z);
                    if (Vector3.Distance(transform.position, target) < 0.85f) { Grab(); return; }
                }
            }

            Vector3 toTarget = target - transform.position; toTarget.y = 0f;
            float maxSpeed = looseBall ? 6.5f : maxShuffleSpeed;
            Vector3 desired = toTarget.magnitude < 0.08f ? Vector3.zero : Vector3.ClampMagnitude(toTarget * 3.2f, maxSpeed);
            float accel = desired.magnitude < velocity.magnitude ? shuffleDeceleration : shuffleAcceleration;
            velocity = Vector3.MoveTowards(velocity, desired, accel * dt);

            Vector3 p = transform.position + velocity * dt;
            p = ResolvePlayerCollisions(p);
            transform.position = p;

            // Footstep sounds/particles for goalkeeper
            float gkSpeed = velocity.magnitude;
            if (gkSpeed > 1.0f && Time.time - lastFootstepTime > 0.35f)
            {
                float stepInterval = gkSpeed > maxShuffleSpeed * 0.8f ? 0.3f : 0.4f;
                if (Time.time - lastFootstepTime > stepInterval)
                {
                    Vector3 footPos = gkSplashLeft
                        ? (sockets != null ? sockets.LeftFootContactPosition : transform.position - transform.right * 0.14f)
                        : (sockets != null ? sockets.RightFootContactPosition : transform.position + transform.right * 0.14f);

                    if (ParticleEffectsManager.Instance != null)
                        ParticleEffectsManager.Instance.PlayFootstepDust(footPos, gkSpeed);

                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayFootstep(footPos, gkSpeed, gkSpeed > maxShuffleSpeed * 0.8f);

                    lastFootstepTime = Time.time;
                    gkSplashLeft = !gkSplashLeft;
                }
            }

            // Mantém a rotação encarando a bola sobre o arco
            Vector3 look = bp - transform.position; look.y = 0f;
            if (look.sqrMagnitude < 0.01f) look = Out;
            if (Vector3.Dot(look.normalized, Out) < 0.1f)
                look = Vector3.Lerp(look.normalized, Out, 0.6f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 420f * dt);
        }

        PlayerController cachedPlayer;
        PlayerController FindPlayer()
        {
            if (cachedPlayer == null) cachedPlayer = FindAnyPlayer();
            return cachedPlayer;
        }

        static PlayerController FindAnyPlayer()
        {
#if UNITY_6000_0_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<PlayerController>();
#elif UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<PlayerController>();
#else
            return UnityEngine.Object.FindObjectOfType<PlayerController>();
#endif
        }

        /// <summary>
        /// Física do Salto (Dive Impulse + Motion Warping):
        /// Aplica impulso controlado na janela ativa de propulsão (`0.10` a `0.45` do tempo normalizado)
        /// rumo ao ponto de interceptação previsto, sem depender apenas do Root Motion do Mixamo.
        /// </summary>
        void UpdateDefending(float dt)
        {
            velocity = Vector3.zero;
            ikWeight = Mathf.MoveTowards(ikWeight, 0f, dt * 8f);

            if (ball != null && !ball.IsHeld)
            {
                float d = Vector3.Distance(ball.transform.position, transform.position);
                if (d < 4.8f)
                {
                    // Combina o ponto futuro de interceptação com a posição instantânea da bola quando muito próxima
                    Vector3 liveTarget = Vector3.Lerp(predictedHitPoint, ball.transform.position, Mathf.Clamp01(1f - d / 3.2f));
                    ikTarget = Vector3.Lerp(ikTarget, liveTarget, 1f - Mathf.Exp(-28f * dt));
                }
            }

            if (diving)
            {
                float elapsed = Time.time - diveStart;
                float k = Mathf.Clamp01(elapsed / Mathf.Max(0.15f, diveDuration));

                // Curva de impulsão concentrada na fase [0.10 .. 0.45] da animação de ponte (Motion Warping)
                float warpProgress;
                if (k < 0.08f)
                {
                    warpProgress = (k / 0.08f) * 0.10f;
                }
                else if (k <= 0.55f)
                {
                    float phase = (k - 0.08f) / 0.47f;
                    float easeOut = 1f - (1f - phase) * (1f - phase);
                    warpProgress = Mathf.Lerp(0.10f, 0.94f, easeOut);
                }
                else
                {
                    float tail = (k - 0.55f) / 0.45f;
                    warpProgress = Mathf.Lerp(0.94f, 1.0f, tail);
                }

                Vector3 p = Vector3.Lerp(diveFrom, diveTo, warpProgress);
                p.y = (ActiveQuadrant == QuadranteDefesa.Dive_Mid || ActiveQuadrant == QuadranteDefesa.Dive_High)
                    ? Mathf.Clamp(diveHeight, 0f, 0.36f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 0.88f))
                    : 0f;
                transform.position = p;
            }

            bool wasDive = diving && anim != null &&
                           (anim.GetCurrentAnimatorStateInfo(0).IsName("GK_Mergulho") ||
                            anim.GetNextAnimatorStateInfo(0).IsName("GK_Mergulho") ||
                            anim.GetCurrentAnimatorStateInfo(0).IsName("GK_Carrinho") ||
                            anim.GetNextAnimatorStateInfo(0).IsName("GK_Carrinho"));
            float total = diving ? Mathf.Clamp(diveDuration + 0.42f, 0.78f, 1.15f) : 0.72f;
            if (stateTime > total)
            {
                State = Estado.Recuperando;
                stateTime = 0f;
                Vector3 p = transform.position; p.y = 0f; transform.position = p;
                diving = false;
                SetCapsuleStanding(true);

                if (wasDive)
                {
                    recoverTime = 0.85f;
                    if (anim != null) anim.CrossFadeInFixedTime("GK_Levantar", 0.18f);
                }
                else
                {
                    recoverTime = 0.45f;
                    if (anim != null)
                    {
                        anim.SetBool(MirrorHash, false);
                        anim.CrossFadeInFixedTime("Locomocao", 0.25f);
                    }
                }
            }
        }

        // ------------------------------------------------------------ Esfera de Interceptação das Luvas (CCD / Segment-Sphere) e Resolução

        Vector3 prevBall;
        bool havePrev;

        Vector3 Palm(Transform hand, Transform fore)
        {
            if (hand == null) return Vector3.one * 999f;
            if (fore == null) return hand.position;
            Vector3 d = hand.position - fore.position;
            return hand.position + (d.sqrMagnitude > 1e-6f ? d.normalized * 0.08f : Vector3.zero);
        }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        void FixedUpdate()
        {
            bool activeShot = ball != null && !ball.IsHeld && State != Estado.ComBola && !resolved &&
                              (State == Estado.Defendendo || shotInFlight || Time.time - ball.LastKickTime < 3.5f || ball.Velocity.x * Mathf.Sign(side) > 1.0f);
            if (!activeShot)
            {
                havePrev = false;
                return;
            }

            Vector3 bp = ball.Body.position;
            Vector3 a = havePrev ? prevBall : bp;
            prevBall = bp;
            havePrev = true;

            if (sockets != null) sockets.UpdateSockets(0f);

            float rb = Arena.BallRadius;
            Vector3 leftPalm = sockets != null ? sockets.LeftPalmCenter : Palm(lHand, lFore);
            Vector3 rightPalm = sockets != null ? sockets.RightPalmCenter : Palm(rHand, rFore);
            Vector3 glovesMid = sockets != null ? sockets.GlovesMidpoint : (leftPalm + rightPalm) * 0.5f;

            // 1. Esfera de Interceptação das Luvas (centrada no ponto médio entre Hand_L e Hand_R + palmas individuais)
            float dMid = SegDist(glovesMid, a, bp);
            float dl = SegDist(leftPalm, a, bp);
            float dr = SegDist(rightPalm, a, bp);
            float dGloveMin = Mathf.Min(dMid, Mathf.Min(dl, dr));

            // 2. Tronco/Peito (mixamorig:Spine2 / Hips) e Pés (bloqueia chutes diretos no corpo mesmo antes do mergulho)
            float dc = chest != null ? SegDist(chest.position, a, bp) : 99f;
            float dh = hips != null ? SegDist(hips.position, a, bp) : 99f;
            float dFootL = lFoot != null ? SegDist(lFoot.position, a, bp) : 99f;
            float dFootR = rFoot != null ? SegDist(rFoot.position, a, bp) : 99f;

            bool hand = dGloveMin < (handReach + rb + 0.06f);
            bool body = Mathf.Min(dc, dh) < (0.34f + rb) ||
                        Mathf.Min(dFootL, dFootR) < (0.26f + rb);

            // 3. Verificação contínua no cruzamento do plano frontal do goleiro (previne tunneling em chutes de 30+ m/s a 30 FPS)
            if (!hand && !body && lHand != null && rHand != null)
            {
                float px = transform.position.x - 0.15f * Mathf.Sign(side);
                if ((a.x - px) * (bp.x - px) <= 0f)
                {
                    float k = Mathf.Abs(bp.x - a.x) > 1e-5f ? (px - a.x) / (bp.x - a.x) : 0f;
                    Vector3 crossPt = Vector3.Lerp(a, bp, k);
                    float nearGlove = Mathf.Min(Vector3.Distance(crossPt, glovesMid),
                                      Mathf.Min(Vector3.Distance(crossPt, leftPalm), Vector3.Distance(crossPt, rightPalm)));
                    if (nearGlove < handReach + rb + 0.22f) hand = true;
                    else if (chest != null && Vector3.Distance(crossPt, chest.position) < 0.36f + rb) body = true;
                }
            }

            if (hand || body)
            {
                if (State != Estado.Defendendo)
                {
                    BeginDefend();
                    diving = false;
                    PlayCatchBlock();
                }
                Resolve(hand, glovesMid);
            }
        }

        /// <summary>
        /// Resolução da Defesa:
        /// - Defesa Firme (Catch): Zera velocidade linear/angular da bola e prende ao GK_CatchSocket (GK_HoldingBall).
        /// - Espalmada / Rebote (Parry/Deflect): Direciona o vetor refletido para as laterais ou linha de fundo
        ///   (nunca de volta para o pé do atacante no meio da área), reduzindo a energia cinética em 50%–70%.
        /// </summary>
        void Resolve(bool handContact, Vector3 glovesMid)
        {
            resolved = true;
            shotInFlight = false;
            var ws = WeatherSystem.Instance;
            float catchSpeed = (ws != null && ws.P != null) ? ws.P.gkCatchSpeed : 21f;
            float fumble = (ws != null && ws.P != null) ? ws.P.gkFumble : 0.04f;

            Vector3 bp = ball.Body.position;
            Vector3 v = ball.Velocity;
            float speed = v.magnitude;
            float spin = ball.Body.angularVelocity.magnitude;

            // Qualidade do encaixe (1.0 = centro do GK_CatchSocket/mãos, menor no limite do Dive_High / ponta dos dedos)
            float distFromGloveCenter = Vector3.Distance(bp, glovesMid);
            float quality = handContact
                ? Mathf.Clamp01(1f - distFromGloveCenter / (handReach + 0.32f))
                : (ActiveQuadrant == QuadranteDefesa.GK_FootSave ? 0.25f : 0.82f);

            if (diving)
            {
                float stretch01 = Mathf.Clamp01(Vector3.Distance(diveFrom, transform.position) / Mathf.Max(0.5f, maxDiveTravel));
                quality *= Mathf.Lerp(1f, 0.55f, stretch01);
                if (ActiveQuadrant == QuadranteDefesa.Dive_High && stretch01 > 0.65f)
                    quality *= 0.65f; //Dive_High no limite do alcance favorece espalmada sobre o travessão/escanteio
            }

            float difficulty = speed / catchSpeed + spin / 130f + (1f - quality) * 0.85f;
            bool canCatch = ActiveQuadrant != QuadranteDefesa.GK_FootSave &&
                            difficulty < 1.0f &&
                            Random.value > fumble * (0.45f + difficulty);

            if (canCatch)
            {
                // --- DEFESA FIRME (Catch no GK_CatchSocket)
                HoldBallInCatchSocket();

                // Particle & Audio: Catch save
                if (ParticleEffectsManager.Instance != null)
                {
                    ParticleEffectsManager.Instance.PlayKickImpact(bp, -v.normalized, Mathf.Clamp01(speed / 30f));
                }
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayGoalkeeperSave(bp, Mathf.Clamp01(speed / 30f));
                }

                GameSettings.Vibrar();
                if (OnSave != null) OnSave(true);
                return;
            }

            // --- ESPALMADA / REBOTE SEGURO (Parry / Deflect)
            // Direciona para as laterais (|Z|) ou linha de fundo (+X acima/ao lado da trave), NUNCA para o meio da área
            float lateralSign = diving && Mathf.Abs(diveSide) > 0.1f
                ? Mathf.Sign(bp.z != 0f ? bp.z : (transform.right * diveSide).z)
                : (Mathf.Abs(bp.z - transform.position.z) > 0.05f ? Mathf.Sign(bp.z - transform.position.z) : (Random.value < 0.5f ? 1f : -1f));
            if (lateralSign == 0f) lateralSign = 1f;

            Vector3 sideWorld = new Vector3(0f, 0f, lateralSign); // Direção da linha lateral
            Vector3 fieldOutward = Out * 0.35f;   // Levemente diagonal mas dominado pela lateral

            // Reduz a energia cinética em 50% a 70% => velocidade final entre 55% e 70% da velocidade de entrada
            float speedRetention = Mathf.Sqrt(Random.Range(0.30f, 0.50f));
            float outSpeed = Mathf.Max(4.8f, speed * speedRetention);

            Vector3 parryDir;
            if (ActiveQuadrant == QuadranteDefesa.Dive_High && bp.y > Arena.GoalHeight - 0.55f)
            {
                // Espalmada por cima do travessão / linha de fundo (escanteio alto)
                parryDir = (new Vector3(0.45f * Mathf.Sign(side), 0.72f, lateralSign * 0.65f)).normalized;
            }
            else
            {
                // Espalmada firme para as laterais da área (longe do centro da grande área)
                float upComp = ActiveQuadrant == QuadranteDefesa.Dive_Low ? Random.Range(0.08f, 0.22f) : Random.Range(0.18f, 0.42f);
                parryDir = (sideWorld * 0.88f + fieldOutward + Vector3.up * upComp).normalized;
            }

            Vector3 nv = parryDir * outSpeed;
            Vector3 outSpin = Vector3.Cross(Vector3.up, parryDir) * (outSpeed / Arena.BallRadius) * 0.6f;

            ball.Deflect(nv, outSpin);

            // Particle & Audio: Deflect/Parry save
            if (ParticleEffectsManager.Instance != null)
            {
                ParticleEffectsManager.Instance.PlayKickImpact(bp, parryDir, Mathf.Clamp01(outSpeed / 25f));
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGoalkeeperSave(bp, Mathf.Clamp01(outSpeed / 25f));
            }

            if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.Kick(bp, 0.45f);
            GameSettings.Vibrar();
            if (OnSave != null) OnSave(false);
        }

        void HoldBallInCatchSocket()
        {
            if (sockets != null)
            {
                sockets.UpdateSockets(0f);
                if (sockets.GK_CatchSocket != null)
                {
                    ball.HoldAtSocket(sockets.GK_CatchSocket);
                }
                else
                {
                    ball.Hold(transform, new Vector3(0f, diving ? Mathf.Max(0.45f, diveHeight + 0.45f) : 1.15f, 0.38f));
                }
            }
            else
            {
                ball.Hold(transform, new Vector3(0f, diving ? Mathf.Max(0.45f, diveHeight + 0.45f) : 1.15f, 0.38f));
            }
            State = Estado.ComBola;
            stateTime = 0f;

            // Audio: Catch sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayIFXConfirmation();
            }
        }

        // ------------------------------------------------------------ Reposição de bola (arremesso com as mãos ou chutão)

        enum Reposicao { Nenhuma, Segurando, Arremesso, Chutao }
        Reposicao repo = Reposicao.Nenhuma;
        float repoStart, repoAt;
        bool repoReleased;
        float retrieveAt = -1f;

        /// <summary>Depois de um gol: o goleiro pega a bola no fundo da rede e repõe em jogo.</summary>
        public void RetrieveAndDistribute()
        {
            retrieveAt = Time.time + 0.9f;
        }

        Vector3? repoTarget;

        /// <summary>Partida (tiro de meta / bola recuperada): pega a bola com as mãos e repõe.</summary>
        public void TakeBallInHands()
        {
            diving = false;
            resolved = true;
            shotInFlight = false;
            SetCapsuleStanding(true);
            if (anim != null) { anim.SetBool(MirrorHash, false); anim.CrossFadeInFixedTime("Locomocao", 0.1f); }
            HoldBallInCatchSocket();
            repo = Reposicao.Nenhuma;
        }

        void BeginDistribution()
        {
            repoStart = Time.time;
            repoReleased = false;
            repoTarget = distributionTarget != null ? distributionTarget() : null;
            bool punt = Random.value < 0.45f;
            if (repoTarget.HasValue)
            {
                float d = Vector3.Distance(transform.position, repoTarget.Value);
                punt = d > 30f;
                Vector3 look = repoTarget.Value - transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.1f) transform.rotation = Quaternion.LookRotation(look);
            }
            repo = punt ? Reposicao.Chutao : Reposicao.Arremesso;
            repoAt = punt ? 0.5f : 0.68f;
            if (anim != null) anim.CrossFadeInFixedTime(punt ? "GK_Chutao" : "GK_Lancar", 0.12f);
        }

        Vector3 FieldDirection()
        {
            if (repoTarget.HasValue)
            {
                Vector3 d = repoTarget.Value - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 1f) return d.normalized;
            }
            float ang = Random.Range(-28f, 28f);
            return Quaternion.Euler(0f, ang, 0f) * Out;
        }

        /// <summary>Velocidade horizontal para a reposição chegar ao companheiro (ou um padrão).</summary>
        float RepoSpeed(float def, float flightTime)
        {
            if (!repoTarget.HasValue) return def;
            Vector3 d = repoTarget.Value - transform.position; d.y = 0f;
            return Mathf.Clamp(d.magnitude / flightTime * 1.05f, 6f, 30f);
        }

        void UpdateDistribution()
        {
            if (retrieveAt > 0f && Time.time >= retrieveAt && ball != null)
            {
                retrieveAt = -1f;
                diving = false;
                SetCapsuleStanding(true);
                if (anim != null) anim.CrossFadeInFixedTime("Locomocao", 0.1f);
                transform.rotation = Quaternion.LookRotation(Out);
                HoldBallInCatchSocket();
                repo = Reposicao.Nenhuma;
            }
            if (ball == null) return;

            if (repo == Reposicao.Nenhuma)
            {
                if (State != Estado.ComBola) return;
                float wait = (diving || stateTime > 1.2f) ? 1.35f : 0.8f;
                if (ball.IsHeld && stateTime > wait) BeginDistribution();
                return;
            }

            float t = Time.time - repoStart;
            if (repo == Reposicao.Arremesso)
            {
                if (!repoReleased && t >= repoAt)
                {
                    repoReleased = true;
                    Vector3 d = FieldDirection();
                    Vector3 throwPos = transform.position + transform.forward * 0.5f + Vector3.up * 1.5f;
                    float up = Random.Range(4.5f, 6.5f);
                    float tFlight = 2f * up / 9.81f + 0.15f;
                    ball.Deflect(d * RepoSpeed(Random.Range(12f, 16f), tFlight) + Vector3.up * up, Vector3.zero);

                    // Audio: Throw sound
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayIFXSelect();
                    }
                }
            }
            else
            {
                if (!repoReleased && t >= 0.14f && t < repoAt)
                {
                    // solta a bola para quicar na frente do pé
                    if (ball.IsHeld) ball.Deflect(transform.forward * 0.6f + Vector3.down * 0.2f, Vector3.zero);
                }
                if (!repoReleased && t >= repoAt)
                {
                    repoReleased = true;
                    Vector3 foot = rFoot != null ? rFoot.position : transform.position + transform.forward * 0.4f;
                    Vector3 p = foot + transform.forward * 0.18f; p.y = Mathf.Max(foot.y, 0.18f);
                    Vector3 d = FieldDirection();
                    ball.PlaceAt(p);
                    float up = Random.Range(11f, 14f);
                    float tFlight = 2f * up / 9.81f * 0.92f;
                    ball.Deflect(d * RepoSpeed(Random.Range(19f, 24f), tFlight) + Vector3.up * up, Vector3.Cross(d, Vector3.up) * 25f);

                    // Audio: Goal kick sound
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayKick(p, 0.9f);
                    }
                    if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.Kick(p, 0.9f);
                }
            }

            if (t > 1.1f)
            {
                repo = Reposicao.Nenhuma;
                State = Estado.Posicionando;
                stateTime = 0f;
                ActiveQuadrant = QuadranteDefesa.Nenhum;
                if (sockets != null) sockets.lockHipsHorizontalInPlace = true;
                if (anim != null) { anim.SetBool(MirrorHash, false); anim.CrossFadeInFixedTime("Locomocao", 0.3f); }
            }
        }

        /// <summary>Gesto do goleiro (ex.: frustração depois do gol), só quando está em pé posicionado.</summary>
        public void PlayGesture(string state)
        {
            if (anim != null && State == Estado.Posicionando && anim.HasState(0, Animator.StringToHash(state)))
                anim.CrossFadeInFixedTime(state, 0.2f);
        }

        void Grab()
        {
            diving = false;
            SetCapsuleStanding(true);
            HoldBallInCatchSocket();
            if (OnSave != null) OnSave(true);
        }

        /// <summary>
        /// Resolve colisão entre jogadores para evitar sobreposição de cápsulas.
        /// Aplica uma força de repulsão simples quando as cápsulas dos jogadores se intersectam.
        /// </summary>
        Vector3 ResolvePlayerCollisions(Vector3 proposedPos)
        {
            if (bodyCapsule == null) return proposedPos;

            Collider[] hits = Physics.OverlapCapsule(
                proposedPos + bodyCapsule.center,
                proposedPos + bodyCapsule.center + Vector3.up * (bodyCapsule.height - bodyCapsule.radius * 2f),
                bodyCapsule.radius,
                1 << Arena.LayerPlayerBody
            );

            Vector3 result = proposedPos;
            foreach (var hit in hits)
            {
                if (hit == bodyCapsule) continue;
                if (hit.attachedRigidbody != null && hit.attachedRigidbody.isKinematic)
                {
                    // Calcula direção de repulsão
                    Vector3 dir = (proposedPos - hit.bounds.center);
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 1e-4f) dir = transform.right;

                    float overlap = (bodyCapsule.radius + hit.bounds.extents.x) - dir.magnitude;
                    if (overlap > 0f)
                    {
                        dir.Normalize();
                        result += dir * (overlap + 0.02f); // Pequeno buffer extra
                    }
                }
            }
            return result;
        }

        // ------------------------------------------------------------ IK das Luvas e Postura

        void OnIK(int layer)
        {
            if (anim == null || ball == null) return;
            if (State == Estado.Posicionando)
            {
                anim.SetLookAtWeight(0.65f, 0f, 0.45f, 0f, 0f);
                anim.SetLookAtPosition(ball.transform.position);
            }
            else
            {
                anim.SetLookAtWeight(0f);
            }
        }
    }
}
