using System;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Camada 2 & 3 da Arquitetura de Personagem:
    /// - Mapeia e vincula programaticamente os ossos Mixamo (mixamorig:Hips, Spine2, Hands, ForeArms, Feet, ToeBase)
    ///   com suporte híbrido a Humanoid Avatar e busca direta por nome na hierarquia.
    /// - Cria e atualiza os Sockets Virtuais:
    ///     * BallControlPoint (0.35m a 0.45m à frente do Root para condução magnética/toques)
    ///     * GK_CatchSocket (centralizado entre Hand_L e Hand_R à frente do mixamorig:Spine2 para encaixe firme)
    /// - Trava deslocamentos residuais horizontais (X, Z) do osso mixamorig:Hips em animações In-Place de locomoção,
    ///   impedindo que a malha 3D desalinhe do CapsuleCollider principal.
    /// </summary>
    [DefaultExecutionOrder(950)]
    public class MixamoBoneSockets : MonoBehaviour
    {
        [Header("Referências do Rig Mixamo (Camada 2 e 3)")]
        public Animator animator;
        public Transform modelTransform;

        [Header("Ossos Mapeados (mixamorig:)")]
        public Transform Hips;          // mixamorig:Hips
        public Transform Chest;         // mixamorig:Spine2
        public Transform Foot_R;        // mixamorig:RightFoot
        public Transform Toe_R;         // mixamorig:RightToeBase
        public Transform Foot_L;        // mixamorig:LeftFoot
        public Transform Toe_L;         // mixamorig:LeftToeBase
        public Transform Hand_R;        // mixamorig:RightHand
        public Transform ForeArm_R;     // mixamorig:RightForeArm
        public Transform Hand_L;        // mixamorig:LeftHand
        public Transform ForeArm_L;     // mixamorig:LeftForeArm
        public Transform Head;          // mixamorig:Head

        [Header("Sockets Virtuais (Camada 3)")]
        public Transform BallControlPoint;
        public Transform GK_CatchSocket;

        [Header("Configuração de Projeção e In-Place Lock")]
        [Tooltip("Distância base do BallControlPoint à frente do Root (0.35m em baixa velocidade até 0.45m em corrida).")]
        [Range(0.30f, 0.50f)]
        public float ballControlDistance = 0.40f;

        [Tooltip("Trava deslocamentos horizontais residuais (X/Z) do mixamorig:Hips durante locomoção In-Place.")]
        public bool lockHipsHorizontalInPlace = true;

        Vector3 hipsRestLocalPos;
        bool hipsRestCaptured;

        public Vector3 RightFootContactPosition
        {
            get
            {
                if (Toe_R != null && Foot_R != null) return Vector3.Lerp(Foot_R.position, Toe_R.position, 0.65f);
                if (Toe_R != null) return Toe_R.position;
                if (Foot_R != null) return Foot_R.position;
                return transform.position + transform.forward * 0.35f + transform.right * 0.12f;
            }
        }

        public Vector3 LeftFootContactPosition
        {
            get
            {
                if (Toe_L != null && Foot_L != null) return Vector3.Lerp(Foot_L.position, Toe_L.position, 0.65f);
                if (Toe_L != null) return Toe_L.position;
                if (Foot_L != null) return Foot_L.position;
                return transform.position + transform.forward * 0.35f - transform.right * 0.12f;
            }
        }

        /// <summary>Centro real da palma direita projetado a partir do antebraço e punho.</summary>
        public Vector3 RightPalmCenter
        {
            get
            {
                if (Hand_R == null) return transform.position + Vector3.up * 1.2f + transform.right * 0.3f;
                if (ForeArm_R == null) return Hand_R.position;
                Vector3 dir = (Hand_R.position - ForeArm_R.position);
                return Hand_R.position + (dir.sqrMagnitude > 1e-6f ? dir.normalized * 0.08f : Vector3.zero);
            }
        }

        /// <summary>Centro real da palma esquerda projetado a partir do antebraço e punho.</summary>
        public Vector3 LeftPalmCenter
        {
            get
            {
                if (Hand_L == null) return transform.position + Vector3.up * 1.2f - transform.right * 0.3f;
                if (ForeArm_L == null) return Hand_L.position;
                Vector3 dir = (Hand_L.position - ForeArm_L.position);
                return Hand_L.position + (dir.sqrMagnitude > 1e-6f ? dir.normalized * 0.08f : Vector3.zero);
            }
        }

        /// <summary>Ponto médio dinâmico entre as duas luvas/mãos (centro da Esfera de Interceptação).</summary>
        public Vector3 GlovesMidpoint
        {
            get
            {
                if (Hand_L != null && Hand_R != null)
                    return (LeftPalmCenter + RightPalmCenter) * 0.5f;
                if (Chest != null)
                    return Chest.position + transform.forward * 0.32f;
                return transform.position + Vector3.up * 1.15f + transform.forward * 0.35f;
            }
        }

        void Awake()
        {
            InitializeSockets();
        }

        public void InitializeSockets()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null && modelTransform == null) modelTransform = animator.transform;

            BindMixamoBones();
            CreateVirtualSockets();

            if (Hips != null && !hipsRestCaptured)
            {
                hipsRestLocalPos = Hips.localPosition;
                hipsRestCaptured = true;
            }
        }

        void BindMixamoBones()
        {
            Transform searchRoot = modelTransform != null ? modelTransform : transform;

            // 1. Busca nominal explícita na hierarquia Mixamo (prioridade para mixamorig: exato)
            Hips = FindBone(searchRoot, "mixamorig:Hips", "Hips");
            Chest = FindBone(searchRoot, "mixamorig:Spine2", "Spine2", "mixamorig:Spine1", "Chest");
            Foot_R = FindBone(searchRoot, "mixamorig:RightFoot", "RightFoot");
            Toe_R = FindBone(searchRoot, "mixamorig:RightToeBase", "RightToeBase", "mixamorig:RightToe_End");
            Foot_L = FindBone(searchRoot, "mixamorig:LeftFoot", "LeftFoot");
            Toe_L = FindBone(searchRoot, "mixamorig:LeftToeBase", "LeftToeBase", "mixamorig:LeftToe_End");
            Hand_R = FindBone(searchRoot, "mixamorig:RightHand", "RightHand");
            ForeArm_R = FindBone(searchRoot, "mixamorig:RightForeArm", "RightForeArm", "RightLowerArm");
            Hand_L = FindBone(searchRoot, "mixamorig:LeftHand", "LeftHand");
            ForeArm_L = FindBone(searchRoot, "mixamorig:LeftForeArm", "LeftForeArm", "LeftLowerArm");
            Head = FindBone(searchRoot, "mixamorig:Head", "Head");

            // 2. Fallback via Avatar Humanoid da Unity caso algum osso tenha sido renomeado
            if (animator != null && animator.isHuman)
            {
                if (Hips == null) Hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (Chest == null) Chest = animator.GetBoneTransform(HumanBodyBones.UpperChest)
                                        ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                                        ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                if (Foot_R == null) Foot_R = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (Toe_R == null) Toe_R = animator.GetBoneTransform(HumanBodyBones.RightToes) ?? Foot_R;
                if (Foot_L == null) Foot_L = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (Toe_L == null) Toe_L = animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? Foot_L;
                if (Hand_R == null) Hand_R = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (ForeArm_R == null) ForeArm_R = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                if (Hand_L == null) Hand_L = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (ForeArm_L == null) ForeArm_L = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                if (Head == null) Head = animator.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        void CreateVirtualSockets()
        {
            Transform existingBcp = transform.Find("BallControlPoint");
            if (existingBcp != null) BallControlPoint = existingBcp;
            else
            {
                var go = new GameObject("BallControlPoint");
                go.transform.SetParent(transform, false);
                BallControlPoint = go.transform;
            }
            // Inicializa na velocidade média (0.5)
            BallControlPoint.localPosition = new Vector3(0f, Arena.BallRadius, Mathf.Lerp(0.35f, 0.45f, 0.5f));
            BallControlPoint.localRotation = Quaternion.identity;

            Transform existingCatch = transform.Find("GK_CatchSocket");
            if (existingCatch != null) GK_CatchSocket = existingCatch;
            else
            {
                var go = new GameObject("GK_CatchSocket");
                go.transform.SetParent(transform, false);
                GK_CatchSocket = go.transform;
            }
            UpdateSockets(0.5f);
        }

        /// <summary>
        /// Atualiza a projeção do BallControlPoint (0.35m a 0.45m conforme velocidade)
        /// e centraliza o GK_CatchSocket entre Hand_L e Hand_R à frente do mixamorig:Spine2.
        /// </summary>
        public void UpdateSockets(float normalizedSpeed01)
        {
            if (BallControlPoint != null)
            {
                float d = Mathf.Lerp(0.35f, 0.45f, Mathf.Clamp01(normalizedSpeed01));
                ballControlDistance = d;
                BallControlPoint.localPosition = new Vector3(0f, Arena.BallRadius, d);
            }

            if (GK_CatchSocket != null)
            {
                if (Hand_L != null && Hand_R != null)
                {
                    Vector3 handsCenter = (LeftPalmCenter + RightPalmCenter) * 0.5f;
                    if (Chest != null)
                    {
                        Vector3 chestFront = Chest.position + transform.forward * 0.28f;
                        GK_CatchSocket.position = Vector3.Lerp(handsCenter, chestFront, 0.25f);
                    }
                    else
                    {
                        GK_CatchSocket.position = handsCenter;
                    }
                    GK_CatchSocket.rotation = transform.rotation;
                }
                else if (Chest != null)
                {
                    GK_CatchSocket.position = Chest.position + transform.forward * 0.34f;
                    GK_CatchSocket.rotation = transform.rotation;
                }
                else
                {
                    GK_CatchSocket.localPosition = new Vector3(0f, 1.15f, 0.38f);
                    GK_CatchSocket.localRotation = Quaternion.identity;
                }
            }
        }

        void LateUpdate()
        {
            if (lockHipsHorizontalInPlace && Hips != null && modelTransform != null)
            {
                // Mantém o quadril alinhado horizontalmente apenas contra desvios extremos (> 0.35m) sem entortar o tronco
                Vector3 localInRoot = transform.InverseTransformPoint(Hips.position);
                const float maxHorizontalDrift = 0.35f;
                Vector2 xz = new Vector2(localInRoot.x, localInRoot.z);
                if (xz.sqrMagnitude > maxHorizontalDrift * maxHorizontalDrift)
                {
                    Vector2 clamped = xz.normalized * maxHorizontalDrift;
                    Vector3 correctedWorld = transform.TransformPoint(new Vector3(clamped.x, localInRoot.y, clamped.y));
                    Hips.position = correctedWorld;
                }
            }

            UpdateSockets(0f);
        }

        /// <summary>Menor distância entre os pés mapeados (Foot_R/Toe_R ou Foot_L/Toe_L) e um ponto no mundo.</summary>
        public float GetClosestFootDistance(Vector3 worldTarget, out Transform closestFootBone, out Vector3 contactWorldPos)
        {
            Vector3 rPos = RightFootContactPosition;
            Vector3 lPos = LeftFootContactPosition;
            Vector2 b2 = new Vector2(worldTarget.x, worldTarget.z);
            float dR = Vector2.Distance(b2, new Vector2(rPos.x, rPos.z));
            float dL = Vector2.Distance(b2, new Vector2(lPos.x, lPos.z));

            if (dR <= dL)
            {
                closestFootBone = Toe_R != null ? Toe_R : Foot_R;
                contactWorldPos = rPos;
                return dR;
            }
            else
            {
                closestFootBone = Toe_L != null ? Toe_L : Foot_L;
                contactWorldPos = lPos;
                return dL;
            }
        }

        static Transform FindBone(Transform root, params string[] names)
        {
            if (root == null) return null;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int n = 0; n < names.Length; n++)
            {
                string target = names[n];
                for (int i = 0; i < all.Length; i++)
                {
                    if (string.Equals(all[i].name, target, StringComparison.OrdinalIgnoreCase))
                        return all[i];
                }
            }
            // Fallback por sufixo (caso o exportador adicione prefixo de namespace diferente)
            for (int n = 0; n < names.Length; n++)
            {
                string suffix = names[n].Replace("mixamorig:", "");
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name.EndsWith(":" + suffix, StringComparison.OrdinalIgnoreCase))
                        return all[i];
                }
            }
            return null;
        }
    }
}
