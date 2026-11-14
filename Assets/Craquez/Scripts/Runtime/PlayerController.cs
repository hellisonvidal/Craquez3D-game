using UnityEngine;

namespace Craquez
{
    public enum TipoChute { Forte = 0, Colocado = 1, Cavadinha = 2, Rasteiro = 3 }

    /// <summary>
    /// Jogador controlado: corrida/sprint relativos à câmera, condução de bola por TOQUES
    /// (a bola é empurrada, rola à frente e o jogador a alcança de novo), finta / puxada / parada,
    /// arrancada (toque longo), chute com barra de força, mira dentro do gol, curva (Magnus) e imprecisão realista.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public BallPhysics ball;
        public Transform cameraTransform;

        [Header("Movimento")]
        public float jogSpeed = 5.4f;
        public float sprintSpeed = 8.1f;
        public float acceleration = 17f;
        public float turnSpeed = 820f;

        [Header("Chute")]
        public float chargeTime = 0.95f;

        public bool InputEnabled { get; set; }

        [Header("IA (modo goleiro: a IA controla o atacante)")]
        public bool aiControlled;
        [System.NonSerialized] public Vector3 aiMoveWorld;
        [System.NonSerialized] public bool aiSprint, aiShoot, aiFinesse, aiChip, aiLow;
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
        bool prevShoot, prevFinesse, prevChip, prevLow, prevSkill, prevKnock;
        // condução
        Vector3 wishWorld;
        bool sprintHeld;
        float nextTouchAt, touchEnd, touchDecel, lastTouchAt = -10f, burstUntil, skillCooldown;
        Vector3 touchDir;
        bool knockQueued;
        public float LastTouchTime { get { return lastTouchAt; } }
        public string LastMove { get; private set; }
        public float LastMoveTime { get; private set; } = -10f;
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
            wishWorld = wish; sprintHeld = sprint;
            bool kicking = Time.time < lockMoveUntil;

            // condução: o jogador "corre para a bola" que acabou de tocar (ajusta a passada)
            if (ball != null && Time.time - lastTouchAt < 1.4f && wish.sqrMagnitude > 0.04f && !kicking)
            {
                Vector3 tb = ball.Body.position - transform.position; tb.y = 0f;
                if (tb.magnitude < 4.5f && tb.magnitude > 0.3f && Vector3.Dot(tb.normalized, wish.normalized) > 0.55f)
                    wish = Vector3.Slerp(wish.normalized, tb.normalized, 0.35f) * wish.magnitude;
            }

            float max = sprint ? sprintSpeed : jogSpeed;
            if (HasBall) max *= sprint ? 0.95f : 0.92f;
            if (Time.time < burstUntil) max += 1.1f;
            Vector3 target = kicking ? Vector3.zero : wish * max;
            if (Charging) target *= 0.6f;

            // aceleração: arranque forte, freada mais forte ainda, mudança brusca de direção custa velocidade
            float a = acceleration * grip;
            float vm = velocity.magnitude;
            if (target.magnitude < vm) a *= 1.8f;
            if (vm > 1f && target.sqrMagnitude > 0.01f)
            {
                float turnDot = Vector3.Dot(velocity / vm, target.normalized);
                if (turnDot < 0.2f) { a *= 1.6f; velocity *= Mathf.Lerp(1f, 0.9f, grip * dt * 10f); } // pivô
            }
            velocity = Vector3.MoveTowards(velocity, target, a * dt);

            Vector3 p = transform.position + velocity * dt;
            p.x = Mathf.Clamp(p.x, -Arena.FenceX + 1f, Arena.FenceX - 1f);
            p.z = Mathf.Clamp(p.z, -Arena.FenceZ + 1f, Arena.FenceZ - 1f);
            p.y = 0f;
            transform.position = p;

