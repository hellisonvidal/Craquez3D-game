using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Craquez
{
    /// <summary>
    /// Goleiro com IA:
    /// - fecha o ângulo (fica na bissetriz bola–centro do gol, adianta conforme a distância);
    /// - lê o chute após um tempo de reação humano (pior com chuva/tempestade);
    /// - prevê a trajetória (arrasto, curva e vento) e escolhe: defesa parada, salto ou mergulho;
    /// - o mergulho tem alcance/velocidade físicos limitados (chutes no canto podem ser indefensáveis);
    /// - IK leva as mãos até a bola; resultado: encaixe, espalmada ou rebote (bola molhada escorrega).
    /// </summary>
    public class GoalkeeperAI : MonoBehaviour
    {
        public enum Estado { Posicionando, Defendendo, Recuperando, ComBola }

        public BallPhysics ball;
        public float maxShuffleSpeed = 4.2f;
        public float maxDiveTravel = 2.3f;      // deslocamento do corpo no mergulho (m)
        public float maxDiveSpeed = 6.8f;       // m/s
        public float handReach = 0.24f;         // raio da palma (a bola tem 0,11 m de raio)

        [Header("Controle humano (modo goleiro)")]
        public bool humanControlled;
        public Transform cameraTransform;
        bool prevDive, prevJump, prevBlock;
        bool shotInFlight;

        /// <summary>true = encaixou, false = espalmou/bloqueou.</summary>
        public event Action<bool> OnSave;

        public Estado State { get; private set; }

        Animator anim;
        Transform lHand, rHand, chest, hips;
        Vector3 velocity;
        float reactAt = -1f;
        float stateTime;
        bool resolved;
        float ikWeight;
        float readyWeight;   // reforço da postura de espera (a animação GK_Pronto já tem as mãos à frente)
        float recoverTime = 0.6f;
        FootGrounder grounder;
        Vector3 ikTarget;
        Vector3 diveFrom, diveTo;
        float diveStart, diveDuration, diveHeight;
        bool diving;
        float diveSide;
        static readonly int LateralHash = Animator.StringToHash("Lateral");
        static readonly int MirrorHash = Animator.StringToHash("Mirror");
        static readonly int DiveSpeedHash = Animator.StringToHash("DiveSpeed");

        Vector3 GoalCenter { get { return Arena.AttackGoalCenter; } }

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var relay = anim.GetComponent<IKRelay>();
                if (relay == null) relay = anim.gameObject.AddComponent<IKRelay>();
                relay.onIK = OnIK;
                if (anim.isHuman)
                {
                    lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                    rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                    chest = anim.GetBoneTransform(HumanBodyBones.Chest);
                    if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Spine);
                    hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    AddBoneCollider(hips, 0.2f);
                    AddBoneCollider(chest, 0.22f);
                    AddBoneCollider(anim.GetBoneTransform(HumanBodyBones.Head), 0.12f);
                    AddBoneCollider(lHand, 0.1f);
                    AddBoneCollider(rHand, 0.1f);
                    AddBoneCollider(anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg), 0.09f);
                    AddBoneCollider(anim.GetBoneTransform(HumanBodyBones.RightLowerLeg), 0.09f);
                }
                var rb = anim.gameObject.GetComponent<Rigidbody>();
                if (rb == null) rb = anim.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        static void AddBoneCollider(Transform bone, float worldRadius)
        {
            if (bone == null) return;
            var sc = bone.gameObject.AddComponent<SphereCollider>();
            float s = Mathf.Max(1e-4f, Mathf.Abs(bone.lossyScale.x));
            sc.radius = worldRadius / s;
            Compat.SetPhysicsMaterial(sc, "Goleiro", 0.6f, 0.6f, 0.25f, false);
        }

        void OnEnable() { BallPhysics.OnShot += HandleShot; }
        void OnDisable() { BallPhysics.OnShot -= HandleShot; }

        public void ResetTo(Vector3 pos)
        {
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(-Vector3.right);
            velocity = Vector3.zero;
            State = Estado.Posicionando;
            reactAt = -1f;
            shotInFlight = false;
            diving = false;
            resolved = false;
            ikWeight = 0f;
            if (anim != null)
            {
                anim.SetBool(MirrorHash, false);
                anim.Play("Locomocao", 0, 0f);
            }
        }

        // ------------------------------------------------------------ leitura do chute

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

            float reaction = Random.Range(0.17f, 0.27f);
            if (ws != null && ws.P != null) reaction += ws.P.gkReaction;
            // chutes de muito perto quase não dão tempo de leitura
            reactAt = Time.time + reaction;
            resolved = false;
        }

        void React()
        {
            reactAt = -1f;
            if (ball == null || ball.IsHeld) return;
            var ws = WeatherSystem.Instance;
            Vector3 wind = ws != null ? ws.Wind : Vector3.zero;
            Vector3 hit; float t;
            Vector3 pos = transform.position;
            if (!BallPhysics.PredictPlaneCrossing(ball.Body.position, ball.Velocity, ball.Body.angularVelocity, wind, pos.x, 3f, out hit, out t))
                return;
            if (Mathf.Abs(hit.z) > Arena.GoalHalfWidth + 1.2f || hit.y > Arena.GoalHeight + 0.8f) return; // vai para fora: só acompanha

            State = Estado.Defendendo;
            stateTime = 0f;
            ikTarget = hit;
            float lateral = Vector3.Dot(hit - pos, transform.right);
            float absLat = Mathf.Abs(lateral);

            if (absLat < 0.6f && hit.y < 2.0f)
            {
                // defesa no corpo / encaixe (IK leva as mãos)
                diving = false;
                PlayBlock();
            }
            else if (absLat < 1.3f && hit.y >= 2.0f)
            {
                StartJump(lateral * 0.6f, Mathf.Clamp(t, 0.25f, 0.6f), Mathf.Clamp(hit.y - 1.9f, 0.2f, 0.7f));
            }
            else
            {
                float wanted = Mathf.Clamp(absLat - 0.8f, 0f, maxDiveTravel);
                float travel = Mathf.Min(wanted, maxDiveSpeed * Mathf.Max(t, 0.05f));
                StartDive(Mathf.Sign(lateral), travel, Mathf.Clamp(t, 0.22f, 0.65f), Mathf.Clamp(hit.y - 0.55f, 0f, 1.3f) * 0.85f);
            }
        }

        void PlayBlock()
        {
            if (anim != null) { anim.SetBool(MirrorHash, false); anim.CrossFadeInFixedTime("GK_Defesa", 0.08f); }
        }

        void BeginDefend()
        {
            State = Estado.Defendendo;
            stateTime = 0f;
            resolved = false;
        }

        void StartDive(float side, float travel, float duration, float height)
        {
            BeginDefend();
            diving = true;
            diveSide = side;
            diveFrom = transform.position;
            diveTo = diveFrom + transform.right * side * travel;
            diveDuration = duration;
            diveHeight = height;
            diveStart = Time.time;
            if (anim != null)
            {
                // a animação base mergulha para a DIREITA do goleiro; espelha para a esquerda
                anim.SetBool(MirrorHash, side < 0f);
                // o quadro de voo (corpo esticado) acontece ~0,5 s após o início: acelera para coincidir com a chegada da bola
                anim.SetFloat(DiveSpeedHash, Mathf.Clamp(0.5f / Mathf.Max(0.15f, duration), 0.8f, 2.2f));
                anim.CrossFadeInFixedTime("GK_Mergulho", 0.05f, 0, 0.02f);
            }
        }

        void StartJump(float lateralOffset, float duration, float height)
        {
            BeginDefend();
            diving = true;
            diveSide = Mathf.Sign(lateralOffset == 0f ? 1f : lateralOffset);
            diveFrom = transform.position;
            diveTo = diveFrom + transform.right * lateralOffset;
            diveDuration = duration;
            diveHeight = height;
            diveStart = Time.time;
            if (anim != null) { anim.SetBool(MirrorHash, lateralOffset < 0f); anim.CrossFadeInFixedTime("GK_Salto", 0.08f, 0, 0.1f); }
        }

        // ------------------------------------------------------------ controle humano

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

            // altura assistida: se há chute no ar, as mãos procuram a altura prevista da bola
            float predictedY = 0.5f;
            if (ball != null && shotInFlight && !ball.IsHeld)
            {
                var ws = WeatherSystem.Instance;
                Vector3 hit; float t;
                if (BallPhysics.PredictPlaneCrossing(ball.Body.position, ball.Velocity, ball.Body.angularVelocity,
                        ws != null ? ws.Wind : Vector3.zero, transform.position.x, 2f, out hit, out t))
                    predictedY = hit.y;
            }

            if (diveDown)
            {
                float lat = Vector3.Dot(stick, transform.right);
                if (Mathf.Abs(lat) < 0.25f) { BeginDefend(); diving = false; PlayBlock(); ikTarget = ball != null ? ball.transform.position : transform.position + Vector3.up; }
                else StartDive(Mathf.Sign(lat), Mathf.Lerp(0.6f, maxDiveTravel, Mathf.Abs(lat)), 0.42f, Mathf.Clamp(predictedY - 0.7f, 0f, 1f) * 0.75f);
                return;
            }
            if (jumpDown) { StartJump(Vector3.Dot(stick, transform.right) * 0.6f, 0.4f, 0.5f); return; }
            if (blockDown) { BeginDefend(); diving = false; PlayBlock(); ikTarget = ball != null ? ball.transform.position : transform.position + Vector3.up; return; }

            float max = InputBridge.Sprint ? 6.2f : maxShuffleSpeed;
            velocity = Vector3.MoveTowards(velocity, stick * max, 16f * dt);
            Vector3 p = transform.position + velocity * dt;
            Vector3 g = GoalCenter;
            p.x = Mathf.Clamp(p.x, g.x - 16.5f, g.x - 0.3f);
            p.z = Mathf.Clamp(p.z, -20f, 20f);
            p.y = 0f;
            transform.position = p;
            if (ball != null)
            {
                Vector3 look = ball.transform.position - p; look.y = 0f;
                if (look.sqrMagnitude > 0.01f && Vector3.Dot(look.normalized, -Vector3.right) > 0.1f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 360f * dt);
            }
            // pega bola solta perto dele
            if (ball != null && !ball.IsHeld && Vector3.Distance(ball.transform.position, transform.position + transform.forward * 0.4f) < 0.7f
                && ball.Velocity.magnitude < 5f && ball.transform.position.y < 1.6f)
                Grab();
        }

        // ------------------------------------------------------------ loop

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            if (reactAt > 0f && Time.time >= reactAt) React();

            switch (State)
            {
                case Estado.Posicionando: if (humanControlled) UpdateHuman(dt); else UpdatePositioning(dt); break;
                case Estado.Defendendo: UpdateDefending(dt); break;
                case Estado.Recuperando:
                    velocity = Vector3.zero;
                    ikWeight = Mathf.MoveTowards(ikWeight, 0f, dt * 3f);
                    if (stateTime > recoverTime)
                    {
                        State = Estado.Posicionando; stateTime = 0f;
                        if (anim != null) anim.SetBool(MirrorHash, false);
                    }
                    break;
                case Estado.ComBola:
                    velocity = Vector3.zero;
                    ikWeight = Mathf.MoveTowards(ikWeight, 0.8f, dt * 3f);
                    if (diving && stateTime > 1.0f) { diving = false; if (anim != null) anim.CrossFadeInFixedTime("GK_Levantar", 0.3f); }
                    if (ball != null && ball.IsHeld) ikTarget = ball.transform.position;
                    break;
            }
            if (anim != null) anim.SetFloat(LateralHash, Vector3.Dot(velocity, transform.right), 0.1f, dt);
            // a animação de espera deixa os braços caídos e os cotovelos para trás: corrigimos com IK
            bool ready = State == Estado.Posicionando && !diving;
            if (grounder == null) grounder = GetComponent<FootGrounder>();
            if (grounder != null) grounder.grounding = ready || (State == Estado.Defendendo && !diving);
            readyWeight = Mathf.MoveTowards(readyWeight, ready ? 0.3f : 0f, dt * (ready ? 2.5f : 6f));
        }

        void UpdatePositioning(float dt)
        {
            ikWeight = Mathf.MoveTowards(ikWeight, 0f, dt * 3f);
            if (ball == null) return;
            Vector3 bp = ball.transform.position;
            Vector3 g = GoalCenter;
            Vector3 toBall = bp - g; toBall.y = 0f;
            float dist = toBall.magnitude;
            Vector3 target;
            if (toBall.x > -0.5f || dist < 0.5f) target = g + new Vector3(-0.5f, 0f, Mathf.Clamp(bp.z, -2.5f, 2.5f));
            else
            {
                float advance = Mathf.Clamp(dist * 0.11f, 0.6f, 3.6f);
                target = g + toBall.normalized * advance;
            }
            target.x = Mathf.Min(target.x, g.x - 0.4f);
            target.z = Mathf.Clamp(target.z, -Arena.GoalHalfWidth + 0.5f, Arena.GoalHalfWidth - 0.5f);
            target.y = 0f;

            // saída do gol em bola solta e lenta perto da área
            bool looseBall = !ball.IsHeld && ball.Grounded && ball.Velocity.magnitude < 4f && dist < 9f && Time.time - ball.LastKickTime > 0.6f;
            PlayerController pc = null;
            if (looseBall)
            {
                pc = FindPlayer();
                if (pc == null || Vector3.Distance(pc.transform.position, bp) > 2.2f)
                {
                    target = new Vector3(bp.x, 0f, bp.z);
                    if (Vector3.Distance(transform.position, target) < 0.85f) { Grab(); return; }
                }
            }

            Vector3 to = target - transform.position; to.y = 0f;
            float maxSpeed = looseBall ? 6.5f : maxShuffleSpeed;
            Vector3 desired = Vector3.ClampMagnitude(to * 3f, maxSpeed);
            velocity = Vector3.MoveTowards(velocity, desired, 14f * dt);
            transform.position += velocity * dt;

            Vector3 look = bp - transform.position; look.y = 0f;
            if (look.sqrMagnitude < 0.01f) look = -Vector3.right;
            // goleiro sempre "de frente" para o jogo
            if (Vector3.Dot(look.normalized, -Vector3.right) < 0.1f) look = Vector3.Lerp(look.normalized, -Vector3.right, 0.6f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 360f * dt);
        }

        PlayerController cachedPlayer;
        PlayerController FindPlayer()
        {
            if (cachedPlayer == null) cachedPlayer = FindAnyPlayer();
            return cachedPlayer;
        }

        static PlayerController FindAnyPlayer()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<PlayerController>();
