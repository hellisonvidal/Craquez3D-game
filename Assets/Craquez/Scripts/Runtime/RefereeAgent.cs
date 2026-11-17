using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Equipe de arbitragem: 1 árbitro e 2 assistentes em lados opostos do campo.
    /// - Árbitro: acompanha o lance pelo sistema diagonal, a uns 12–20 m da bola, sem atrapalhar a jogada;
    ///   apita, mostra cartões e sinaliza vantagem.
    /// - Assistentes: cada um corre na sua lateral cobrindo uma metade do campo (um embaixo cuidando de +X,
    ///   outro em cima cuidando de -X), alinhado ao penúltimo defensor (ou à bola, se ela estiver além);
    ///   levantam a bandeira no impedimento e na bola que sai pela lateral.
    /// </summary>
    public class RefereeAgent : MonoBehaviour
    {
        public enum Papel { Principal, Assistente }

        public Papel papel;
        [Tooltip("Assistente: +1 cobre a metade +X (lateral de baixo), -1 a metade -X (lateral de cima).")]
        public float half;
        public float SideZ { get { return papel == Papel.Assistente ? -half * (Arena.HalfWidth + 1.4f) : transform.position.z; } }

        MatchSession match;
        Animator anim;
        Vector3 velocity;
        float gestureUntil, flagUntil;
        Transform flag, card;
        Renderer cardRenderer;
        Material yellowMat, redMat;
        float diagonalSide = 1f;
        static readonly int SpeedHash = Animator.StringToHash("Speed");

        public void Setup(MatchSession m, Papel p, float halfSide, Material propMaterial)
        {
            match = m;
            papel = p;
            half = halfSide;
            // árbitros não trombam com os jogadores (a bola ainda pode bater neles, como no jogo de verdade)
            gameObject.layer = 0;
            anim = GetComponentInChildren<Animator>();
            if (anim != null) { anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            var pc = GetComponent<PlayerController>();
            if (pc != null) Destroy(pc);
            Transform hand = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (propMaterial != null && hand != null)
            {
                yellowMat = new Material(propMaterial); Tint(yellowMat, new Color(1f, 0.85f, 0.05f));
                redMat = new Material(propMaterial); Tint(redMat, new Color(0.9f, 0.08f, 0.06f));
                if (p == Papel.Assistente)
                {
                    flag = new GameObject("Bandeira").transform;
                    flag.SetParent(hand, false);
                    var pole = Prop(PrimitiveType.Cylinder, flag, new Vector3(0f, 0.25f, 0f), new Vector3(0.018f, 0.3f, 0.018f), propMaterial);
                    pole.name = "Haste";
                    var cloth = Prop(PrimitiveType.Cube, flag, new Vector3(0.12f, 0.46f, 0f), new Vector3(0.22f, 0.16f, 0.006f), redMat);
                    cloth.name = "Pano";
                    var cloth2 = Prop(PrimitiveType.Cube, flag, new Vector3(0.12f, 0.46f, 0.004f), new Vector3(0.11f, 0.08f, 0.006f), yellowMat);
                    cloth2.transform.localPosition = new Vector3(0.065f, 0.5f, 0.004f);
                    // a escala do osso da mão varia entre modelos: compensa para ficar com o tamanho real
                    float ls = hand.lossyScale.y;
                    if (ls > 1e-4f) flag.localScale = Vector3.one / ls;
                }
                else
                {
                    card = Prop(PrimitiveType.Cube, hand, new Vector3(0f, 0.1f, 0f), new Vector3(0.07f, 0.1f, 0.004f), yellowMat).transform;
                    float ls = hand.lossyScale.y;
                    if (ls > 1e-4f) { card.localScale /= ls; card.localPosition /= ls; }
                    cardRenderer = card.GetComponent<Renderer>();
                    card.gameObject.SetActive(false);
                }
            }
        }

        static void Tint(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static GameObject Prop(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(t);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null && mat != null) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            return go;
        }

        void OnDestroy()
        {
            if (yellowMat != null) Destroy(yellowMat);
            if (redMat != null) Destroy(redMat);
        }

        // ------------------------------------------------------------ ciclo (chamado pela partida)

        public void Tick(float dt)
        {
            if (match == null || !match.Active) return;
            Vector3 target = papel == Papel.Principal ? MainTarget() : AssistantTarget();
            Vector3 look = match.BallPos;
            if (papel == Papel.Assistente) look = new Vector3(transform.position.x, 0f, 0f) * 0.3f + match.BallPos * 0.7f;

            Vector3 to = target - transform.position; to.y = 0f;
            float dist = to.magnitude;
            float max = dist > 14f ? 6.8f : (dist > 4f ? 4.2f : 1.6f);
            if (papel == Papel.Assistente) max = dist > 8f ? 7f : (dist > 2f ? 4f : 1.5f);
            Vector3 desired = dist > 0.5f ? to / dist * Mathf.Min(max, dist * 1.4f) : Vector3.zero;
            bool gesturing = Time.time < gestureUntil;
            if (gesturing) desired = Vector3.zero;
            float accel = desired.magnitude > velocity.magnitude ? 8f : 12f;
            velocity = Vector3.MoveTowards(velocity, desired, accel * dt);
            Vector3 p = transform.position + velocity * dt;
            p.y = 0f;
            transform.position = p;

            // corre de frente quando vai longe; senão vira para o lance (assistente corre de lado)
            Vector3 face = velocity.magnitude > 3f && papel == Papel.Principal ? velocity : look - transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), 360f * dt);
            if (anim != null) anim.SetFloat(SpeedHash, velocity.magnitude, 0.1f, dt);

            if (flag != null) flag.gameObject.SetActive(true);
            if (flagUntil > 0f && Time.time >= flagUntil)
            {
                flagUntil = 0f;
                if (anim != null) anim.CrossFadeInFixedTime("Locomocao", 0.25f);
            }
            if (card != null && card.gameObject.activeSelf && !gesturing) card.gameObject.SetActive(false);
        }

        Vector3 MainTarget()
        {
            Vector3 b = match.BallPos; b.y = 0f;
            if (match.Phase == MatchSession.Fase.BolaParada && match.Restart != null)
                b = match.Restart.spot;
            // sistema diagonal: fica atrás e ao lado do lance, do lado oposto ao assistente daquela metade
            if (Mathf.Abs(b.x) > 6f) diagonalSide = Mathf.Sign(b.x);
            float back = 12f;
            Vector3 t = b + new Vector3(-diagonalSide * back * 0.6f, 0f, diagonalSide * 11f);
            // não fica na linha da bola com o gol nem em cima de jogadores
            if (match.Times != null)
                foreach (var m in match.All)
                {
                    if (m == null || m.expulso) continue;
                    Vector3 d = t - m.Pos; d.y = 0f;
                    if (d.magnitude < 3f && d.sqrMagnitude > 1e-3f) t += d.normalized * (3f - d.magnitude);
                }
            t.x = Mathf.Clamp(t.x, -Arena.HalfLength + 4f, Arena.HalfLength - 4f);
            t.z = Mathf.Clamp(t.z, -Arena.HalfWidth + 3f, Arena.HalfWidth - 3f);
            return t;
        }

        Vector3 AssistantTarget()
        {
            // alinha com o penúltimo defensor do time que defende o gol desta metade (ou com a bola, se ela estiver além)
            float x = 0f;
            if (match.Times != null)
            {
                TimeInfo defending = match.Times[0].OwnGoal.x * half > 0f ? match.Times[0] : match.Times[1];
                TimeInfo attacking = match.OpponentOf(defending);
                float line = match.OffsideLineStrict(attacking);         // em coordenadas de quem ataca
                float ballF = attacking.Fwd(match.BallPos);
                float f = Mathf.Max(line, ballF);
                x = f * attacking.attackDir;
            }
            if (x * half < 0f) x = 0f;                                    // não sai da sua metade
            x = Mathf.Clamp(x, -Arena.HalfLength, Arena.HalfLength);
            return new Vector3(x, 0f, SideZ);
        }

        // ------------------------------------------------------------ gestos

        public void Whistle(int times)
        {
            if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.Whistle(times);
            if (papel == Papel.Principal && anim != null && Time.time > gestureUntil)
            {
                anim.CrossFadeInFixedTime("Apitar", 0.15f);
                gestureUntil = Time.time + 0.9f;
            }
        }

        public void ShowCard(bool red)
        {
            if (card != null)
            {
                card.gameObject.SetActive(true);
                if (cardRenderer != null) cardRenderer.sharedMaterial = red ? redMat : yellowMat;
            }
            if (anim != null) anim.CrossFadeInFixedTime("Cartao", 0.2f);
            gestureUntil = Time.time + 2.2f;
        }

        public void SignalAdvantage()
        {
            if (anim != null && Time.time > gestureUntil) anim.CrossFadeInFixedTime("Conversar", 0.2f);
            gestureUntil = Time.time + 0.6f;
        }

        public void RaiseFlag(float seconds)
        {
            if (anim != null) anim.CrossFadeInFixedTime("Bandeira", 0.15f);
            flagUntil = Time.time + seconds;
            gestureUntil = Time.time + seconds * 0.8f;
        }
    }
}