            // orientação: gira mais rápido devagar, mais lento em sprint
            Vector3 face = velocity.sqrMagnitude > 0.04f ? velocity : (wish.sqrMagnitude > 0.04f ? wish : transform.forward);
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
                float ts = turnSpeed * Mathf.Lerp(1.15f, 0.7f, Mathf.Clamp01(vm / sprintSpeed)) * Mathf.Lerp(0.6f, 1f, grip);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, q, ts * dt);
            }

            HandleSkillInput(mv);

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

            if (!Charging && !busy)
            {
                if (s && !prevShoot) StartCharge(TipoChute.Forte);
                else if (f && !prevFinesse) StartCharge(TipoChute.Colocado);
                else if (c && !prevChip) StartCharge(TipoChute.Cavadinha);
                else if (l && !prevLow) StartCharge(TipoChute.Rasteiro);
            }
            if (Charging)
            {
                bool held = ChargingType == TipoChute.Forte ? s : (ChargingType == TipoChute.Colocado ? f : (ChargingType == TipoChute.Cavadinha ? c : l));
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
            kickType = ChargingType;
            kickPower = Charge;
            kickAim = mv;
            kickAimWorld = aiControlled ? aiAimWorld : Vector3.zero;
            kickPending = true;
            string state = kickType == TipoChute.Colocado ? "ChuteColocado" : (kickType == TipoChute.Cavadinha ? "Cavadinha" : "ChuteForte");
            if (anim != null) anim.CrossFadeInFixedTime(state, 0.08f, 0, 0f);
            float contact = kickType == TipoChute.Colocado || kickType == TipoChute.Cavadinha ? 0.21f : 0.24f;
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

        // ------------------------------------------------------------ dribles (finta / puxada / parar) e arrancada

        void HandleSkillInput(Vector2 mv)
        {
            bool human = InputEnabled && !aiControlled;
            if (human && Time.unscaledTime < blockUntil) { prevSkill = prevKnock = true; return; }
            bool skill = human && InputBridge.Skill;
            bool knock = human && InputBridge.KnockOn;
            if (skill && !prevSkill) TrySkill();
            if (knock && !prevKnock) knockQueued = true;
            if (!knock && knockQueued && Time.time - lastTouchAt > 1.5f && !HasBall) knockQueued = false;
            prevSkill = skill; prevKnock = knock;
        }

        void Move(string name) { LastMove = name; LastMoveTime = Time.time; }

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
                // parar a bola com a sola
                Compat.SetVelocity(ball.Body, new Vector3(0f, bv.y, 0f));
                ball.Body.angularVelocity = Vector3.zero;
                velocity *= 0.25f;
                touchEnd = 0f; nextTouchAt = Time.time + 0.25f; lastTouchAt = Time.time;
                Move("PAROU A BOLA");
                return;
            }
            Vector3 wd = w.normalized;
            float dot = Vector3.Dot(wd, fwd);
            if (dot < -0.45f)
            {
                // puxada (meia-lua para trás): bola volta, jogador gira
                Compat.SetVelocity(ball.Body, new Vector3(wd.x * 3.2f, bv.y, wd.z * 3.2f));
                velocity = wd * 1.2f;
                transform.rotation = Quaternion.LookRotation(Vector3.Slerp(fwd, wd, 0.6f));
                StartTouchDecay(wd, 3.2f, 0.5f);
                Move("PUXADA");
            }
            else
            {
                // corte lateral / finta de corpo: bola e corpo saem juntos para o lado
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                if (Vector3.Dot(side, wd) < 0f) side = -side;
                Vector3 dir = (side * 0.85f + fwd * 0.45f).normalized;
                float sp = Mathf.Max(velocity.magnitude * 0.8f, 3.2f);
                Compat.SetVelocity(ball.Body, new Vector3(dir.x * (sp + 2.2f), bv.y, dir.z * (sp + 2.2f)));
                velocity = dir * sp;
                burstUntil = Time.time + 0.45f;
                StartTouchDecay(dir, 2.2f, 0.45f);
                Move("CORTE");
            }
            lastTouchAt = Time.time;
            nextTouchAt = Time.time + 0.3f;
        }

        void StartTouchDecay(Vector3 dir, float extra, float duration)
        {
            touchDir = dir;
            touchEnd = Time.time + duration;
            touchDecel = 2f * extra / Mathf.Max(0.1f, duration); // a vantagem da bola some na metade e ela fica levemente mais lenta
        }

        // ------------------------------------------------------------ condução por toques

        void FixedUpdate()
        {
            HasBall = false;
            if (ball == null || ball.Body.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            float now = Time.time;
            Vector3 pos = transform.position;
            Vector3 bp = ball.Body.position;
            Vector3 to = bp - pos; to.y = 0f;
            float d = to.magnitude;
            Vector3 bv = ball.Velocity;
            Vector3 flatBv = new Vector3(bv.x, 0f, bv.z);
            Vector3 rel = flatBv - velocity;
            bool low = bp.y < 0.35f;
            bool recentlyKicked = now - ball.LastKickTime < 0.35f;
            bool controlled = InputEnabled || aiControlled;

            // preparando chute: ajusta a passada até a bola (como antes)
            if (kickPending && low && d < 2.2f)
            {
                HasBall = true;
                Vector3 tgt = pos + transform.forward * 0.48f + transform.right * 0.1f;
                Vector3 desired = velocity + (tgt - bp) * 12f; desired.y = 0f;
                Vector3 nv = Vector3.MoveTowards(flatBv, desired, 60f * Mathf.Lerp(0.75f, 1f, Grip()) * dt);
                Compat.SetVelocity(ball.Body, new Vector3(nv.x, bv.y, nv.z));
                return;
            }
            if (!controlled || !low || recentlyKicked) { touchEnd = 0f; return; }

            // depois do toque a bola perde a vantagem (grama segura a bola tocada de leve)
            if (now < touchEnd && d < 6f)
            {
                float along = Vector3.Dot(flatBv, touchDir);
                float roll = WeatherSystem.Instance != null ? WeatherSystem.Instance.RollingDeceleration : 1.1f;
                float dec = Mathf.Max(0f, touchDecel - roll) * dt;
                if (along > 0.5f) flatBv -= touchDir * Mathf.Min(dec, along - 0.5f);
                Compat.SetVelocity(ball.Body, new Vector3(flatBv.x, bv.y, flatBv.z));
            }

            float spd = velocity.magnitude;

            // assistência de condução: logo depois do toque, a bola acompanha a direção pedida e não "foge"
            Vector3 wAssist = wishWorld;
            if (now - lastTouchAt < 1.1f && d < 3.2f && wAssist.sqrMagnitude > 0.03f && flatBv.sqrMagnitude > 0.25f)
            {
                Vector3 wdir = wAssist.normalized;
                Vector3 fb = flatBv;
                float bs = fb.magnitude;
                if (Vector3.Dot(to.normalized, wdir) > -0.2f)
                {
                    float maxTurn = 260f * Mathf.Lerp(0.6f, 1f, Grip()) * dt;
                    Vector3 nd = Vector3.RotateTowards(fb / bs, wdir, maxTurn * Mathf.Deg2Rad, 0f);
                    fb = nd * bs;
                }
                // não deixa a bola abrir demais à frente
                float maxLead = sprintHeld ? 3.0f : 1.7f;
                float ahead = Vector3.Dot(to, fb.normalized);
                if (ahead > maxLead)
                {
                    float pspd = Mathf.Max(spd, 1f);
                    float target = Mathf.Max(pspd * 0.85f, 0.5f);
                    if (bs > target) fb = fb.normalized * Mathf.MoveTowards(bs, target, 14f * dt);
                }
                flatBv = fb;
                Compat.SetVelocity(ball.Body, new Vector3(fb.x, bv.y, fb.z));
                rel = flatBv - velocity;
            }

            float reach = 0.85f + 0.05f * spd;
            Vector3 dirTo = d > 1e-3f ? to / d : transform.forward;
            bool inFront = Vector3.Dot(dirTo, transform.forward) > -0.25f || d < 0.45f;
            bool inReach = d < reach && inFront && rel.magnitude < 13f;
            HasBall = inReach || (now - lastTouchAt < 0.9f && d < 3.5f);
            if (!inReach) return;

            Vector3 wish = wishWorld;
            float wm = Mathf.Clamp01(wish.magnitude);
            Vector3 wd = wm > 0.15f ? wish / Mathf.Max(wish.magnitude, 1e-3f) : transform.forward;
            Vector3 ballDir = flatBv.sqrMagnitude > 0.25f ? flatBv.normalized : wd;
            bool sharpTurn = wm > 0.15f && Vector3.Dot(ballDir, wd) < 0.7f;
            if (now < nextTouchAt && !sharpTurn) return;
            if (sharpTurn && now - lastTouchAt < 0.12f) return;

            float grip = Grip();
            float jitter = Random.Range(0.92f, 1.08f);

            // 1) bola chegando rápido: domínio (amortece)
            if (rel.magnitude > 6.5f && now - lastTouchAt > 0.6f)
            {
                float keep = Mathf.Lerp(0.35f, 0.5f, 1f - grip); // molhado: domínio pior
                Vector3 nv = velocity + rel * keep;
                nv = Vector3.ClampMagnitude(nv - velocity, 3.5f) + velocity;
                Compat.SetVelocity(ball.Body, new Vector3(nv.x, Mathf.Min(bv.y, 0.5f), nv.z));
                lastTouchAt = now; nextTouchAt = now + 0.25f; touchEnd = 0f;
                Move("DOMÍNIO");
                return;
            }

            // 2) parado: bola colada ao pé
            if (wm < 0.15f && spd < 1.3f)
            {
                Vector3 hold = pos + transform.forward * 0.42f;
                Vector3 nv = (hold - bp) * 3.5f; nv.y = 0f;
                nv = Vector3.ClampMagnitude(nv, 1.6f);
                Compat.SetVelocity(ball.Body, new Vector3(nv.x, bv.y, nv.z));
                nextTouchAt = now + 0.1f; touchEnd = 0f;
                return;
            }

            // 3) toque de condução: curto no trote, longo no sprint, bem longo na arrancada
            float playerSpeed = Mathf.Max(spd * Mathf.Max(0f, Vector3.Dot(velocity.normalized, wd)), wm * (sprintHeld ? sprintSpeed : jogSpeed) * 0.75f);
            float interval;
            if (knockQueued)
            {
                // arrancada: bola lançada à frente (rola ~5–7 m), o jogador dispara atrás dela
                knockQueued = false;
                interval = 1.0f;
                float extraK = Random.Range(4.2f, 5.2f) * Mathf.Lerp(1.12f, 1f, grip);
                Vector3 vk = wd * (playerSpeed + extraK);
                Compat.SetVelocity(ball.Body, new Vector3(vk.x, Mathf.Min(bv.y, 0.3f), vk.z));
                ball.Body.angularVelocity = Vector3.Cross(Vector3.up, wd) * (vk.magnitude / Arena.BallRadius) * 0.6f;
                StartTouchDecay(wd, extraK, 3.4f);
                burstUntil = now + 1.4f;
                Move("ARRANCADA");
            }
            else
            {
                float touchDist;
                if (sprintHeld) { touchDist = Mathf.Lerp(1.2f, 2.3f, Mathf.Clamp01(spd / sprintSpeed)); interval = 0.46f; }
                else { touchDist = Mathf.Lerp(0.45f, 1.0f, Mathf.Clamp01(spd / jogSpeed)); interval = 0.36f; }
                if (sharpTurn) { touchDist *= 0.6f; playerSpeed *= 0.75f; }
                touchDist *= jitter * Mathf.Lerp(1.15f, 1f, grip); // gramado molhado: o toque escapa mais
                interval *= jitter;
                // excesso inicial E que some linearmente até -E no fim do intervalo => a bola ganha "touchDist" e volta ao pé
                float extra = Mathf.Min(4f * touchDist / interval, 13f);
                Vector3 v = wd * (playerSpeed + extra);
                Compat.SetVelocity(ball.Body, new Vector3(v.x, Mathf.Min(bv.y, 0.3f), v.z));
                ball.Body.angularVelocity = Vector3.Cross(Vector3.up, wd) * (v.magnitude / Arena.BallRadius) * 0.6f;
                StartTouchDecay(wd, extra, interval);
            }
            lastTouchAt = now;
            nextTouchAt = now + interval * 0.55f;
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
                float elev = t == TipoChute.Cavadinha ? 35f : (t == TipoChute.Colocado ? 9f : (t == TipoChute.Rasteiro ? 1.5f : Mathf.Lerp(3f, 16f, power)));
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
                case TipoChute.Rasteiro: ty = 0.14f; speed = Mathf.Lerp(15f, 29f, power); break;
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
            else if (t == TipoChute.Rasteiro)
            {
                spin = -backAxis * Mathf.Lerp(10f, 22f, power); // topspin: a bola cola no chão
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
                case TipoChute.Rasteiro: sigma = 0.8f + Mathf.Max(0f, power - 0.85f) * 6f; break;
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