#else
            return UnityEngine.Object.FindObjectOfType<PlayerController>();
#endif
        }

        void UpdateDefending(float dt)
        {
            velocity = Vector3.zero;
            ikWeight = Mathf.MoveTowards(ikWeight, 1f, dt * 7f);
            if (ball != null && !ball.IsHeld)
            {
                float d = Vector3.Distance(ball.transform.position, transform.position);
                if (d < 4f) ikTarget = Vector3.Lerp(ikTarget, ball.transform.position, 1f - Mathf.Exp(-25f * dt));
            }
            if (diving)
            {
                float k = Mathf.Clamp01((Time.time - diveStart) / diveDuration);
                float e = 1f - (1f - k) * (1f - k);
                Vector3 p = Vector3.Lerp(diveFrom, diveTo, e);
                p.y = diveHeight * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 0.85f));
                transform.position = p;
            }
            bool wasDive = diving && anim != null && (anim.GetCurrentAnimatorStateInfo(0).IsName("GK_Mergulho") || anim.GetNextAnimatorStateInfo(0).IsName("GK_Mergulho"));
            float total = diving ? 1.15f : 0.9f;
            if (stateTime > total)
            {
                State = Estado.Recuperando;
                stateTime = 0f;
                Vector3 p = transform.position; p.y = 0f; transform.position = p;
                diving = false;
                if (wasDive)
                {
                    // deitado no chão: levanta (a transição de animação já cuida do resto)
                    recoverTime = 1.05f;
                    if (anim != null) anim.CrossFadeInFixedTime("GK_Levantar", 0.2f);
                }
                else
                {
                    recoverTime = 0.5f;
                    if (anim != null) { anim.SetBool(MirrorHash, false); anim.CrossFadeInFixedTime("Locomocao", 0.3f); }
                }
            }
        }

        Vector3 prevBall;
        bool havePrev;
        Transform lFore, rFore;

        Vector3 Palm(Transform hand, Transform fore)
        {
            if (hand == null) return Vector3.one * 999f;
            if (fore == null) return hand.position;
            Vector3 d = hand.position - fore.position;
            return hand.position + d.normalized * 0.08f; // centro da palma, além do pulso
        }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        void FixedUpdate()
        {
            if (State != Estado.Defendendo || resolved || ball == null || ball.IsHeld) { havePrev = false; return; }
            Vector3 bp = ball.Body.position;
            Vector3 a = havePrev ? prevBall : bp;
            prevBall = bp; havePrev = true;
            if (lFore == null && anim != null && anim.isHuman)
            {
                lFore = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rFore = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }
            // teste contínuo: o trecho percorrido pela bola neste passo contra palmas e corpo (bola rápida não "atravessa" a mão)
            float rb = Arena.BallRadius;
            float dl = SegDist(Palm(lHand, lFore), a, bp);
            float dr = SegDist(Palm(rHand, rFore), a, bp);
            float dc = chest != null ? SegDist(chest.position, a, bp) : 99f;
            float dh = hips != null ? SegDist(hips.position, a, bp) : 99f;
            bool hand = Mathf.Min(dl, dr) < handReach + rb;
            bool body = Mathf.Min(dc, dh) < 0.27f + rb;

            // a bola cruzou a linha do goleiro: se as mãos estão perto o bastante, é contato de ponta de dedo
            if (!hand && !body && lHand != null && rHand != null)
            {
                float px = transform.position.x - 0.15f;
                if ((a.x - px) * (bp.x - px) <= 0f)
                {
                    float k = Mathf.Abs(bp.x - a.x) > 1e-5f ? (px - a.x) / (bp.x - a.x) : 0f;
                    Vector3 c = Vector3.Lerp(a, bp, k);
                    float near = Mathf.Min(Vector3.Distance(c, Palm(lHand, lFore)), Vector3.Distance(c, Palm(rHand, rFore)));
                    if (near < handReach + rb + 0.16f) hand = true;
                }
            }
            if (hand || body) Resolve(hand);
        }

        void Resolve(bool handContact)
        {
            resolved = true;
            shotInFlight = false;
            var ws = WeatherSystem.Instance;
            float catchSpeed = (ws != null && ws.P != null) ? ws.P.gkCatchSpeed : 20f;
            float fumble = (ws != null && ws.P != null) ? ws.P.gkFumble : 0.05f;
            Vector3 bp = ball.Body.position;
            Vector3 v = ball.Velocity;
            float speed = v.magnitude;
            float spin = ball.Body.angularVelocity.magnitude;

            // qualidade do contato: 1 = bola no meio das mãos/peito, 0 = ponta dos dedos
            Transform hand = null;
            float dl = lHand != null ? Vector3.Distance(bp, lHand.position) : 99f;
            float dr = rHand != null ? Vector3.Distance(bp, rHand.position) : 99f;
            hand = dl < dr ? lHand : rHand;
            Vector3 handsMid = (lHand != null && rHand != null) ? (Palm(lHand, lFore) + Palm(rHand, rFore)) * 0.5f : bp;
            float quality = handContact
                ? Mathf.Clamp01(1f - Vector3.Distance(bp, handsMid) / (handReach + 0.3f))
                : 0.8f;
            // esticado no limite do mergulho = contato pior
            if (diving) quality *= Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(Vector3.Distance(diveFrom, transform.position) / maxDiveTravel));

            // encaixe: velocidade, rotação (bola com efeito/oscilando é mais difícil), umidade e qualidade
            float difficulty = speed / catchSpeed + spin / 120f + (1f - quality) * 0.9f;
            bool caught = difficulty < 1f && Random.value > fumble * (0.5f + difficulty);

            if (caught)
            {
                ball.Hold(transform, new Vector3(0f, diving ? Mathf.Max(0.45f, diveHeight + 0.45f) : 1.15f, 0.38f));
                State = Estado.ComBola;
                stateTime = 0f;
                GameSettings.Vibrar();
                if (OnSave != null) OnSave(true);
                return;
            }

            Vector3 outward = transform.forward; // para o campo
            Vector3 nv;
            if (handContact && quality < 0.3f)
            {
                // ponta dos dedos: desvia pouco a trajetória, mantém boa parte da velocidade (pode bater na trave ou entrar!)
                Vector3 away = (bp - handsMid); away.y *= 0.5f;
                if (away.sqrMagnitude < 1e-4f) away = transform.right * diveSide;
                Vector3 dir = Vector3.Slerp(v.normalized, away.normalized, Random.Range(0.15f, 0.35f));
                nv = dir * speed * Random.Range(0.7f, 0.85f);
            }
            else if (handContact)
            {
                // espalmada: reflete na normal do contato mão–bola, perde energia, empurra para fora
                Vector3 n = hand != null ? (bp - hand.position).normalized : outward;
                n = (n + outward * 0.8f).normalized;
                Vector3 refl = Vector3.Reflect(v, n) * Random.Range(0.3f, 0.5f);
                Vector3 side = transform.right * (diving ? diveSide : Mathf.Sign(Vector3.Dot(bp - transform.position, transform.right)));
                nv = refl + side * Random.Range(2f, 5f) + Vector3.up * Random.Range(1f, 4f);
            }
            else
            {
                // no corpo: rebote para a frente (perigoso!)
                nv = Vector3.Reflect(v, outward) * Random.Range(0.25f, 0.45f) + Vector3.up * Random.Range(0.5f, 2f);
            }
            ball.Deflect(nv, Random.insideUnitSphere * 25f);
            if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.Kick(bp, 0.4f);
            GameSettings.Vibrar();
            if (OnSave != null) OnSave(false);
        }

        void Grab()
        {
            ball.Hold(transform, new Vector3(0f, 1.15f, 0.38f));
            State = Estado.ComBola;
            stateTime = 0f;
            diving = false;
            if (OnSave != null) OnSave(true);
        }

        // ------------------------------------------------------------ IK

        void OnIK(int layer)
        {
            if (anim == null || ball == null) return;
            anim.SetLookAtWeight(1f, 0.25f, 0.85f, 1f, 0.5f);
            anim.SetLookAtPosition(ball.transform.position);
            float w = Mathf.Max(ikWeight, readyWeight);
            if (w <= 0.001f) return;

            Vector3 r = transform.right, f = transform.forward, u = Vector3.up;
            Vector3 body = anim.bodyPosition;
            // postura de goleiro: mãos à frente do corpo, palmas para a bola, levemente abertas
            float breathe = Mathf.Sin(Time.time * 2.1f) * 0.015f;
            Vector3 readyL = body + u * (0.14f + breathe) + f * 0.4f - r * 0.3f;
            Vector3 readyR = body + u * (0.14f + breathe) + f * 0.4f + r * 0.3f;
            Vector3 ballL = ikTarget - r * 0.11f;
            Vector3 ballR = ikTarget + r * 0.11f;
            float k = ikWeight / Mathf.Max(w, 1e-3f); // quanto do alvo vem da bola
            Vector3 lt = Vector3.Lerp(readyL, ballL, k);
            Vector3 rt = Vector3.Lerp(readyR, ballR, k);

            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            anim.SetIKPosition(AvatarIKGoal.LeftHand, lt);
            anim.SetIKPosition(AvatarIKGoal.RightHand, rt);

            // cotovelos para fora e um pouco à frente (nunca para trás)
            float hw = readyWeight * 0.8f;
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, hw);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, hw);
            anim.SetIKHintPosition(AvatarIKHint.LeftElbow, body + u * 0.25f - r * 0.55f + f * 0.12f);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, body + u * 0.25f + r * 0.55f + f * 0.12f);
        }
    }
}
