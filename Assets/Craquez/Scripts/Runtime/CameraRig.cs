using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Câmeras do treino. Padrão: atrás do atacante, enquadrando a corrida para o gol.
    /// Outras: dinâmica baixa, transmissão de TV, aérea, atrás do gol e (modo goleiro) atrás do goleiro.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public enum Modo { AtrasDoJogador = 0, Dinamica = 1, TV = 2, Aerea = 3, AtrasDoGol = 4, Goleiro = 5, Menu = 6, VisaoJogador = 7, Comemoracao = 8, LateralProxima = 9, Estadio = 10 }

        public static readonly Modo[] ModosAtacante = { Modo.AtrasDoJogador, Modo.Estadio, Modo.Dinamica, Modo.TV, Modo.LateralProxima, Modo.Aerea, Modo.AtrasDoGol, Modo.VisaoJogador };
        public static readonly Modo[] ModosGoleiro = { Modo.Goleiro, Modo.Estadio, Modo.AtrasDoGol, Modo.TV, Modo.LateralProxima, Modo.Aerea };
        public static readonly Modo[] ModosPartida = { Modo.Estadio, Modo.TV, Modo.LateralProxima, Modo.AtrasDoJogador, Modo.VisaoJogador, Modo.Aerea, Modo.Dinamica };

        public static string Nome(Modo m)
        {
            switch (m)
            {
                case Modo.Estadio: return "Estádio Ampla (EA / eFootball)";
                case Modo.AtrasDoJogador: return "Atrás do atacante";
                case Modo.Dinamica: return "Dinâmica (baixa)";
                case Modo.TV: return "Transmissão TV";
                case Modo.LateralProxima: return "Lateral próxima (2 lados)";
                case Modo.Aerea: return "Aérea";
                case Modo.AtrasDoGol: return "Atrás do gol";
                case Modo.Goleiro: return "Goleiro";
                case Modo.VisaoJogador: return "Visão do jogador";
                case Modo.Comemoracao: return "Comemoração";
                default: return "Menu";
            }
        }

        public Transform player;
        public Transform keeper;
        public BallPhysics ball;
        public Modo mode = Modo.AtrasDoJogador;
        public bool goalkeeperRole;
        [Header("Partida")]
        public bool matchMode;
        [Tooltip("+1 = o jogador controlado ataca o gol em +X; -1 = em -X.")]
        public float attackDir = 1f;
        [Tooltip("Cabeça do jogador controlado (visão do jogador).")]
        public Transform povHead;
        PovHeadHider hider;

        Camera cam;
        Vector3 vel;
        Vector3 lookPoint;
        Vector3 ballLead;
        Vector2 peek;
        float smoothedHeadY;
        Transform cachedPlayer;
        Transform autoPovHead;
        float shake;
        float orbit;

        void Awake()
        {
            cam = GetComponent<Camera>();
            WeatherSystem.OnLightning += OnThunder;
            BallPhysics.OnPostHit += OnPostShake;
        }

        void OnDestroy()
        {
            WeatherSystem.OnLightning -= OnThunder;
            BallPhysics.OnPostHit -= OnPostShake;
            UpdateHeadHider(false);
        }

        void OnThunder(float v) { shake = Mathf.Max(shake, 0.06f * v); }
        void OnPostShake(float v) { shake = Mathf.Max(shake, 0.045f * v); }

        public void Cycle()
        {
            var list = matchMode ? ModosPartida : (goalkeeperRole ? ModosGoleiro : ModosAtacante);
            int i = System.Array.IndexOf(list, mode);
            mode = list[(i + 1 + list.Length) % list.Length];
        }

        public void Snap()
        {
            ballLead = Vector3.zero;
            peek = Vector2.zero;
            smoothedHeadY = 0f;
            Vector3 pos, look; float fov;
            Desired(out pos, out look, out fov);
            transform.position = pos;
            lookPoint = look;
            Vector3 fwd = look - pos;
            if (fwd.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(fwd);
            if (cam != null) cam.fieldOfView = fov;
            vel = Vector3.zero;
        }

        Transform ActiveHead()
        {
            if (povHead != null) return povHead;
            if (player != cachedPlayer)
            {
                cachedPlayer = player;
                var s = player != null ? player.GetComponent<MixamoBoneSockets>() : null;
                autoPovHead = s != null ? s.Head : null;
            }
            return autoPovHead;
        }

        static void ClampInsideFence(ref Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -(Arena.FenceX - 0.6f), Arena.FenceX - 0.6f);
            p.z = Mathf.Clamp(p.z, -(Arena.FenceZ - 0.6f), Arena.FenceZ - 0.6f);
            if (p.y < 0.65f) p.y = 0.65f;
        }

        void Desired(out Vector3 pos, out Vector3 look, out float fov)
        {
            float ad = attackDir >= 0f ? 1f : -1f;
            Vector3 goal = new Vector3(ad * Arena.HalfLength, 1.0f, 0f);
            Vector3 pp = player != null ? player.position : Vector3.zero;
            Vector3 bp = ball != null ? ball.transform.position : pp;
            Vector3 kp = keeper != null ? keeper.position : Arena.AttackGoalCenter;
            switch (mode)
            {
                case Modo.Estadio:
                    {
                        // Câmera padrão EA FC / eFootball (Estádio / Dinâmica Ampla):
                        // Tribuna central alta com visão ampla dos dois lados do campo (~84 m de extensão horizontal)
                        // e leve ângulo de perspectiva rumo às grandes áreas sem cortar o meio-campo.
                        float leadX = bp.x + ballLead.x * 0.85f + peek.x * 9f;
                        float leadZ = bp.z + ballLead.z * 0.55f + peek.y * 5f;
                        float boxFactor = Mathf.InverseLerp(22f, 47f, Mathf.Abs(bp.x));
                        float camX = Mathf.Clamp(leadX * 0.44f, -21.5f, 21.5f);
                        float lookX = Mathf.Clamp(leadX * 0.64f, -32.5f, 32.5f);
                        float lookZ = Mathf.Clamp(leadZ * 0.30f, -8.5f, 9.5f) + 1.4f;
                        pos = new Vector3(camX, Mathf.Lerp(23.6f, 21.6f, boxFactor), -Arena.HalfWidth - Mathf.Lerp(17.8f, 15.6f, boxFactor));
                        look = new Vector3(lookX, 0.55f, lookZ);
                        float aspect = cam != null && cam.aspect > 0.5f ? cam.aspect : 1.7778f;
                        // Garante abertura horizontal >= ~73° em 16:9 (mostrando os dois lados do campo com clareza)
                        float baseV = 2f * Mathf.Atan(0.735f / Mathf.Min(aspect, 1.85f)) * Mathf.Rad2Deg;
                        fov = Mathf.Clamp(Mathf.Lerp(baseV, baseV - 2.2f, boxFactor), 41f, 54f);
                        break;
                    }
                case Modo.TV when matchMode:
                    {
                        // transmissão: lateral alta com antecipação suave da bola e aproximação na grande área
                        float leadX = bp.x + ballLead.x + peek.x * 7f;
                        float leadZ = bp.z + ballLead.z * 0.7f + peek.y * 4.5f;
                        float x = Mathf.Clamp(Mathf.Lerp(leadX, pp.x, 0.14f), -41f, 41f);
                        look = new Vector3(Mathf.Clamp(leadX, -47f, 47f), 0.55f, Mathf.Clamp(leadZ, -25f, 25f) * 0.75f);
                        float boxZoom = Mathf.InverseLerp(30f, 48f, Mathf.Abs(bp.x));
                        Vector3 far = new Vector3(x, 21f, -Arena.HalfWidth - 24f);
                        pos = look + (far - look) * Mathf.Lerp(0.75f, 0.66f, boxZoom);
                        fov = Mathf.Lerp(36f, 33.5f, boxZoom);
                        break;
                    }
                case Modo.LateralProxima:
                    {
                        // câmera próxima na beira da lateral frontal: enquadra os dois lados do campo de frente simultaneamente
                        float cx = Mathf.Clamp((bp.x + ballLead.x) * 0.12f + peek.x * 6f, -5.5f, 5.5f);
                        float cz = Mathf.Clamp(bp.z * 0.16f + peek.y * 3f, -3.5f, 4.5f) + 2.2f;
                        pos = new Vector3(cx, 12.8f, -Arena.HalfWidth - 9.2f);
                        ClampInsideFence(ref pos);
                        look = new Vector3(cx * 0.75f, 0.55f, cz);
                        float aspect = cam != null && cam.aspect > 0.5f ? cam.aspect : 1.7778f;
                        fov = Mathf.Clamp(2f * Mathf.Atan(1.16f / aspect) * Mathf.Rad2Deg, 58f, 74f);
                        break;
                    }
                case Modo.VisaoJogador:
                    {
                        // olhos do jogador: estabiliza o balanço vertical da corrida e acompanha a bola na visão periférica
                        Transform headTf = ActiveHead();
                        Vector3 rawHead = headTf != null ? headTf.position : pp + Vector3.up * 1.66f;
                        if (smoothedHeadY <= 0.01f || Mathf.Abs(rawHead.y - smoothedHeadY) > 0.6f) smoothedHeadY = rawHead.y;
                        else smoothedHeadY = Mathf.Lerp(smoothedHeadY, rawHead.y, 1f - Mathf.Exp(-10f * Time.deltaTime));
                        Vector3 head = new Vector3(rawHead.x, smoothedHeadY, rawHead.z);
                        Vector3 f = player != null ? player.forward : Vector3.right * ad;
                        f.y = 0f;
                        f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.right * ad;
                        if (peek.sqrMagnitude > 0.001f) f = Quaternion.Euler(0f, peek.x * 38f, 0f) * f;
                        pos = head + f * 0.13f + Vector3.up * 0.05f;
                        Vector3 ahead = pos + f * 12f + Vector3.down * (1.05f - peek.y * 2.4f);
                        Vector3 toB = bp - pos; toB.y = 0f;
                        float vis = toB.sqrMagnitude > 0.1f ? Mathf.Clamp01((Vector3.Dot(toB.normalized, f) - 0.1f) / 0.65f) : 0f;
                        look = Vector3.Lerp(ahead, bp + Vector3.up * 0.2f, vis * 0.52f);
                        fov = 72f;
                        break;
                    }
                case Modo.Comemoracao:
                    {
                        Vector3 f = player != null ? player.forward : Vector3.right;
                        f.y = 0f;
                        f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.right;
                        pos = pp + f * 4.5f + Vector3.up * 1.9f + Vector3.Cross(Vector3.up, f) * 1.2f;
                        ClampInsideFence(ref pos);
                        look = pp + Vector3.up * 1.2f;
                        fov = 46f;
                        break;
                    }
                case Modo.TV:
                    {
                        float focusX = Mathf.Lerp(pp.x, bp.x + ballLead.x * 0.7f, 0.55f) + peek.x * 6f;
                        float x = Mathf.Clamp(focusX, -38f, 46f);
                        pos = new Vector3(x - 4f, 17f, -Arena.HalfWidth - 16f);
                        look = new Vector3(focusX + 3f, 0.5f, (Mathf.Lerp(pp.z, bp.z, 0.5f) + peek.y * 4f) * 0.8f);
                        fov = 34f;
                        break;
                    }
                case Modo.Aerea:
                    {
                        Vector3 c = Vector3.Lerp(pp, bp + ballLead * 0.6f, 0.5f);
                        pos = new Vector3(c.x - 14f * ad + peek.x * 5f, 32f, c.z * 0.6f + peek.y * 5f);
                        look = new Vector3(c.x + 4f * ad + peek.x * 5f, 0f, c.z * 0.8f + peek.y * 5f);
                        fov = 48f;
                        break;
                    }
                case Modo.AtrasDoGol:
                    {
                        pos = Arena.AttackGoalCenter + new Vector3(8.5f, 4.2f, Mathf.Clamp(bp.z * 0.25f, -3.5f, 3.5f));
                        ClampInsideFence(ref pos);
                        look = new Vector3(Mathf.Lerp(bp.x, Arena.HalfLength - 12f, 0.3f), 0.8f, bp.z * 0.7f);
                        fov = 50f;
                        break;
                    }
                case Modo.Goleiro:
                    {
                        float gkSide = Mathf.Abs(kp.x) > 1f ? Mathf.Sign(kp.x) : 1f;
                        Vector3 toBall = bp - kp; toBall.y = 0f;
                        Vector3 dir = toBall.sqrMagnitude > 0.5f ? toBall.normalized : -Vector3.right * gkSide;
                        pos = kp - dir * 4.5f + Vector3.up * 2.6f;
                        pos.x = gkSide > 0f
                            ? Mathf.Min(pos.x, Arena.HalfLength + 1.6f)
                            : Mathf.Max(pos.x, -Arena.HalfLength - 1.6f);
                        ClampInsideFence(ref pos);
                        look = Vector3.Lerp(kp + Vector3.up * 1.2f, bp + ballLead * 0.35f, 0.62f);
                        fov = 58f;
                        break;
                    }
                case Modo.Dinamica:
                    {
                        Vector3 toGoal = goal - pp; toGoal.y = 0f;
                        float bylineBlend = Mathf.InverseLerp(3.5f, 15f, Mathf.Abs(goal.x - pp.x));
                        Vector3 goalDir = toGoal.sqrMagnitude > 1f ? toGoal.normalized : Vector3.right * ad;
                        Vector3 dir = Vector3.Slerp(Vector3.right * ad, goalDir, bylineBlend).normalized;
                        Vector3 side = Vector3.Cross(Vector3.up, dir);
                        pos = pp - dir * 3.6f + Vector3.up * 1.48f + side * 1.05f;
                        ClampInsideFence(ref pos);
                        look = pp + dir * (6f + peek.y * 3f) + side * (peek.x * 3.2f) + Vector3.up * 1.0f;
                        fov = 60f;
                        break;
                    }
                case Modo.Menu:
                    {
                        orbit += Time.deltaTime * 4f;
                        Vector3 c = new Vector3(38f, 0f, 0f);
                        pos = c + Quaternion.Euler(0f, orbit, 0f) * new Vector3(-26f, 9f, -18f);
                        look = Arena.AttackGoalCenter + new Vector3(-10f, 1.2f, 0f);
                        fov = 45f;
                        break;
                    }
                default:
                    {
                        // atrás do atacante, enquadrando a corrida até o gol sem chicotear na linha de fundo
                        Vector3 toGoal = goal - pp; toGoal.y = 0f;
                        float bylineBlend = Mathf.InverseLerp(4f, 16f, Mathf.Abs(goal.x - pp.x));
                        Vector3 goalDir = toGoal.sqrMagnitude > 1f ? toGoal.normalized : Vector3.right * ad;
                        Vector3 dir = matchMode ? Vector3.right * ad : Vector3.Slerp(Vector3.right * ad, goalDir, bylineBlend).normalized;
                        Vector3 side = Vector3.Cross(Vector3.up, dir);
                        float dist = matchMode ? 9f : Mathf.Lerp(5.0f, 6.4f, Mathf.Clamp01(toGoal.magnitude / 35f));
                        pos = pp - dir * dist + Vector3.up * (matchMode ? 4.2f : 2.35f) + side * Mathf.Clamp((bp - pp).z * 0.15f, -1.2f, 1.2f);
                        ClampInsideFence(ref pos);
                        Vector3 baseLook = Vector3.Lerp(pp + Vector3.up * 1.0f + dir * 4.5f,
                            matchMode ? Vector3.Lerp(pp, bp + ballLead * 0.5f, 0.5f) + dir * 6f : goal, 0.14f);
                        look = baseLook + side * (peek.x * 3.5f) + dir * (peek.y * 3.0f);
                        fov = matchMode ? 58f : 54f;
                        break;
                    }
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 targetLead = Vector3.zero;
            if (ball != null && !ball.IsHeld)
            {
                Vector3 bv = ball.Velocity;
                targetLead = Vector3.ClampMagnitude(new Vector3(bv.x, 0f, bv.z) * 0.22f, 4.5f);
            }
            ballLead = Vector3.Lerp(ballLead, targetLead, 1f - Mathf.Exp(-4.5f * dt));
            peek = Vector2.Lerp(peek, InputBridge.Look, 1f - Mathf.Exp(-8f * dt));

            Vector3 pos, look; float fov;
            Desired(out pos, out look, out fov);
            bool pov = mode == Modo.VisaoJogador;
            UpdateHeadHider(pov);
            if (cam != null) cam.nearClipPlane = pov ? 0.05f : 0.1f;
            float smooth = mode == Modo.Menu ? 0.5f : (mode == Modo.Dinamica ? 0.12f : (pov ? 0.03f : 0.20f));
            transform.position = Vector3.SmoothDamp(transform.position, pos, ref vel, smooth);
            lookPoint = Vector3.Lerp(lookPoint, look, 1f - Mathf.Exp(-(pov ? 11f : 6.5f) * dt));
            Vector3 fwd = lookPoint - transform.position;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.right;
            Quaternion q = Quaternion.LookRotation(fwd);
            if (shake > 0f)
            {
                q *= Quaternion.Euler(Random.insideUnitSphere * shake * 20f);
                shake = Mathf.MoveTowards(shake, 0f, dt * 0.16f);
            }
            transform.rotation = q;
            if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-4.5f * dt));
        }

        /// <summary>Na visão do jogador, oculta apenas as malhas da cabeça e cabelo (mantendo sombra e sem deformar o pescoço).</summary>
        void UpdateHeadHider(bool pov)
        {
            Transform targetRoot = pov ? player : null;
            if (hider != null && (targetRoot == null || hider.targetRoot != targetRoot))
            {
                hider.Restore();
                Destroy(hider);
                hider = null;
            }
            if (targetRoot != null && hider == null)
            {
                hider = targetRoot.gameObject.AddComponent<PovHeadHider>();
                hider.Bind(targetRoot);
            }
        }
    }

    /// <summary>
    /// Oculta a cabeça e o corte de cabelo 3D na visão em primeira pessoa usando ShadowsOnly:
    /// a câmera não vê o interior da malha, a sombra da cabeça continua no gramado e o pescoço não deforma.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    public class PovHeadHider : MonoBehaviour
    {
        public Transform targetRoot;
        readonly System.Collections.Generic.List<Renderer> renderers = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<UnityEngine.Rendering.ShadowCastingMode> modes = new System.Collections.Generic.List<UnityEngine.Rendering.ShadowCastingMode>();

        public void Bind(Transform root)
        {
            targetRoot = root;
            renderers.Clear();
            modes.Clear();
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || PlayerLook.EhCorpo(r)) continue;
                renderers.Add(r);
                modes.Add(r.shadowCastingMode);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null)
                    renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        public void Restore()
        {
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null)
                    renderers[i].shadowCastingMode = modes[i];
            renderers.Clear();
            modes.Clear();
        }

        void OnDestroy() { Restore(); }
    }
}
