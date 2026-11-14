using UnityEngine;

namespace Craquez
{
    public enum TipoChute { Forte = 0, Colocado = 1, Cavadinha = 2 }

    /// <summary>
    /// Jogador controlado: corrida/sprint relativos à câmera, condução de bola por física,
    /// chute com barra de força, mira dentro do gol, curva (Magnus) e imprecisão realista.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public BallPhysics ball;
        public Transform cameraTransform;

        [Header("Movimento")]
        public float jogSpeed = 5.2f;
        public float sprintSpeed = 7.8f;
        public float acceleration = 13f;
        public float turnSpeed = 640f;

        [Header("Chute")]
        public float chargeTime = 0.95f;

        public bool InputEnabled { get; set; }

        [Header("IA (modo goleiro: a IA controla o atacante)")]
        public bool aiControlled;
        [System.NonSerialized] public Vector3 aiMoveWorld;
        [System.NonSerialized] public bool aiSprint, aiShoot, aiFinesse, aiChip;
        [System.NonSerialized] public Vector3 aiAimWorld;
        public bool Busy { get { return kickPending || Time.time < lockMoveUntil; } }
        public bool Charging { get; private set; }
        public float Charge { get; private set; }
        public TipoChute ChargingType { get; private set; }
        public bool HasBall { get; private set; }
        public Vector3 Velocity { get { return velocity; } }
        public Animator Anim { get { return anim; } }

