using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Mantém os pés do personagem apoiados no gramado: depois da animação (LateUpdate), mede a altura
    /// dos tornozelos e dos dedos em relação à pose de repouso e corrige a altura do modelo, suavemente.
    /// Assim o jogador não "flutua" nem afunda, seja qual for a escala do rig ou a altura do clipe.
    /// Pode ser desligado (ex.: goleiro mergulhando ou saltando).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class FootGrounder : MonoBehaviour
    {
        public bool grounding = true;
        [Tooltip("Altura do personagem (m), igual à usada no prefab.")]
        public float height = 1.8f;
        // medidas dos modelos (tools/blender): tornozelo e dedos acima da sola, em fração da altura
        const float AnkleRatio = 0.088f, ToeRatio = 0.0265f;
        [Tooltip("Rapidez da correção (1/s).")]
        public float speed = 14f;

        Animator anim;
        Transform model;
        Transform lFoot, rFoot, lToe, rToe;
        float lFootRest, rFootRest, lToeRest, rToeRest;
        Vector3 baseLocal;
        float offset;
        bool ready;

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            if (anim == null || !anim.isHuman) { enabled = false; return; }
            model = anim.transform;
            baseLocal = model.localPosition;
            lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            lToe = anim.GetBoneTransform(HumanBodyBones.LeftToes);
            rToe = anim.GetBoneTransform(HumanBodyBones.RightToes);
            if (lFoot == null || rFoot == null) { enabled = false; return; }
            // mede a altura real das articulações em repouso antes do Animator sobrescrever o primeiro quadro
            float y0 = transform.position.y;
            lFootRest = Mathf.Clamp(lFoot.position.y - y0, 0.06f, 0.22f);
            rFootRest = Mathf.Clamp(rFoot.position.y - y0, 0.06f, 0.22f);
            lToeRest = lToe != null ? Mathf.Clamp(lToe.position.y - y0, 0.02f, 0.12f) : AnkleRatio * height;
            rToeRest = rToe != null ? Mathf.Clamp(rToe.position.y - y0, 0.02f, 0.12f) : AnkleRatio * height;
            ready = true;
        }

        void Start()
        {
            if (!ready && enabled) Awake();
            if (!ready) return;
            // garante coerência caso height tenha sido ajustado no prefab
            if (lToe == null) lToeRest = ToeRatio * height;
            if (rToe == null) rToeRest = ToeRatio * height;
        }

        void LateUpdate()
        {
            if (!ready) return;
            float target = 0f; // desligado (mergulho/salto/levantar): retorna suavemente para 0 sem flutuar
            if (grounding)
            {
                float y0 = transform.position.y;
                // quanto o ponto de apoio mais baixo está acima (ou abaixo) do chão, já descontada a correção atual
                float c = Mathf.Min(
                    Mathf.Min(lFoot.position.y - y0 - lFootRest, rFoot.position.y - y0 - rFootRest),
                    Mathf.Min((lToe != null ? lToe.position.y : lFoot.position.y) - y0 - lToeRest,
                              (rToe != null ? rToe.position.y : rFoot.position.y) - y0 - rToeRest));
                target = offset - c;
                target = Mathf.Clamp(target, -0.35f, 0.35f);
            }
            offset = Mathf.Lerp(offset, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
            model.localPosition = baseLocal + Vector3.up * offset / Mathf.Max(1e-4f, transform.lossyScale.y);
        }
    }
}
