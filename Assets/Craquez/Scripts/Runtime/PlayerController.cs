using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Craquez
{
    public enum TipoChute { Forte = 0, Colocado = 1, Cavadinha = 2, Rasteiro = 3 }

    /// <summary>
    /// Controlador Cinemático/Híbrido dos Jogadores de Linha:
    /// - Camada 1: Root com CapsuleCollider na Layer PlayerBody (ignora colisão direta com a Bola para não rebater invisivelmente).
    /// - Camada 2: Skeletal Mesh Mixamo alinhada na base dos pés com trava horizontal In-Place no mixamorig:Hips.
    /// - Camada 3: Mapeamento de Sockets Mixamo (Foot_R, Foot_L, Hand_R, Hand_L, Chest) + BallControlPoint (0.35m–0.45m).
    /// - Física Cinemática: Acceleration = 18 m/s², Deceleration = 25 m/s², Turn Rate Dinâmico (540°/s baixa vel -> 300°/s Sprint).
    /// - Sincronização com Animator/Blend Space: Speed e Direction/Angle normalizados sem foot sliding.
    /// - Condução Magnética por Toques (< 0.65m) guiando a bola ao BallControlPoint com spin compatível.
    /// - Chutes Determinísticos na janela normalizada [0.35..0.45] da animação do Mixamo validando a distância real do pé.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public BallPhysics ball;
        public Transform cameraTransform;

        [Header("Física Cinemática (Inércia Controlada)")]
        [Tooltip("Velocidade de corrida sem sprint (m/s). Com os atributos fica entre ~4,7 e ~6,1 m/s.")]
        public float jogSpeed = 5.5f;
        [Tooltip("Velocidade de sprint (m/s). Com os atributos: ~7,7 a ~9,9 m/s (antes chegava a 11 m/s, acima de qualquer velocista).")]
        public float sprintSpeed = 9.0f;
        [Tooltip("Aceleração saindo do parado (m/s²); cai conforme o jogador se aproxima da velocidade máxima.")]
        public float acceleration = 13f;
        [Tooltip("Velocidade (m/s) do clipe mais rápido da locomoção, medida no setup: acima dela a animação acelera.")]
        public float locoTopSpeed = 10.2f;
        [Tooltip("Desaceleração/frenagem (m/s²): um atleta para de um sprint em ~1 s.")]
        public float deceleration = 15f;
        [Tooltip("Taxa de rotação em baixa velocidade (graus/s)")]
        public float lowSpeedTurnRate = 560f;
        [Tooltip("Taxa de rotação durante sprint (graus/s)")]
        public float sprintTurnRate = 310f;

        [Header("Domínio e Condução Magnética")]
        [Tooltip("Raio de domínio para condução magnética rumo ao BallControlPoint (0,28-0,38 m à frente).")]
        public float possessionRadius = 0.56f;
        [Tooltip("Alcance máximo do pé no frame de impacto do chute.")]
        public float kickFootReach = 1.05f;

        [Header("Chute")]
        [Tooltip("Tempo para carregar o chute totalmente (s)")]
        public float chargeTime = 0.7f;

        public bool InputEnabled { get; set; }

        [Header("IA (modo goleiro: a IA controla o atacante)")]
        public bool aiControlled;
        [System.NonSerialized] public Vector3 aiMoveWorld;
        [System.NonSerialized] public bool aiSprint, aiShoot, aiFinesse, aiChip, aiLow;
        [System.NonSerialized] public Vector3 aiAimWorld;

        [Header("Partida 11 x 11")]
        [Tooltip("+1 = ataca o gol em +X; -1 = ataca o gol em -X.")]
        public float attackDir = 1f;
        [Tooltip("Falso quando outro jogador tem a posse: este não mexe na bola (a partida arbitra a posse).")]
        public bool ballAuthority = true;
        [Tooltip("Na partida ✕/△ são passe/lançamento (tratados pela sessão) e não chutes.")]
        public bool matchControls;
        [Tooltip("Multiplicadores individuais (atributos, cansaço, contusão).")]
        public float speedScale = 1f, accelScale = 1f, accuracyScale = 1f;
        [Tooltip("Massa corporal (kg) usada nas trombadas.")]
        public float bodyMass = 75f;
        /// <summary>Se definido, o jogador só começa a carregar um chute quando retorna true.</summary>
        [NonSerialized] public Func<bool> canShoot;
        /// <summary>Ponto que a IA quer encarar quando está parada ou devagar (ex.: a bola).</summary>
        [NonSerialized] public Vector3 aiFacePoint;
        [NonSerialized] public bool aiFace;
        /// <summary>Modo cercar (jockey defensivo): encara a bola com agilidade lateral reforçada.</summary>
        [NonSerialized] public bool jockeyMode;
        /// <summary>Alvo lateral (z) da finalização da IA; NaN = escolhe pela direção do corpo.</summary>
        [NonSerialized] public float aiShotTargetZ = float.NaN;
        /// <summary>Trombada com outro corpo: (outro jogador ou null, normal empurrando este jogador, velocidade de aproximação).</summary>
        public event Action<PlayerController, Component, Vector3, float> OnBodyContact;
        /// <summary>Carrinho tocou a bola (antes ou sem tocar no adversário).</summary>
        public event Action<PlayerController> OnSlideBall;

        public Vector3 GoalTarget { get { return new Vector3(attackDir * Arena.HalfLength, 0f, 0f); } }
        public Vector3 ControlPoint { get { return ControlPointWorld(); } }
        /// <summary>Caído, levantando ou se contorcendo: sem controle do corpo nem da bola.</summary>
        public bool Incapacitated { get { return fallen || Time.time < getUpUntil; } }
        public bool Fallen { get { return fallen; } }
        public bool Sliding { get { return Time.time < slideUntil; } }
        public float SlideStartTime { get { return slideStart; } }
        public bool SlideTouchedBall { get { return slideTouched; } }
        public bool PassPending { get { return kickPending && passPending; } }

        public bool Busy { get { return kickPending || Time.time < lockMoveUntil || Incapacitated || Sliding; } }
        public bool Charging { get; private set; }
        public float Charge { get; private set; }
        public TipoChute ChargingType { get; private set; }

        /// <summary>Posse ativa da bola (alias compatível com HasPossession e HasBall).</summary>
        public bool HasPossession { get; private set; }
        public bool HasBall { get { return HasPossession; } private set { HasPossession = value; } }

        public Vector3 Velocity { get { return velocity; } }
        public Animator Anim { get { return anim; } }
        public MixamoBoneSockets Sockets { get { return sockets; } }
        public CapsuleCollider BodyCollider { get { return bodyCapsule; } }

        Animator anim;
        MixamoBoneSockets sockets;
        CapsuleCollider bodyCapsule;
        Rigidbody rootBody;
        Transform lToe, rToe;

        Vector3 velocity;
        bool prevShoot, prevFinesse, prevChip, prevLow, prevSkill, prevKnock;

        // Condução e drible
        Vector3 wishWorld;
        bool sprintHeld;
        float nextTouchAt, touchEnd, touchDecel, lastTouchAt = -10f, burstUntil, skillCooldown;
        Vector3 touchDir;
        bool knockQueued;
        public float LastTouchTime { get { return lastTouchAt; } }
        public string LastMove { get; private set; }
        public float LastMoveTime { get; private set; } = -10f;

        // Chute determinístico sincronizado com janela [0.35, 0.45] da animação Mixamo
        bool kickPending;
        float kickStartedAt, kickFallbackContactAt, lockMoveUntil;
        string activeKickState;
        TipoChute kickType;
        float kickPower;
        Vector2 kickAim;
        Vector3 kickAimWorld;
        Vector3 ballVelAtContact;
        float splashTimer;
        bool splashLeftFoot;

        // Passe / lançamento
        bool passPending, passLofted;
        Vector3 passTarget;
        float passError;

        // Quedas, contusões e carrinho
        bool fallen;
        float fallUntil, getUpUntil, slideUntil, slideStart;
        bool hurt, slideTouched;
        Vector3 slideDir;
        FootGrounder grounder;
        static readonly Collider[] overlapBuf = new Collider[16];
        readonly System.Collections.Generic.Dictionary<Collider, float> lastContact = new System.Collections.Generic.Dictionary<Collider, float>();

        // Particle & Audio integration
        float lastFootstepTime;
        float lastSlideTime;
        Vector3 lastPosition;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int DirectionHash = Animator.StringToHash("Direction");
        static readonly int AngleHash = Animator.StringToHash("Angle");
        bool hasDirectionParam, hasAngleParam;

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

                CacheAnimatorParameters();
            }

            lToe = sockets.Toe_L != null ? sockets.Toe_L : sockets.Foot_L;
            rToe = sockets.Toe_R != null ? sockets.Toe_R : sockets.Foot_R;
            grounder = GetComponent<FootGrounder>();
        }

        /// <summary>
        /// Camada 1 - Root / Capsule Collider (Apenas Locomoção e Corpo-a-Corpo):
        /// Restrito ao eixo central do tronco e posto na Layer PlayerBody,
        /// que ignora colisão direta com a Layer Ball (impede a "parede invisível").
        /// Usa as dimensões do prefab (baseadas na altura real do modelo escalonado).
        /// </summary>
        void ConfigureLayer1Capsule()
        {
            gameObject.layer = Arena.LayerPlayerBody;
            bodyCapsule = GetComponent<CapsuleCollider>();
            if (bodyCapsule == null) bodyCapsule = gameObject.AddComponent<CapsuleCollider>();
            bodyCapsule.direction = 1; // Eixo Y

            // Usa as dimensões já configuradas no prefab (baseadas na altura do modelo)
            // O prefab cria o capsule com height = altura do personagem e center = (0, height/2, 0)
            // Se por algum motivo não estiver configurado, usa valores padrão proporcionais
            if (bodyCapsule.height <= 0.1f || bodyCapsule.center.y <= 0.1f)
            {
                // Fallback: calcula baseado no FootGrounder
                var grounder = GetComponent<FootGrounder>();
                float h = grounder != null ? grounder.height : 1.78f;
                bodyCapsule.height = h;
                bodyCapsule.center = new Vector3(0f, h * 0.5f, 0f);
                bodyCapsule.radius = 0.24f;
            }

            rootBody = GetComponent<Rigidbody>();
            if (rootBody == null) rootBody = gameObject.AddComponent<Rigidbody>();
            rootBody.isKinematic = true;
            rootBody.useGravity = false;
            rootBody.interpolation = RigidbodyInterpolation.None;

            if (ball != null && ball.Sphere != null)
                Physics.IgnoreCollision(bodyCapsule, ball.Sphere, true);
        }

        void CacheAnimatorParameters()
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            foreach (var p in anim.parameters)
            {
                if (p.nameHash == DirectionHash && p.type == AnimatorControllerParameterType.Float) hasDirectionParam = true;
                if (p.nameHash == AngleHash && p.type == AnimatorControllerParameterType.Float) hasAngleParam = true;
            }
        }

        /// <summary>Distância horizontal do pé mais próximo (Foot_R/Foot_L) até a bola.</summary>
        float FootDistance(Vector3 bp, out Transform foot, out Vector3 footContactPos)
        {
            if (sockets != null)
                return sockets.GetClosestFootDistance(bp, out foot, out footContactPos);

            foot = null;
            footContactPos = transform.position + transform.forward * 0.4f;
            if (lToe == null || rToe == null) return 99f;
            Vector3 b = new Vector3(bp.x, 0f, bp.z);
            Vector3 l = lToe.position; l.y = 0f;
            Vector3 r = rToe.position; r.y = 0f;
            float dl = Vector3.Distance(b, l), dr = Vector3.Distance(b, r);
            foot = dl < dr ? lToe : rToe;
            footContactPos = foot.position;
            return Mathf.Min(dl, dr);
        }

        public void Teleport(Vector3 pos, Vector3 lookAt)
        {
            pos.y = 0f;
            transform.position = pos;
            Vector3 d = lookAt - pos; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
            velocity = Vector3.zero;
            Charging = false; Charge = 0f; kickPending = false; passPending = false; lockMoveUntil = 0f;
            fallen = false; hurt = false; fallStartedAt = -1f; hurtGetUpAt = -1f; staggerUntil = 0f; getUpUntil = 0f; slideUntil = 0f;
            if (grounder != null) grounder.grounding = true;
            HasPossession = false;
            if (sockets != null)
            {
                sockets.SetProceduralLean(0f, 0f, true);
                sockets.UpdateSockets(0f);
            }
            if (anim != null)
            {
                anim.SetFloat(SpeedHash, 0f);
                if (hasDirectionParam) anim.SetFloat(DirectionHash, 0f);
                if (hasAngleParam) anim.SetFloat(AngleHash, 0f);
                anim.Play("Locomocao", 0, 0f);
            }
        }

        float Grip()
        {
            var ws = WeatherSystem.Instance;
            return (ws != null && ws.P != null) ? ws.P.playerGrip : 1f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float grip = Grip();

            if (ball != null && ball.Sphere != null && bodyCapsule != null)
                Physics.IgnoreCollision(bodyCapsule, ball.Sphere, true);

            UpdateBodyState(dt);
            bool disabled = Incapacitated || Sliding;
            Vector2 mv = InputEnabled && !aiControlled && !disabled ? InputBridge.Move : Vector2.zero;
            Vector3 camF = Vector3.forward, camR = Vector3.right;
            if (cameraTransform != null)
            {
                camF = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                camR = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }
            Vector3 wish = disabled ? Vector3.zero : (aiControlled ? Vector3.ClampMagnitude(aiMoveWorld, 1f) : camF * mv.y + camR * mv.x);
            bool sprint = !disabled && (aiControlled ? aiSprint : (InputEnabled && InputBridge.Sprint));
            bool jockey = !disabled && !HasPossession && jockeyMode && !sprint;
            wishWorld = wish;
            sprintHeld = sprint;
            bool kicking = Time.time < lockMoveUntil;

            // Condução assistida: ajusta levemente o vetor desejado na direção da bola recém-tocada
            if (ball != null && Time.time - lastTouchAt < 1.35f && wish.sqrMagnitude > 0.04f && !kicking)
            {
                Vector3 tb = ball.Body.position - transform.position; tb.y = 0f;
                if (tb.magnitude < 4.2f && tb.magnitude > 0.28f && Vector3.Dot(tb.normalized, wish.normalized) > 0.55f)
                    wish = Vector3.Slerp(wish.normalized, tb.normalized, 0.32f) * wish.magnitude;
            }

            float max = (jockey ? jogSpeed * 0.78f : (sprint ? sprintSpeed : jogSpeed)) * speedScale;
            if (HasPossession) max *= sprint ? 0.95f : 0.92f;
            if (Time.time < burstUntil) max += 1.15f;

            var pfSurf = PuddleField.Instance;
            float surfDepth = 0f;
            Superficie surf = pfSurf != null ? pfSurf.SurfaceAt(transform.position, out surfDepth) : Superficie.Grama;
            if (surf == Superficie.Poca)
            {
                max *= Mathf.Lerp(1f, 0.84f, surfDepth);
                grip *= Mathf.Lerp(1f, 0.72f, surfDepth);
            }
            else if (surf == Superficie.Encharcado)
            {
                grip *= Mathf.Lerp(1f, 0.88f, surfDepth);
            }

            Vector3 target;
            if (Time.time < staggerUntil) target = Vector3.zero;
            else if (kickPending)
            {
                // Na preparação do chute em corrida, mantém o embalo até a bola (evita furar bolas quicando/adiantadas)
                Vector3 carry = velocity * 0.58f;
                if (ball != null)
                {
                    Vector3 toB = ball.Body.position - (transform.position + transform.forward * 0.38f);
                    toB.y = 0f;
                    if (toB.magnitude < 1.9f) carry += Vector3.ClampMagnitude(toB * 3.8f, 3.2f);
                }
                target = Vector3.ClampMagnitude(carry, max * 0.75f);
            }
            else if (kicking) target = Vector3.zero;
            else target = wish * max;

            if (Charging) target *= 0.68f;
            if (Sliding)
            {
                // carrinho: o corpo desliza na grama (mais longe com o gramado molhado) e freia sozinho
                float slideFriction = Mathf.Lerp(9.5f, 6f, 1f - grip);
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, slideFriction * dt);
                target = velocity;
            }
            else if (fallen)
            {
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, 11f * grip * dt);
                target = velocity;
            }

            // --- 2. Física Cinemática dos Jogadores de Linha (saída ágil + inércia realista em alta velocidade)
            Vector3 prevVel = velocity;
            float currentSpeed = velocity.magnitude;
            float targetSpeed = target.magnitude;
            bool isBraking = targetSpeed < currentSpeed - 0.05f ||
                             (currentSpeed > 0.5f && targetSpeed > 0.05f && Vector3.Dot(velocity.normalized, target.normalized) < 0.15f);
            float topSpeed = Mathf.Max(0.5f, sprintSpeed * speedScale);
            float accelCurve = Mathf.Lerp(1.12f, 0.32f, Mathf.Clamp01(currentSpeed / topSpeed));
            float accelRate = (isBraking ? deceleration : acceleration * accelScale * accelCurve) * grip;
            if (jockey) accelRate *= 1.28f;

            if (currentSpeed > 1.2f && target.sqrMagnitude > 0.01f)
            {
                float turnDot = Vector3.Dot(velocity / currentSpeed, target.normalized);
                if (turnDot < 0.15f)
                {
                    // Corte brusco / pivô usa taxa de frenagem reforçada para eliminar "patinação no gelo"
                    accelRate = deceleration * 1.22f * grip;
                }
            }
            // freada brusca vinda de um sprint: animação própria (corpo para trás, perna da frente travando)
            if (currentSpeed > 6f && targetSpeed < 1f && !kicking && !Sliding && !fallen && Time.time > freadaCooldown && anim != null &&
                anim.GetCurrentAnimatorStateInfo(0).IsName("Locomocao") && !anim.IsInTransition(0))
            {
                freadaCooldown = Time.time + 1.5f;
                Play("Freada", 0.1f);
            }
            UpdatePendingCut();
            if (!Sliding && !fallen)
            {
                velocity = Vector3.MoveTowards(velocity, target, accelRate * dt);
                if (target.sqrMagnitude < 1e-4f && velocity.sqrMagnitude < 0.0064f)
                    velocity = Vector3.zero;
            }

            Vector3 p = transform.position + velocity * dt;
            // alambrado: além de limitar a posição, zera a velocidade contra a grade (antes o jogador "corria parado")
            if (Mathf.Abs(p.x) > Arena.FenceX - 1f) { p.x = Mathf.Sign(p.x) * (Arena.FenceX - 1f); if (velocity.x * p.x > 0f) velocity.x = 0f; }
            if (Mathf.Abs(p.z) > Arena.FenceZ - 1f) { p.z = Mathf.Sign(p.z) * (Arena.FenceZ - 1f); if (velocity.z * p.z > 0f) velocity.z = 0f; }
            p.y = 0f;

            // Evita sobreposição com outros jogadores (evita atravessar o goleiro/outro jogador)
            p = ResolvePlayerCollisions(p);

            transform.position = p;

            // --- Turn Rate Dinâmico: 560°/s em baixa velocidade/proteção de bola; 310°/s durante Sprint
            float speedRatio = Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.1f, sprintSpeed));
            float dynamicTurnRate = Mathf.Lerp(lowSpeedTurnRate, sprintTurnRate, speedRatio) * Mathf.Lerp(0.75f, 1f, grip);

            Quaternion prevRot = transform.rotation;
            Vector3 face = velocity.sqrMagnitude > 0.04f ? velocity : (wish.sqrMagnitude > 0.04f ? wish : transform.forward);
            if (jockey && ball != null && velocity.sqrMagnitude < 16f && !kicking && !Charging)
            {
                Vector3 tb = ball.transform.position - transform.position; tb.y = 0f;
                if (tb.sqrMagnitude > 0.04f) face = tb;
            }
            else if (aiControlled && aiFace && velocity.sqrMagnitude < 5.5f && !kicking && !Charging)
            {
                Vector3 tf = aiFacePoint - transform.position; tf.y = 0f;
                if (tf.sqrMagnitude > 0.04f) face = tf;
            }
            if ((Charging || kicking) && ball != null)
            {
                Vector3 toBall = ball.transform.position - transform.position; toBall.y = 0f;
                Vector3 aimAt = passPending ? passTarget : GoalTarget;
                Vector3 toGoal = aimAt - transform.position; toGoal.y = 0f;
                face = toBall.magnitude < 2.5f ? Vector3.Lerp(toBall.normalized, toGoal.normalized, 0.5f) : toGoal;
            }
            if (Sliding) face = slideDir;
            if (fallen) face = transform.forward;
            face.y = 0f;
            if (face.sqrMagnitude > 1e-4f)
            {
                Quaternion q = Quaternion.LookRotation(face);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, q, dynamicTurnRate * dt);
            }

            // Inclinação procedural do corpo nas curvas e acelerações + projeção dinâmica do BallControlPoint
            if (sockets != null)
            {
                bool freeLoco = !kicking && !Incapacitated && !Sliding;
                sockets.lockHipsHorizontalInPlace = freeLoco;
                if (freeLoco)
                {
                    float yawDeltaDeg = Mathf.DeltaAngle(prevRot.eulerAngles.y, transform.eulerAngles.y);
                    float yawRateRad = (yawDeltaDeg * Mathf.Deg2Rad) / dt;
                    float rollDeg = -yawRateRad * velocity.magnitude * 1.15f;
                    Vector3 worldAccel = (velocity - prevVel) / dt;
                    float fwdAccel = Vector3.Dot(worldAccel, transform.forward);
                    float pitchDeg = fwdAccel * 0.35f;
                    sockets.SetProceduralLean(rollDeg, pitchDeg);
                }
                else sockets.SetProceduralLean(0f, 0f);
                sockets.UpdateSockets(speedRatio);
            }

            if (!disabled) HandleSkillInput(mv);
            SyncAnimatorParameters(dt, face);
            if (!disabled) HandleShootInput(dt, mv);
            CheckKickWindowContact();

            // Passadas e respingos visíveis ao correr sobre poças ou gramado encharcado
            splashTimer -= dt;
            if (pfSurf != null && velocity.magnitude > 0.9f && splashTimer <= 0f && surf != Superficie.Grama)
            {
                splashLeftFoot = !splashLeftFoot;
                Vector3 footPos = splashLeftFoot
                    ? (sockets != null ? sockets.LeftFootContactPosition : transform.position - transform.right * 0.12f)
                    : (sockets != null ? sockets.RightFootContactPosition : transform.position + transform.right * 0.12f);
                float baseInt = surf == Superficie.Poca ? Mathf.Lerp(0.45f, 1.0f, surfDepth) : 0.28f;
                pfSurf.FootStep(footPos, baseInt * Mathf.Clamp01(velocity.magnitude / jogSpeed));
                splashTimer = Mathf.Lerp(0.36f, 0.17f, speedRatio);
            }

            // Particle & Audio: Footstep effects when running
            float speed = velocity.magnitude;
            if (speed > 1.5f && Time.time - lastFootstepTime > 0.3f)
            {
                bool isSprinting = sprintHeld && speed > jogSpeed;
                float stepInterval = isSprinting ? 0.25f : 0.35f;
                if (Time.time - lastFootstepTime > stepInterval)
                {
                    Vector3 footPos = splashLeftFoot
                        ? (sockets != null ? sockets.LeftFootContactPosition : transform.position - transform.right * 0.12f)
                        : (sockets != null ? sockets.RightFootContactPosition : transform.position + transform.right * 0.12f);

                    // Particle: small dust puff
                    if (ParticleEffectsManager.Instance != null)
                        ParticleEffectsManager.Instance.PlayFootstepDust(footPos, speed);

                    // Audio: footstep sound
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayFootstep(footPos, speed, isSprinting);

                    lastFootstepTime = Time.time;
                    splashLeftFoot = !splashLeftFoot;
                }
            }
        }

        void SyncAnimatorParameters(float dt, Vector3 desiredFace)
        {
            if (anim == null) return;
            float spd = velocity.magnitude;
            LocomotionAnim.Drive(anim, SpeedHash, spd, transform.localScale.y, locoTopSpeed, dt, 0.06f);

            Vector3 moveDir = spd > 0.1f ? velocity / spd : ( desiredFace.sqrMagnitude > 1e-4f ? desiredFace.normalized : transform.forward );
            float signedAngle = Vector3.SignedAngle(transform.forward, moveDir, Vector3.up);
            float normalizedDir = Mathf.Clamp(signedAngle / 180f, -1f, 1f);

            if (hasDirectionParam) anim.SetFloat(DirectionHash, normalizedDir, 0.08f, dt);
            if (hasAngleParam) anim.SetFloat(AngleHash, signedAngle, 0.08f, dt);
        }

        // ------------------------------------------------------------ Entrada e Janela Determinística de Chute [0.35 - 0.45]

        float blockUntil;

        /// <summary>Ignora botões já pressionados (ex.: ✕ usado para sair do menu) até serem soltos.</summary>
        public void BlockInput(float seconds)
        {
            blockUntil = Time.unscaledTime + seconds;
            prevShoot = prevFinesse = prevChip = prevLow = prevSkill = prevKnock = true;
        }

        void HandleShootInput(float dt, Vector2 mv)
        {
            bool human = InputEnabled && !aiControlled;
            if (human && Time.unscaledTime < blockUntil)
            {
                prevShoot = prevFinesse = prevChip = prevLow = true;
                return;
            }
            bool s = aiControlled ? aiShoot : human && InputBridge.Shoot;
            bool f = aiControlled ? aiFinesse : human && InputBridge.Finesse;
            bool c = aiControlled ? aiChip : human && InputBridge.Chip;
            bool l = aiControlled ? aiLow : human && InputBridge.LowShot;
            bool busy = kickPending || Time.time < lockMoveUntil;

            // Na partida (controles eFootball): ✕ = Passe, △ = Profundidade, ○ = Cruzamento/Passe Alto (tratados pelo MatchSession);
            // □ = Chute (R1 + □ = Chute Colocado; L1 + □ = Cavadinha).
            if (matchControls && human)
            {
                f = false; c = false; l = false;
            }
            bool allowed = canShoot == null || canShoot();
            if (!Charging && !busy && allowed)
            {
                if (s && !prevShoot)
                {
                    TipoChute st = TipoChute.Forte;
                    if (human)
                    {
                        if (InputBridge.KnockOn) st = TipoChute.Cavadinha;
                        else if (InputBridge.Skill) st = TipoChute.Colocado;
                    }
                    StartCharge(st);
                }
                else if (f && !prevFinesse) StartCharge(TipoChute.Colocado);
                else if (c && !prevChip) StartCharge(TipoChute.Cavadinha);
                else if (l && !prevLow) StartCharge(TipoChute.Rasteiro);
            }
            if (Charging)
            {
                // Permite acionar R1 (Colocado) ou L1 (Cavadinha) também durante o carregamento do chute eFootball
                if (human && matchControls && s)
                {
                    if (InputBridge.KnockOn) ChargingType = TipoChute.Cavadinha;
                    else if (InputBridge.Skill) ChargingType = TipoChute.Colocado;
                }
                bool held = (matchControls && human)
                    ? s
                    : (ChargingType == TipoChute.Forte ? s :
                      (ChargingType == TipoChute.Colocado ? f :
                      (ChargingType == TipoChute.Cavadinha ? c : l)));
                Charge = Mathf.Min(1f, Charge + dt / chargeTime);
                if (!held) ReleaseShot(mv);
            }
            prevShoot = s; prevFinesse = f; prevChip = c; prevLow = l;
        }

        void StartCharge(TipoChute t)
        {
            Charging = true;
            ChargingType = t;
            Charge = 0.05f;
        }

        void ReleaseShot(Vector2 mv)
        {
            Charging = false;
            // No eFootball, um toque bem rápido no botão de chute (< 16% de barra) bate rasteiro firme no canto
            kickType = (ChargingType == TipoChute.Forte && Charge < 0.16f) ? TipoChute.Rasteiro : ChargingType;
            kickPower = Charge;
            kickAim = mv;
            kickAimWorld = aiControlled ? aiAimWorld : Vector3.zero;
            passPending = false;
            BeginKick();
        }

        /// <summary>IA: finaliza na direção do gol atacado, mirando a coordenada lateral targetZ.</summary>
        public bool AIShoot(TipoChute t, float power, float targetZ)
        {
            if (Busy || Charging || ball == null) return false;
            kickType = t;
            kickPower = Mathf.Clamp01(power);
            kickAim = Vector2.zero;
            kickAimWorld = Vector3.zero;
            aiShotTargetZ = targetZ;
            passPending = false;
            BeginKick();
            return true;
        }

        bool passWhipped;
        float passPower01 = 0.5f;

        /// <summary>
        /// Passe rasteiro, passe em profundidade, lançamento ou cruzamento com curva.
        /// Usa as animações nativas PasseCurto / PasseLongo para resposta ágil estilo eFootball.
        /// </summary>
        public bool Pass(Vector3 target, bool lofted, float error01, float power01 = 0.5f, bool whippedCross = false)
        {
            if (Busy || ball == null) return false;
            Charging = false;
            passTarget = new Vector3(target.x, 0f, target.z);
            passLofted = lofted;
            passWhipped = whippedCross;
            passPower01 = Mathf.Clamp01(power01);
            passError = Mathf.Clamp01(error01);
            passPending = true;
            kickType = lofted ? TipoChute.Colocado : TipoChute.Rasteiro;
            kickPower = passPower01;
            kickAim = Vector2.zero;
            BeginKick();
            return true;
        }

        void BeginKick()
        {
            kickPending = true;
            kickStartedAt = Time.time;

            if (passPending)
            {
                bool hasShort = anim != null && anim.HasState(0, Animator.StringToHash("PasseCurto"));
                bool hasLong = anim != null && anim.HasState(0, Animator.StringToHash("PasseLongo"));
                activeKickState = passLofted
                    ? (hasLong ? "PasseLongo" : "ChuteColocado")
                    : (hasShort ? "PasseCurto" : "ChuteRasteiro");
                if (ball != null && !ball.Grounded && ball.transform.position.y > 1.35f)
                    activeKickState = "Cabeceio";
                if (anim != null) anim.CrossFadeInFixedTime(activeKickState, 0.05f, 0, 0f);

                float passContact = passLofted ? 0.21f : 0.14f;
                kickFallbackContactAt = Time.time + passContact;
                lockMoveUntil = Time.time + passContact + (passLofted ? 0.20f : 0.14f);
                return;
            }

            activeKickState = kickType == TipoChute.Colocado ? "ChuteColocado" :
                              (kickType == TipoChute.Cavadinha ? "Cavadinha" :
                              (kickType == TipoChute.Rasteiro ? "ChuteRasteiro" : "ChuteForte"));
            if (ball != null && !ball.Grounded)
            {
                if (ball.transform.position.y > 1.35f) activeKickState = "Cabeceio";
                else if (ball.transform.position.y > 0.82f && kickType == TipoChute.Forte) activeKickState = "Voleio";
            }
            if (anim != null) anim.CrossFadeInFixedTime(activeKickState, 0.07f, 0, 0f);

            float fallback = kickType == TipoChute.Colocado ? 0.34f : (kickType == TipoChute.Cavadinha ? 0.28f : 0.24f);
            kickFallbackContactAt = Time.time + fallback;
            lockMoveUntil = Time.time + fallback + 0.28f;
        }

        /// <summary>
        /// Avalia se a animação de chute/passe entrou na janela de impacto E o pé já avançou à frente do corpo,
        /// garantindo que a bola só saia quando o pé visualmente encosta nela.
        /// </summary>
        void CheckKickWindowContact()
        {
            if (!kickPending) return;

            float minNorm = passPending ? (passLofted ? 0.28f : 0.22f) : (kickType == TipoChute.Colocado ? 0.36f : 0.26f);
            float maxNorm = minNorm + 0.28f;
            bool inImpactWindow = false;
            if (anim != null && !string.IsNullOrEmpty(activeKickState))
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                var next = anim.GetNextAnimatorStateInfo(0);
                if (st.IsName(activeKickState))
                {
                    float norm = st.normalizedTime % 1f;
                    if (norm >= minNorm && norm <= maxNorm) inImpactWindow = true;
                }
                else if (next.IsName(activeKickState))
                {
                    float norm = next.normalizedTime % 1f;
                    if (norm >= minNorm && norm <= maxNorm) inImpactWindow = true;
                }
            }

            bool footForward = true;
            if (activeKickState != "Cabeceio" && activeKickState != "Voleio" && (rToe != null || lToe != null))
            {
                float thr = (passPending ? 0.06f : 0.11f) * Mathf.Max(0.75f, transform.lossyScale.y);
                float fR = rToe != null ? Vector3.Dot(rToe.position - transform.position, transform.forward) : -1f;
                float fL = lToe != null ? Vector3.Dot(lToe.position - transform.position, transform.forward) : -1f;
                footForward = Mathf.Max(fR, fL) > thr;
            }

            if ((inImpactWindow && footForward) || Time.time >= kickFallbackContactAt + (passPending ? 0.04f : 0.08f))
            {
                DoContact();
            }
        }

        void DoContact()
        {
            kickPending = false;
            Charge = 0f;
            if (ball == null || ball.IsHeld || !ballAuthority) { passPending = false; return; }

            Vector3 bp = ball.Body.position;
            Transform kickingFoot;
            Vector3 footContactPos;
            float footDist = FootDistance(bp, out kickingFoot, out footContactPos);

            Vector3 strike = ControlPointWorld();
            float rootStrikeDist = Vector3.Distance(new Vector3(bp.x, 0f, bp.z), new Vector3(strike.x, 0f, strike.z));
            float effectiveOff = Mathf.Min(footDist, rootStrikeDist);

            // Valida se a bola está dentro do alcance do pé (Foot_R / Foot_L) ou do ponto de batida (ou cabeça/voleio no ar)
            float maxBallY = (activeKickState == "Cabeceio" || activeKickState == "Voleio") ? 2.35f : 0.95f;
            if (effectiveOff > kickFootReach || bp.y > maxBallY) return;

            Vector3 spin;
            ballVelAtContact = ball.Velocity;
            // Aplica vetor de impulso determinístico (Direção + Força + Elevação + Efeito Magnus),
            // ignorando o ângulo torto da malha 3D do pé
            Vector3 v = passPending ? ComputePass(bp, effectiveOff, out spin) : ComputeShot(kickType, kickPower, kickAim, effectiveOff, out spin);
            passPending = false;
            aiShotTargetZ = float.NaN;
            GameSettings.Vibrar();
            ball.Kick(v, spin);
            HasPossession = false;
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.Kick(bp, Mathf.Clamp01(v.magnitude / 30f));

            // Particle & Audio: Kick effects
            float kickPowerNormalized = Mathf.Clamp01(v.magnitude / 30f);
            Vector3 kickDirection = v.normalized;

            // Kick dust at foot position
            if (ParticleEffectsManager.Instance != null)
            {
                ParticleEffectsManager.Instance.PlayKickDust(footContactPos, -kickDirection, kickPowerNormalized);
                ParticleEffectsManager.Instance.PlayKickImpact(bp, kickDirection, kickPowerNormalized);

                // Grass scorch for very powerful kicks
                if (kickPowerNormalized > 0.8f && bp.y < 0.5f)
                    ParticleEffectsManager.Instance.PlayGrassScorch(footContactPos);
            }

            // Audio: Kick sound with power variation
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayKick(bp, kickPowerNormalized);
            }
        }

        Vector3 ControlPointWorld()
        {
            if (sockets != null && sockets.BallControlPoint != null)
                return sockets.BallControlPoint.position;
            return transform.position + transform.forward * 0.40f;
        }

        // ------------------------------------------------------------ Dribles (Finta / Puxada / Parar) e Arrancada

        void HandleSkillInput(Vector2 mv)
        {
            bool human = InputEnabled && !aiControlled;
            if (human && Time.unscaledTime < blockUntil) { prevSkill = prevKnock = true; return; }
            bool skill = human && InputBridge.Skill;
            bool knock = human && InputBridge.KnockOn;
            if (skill && !prevSkill) TrySkill();
            if (knock && !prevKnock) knockQueued = true;
            if (!knock && knockQueued && Time.time - lastTouchAt > 1.5f && !HasPossession) knockQueued = false;
            prevSkill = skill; prevKnock = knock;
        }

        void Move(string name) { LastMove = name; LastMoveTime = Time.time; }

        /// <summary>IA: corte/finta/puxada na direção pedida (mesmo drible do botão).</summary>
        public void AISkill(Vector3 wish)
        {
            if (!aiControlled || Busy) return;
            wishWorld = wish;
            TrySkill();
        }

        void TrySkill()
        {
            if (ball == null || Charging || kickPending || Time.time < skillCooldown) return;
            Vector3 bp = ball.Body.position;
            Vector3 to = bp - transform.position; to.y = 0f;
            if (to.magnitude > 1.6f || bp.y > 0.5f) return;
            skillCooldown = Time.time + 0.55f;
            Vector3 fwd = transform.forward;
            Vector3 w = wishWorld;
            Vector3 bv = ball.Velocity;
            if (w.sqrMagnitude < 0.09f)
            {
                // Parar a bola com a sola no BallControlPoint
                Compat.SetVelocity(ball.Body, new Vector3(0f, bv.y, 0f));
                ball.Body.angularVelocity = Vector3.zero;
                velocity *= 0.25f;
                touchEnd = 0f; nextTouchAt = Time.time + 0.25f; lastTouchAt = Time.time;
                if (anim != null) anim.CrossFadeInFixedTime("Dominio", 0.08f, 0, 0f);
                Move("PAROU A BOLA");
                return;
            }
            Vector3 wd = w.normalized;
            float dot = Vector3.Dot(wd, fwd);
            bool toRight = Vector3.Dot(Vector3.Cross(Vector3.up, fwd), wd) >= 0f;
            if (dot < -0.45f)
            {
                // Puxada (meia-lua para trás): bola volta com rotação compatível, jogador gira
                Play(toRight ? "PuxadaDir" : "PuxadaEsq", 0.08f);
                Vector3 vPull = new Vector3(wd.x * 3.2f, bv.y, wd.z * 3.2f);
                Compat.SetVelocity(ball.Body, vPull);
                ball.Body.angularVelocity = Vector3.Cross(Vector3.up, wd) * (3.2f / Arena.BallRadius);
                velocity = wd * 1.2f;
                transform.rotation = Quaternion.LookRotation(Vector3.Slerp(fwd, wd, 0.6f));
                StartTouchDecay(wd, 3.2f, 0.5f);
                Move("PUXADA");
            }
            else
            {
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                if (Vector3.Dot(side, wd) < 0f) side = -side;
                Vector3 dir = (side * 0.85f + fwd * 0.45f).normalized;
                if (velocity.magnitude < 2.5f)
                {
                    // devagar: corte seco, bola e corpo saem juntos para o lado
                    ExecuteCut(dir);
                    if (anim != null) anim.CrossFadeInFixedTime("Drible", 0.08f, 0, 0f);
                    Move("CORTE");
                }
                else
                {
                    // em velocidade: finta de corpo ou pedalada; a bola sai para o lado depois da finta
                    bool pedalada = Random.value < 0.45f;
                    string st = (pedalada ? "Pedalada" : "FintaCorpo") + (toRight ? "Dir" : "Esq");
                    if (anim != null && anim.HasState(0, Animator.StringToHash(st)))
                    {
                        Play(st, 0.08f);
                        cutDir = dir;
                        cutAt = Time.time + (pedalada ? 0.5f : 0.4f);  // ~quadro em que o pé de saída leva a bola (ritmo 1,3x)
                        velocity *= 0.8f;
                        skillCooldown = Time.time + 0.9f; // a jogada inteira dura ~0,8 s
                        Move(pedalada ? "PEDALADA" : "FINTA DE CORPO");
                    }
                    else
                    {
                        ExecuteCut(dir);
                        if (anim != null) anim.CrossFadeInFixedTime("Drible", 0.08f, 0, 0f);
                        Move("CORTE");
                    }
                }
            }
            lastTouchAt = Time.time;
            nextTouchAt = Time.time + 0.3f;
        }

        // finta/pedalada em andamento: direção e instante em que a bola sai para o lado
        Vector3 cutDir;
        float cutAt = -1f, freadaCooldown;

        /// <summary>Bola e corpo saem juntos na direção do drible (corte, ou o fim da finta/pedalada).</summary>
        void ExecuteCut(Vector3 dir)
        {
            if (ball == null) return;
            Vector3 bv = ball.Velocity;
            float sp = Mathf.Max(velocity.magnitude * 0.8f, 3.2f);
            Vector3 vCut = new Vector3(dir.x * (sp + 2.2f), bv.y, dir.z * (sp + 2.2f));
            Compat.SetVelocity(ball.Body, vCut);
            ball.Body.angularVelocity = Vector3.Cross(Vector3.up, dir) * ((sp + 2.2f) / Arena.BallRadius);
            velocity = dir * sp;
            burstUntil = Time.time + 0.45f;
            StartTouchDecay(dir, 2.2f, 0.45f);
            lastTouchAt = Time.time;
            nextTouchAt = Time.time + 0.3f;
        }

        void UpdatePendingCut()
        {
            if (cutAt < 0f || Time.time < cutAt) return;
            cutAt = -1f;
            if (ball == null || Incapacitated || Sliding) return;
            Vector3 to = ball.Body.position - transform.position; to.y = 0f;
            if (to.magnitude < 1.4f && ball.Body.position.y < 0.5f) ExecuteCut(cutDir); // perdeu a bola na finta: não corta
        }

        void StartTouchDecay(Vector3 dir, float extra, float duration)
        {
            touchDir = dir;
            touchEnd = Time.time + duration;
            touchDecel = 2f * extra / Mathf.Max(0.1f, duration);
        }

        // ------------------------------------------------------------ 3. Sistema Híbrido de Interação com a Bola (Condução Magnética + Toques)

        float prevSprintTap = -10f;
        bool prevSprintState;

        void FixedUpdate()
        {
            HasPossession = false;
            if (ball == null || ball.Body.isKinematic) return;
            if (Sliding && !slideTouched) CheckSlideBall();
            if (!ballAuthority || Incapacitated || Sliding) { touchEnd = 0f; return; }

            float dt = Time.fixedDeltaTime;
            float now = Time.time;
            Vector3 pos = transform.position;
            Vector3 bp = ball.Body.position;
            Vector3 controlPt = ControlPointWorld();

            // Toque duplo em R2 / Sprint = Adiantada (Sharp Touch estilo eFootball)
            bool human = InputEnabled && !aiControlled;
            if (human && sprintHeld && !prevSprintState)
            {
                if (now - prevSprintTap < 0.28f && wishWorld.sqrMagnitude > 0.15f)
                    knockQueued = true;
                prevSprintTap = now;
            }
            prevSprintState = sprintHeld;

            Vector3 toBallFromRoot = bp - pos; toBallFromRoot.y = 0f;
            float distFromRoot = toBallFromRoot.magnitude;

            Vector3 toControlPt = controlPt - bp; toControlPt.y = 0f;
            float distToControlPt = toControlPt.magnitude;

            Vector3 bv = ball.Velocity;
            Vector3 flatBv = new Vector3(bv.x, 0f, bv.z);
            Vector3 rel = flatBv - velocity;
            bool trapHeight = bp.y < 1.18f;
            bool low = bp.y < 0.44f;
            bool recentlyKicked = now - ball.LastKickTime < 0.24f;
            bool controlled = InputEnabled || aiControlled;

            // Preparando chute ou passe: acomoda suavemente a bola no ponto de batida à frente do pé de apoio
            if (kickPending && bp.y < 0.95f && distFromRoot < 2.2f)
            {
                HasPossession = true;
                float fwdDist = passPending ? 0.36f : 0.42f;
                Vector3 strikeSpot = pos + transform.forward * (fwdDist * Mathf.Max(0.8f, transform.lossyScale.y)) + transform.right * 0.07f;
                Vector3 desired = velocity * 0.55f + (strikeSpot - bp) * (passPending ? 15f : 12f);
                desired.y = 0f;
                Vector3 nv = Vector3.MoveTowards(flatBv, desired, 44f * Mathf.Lerp(0.75f, 1f, Grip()) * dt);
                float vy = bp.y > Arena.BallRadius + 0.08f ? Mathf.Min(bv.y, -1.4f) : bv.y;
                Compat.SetVelocity(ball.Body, new Vector3(nv.x, vy, nv.z));
                ball.Body.angularVelocity = Vector3.Cross(Vector3.up, nv) / Arena.BallRadius;
                return;
            }
            if (!controlled || !trapHeight || recentlyKicked) { touchEnd = 0f; return; }

            if (now < touchEnd && distFromRoot < 4.5f && low)
            {
                float along = Vector3.Dot(flatBv, touchDir);
                float roll = WeatherSystem.Instance != null ? WeatherSystem.Instance.RollingDeceleration : 1.1f;
                float dec = Mathf.Max(0f, touchDecel - roll) * dt;
                if (along > 0.6f)
                {
                    flatBv -= touchDir * Mathf.Min(dec, along - 0.6f);
                    Compat.SetVelocity(ball.Body, new Vector3(flatBv.x, bv.y, flatBv.z));
                }
            }

            float spd = velocity.magnitude;
            float grip = Grip();
            Vector3 wish = wishWorld;
            float wm = Mathf.Clamp01(wish.magnitude);
            Vector3 wd = wm > 0.14f ? wish / Mathf.Max(wish.magnitude, 1e-3f) : transform.forward;

            // Verifica alcance físico do domínio (pés/peito/coxa à frente do atleta)
            float reach = 0.88f + 0.05f * spd;
            Vector3 dirTo = distFromRoot > 1e-3f ? toBallFromRoot / distFromRoot : transform.forward;
            bool inFront = Vector3.Dot(dirTo, transform.forward) > -0.30f || distFromRoot < 0.50f;
            bool inControlZone = distToControlPt < possessionRadius + 0.10f || (distFromRoot < reach && inFront);
            bool inReach = inControlZone && rel.magnitude < 16.5f;

            HasPossession = (inReach && low) || (now - lastTouchAt < 0.85f && distFromRoot < 2.8f && low);
            if (!inReach) return;

            // 1) DOMÍNIO ORIENTADO AMORTECIDO (bola vindo rápida ou a meia-altura: amortece na direção do analógico sem elástico!)
            bool incomingFast = rel.magnitude > 4.6f && now - lastTouchAt > 0.30f;
            bool aerialTrap = bp.y >= 0.40f && now - lastTouchAt > 0.28f;
            if (incomingFast || aerialTrap)
            {
                Vector3 trapLead = pos + wd * (wm > 0.15f ? (sprintHeld ? 0.58f : 0.40f) : 0.34f);
                Vector3 toTrap = trapLead - bp; toTrap.y = 0f;
                float carryShare = wm > 0.15f ? (sprintHeld ? 1.08f : 0.90f) : 0.25f;
                float residual = Mathf.Lerp(0.08f, 0.22f, 1f - grip);
                Vector3 nv = wd * (spd * carryShare) + toTrap * 4.2f + rel * residual;
                nv = Vector3.ClampMagnitude(nv - velocity, sprintHeld ? 3.8f : 2.6f) + velocity;
                float vy = bp.y > Arena.BallRadius + 0.06f ? Mathf.Clamp(bv.y * 0.2f - 1.6f, -3.2f, 0.15f) : Mathf.Min(bv.y, 0.15f);
                Compat.SetVelocity(ball.Body, new Vector3(nv.x, vy, nv.z));
                ball.Body.angularVelocity = Vector3.Cross(Vector3.up, nv) / Arena.BallRadius;

                if (!Busy && anim != null)
                {
                    if (bp.y > 0.52f) Play("Dominio", 0.06f);
                    else if (rel.magnitude > 8.0f && spd < 3.2f) Play("DominioCurto", 0.06f, "Dominio");
                }
                lastTouchAt = now;
                nextTouchAt = now + 0.18f;
                touchEnd = 0f;
                HasPossession = true;
                Move("DOMÍNIO");
                return;
            }

            if (!low) return;

            // 2) PROTEÇÃO DE BOLA / PARADO (analógico solto: acomoda a bola suavemente nos pés sem vibrar)
            if (wm < 0.14f && spd < 1.15f && !knockQueued)
            {
                Vector3 settleTarget = pos + transform.forward * (0.35f * Mathf.Max(0.85f, transform.lossyScale.y));
                Vector3 errPos = settleTarget - bp; errPos.y = 0f;
                Vector3 desiredVel = Vector3.ClampMagnitude(errPos * 6.5f, 2.2f);
                Vector3 nv = Vector3.MoveTowards(flatBv, desiredVel, 22f * grip * dt);
                if (errPos.magnitude < 0.03f && nv.magnitude < 0.08f) nv = Vector3.zero;
                Compat.SetVelocity(ball.Body, new Vector3(nv.x, Mathf.Min(bv.y, 0.1f), nv.z));
                ball.Body.angularVelocity = nv.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, nv) / Arena.BallRadius : Vector3.zero;
                nextTouchAt = now + 0.08f;
                touchEnd = 0f;
                return;
            }

            // 3) CONDUÇÃO NATURAL SINCRONIZADA COM A PASSADA (sem puxão magnético para trás!)
            // Alvo natural de condução à frente do atleta conforme velocidade e Dash (R2)
            float leadDist = sprintHeld
                ? Mathf.Lerp(0.50f, 0.82f, Mathf.Clamp01(spd / Mathf.Max(1f, sprintSpeed)))
                : Mathf.Lerp(0.34f, 0.48f, Mathf.Clamp01(spd / Mathf.Max(1f, jogSpeed)));
            Vector3 ballDir = flatBv.sqrMagnitude > 0.20f ? flatBv.normalized : wd;
            bool sharpTurn = wm > 0.14f && Vector3.Dot(ballDir, wd) < 0.68f;
            if (sharpTurn) leadDist *= 0.72f;

            Vector3 idealBallPos = pos + wd * (leadDist * Mathf.Max(0.85f, transform.lossyScale.y));
            Vector3 toIdeal = idealBallPos - bp; toIdeal.y = 0f;

            // Adianta a bola com explosão (Adiantada / Sharp Touch)
            if (knockQueued)
            {
                knockQueued = false;
                float baseSpd = Mathf.Max(spd, wm * sprintSpeed * 0.82f);
                float extraK = Random.Range(3.4f, 4.3f) * Mathf.Lerp(1.10f, 1f, grip);
                Vector3 vk = wd * (baseSpd + extraK);
                Compat.SetVelocity(ball.Body, new Vector3(vk.x, Mathf.Min(bv.y, 0.22f), vk.z));
                ball.Body.angularVelocity = Vector3.Cross(Vector3.up, wd) * (vk.magnitude / Arena.BallRadius) * 0.88f;
                burstUntil = now + 1.15f;
                lastTouchAt = now;
                nextTouchAt = now + 0.48f;
                Play("Arrancada", 0.08f);
                Move("ADIANTADA");
                return;
            }

            // Entre um toque e outro da passada, guia suavemente sem travar o rolamento natural na grama
            if (now < nextTouchAt && !sharpTurn && toIdeal.magnitude < 0.65f)
            {
                Vector3 guideVel = velocity + toIdeal * (sprintHeld ? 4.8f : 6.2f);
                guideVel.y = 0f;
                Vector3 nv = Vector3.MoveTowards(flatBv, guideVel, (sprintHeld ? 14f : 18f) * grip * dt);
                Compat.SetVelocity(ball.Body, new Vector3(nv.x, Mathf.Min(bv.y, 0.18f), nv.z));
                if (nv.sqrMagnitude > 0.02f)
                    ball.Body.angularVelocity = Vector3.Cross(Vector3.up, nv) / Arena.BallRadius;
                return;
            }

            if (sharpTurn && now - lastTouchAt < 0.09f) return;

            Transform foot;
            Vector3 footPos;
            float fd = FootDistance(bp, out foot, out footPos);
            bool footReady = fd < Arena.BallRadius + 0.28f || distToControlPt < 0.42f || sharpTurn || now > nextTouchAt + 0.18f || foot == null;
            if (!footReady) return;

            // Toque cadenciado da chuteira na bola: leve impulso à frente que decai naturalmente pelo atrito da grama
            float runAlign = spd > 0.2f ? Mathf.Max(0.25f, Vector3.Dot(velocity.normalized, wd)) : 1f;
            float carrySpeed = Mathf.Max(spd * runAlign, wm * (sprintHeld ? sprintSpeed : jogSpeed) * 0.82f);
            float touchPush = sprintHeld ? Random.Range(1.15f, 1.55f) : Random.Range(0.65f, 0.95f);
            if (sharpTurn) { carrySpeed *= 0.82f; touchPush = 0.75f; }

            Vector3 vTouch = wd * (carrySpeed + touchPush) + toIdeal * 2.8f;
            vTouch.y = 0f;
            Compat.SetVelocity(ball.Body, new Vector3(vTouch.x, Mathf.Min(bv.y, 0.18f), vTouch.z));
            ball.Body.angularVelocity = Vector3.Cross(Vector3.up, vTouch) / Arena.BallRadius;

            float cadence = sprintHeld ? 0.27f : 0.23f;
            lastTouchAt = now;
            nextTouchAt = now + cadence * Random.Range(0.94f, 1.06f);
        }

        // ------------------------------------------------------------ Cálculo Determinístico do Chute

        Vector3 ComputeShot(TipoChute t, float power, Vector2 aim, float offFoot, out Vector3 spin)
        {
            Vector3 from = ball.Body.position;
            // força efetiva: um toque rápido já sai com ~40% (antes ~10%: chute "mascado") e a curva cresce rápido no começo
            float pw = Mathf.Lerp(0.4f, 1f, Mathf.Sqrt(Mathf.Clamp01(power)));
            Vector3 fwd = transform.forward;
            float dirX = attackDir >= 0f ? 1f : -1f;
            float goalX = dirX * (Arena.HalfLength + 0.25f);
            spin = Vector3.zero;

            Vector3 camF = Vector3.forward, camR = Vector3.right;
            if (cameraTransform != null)
            {
                camF = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                camR = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }
            Vector3 aimW = aiControlled ? kickAimWorld : camF * aim.y + camR * aim.x;

            bool towardGoal = fwd.x * dirX > 0.15f && (goalX - from.x) * dirX > 1.5f;
            float speed;
            Vector3 v;

            if (!towardGoal)
            {
                speed = Mathf.Lerp(12f, 30f, pw);
                Vector3 dir = aimW.sqrMagnitude > 0.06f ? aimW.normalized : fwd;
                float elev = t == TipoChute.Cavadinha ? 35f : (t == TipoChute.Colocado ? 9f : (t == TipoChute.Rasteiro ? 1.5f : Mathf.Lerp(3f, 16f, power)));
                v = Quaternion.AngleAxis(-elev, Vector3.Cross(Vector3.up, dir)) * dir * speed;
                if (t == TipoChute.Colocado) spin = Vector3.up * 35f;
                return ApplyError(v, t, power, offFoot);
            }

            float lat;
            if (aiControlled && !float.IsNaN(aiShotTargetZ)) lat = Mathf.Clamp(aiShotTargetZ / (Arena.GoalHalfWidth - 0.45f), -1f, 1f);
            else if (aimW.sqrMagnitude > 0.06f) lat = Mathf.Clamp(aimW.z * 1.35f, -1f, 1f);
            else
            {
                float zAtGoal = from.z + fwd.z / Mathf.Max(fwd.x * dirX, 0.15f) * ((goalX - from.x) * dirX);
                lat = Mathf.Clamp(zAtGoal / Arena.GoalHalfWidth, -1f, 1f);
            }
            float tz = lat * (Arena.GoalHalfWidth - 0.45f);
            float ty;
            switch (t)
            {
                case TipoChute.Forte: ty = Mathf.Lerp(0.25f, 2.0f, Mathf.Pow(power, 1.5f)); speed = Mathf.Lerp(15f, 34f, pw); break;
                case TipoChute.Colocado: ty = Mathf.Lerp(0.5f, 1.75f, power); speed = Mathf.Lerp(13f, 26f, pw); break;
                case TipoChute.Rasteiro: ty = 0.14f; speed = Mathf.Lerp(15f, 30f, pw); break;
                default: ty = 2.0f; speed = 0f; break;
            }
            Vector3 T = new Vector3(goalX, ty, tz);
            Vector3 flat0 = new Vector3(T.x - from.x, 0f, T.z - from.z).normalized;
            if (t != TipoChute.Cavadinha)
            {
                speed += 0.35f * Mathf.Max(0f, Vector3.Dot(velocity, flat0));
                speed += 0.18f * Mathf.Max(0f, -Vector3.Dot(ballVelAtContact, flat0));
                speed *= 1f - 0.25f * Mathf.Clamp01(offFoot / 0.85f);
                speed = Mathf.Min(speed, 36f);
            }

            // Efeito Magnus determinístico
            Vector3 flatDir = new Vector3(T.x - from.x, 0f, T.z - from.z).normalized;
            Vector3 backAxis = Vector3.Cross(flatDir, Vector3.up);
            if (t == TipoChute.Colocado)
            {
                float side = Mathf.Abs(tz - from.z) > 0.3f ? Mathf.Sign(tz - from.z) : (tz >= 0f ? 1f : -1f);
                float zComp = Vector3.Cross(Vector3.up, flatDir).z;
                spin = Vector3.up * (Mathf.Lerp(30f, 55f, power) * side * Mathf.Sign(zComp == 0f ? 1f : zComp));
                spin += backAxis * 8f;
            }
            else if (t == TipoChute.Forte)
            {
                spin = backAxis * Random.Range(-10f, 6f) + Vector3.up * Random.Range(-3.5f, 3.5f);
            }
            else if (t == TipoChute.Rasteiro)
            {
                spin = -backAxis * Mathf.Lerp(10f, 22f, power);
            }
            else
            {
                spin = backAxis * Mathf.Lerp(18f, 30f, power);
            }

            var wsx = WeatherSystem.Instance;
            float wetK = (wsx != null && wsx.P != null) ? Mathf.Lerp(1f, 0.78f, wsx.P.wetness) : 1f;
            spin = spin * wetK + Vector3.up * Random.Range(-1f, 1f) * offFoot * 24f;

            Vector3 A = T;
            v = Vector3.zero;
            for (int it = 0; it < 5; it++)
            {
                v = t == TipoChute.Cavadinha ? LobVelocity(from, A, Mathf.Lerp(32f, 44f, power)) : Ballistic(from, A, speed);
                Vector3 hit; float time;
                if (BallPhysics.PredictPlaneCrossing(from, v, spin, Vector3.zero, T.x, 4f, out hit, out time))
                {
                    Vector3 e = T - hit; e.x = 0f;
                    if (e.magnitude < 0.03f) break;
                    A += e;
                }
                else A.y += 0.6f;
            }
            return ApplyError(v, t, power, offFoot);
        }

        Vector3 ApplyError(Vector3 v, TipoChute t, float power, float offFoot)
        {
            float sigma;
            float lift = 0f;
            switch (t)
            {
                case TipoChute.Forte:
                    sigma = 0.85f + Mathf.Max(0f, power - 0.78f) * 12f;
                    lift = Mathf.Max(0f, power - 0.88f) * 28f;
                    break;
                case TipoChute.Colocado: sigma = 0.6f + Mathf.Max(0f, power - 0.85f) * 7f; break;
                case TipoChute.Rasteiro: sigma = 0.7f + Mathf.Max(0f, power - 0.85f) * 5f; break;
                default: sigma = 1.0f; break;
            }
            if (aiControlled ? aiSprint : InputBridge.Sprint) sigma += 1.0f;
            sigma += velocity.magnitude * 0.08f + offFoot * 3.2f;
            sigma *= accuracyScale;
            var ws = WeatherSystem.Instance;
            if (ws != null) sigma += ws.Current == Clima.Tempestade ? 0.85f : (ws.Current == Clima.Chuva ? 0.35f : 0f);

            float yaw = Gauss() * sigma;
            float pitch = Gauss() * sigma * 0.7f + lift;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            Vector3 right = Vector3.Cross(Vector3.up, flat.normalized);
            v = Quaternion.AngleAxis(yaw, Vector3.up) * v;
            v = Quaternion.AngleAxis(-pitch, right) * v;
            return v;
        }

        static float Gauss()
        {
            float u1 = Mathf.Max(1e-6f, Random.value), u2 = Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        static Vector3 Ballistic(Vector3 from, Vector3 to, float speed)
        {
            float g = -Physics.gravity.y;
            Vector3 d = to - from;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float D = flat.magnitude;
            float dy = d.y;
            float v2 = speed * speed;
            float disc = v2 * v2 - g * (g * D * D + 2f * dy * v2);
            float ang;
            if (disc < 0f) ang = 45f * Mathf.Deg2Rad;
            else ang = Mathf.Atan((v2 - Mathf.Sqrt(disc)) / (g * D));
            Vector3 dir = flat.normalized;
            return dir * (speed * Mathf.Cos(ang)) + Vector3.up * (speed * Mathf.Sin(ang));
        }

        static Vector3 LobVelocity(Vector3 from, Vector3 to, float angleDeg)
        {
            float g = -Physics.gravity.y;
            Vector3 d = to - from;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float D = flat.magnitude;
            float th = angleDeg * Mathf.Deg2Rad;
            float denom = 2f * Mathf.Cos(th) * Mathf.Cos(th) * (D * Mathf.Tan(th) - d.y);
            float v = denom > 0.01f ? Mathf.Sqrt(g * D * D / denom) : 15f;
            v = Mathf.Clamp(v, 6f, 26f);
            Vector3 dir = flat.normalized;
            return dir * (v * Mathf.Cos(th)) + Vector3.up * (v * Mathf.Sin(th));
        }

        /// <summary>
        /// Contato corpo a corpo: separa as cápsulas considerando a escala real de cada jogador (altura e porte),
        /// troca quantidade de movimento bilateralmente conforme a massa de cada um e avisa a partida.
        /// </summary>
        Vector3 ResolvePlayerCollisions(Vector3 proposedPos)
        {
            if (bodyCapsule == null || Incapacitated) return proposedPos;
            float sc = Mathf.Max(0.5f, transform.lossyScale.y);
            float r = bodyCapsule.radius * sc;
            float h = bodyCapsule.height * sc;
            Vector3 a = proposedPos + Vector3.up * (r + 0.05f);
            Vector3 b = proposedPos + Vector3.up * Mathf.Max(r + 0.06f, h - r);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, r, overlapBuf, 1 << Arena.LayerPlayerBody, QueryTriggerInteraction.Ignore);

            Vector3 result = proposedPos;
            for (int i = 0; i < n; i++)
            {
                var hit = overlapBuf[i];
                if (hit == bodyCapsule || hit == null) continue;
                if (hit.attachedRigidbody == null || !hit.attachedRigidbody.isKinematic) continue;

                Vector3 oc = hit.transform.position;
                Vector3 dir = result - oc;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) dir = transform.right * 0.01f;
                float osc = Mathf.Max(0.5f, hit.transform.lossyScale.y);
                float otherR = hit is CapsuleCollider cc && cc.direction == 1 ? cc.radius * osc : hit.bounds.extents.x;
                float dist = dir.magnitude;
                float overlap = (r + otherR) - dist;
                if (overlap <= 0f) continue;
                Vector3 nrm = dir / dist;

                var otherPc = hit.GetComponent<PlayerController>();
                if (otherPc != null && otherPc.Incapacitated)
                {
                    // Jogador caído no gramado: desvio lateral suave sem empurrá-lo nem prender os pés
                    result += nrm * Mathf.Min(overlap * 0.45f, 0.08f);
                    continue;
                }
                Component other = otherPc;
                Vector3 ov = Vector3.zero;
                float om = 80f;
                if (otherPc != null) { ov = otherPc.Velocity; om = otherPc.bodyMass; }
                else
                {
                    var gk = hit.GetComponent<GoalkeeperAI>();
                    if (gk != null) { ov = gk.Velocity; om = 85f; other = gk; }
                }

                float share = om / Mathf.Max(1f, bodyMass + om);
                if (otherPc != null)
                {
                    // Separação dividida pela razão inversa das massas (o mais pesado cede menos terreno)
                    result += nrm * ((overlap + 0.01f) * share);
                    Vector3 otherPos = otherPc.transform.position - nrm * ((overlap + 0.01f) * (1f - share));
                    otherPos.x = Mathf.Clamp(otherPos.x, -(Arena.FenceX - 1f), Arena.FenceX - 1f);
                    otherPos.z = Mathf.Clamp(otherPos.z, -(Arena.FenceZ - 1f), Arena.FenceZ - 1f);
                    otherPos.y = 0f;
                    otherPc.transform.position = otherPos;
                }
                else
                {
                    result += nrm * (overlap + 0.01f);
                }

                // Troca bilateral de quantidade de movimento (choque parcialmente inelástico)
                float closing = Vector3.Dot(velocity - ov, -nrm);
                if (closing > 0f)
                {
                    if (!fallen) velocity += nrm * (closing * share * 1.02f);
                    if (otherPc != null && !otherPc.fallen) otherPc.ReceiveBodyImpulse(-nrm * (closing * (1f - share) * 1.02f));
                }
                NotifyContactPair(hit, otherPc, other, nrm, closing);
                if (otherPc != null) otherPc.NotifyContactPair(bodyCapsule, this, this, -nrm, closing);
            }
            return result;
        }

        internal void ReceiveBodyImpulse(Vector3 dv)
        {
            velocity += dv;
        }

        internal void NotifyContactPair(Collider hit, PlayerController otherPc, Component other, Vector3 nrm, float closing)
        {
            float last;
            if (closing > 0.6f && (!lastContact.TryGetValue(hit, out last) || Time.time - last > 0.45f))
            {
                lastContact[hit] = Time.time;
                if (OnBodyContact != null) OnBodyContact(otherPc, other, nrm, closing);
            }
        }

        // ------------------------------------------------------------ Passe

        Vector3 ComputePass(Vector3 from, float offFoot, out Vector3 spin)
        {
            Vector3 d = passTarget - from; d.y = 0f;
            float D = Mathf.Max(0.5f, d.magnitude);
            Vector3 dir = d / D;
            float sigma = (0.9f + passError * 7.5f + offFoot * 3.2f + velocity.magnitude * 0.10f) * accuracyScale;
            var ws = WeatherSystem.Instance;
            if (ws != null && ws.P != null) sigma += ws.P.wetness * 1.0f;
            dir = Quaternion.AngleAxis(Gauss() * sigma, Vector3.up) * dir;
            float lenErr = 1f + Gauss() * (0.025f + passError * 0.075f);
            float pBoost = Mathf.Lerp(0.88f, 1.24f, passPower01);
            Vector3 v;
            if (!passLofted)
            {
                float roll = ws != null ? ws.RollingDeceleration : 1.1f;
                float arrive = Mathf.Lerp(8.2f, 12.5f, Mathf.Clamp01(D / 38f)) * pBoost;
                float v0 = Mathf.Sqrt(arrive * arrive + 2f * roll * 1.65f * D) * lenErr;
                v0 = Mathf.Clamp(v0, 8.5f, 30f);
                v = dir * v0 + Vector3.up * 0.22f;
                spin = Vector3.Cross(Vector3.up, dir) * (v0 / Arena.BallRadius) * 0.92f;
            }
            else if (passWhipped)
            {
                // Cruzamento tenso com curva (Magnus lateral + topspin mergulhando na área estilo eFootball)
                float T = Mathf.Clamp(D / (23f * pBoost), 0.58f, 1.55f);
                float g = -Physics.gravity.y;
                float horiz = D / T * 1.06f * lenErr;
                v = dir * horiz + Vector3.up * (0.5f * g * T + 0.35f);
                float sideCurl = Mathf.Sign(from.z == 0f ? 1f : -from.z) * 26f;
                spin = Vector3.up * sideCurl + Vector3.Cross(Vector3.up, dir) * 12f;
            }
            else
            {
                float T = Mathf.Clamp(D / (20.5f * pBoost), 0.65f, 2.05f);
                float g = -Physics.gravity.y;
                float horiz = D / T * 1.07f * lenErr;
                v = dir * horiz + Vector3.up * (0.5f * g * T + 0.25f);
                spin = Vector3.Cross(dir, Vector3.up) * 14f;
            }
            passWhipped = false;
            return v;
        }

        // ------------------------------------------------------------ Quedas, contusões, trombadas e carrinho

        void UpdateBodyState(float dt)
        {
            if (fallen && (Time.time >= fallUntil || (fallStartedAt > 0f && Time.time - fallStartedAt > 7.5f)))
            {
                fallen = false;
                fallStartedAt = -1f;
                if (hurt)
                {
                    Play("CaidoDor", 0.22f, "Levantar");
                    hurtGetUpAt = Time.time + 1.55f;
                    getUpUntil = Time.time + 2.65f;
                }
                else
                {
                    Play("Levantar", 0.22f, "Locomocao");
                    getUpUntil = Time.time + 1.05f;
                }
            }
            if (hurt && hurtGetUpAt > 0f && Time.time >= hurtGetUpAt)
            {
                hurtGetUpAt = -1f;
                hurt = false;
                Play("Levantar", 0.25f, "Locomocao");
                getUpUntil = Time.time + 1.05f;
            }
            if (!fallen && !hurt && getUpUntil > 0f && Time.time >= getUpUntil)
            {
                getUpUntil = 0f;
                if (anim != null)
                {
                    var st = anim.GetCurrentAnimatorStateInfo(0);
                    if (st.IsName("Queda") || st.IsName("QuedaCostas") || st.IsName("CaidoDor") || st.IsName("Levantar"))
                        Play("Locomocao", 0.2f);
                }
            }
            if (slideUntil > 0f && Time.time >= slideUntil)
            {
                slideUntil = 0f;
                Play("CarrinhoFim", 0.15f, "Locomocao");
                lockMoveUntil = Time.time + 0.45f;
            }
            if (grounder != null) grounder.grounding = !fallen && Time.time >= getUpUntil && !Sliding;
        }
        float fallStartedAt = -1f;
        float hurtGetUpAt = -1f;
        float staggerUntil;
        public bool Staggered { get { return Time.time < staggerUntil; } }

        /// <summary>Toca um estado do Animator se ele existir (ou o alternativo).</summary>
        public void Play(string state, float blend, string fallback = null)
        {
            if (anim == null) return;
            if (anim.HasState(0, Animator.StringToHash(state))) anim.CrossFadeInFixedTime(state, blend, 0, 0f);
            else if (!string.IsNullOrEmpty(fallback) && anim.HasState(0, Animator.StringToHash(fallback))) anim.CrossFadeInFixedTime(fallback, blend, 0, 0f);
        }

        void CancelActions()
        {
            cutAt = -1f;
            Charging = false; Charge = 0f;
            kickPending = false; passPending = false;
            HasPossession = false;
            knockQueued = false;
        }

        /// <summary>Queda (trombada, falta, escorregão). lieSeconds: tempo no chão. injured: levanta segurando a contusão.</summary>
        public void Fall(Vector3 push, float lieSeconds, bool injured)
        {
            if (fallen) return;
            CancelActions();
            fallen = true;
            hurt = injured;
            slideUntil = 0f;
            fallStartedAt = Time.time;
            fallUntil = Time.time + Mathf.Clamp(lieSeconds, 0.65f, 6.5f);
            push.y = 0f;
            bool backward = push.sqrMagnitude > 0.01f && Vector3.Dot(transform.forward, push.normalized) < -0.15f;
            velocity = Vector3.ClampMagnitude(velocity * 0.38f + push * 0.72f, 4.2f);
            Vector3 p0 = transform.position; p0.y = 0f; transform.position = p0;
            if (push.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(backward ? -push.normalized : push.normalized);
            Play(backward ? "QuedaCostas" : "Queda", 0.1f, "Locomocao");
            if (grounder != null) grounder.grounding = false;
        }

        /// <summary>Desequilíbrio sem cair: recua o corpo e perde velocidade por um instante.</summary>
        public void Stagger(Vector3 push, bool head)
        {
            if (fallen || Sliding) return;
            Charging = false;
            push.y = 0f;
            velocity = velocity * 0.45f + Vector3.ClampMagnitude(push, 4f);
            staggerUntil = Time.time + 0.42f;
            Play(head ? "AtingidoCabeca" : (push.magnitude > 2.2f ? "Empurrado" : "Atingido"), 0.08f);
        }

        /// <summary>Carrinho na direção dada.</summary>
        public bool StartSlide(Vector3 dir)
        {
            if (Busy || fallen) return false;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            slideDir = dir.normalized;
            CancelActions();
            slideStart = Time.time;
            slideUntil = Time.time + 0.8f;
            slideTouched = false;
            velocity = slideDir * Mathf.Clamp(velocity.magnitude + 2f, 6.5f, 9.5f);
            transform.rotation = Quaternion.LookRotation(slideDir);
            Play("CarrinhoInicio", 0.08f, "Carrinho");
            if (ParticleEffectsManager.Instance != null)
                ParticleEffectsManager.Instance.PlaySlideDust(transform.position, slideDir, velocity.magnitude);
            return true;
        }

        /// <summary>Bote em pé: o jogador estica a perna na direção da bola.</summary>
        public void Lunge(Vector3 dir)
        {
            if (Busy) return;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                velocity += dir.normalized * 1.8f;
                transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
            lockMoveUntil = Time.time + 0.35f;
            Play("Dominio", 0.06f);
        }

        void CheckSlideBall()
        {
            Vector3 bp = ball.Body.position;
            if (bp.y > 0.55f) return;
            Transform foot; Vector3 fp;
            float fd = FootDistance(bp, out foot, out fp);
            Vector3 rel = bp - transform.position; rel.y = 0f;
            if (fd < 0.62f || (rel.magnitude < 0.95f && Vector3.Dot(rel.normalized, slideDir) > 0.2f))
            {
                slideTouched = true;
                Vector3 side = Vector3.Cross(Vector3.up, slideDir) * Random.Range(-0.8f, 0.8f);
                Vector3 v = (slideDir * 0.7f + side).normalized * Random.Range(5f, 9f) + Vector3.up * Random.Range(0.3f, 1.5f);
                ball.Deflect(v, Vector3.Cross(Vector3.up, v) * 20f);
                if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.Kick(bp, 0.45f);
                if (OnSlideBall != null) OnSlideBall(this);
            }
        }

        // ------------------------------------------------------------ IK (Olhar para a bola)

        void OnIK(int layer)
        {
            if (anim == null || ball == null) return;
            if (kickPending || Time.time < lockMoveUntil || Incapacitated || Sliding) { anim.SetLookAtWeight(0f); return; }
            Vector3 bp = ball.transform.position;
            float d = Vector3.Distance(bp, transform.position);
            // bodyWeight = 0 para não entortar o tronco; apenas leve acompanhamento de cabeça quando a bola está longe
            Vector3 lookTarget = d < 2.5f
                ? transform.position + transform.forward * 5f + Vector3.up * 1.45f
                : new Vector3(bp.x, Mathf.Max(bp.y, 1.0f), bp.z);
            float w = d < 2.5f ? 0.15f : Mathf.Clamp01(1.1f - d / 25f) * 0.45f;
            anim.SetLookAtWeight(w, 0f, 0.35f, 0f, 0f);
            anim.SetLookAtPosition(lookTarget);
        }
    }
}