        Animator anim;
        Vector3 velocity;
        bool prevShoot, prevFinesse, prevChip;
        bool kickPending;
        float kickContactAt, lockMoveUntil;
        TipoChute kickType;
        float kickPower;
        Vector2 kickAim;
        Vector3 kickAimWorld;
        Vector3 ballVelAtContact;
        float splashTimer;
        static readonly int SpeedHash = Animator.StringToHash("Speed");

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
            }
        }

        public void Teleport(Vector3 pos, Vector3 lookAt)
        {
            pos.y = 0f;
            transform.position = pos;
            Vector3 d = lookAt - pos; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
            velocity = Vector3.zero;
            Charging = false; Charge = 0f; kickPending = false; lockMoveUntil = 0f;
            if (anim != null) { anim.SetFloat(SpeedHash, 0f); anim.Play("Locomocao", 0, 0f); }
        }

        float Grip()
        {
            var ws = WeatherSystem.Instance;
            return (ws != null && ws.P != null) ? ws.P.playerGrip : 1f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float grip = Grip();
            Vector2 mv = InputEnabled && !aiControlled ? InputBridge.Move : Vector2.zero;
            Vector3 camF = Vector3.forward, camR = Vector3.right;
            if (cameraTransform != null)
            {
                camF = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                camR = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }
            Vector3 wish = aiControlled ? Vector3.ClampMagnitude(aiMoveWorld, 1f) : camF * mv.y + camR * mv.x;
            bool sprint = aiControlled ? aiSprint : (InputEnabled && InputBridge.Sprint);
            bool kicking = Time.time < lockMoveUntil;
            float max = sprint ? sprintSpeed : jogSpeed;
            if (HasBall) max *= 0.93f;
            Vector3 target = kicking ? Vector3.zero : wish * max;
            if (Charging) target *= 0.6f;

            float a = acceleration * grip;
            if (target.magnitude < velocity.magnitude) a *= 1.5f;
            velocity = Vector3.MoveTowards(velocity, target, a * dt);

            Vector3 p = transform.position + velocity * dt;
            p.x = Mathf.Clamp(p.x, -Arena.FenceX + 1f, Arena.FenceX - 1f);
            p.z = Mathf.Clamp(p.z, -Arena.FenceZ + 1f, Arena.FenceZ - 1f);
            p.y = 0f;
            transform.position = p;

            // orientação
            Vector3 face = velocity.sqrMagnitude > 0.04f ? velocity : transform.forward;
            if ((Charging || kicking) && ball != null)
            {
                Vector3 toBall = ball.transform.position - transform.position; toBall.y = 0f;
                Vector3 toGoal = Arena.AttackGoalCenter - transform.position; toGoal.y = 0f;
                face = toBall.magnitude < 2.5f ? Vector3.Lerp(toBall.normalized, toGoal.normalized, 0.5f) : toGoal;
            }
            face.y = 0f;
            if (face.sqrMagnitude > 1e-4f)
            {
                Quaternion q = Quaternion.LookRotation(face);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, q, turnSpeed * Mathf.Lerp(0.6f, 1f, grip) * dt);
            }

            if (anim != null) anim.SetFloat(SpeedHash, velocity.magnitude, 0.08f, dt);

            HandleShootInput(dt, mv);

            if (kickPending && Time.time >= kickContactAt) DoContact();

            // respingos ao correr nas poças
            var pf = PuddleField.Instance;
            float depth;
            splashTimer -= dt;
            if (pf != null && velocity.magnitude > 1.8f && splashTimer <= 0f && pf.Query(transform.position, out depth))
            {
                if (WeatherSystem.Instance != null) WeatherSystem.Instance.Splash(transform.position + transform.forward * 0.2f, Mathf.Clamp01(velocity.magnitude / 8f) * depth);
                splashTimer = Mathf.Lerp(0.45f, 0.22f, velocity.magnitude / sprintSpeed);
            }
        }

        // ------------------------------------------------------------ entrada de chute

        void HandleShootInput(float dt, Vector2 mv)
        {
            bool human = InputEnabled && !aiControlled;
            bool s = aiControlled ? aiShoot : human && InputBridge.Shoot;
            bool f = aiControlled ? aiFinesse : human && InputBridge.Finesse;
            bool c = aiControlled ? aiChip : human && InputBridge.Chip;
            bool busy = kickPending || Time.time < lockMoveUntil;

            if (!Charging && !busy)
            {
                if (s && !prevShoot) StartCharge(TipoChute.Forte);
                else if (f && !prevFinesse) StartCharge(TipoChute.Colocado);
                else if (c && !prevChip) StartCharge(TipoChute.Cavadinha);
            }
            if (Charging)
            {
                bool held = ChargingType == TipoChute.Forte ? s : (ChargingType == TipoChute.Colocado ? f : c);
                Charge = Mathf.Min(1f, Charge + dt / chargeTime);
                if (!held) ReleaseShot(mv);
            }
            prevShoot = s; prevFinesse = f; prevChip = c;
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
            kickType = ChargingType;
            kickPower = Charge;
            kickAim = mv;
            kickAimWorld = aiControlled ? aiAimWorld : Vector3.zero;
            kickPending = true;
            string state = kickType == TipoChute.Forte ? "ChuteForte" : (kickType == TipoChute.Colocado ? "ChuteColocado" : "Cavadinha");
            if (anim != null) anim.CrossFadeInFixedTime(state, 0.08f, 0, 0f);
            float contact = kickType == TipoChute.Forte ? 0.24f : 0.21f;
            kickContactAt = Time.time + contact;
            lockMoveUntil = Time.time + contact + 0.3f;
        }

        void DoContact()
        {
            kickPending = false;
            Charge = 0f;
            if (ball == null || ball.IsHeld) return;
            Vector3 strike = StrikePoint();
            Vector3 bp = ball.Body.position;
            float off = Vector3.Distance(new Vector3(bp.x, 0f, bp.z), new Vector3(strike.x, 0f, strike.z));
            if (off > 1.0f || bp.y > 0.9f) return; // furou: a bola não estava no pé

            Vector3 spin;
            ballVelAtContact = ball.Velocity;
            Vector3 v = ComputeShot(kickType, kickPower, kickAim, off, out spin);
            GameSettings.Vibrar();
            ball.Kick(v, spin);
            if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.Kick(bp, Mathf.Clamp01(v.magnitude / 30f));
        }

        Vector3 StrikePoint()
        {
            return transform.position + transform.forward * 0.48f + transform.right * 0.1f;
        }

        // ------------------------------------------------------------ condução

        void FixedUpdate()
        {
            HasBall = false;
            if (ball == null || ball.Body.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            Vector3 bp = ball.Body.position;
            Vector3 to = bp - pos; to.y = 0f;
            float d = to.magnitude;
            Vector3 bv = ball.Velocity;
            Vector3 rel = bv - velocity; rel.y = 0f;
            bool low = bp.y < 0.4f;
            bool recentlyKicked = Time.time - ball.LastKickTime < 0.45f;
            bool inFront = d < 0.75f || Vector3.Dot(to / Mathf.Max(d, 1e-3f), transform.forward) > -0.2f;
            bool control = (InputEnabled || aiControlled) && low && !recentlyKicked && d < 1.3f && rel.magnitude < 11f && inFront;

            if (!(control || (kickPending && low && d < 1.8f))) return;
            HasBall = true;

            float lead = kickPending ? 0.48f : 0.55f + 0.085f * velocity.magnitude;
            Vector3 tgt = pos + transform.forward * lead + (kickPending ? transform.right * 0.1f : Vector3.zero);
            Vector3 desired = velocity + (tgt - bp) * (kickPending ? 12f : 7.5f);
            desired.y = 0f;
            Vector3 cur = new Vector3(bv.x, 0f, bv.z);
            float maxDv = (kickPending ? 60f : 32f) * Mathf.Lerp(0.75f, 1f, Grip()) * dt;
            Vector3 nv = Vector3.MoveTowards(cur, desired, maxDv);
            Compat.SetVelocity(ball.Body, new Vector3(nv.x, bv.y, nv.z));
        }

        // ------------------------------------------------------------ cálculo do chute

        Vector3 ComputeShot(TipoChute t, float power, Vector2 aim, float offFoot, out Vector3 spin)
        {
            Vector3 from = ball.Body.position;
            Vector3 fwd = transform.forward;
            float goalX = Arena.HalfLength + 0.25f;
            spin = Vector3.zero;

            Vector3 camF = Vector3.forward, camR = Vector3.right;
            if (cameraTransform != null)
            {
                camF = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                camR = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }
            Vector3 aimW = aiControlled ? kickAimWorld : camF * aim.y + camR * aim.x;

            bool towardGoal = fwd.x > 0.15f && from.x < goalX - 1.5f;
            float speed;
            Vector3 v;

            if (!towardGoal)
            {
                // chute "livre" na direção do corpo
                speed = Mathf.Lerp(10f, 28f, power);
                Vector3 dir = aimW.sqrMagnitude > 0.06f ? aimW.normalized : fwd;
                float elev = t == TipoChute.Cavadinha ? 35f : (t == TipoChute.Colocado ? 9f : Mathf.Lerp(3f, 16f, power));
                v = Quaternion.AngleAxis(-elev, Vector3.Cross(Vector3.up, dir)) * dir * speed;
                if (t == TipoChute.Colocado) spin = Vector3.up * 35f;
                return ApplyError(v, t, power, offFoot);
            }

            // --- mira lateral dentro do gol
            float lat;
            if (aimW.sqrMagnitude > 0.06f) lat = Mathf.Clamp(aimW.z * 1.35f, -1f, 1f);
            else
            {
                float zAtGoal = from.z + fwd.z / Mathf.Max(fwd.x, 0.15f) * (goalX - from.x);
                lat = Mathf.Clamp(zAtGoal / Arena.GoalHalfWidth, -1f, 1f);
            }
            float tz = lat * (Arena.GoalHalfWidth - 0.45f);
            float ty;
            switch (t)
            {
                case TipoChute.Forte: ty = Mathf.Lerp(0.25f, 2.0f, Mathf.Pow(power, 1.5f)); speed = Mathf.Lerp(14f, 33f, power); break;
                case TipoChute.Colocado: ty = Mathf.Lerp(0.5f, 1.75f, power); speed = Mathf.Lerp(12f, 24f, power); break;
                default: ty = 2.0f; speed = 0f; break;
            }
            Vector3 T = new Vector3(goalX, ty, tz);
            Vector3 flat0 = new Vector3(T.x - from.x, 0f, T.z - from.z).normalized;
            if (t != TipoChute.Cavadinha)
            {
                // a corrida soma energia ao chute; bola vindo de encontro (de primeira) também
                speed += 0.35f * Mathf.Max(0f, Vector3.Dot(velocity, flat0));
                speed += 0.18f * Mathf.Max(0f, -Vector3.Dot(ballVelAtContact, flat0));
                // contato ruim (bola longe do pé) perde força
                speed *= 1f - 0.28f * Mathf.Clamp01(offFoot / 0.8f);
                speed = Mathf.Min(speed, 36f);
            }

            // efeito
            Vector3 flatDir = new Vector3(T.x - from.x, 0f, T.z - from.z).normalized;
            Vector3 backAxis = Vector3.Cross(flatDir, Vector3.up); // rotação "para trás" (gera sustentação)
            if (t == TipoChute.Colocado)
            {
                float side = Mathf.Abs(tz - from.z) > 0.3f ? Mathf.Sign(tz - from.z) : (tz >= 0f ? 1f : -1f);
                float zComp = Vector3.Cross(Vector3.up, flatDir).z; // sinal da componente lateral do Magnus para ω = +up
                // força lateral (Magnus) = w·(up × v) => para curvar para 'side' em z: w = side·sinal(zComp)
                spin = Vector3.up * (Mathf.Lerp(30f, 55f, power) * side * Mathf.Sign(zComp == 0f ? 1f : zComp));
                spin += backAxis * 8f;
            }
            else if (t == TipoChute.Forte)
            {
                spin = backAxis * Random.Range(-12f, 6f) + Vector3.up * Random.Range(-4f, 4f);
            }
            else
            {
                spin = backAxis * Mathf.Lerp(18f, 30f, power);
            }

            // bola molhada: menos atrito pé–bola => menos rotação; contato ruim => efeito lateral indesejado
            var wsx = WeatherSystem.Instance;
            float wetK = (wsx != null && wsx.P != null) ? Mathf.Lerp(1f, 0.78f, wsx.P.wetness) : 1f;
            spin = spin * wetK + Vector3.up * Random.Range(-1f, 1f) * offFoot * 30f;

            // --- solução balística + correção iterativa com a simulação real (arrasto e Magnus, sem vento)
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
                    sigma = 1.0f + Mathf.Max(0f, power - 0.75f) * 14f;
                    lift = Mathf.Max(0f, power - 0.88f) * 30f; // força demais => "balão"
                    break;
                case TipoChute.Colocado: sigma = 0.7f + Mathf.Max(0f, power - 0.85f) * 8f; break;
                default: sigma = 1.2f; break;
            }
            if (aiControlled ? aiSprint : InputBridge.Sprint) sigma += 1.2f;
            sigma += velocity.magnitude * 0.1f + offFoot * 4f;
            var ws = WeatherSystem.Instance;
            if (ws != null) sigma += ws.Current == Clima.Tempestade ? 0.9f : (ws.Current == Clima.Chuva ? 0.4f : 0f);

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

        /// <summary>Velocidade de módulo "speed" que acerta o alvo (trajetória baixa, sem arrasto).</summary>
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

        /// <summary>Cavadinha: ângulo fixo, resolve a velocidade necessária.</summary>
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

        // ------------------------------------------------------------ IK (olhar para a bola)

        void OnIK(int layer)
        {
            if (anim == null || ball == null) return;
            float d = Vector3.Distance(ball.transform.position, transform.position);
            float w = Mathf.Clamp01(1.2f - d / 25f);
            anim.SetLookAtWeight(w, 0.15f, 0.7f, 1f, 0.6f);
            anim.SetLookAtPosition(ball.transform.position);
        }
    }
}
