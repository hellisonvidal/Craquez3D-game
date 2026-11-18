using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Craquez.EditorTools
{
    /// <summary>
    /// Diagnóstico de animação (roda sem abrir a Unity, no GitHub Actions):
    /// aplica cada clipe nos prefabs reais (retarget Humanoid da própria Unity) e grava, quadro a quadro,
    /// a posição de cada articulação em Diagnostico/poses.json — para conferir pernas, pés e braços.
    /// Unity -batchmode -quit -projectPath . -executeMethod Craquez.EditorTools.CraquezDiag.Run
    /// </summary>
    public static class CraquezDiag
    {
        static readonly HumanBodyBones[] Bones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
        };

        public static void Run()
        {
            int code = 1;
            try
            {
                if (!CraquezSetup.Configure(false)) throw new Exception("setup falhou");
                Directory.CreateDirectory("Diagnostico");
                var sb = new StringBuilder();
                sb.Append("[\n");
                bool first = true;
                Sample("Jogador", "Jogador", new[] { "Idle", "Andar", "Trote", "Correr", "Disparada", "ChuteForte", "ChuteColocado", "Cavadinha" }, sb, ref first);
                Sample("Jogador", "Jogador/Jogadas", new[] { "FintaCorpoDir", "PedaladaDir", "PuxadaDir", "Arrancada", "Freada" }, sb, ref first);
                Sample("Goleiro", "Goleiro", new[] { "GK_Pronto", "GK_DeslocarDir", "GK_Mergulho", "GK_Salto", "GK_Defesa", "GK_Levantar" }, sb, ref first);
                sb.Append("\n]\n");
                File.WriteAllText("Diagnostico/poses.json", sb.ToString());
                Debug.Log("[Craquez 3D] Diagnóstico salvo em Diagnostico/poses.json");
                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[Craquez 3D] Diagnóstico falhou: " + e);
            }
            EditorApplication.Exit(code);
        }

        static AnimationClip LoadClip(string folder, string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Craquez/Animations/" + folder + "/" + name + ".fbx"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        static void Sample(string prefabName, string folder, string[] clips, StringBuilder sb, ref bool first)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Craquez/Gerado/Prefabs/" + prefabName + ".prefab");
            if (prefab == null) { Debug.LogError("prefab ausente: " + prefabName); return; }
            var go = (GameObject)UnityEngine.Object.Instantiate(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var anim = go.GetComponentInChildren<Animator>();
            Debug.Log("[Diag] " + prefabName + " avatar=" + (anim.avatar != null ? anim.avatar.name : "null") + " humano=" + (anim.avatar != null && anim.avatar.isHuman) +
                      " escala=" + anim.transform.localScale + " pos=" + anim.transform.localPosition + " humanScale=" + anim.humanScale);

            // mapeamento dos ossos e limites (pernas/pés)
            var mi = AssetImporter.GetAtPath("Assets/Craquez/Models/" + prefabName + ".fbx") as ModelImporter;
            if (mi != null)
                foreach (var hb in mi.humanDescription.human)
                    if (hb.humanName.Contains("Leg") || hb.humanName.Contains("Foot") || hb.humanName.Contains("Toes"))
                        Debug.Log("[Diag] mapa " + prefabName + " " + hb.humanName + " -> " + hb.boneName + " limite padrão=" + hb.limit.useDefaultValues);
            var hph = new HumanPoseHandler(anim.avatar, anim.transform);
            var hp = new HumanPose();
            string[] musc = { "Left Foot Up-Down", "Left Foot Twist In-Out", "Left Lower Leg Twist In-Out", "Left Upper Leg Twist In-Out",
                              "Right Foot Up-Down", "Right Foot Twist In-Out", "Right Lower Leg Twist In-Out", "Right Upper Leg Twist In-Out" };
            var mIdx = new int[musc.Length];
            for (int i = 0; i < musc.Length; i++) mIdx[i] = Array.IndexOf(HumanTrait.MuscleName, musc[i]);

            // pose de repouso (sem animação)
            Write(sb, ref first, prefabName, "REPOUSO", 0f, anim, go.transform);

            foreach (var name in clips)
            {
                var clip = LoadClip(folder, name);
                if (clip == null) { Debug.LogWarning("[Diag] clipe ausente " + name); continue; }
                Debug.Log("[Diag] " + name + " len=" + clip.length + " humanMotion=" + clip.humanMotion + " loop=" + clip.isLooping + " avgSpeed=" + clip.averageSpeed);
                var graph = PlayableGraph.Create("diag");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "o", anim);
                var cp = AnimationClipPlayable.Create(graph, clip);
                cp.SetApplyFootIK(false);
                output.SetSourcePlayable(cp);
                graph.Play();
                const int N = 8;
                for (int k = 0; k <= N; k++)
                {
                    float t = clip.length * k / N;
                    cp.SetTime(t);
                    graph.Evaluate(0f);
                    Write(sb, ref first, prefabName, name, t, anim, go.transform);
                    if (prefabName == "Jogador" && (name == "Correr" || name == "Andar") && k % 2 == 0)
                    {
                        hph.GetHumanPose(ref hp);
                        var m = new StringBuilder("[Diag] musc " + name + " t=" + t.ToString("0.00") + ":");
                        for (int i = 0; i < musc.Length; i++) if (mIdx[i] >= 0) m.Append(" ").Append(musc[i].Replace(" ", "")).Append("=").Append(hp.muscles[mIdx[i]].ToString("0.00"));
                        Debug.Log(m.ToString());
                    }
                }
                graph.Destroy();
            }
            hph.Dispose();
            UnityEngine.Object.DestroyImmediate(go);
        }

        static void Write(StringBuilder sb, ref bool first, string model, string clip, float t, Animator anim, Transform root)
        {
            if (!first) sb.Append(",\n");
            first = false;
            sb.Append("{\"model\":\"").Append(model).Append("\",\"clip\":\"").Append(clip).Append("\",\"t\":").Append(t.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(",\"bones\":{");
            bool f2 = true;
            foreach (var b in Bones)
            {
                var tr = anim.GetBoneTransform(b);
                if (tr == null) continue;
                Vector3 p = root.InverseTransformPoint(tr.position);
                if (!f2) sb.Append(',');
                f2 = false;
                sb.Append('"').Append(b.ToString()).Append("\":[")
                  .Append(p.x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                  .Append(p.y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                  .Append(p.z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append(']');
            }
            sb.Append("}}");
        }
    }
}
