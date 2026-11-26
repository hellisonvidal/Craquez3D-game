using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Formato do rosto: cria, a partir da malha de cada modelo, deformações suaves na cabeça e nos cabelos 3D
    /// (blend shapes geradas em tempo de execução, sem mexer no FBX nem no esqueleto) e dá a cada jogador
    /// uma combinação: maxilar largo/fino, queixo longo/curto, nariz grande, bochechas cheias, testa alta,
    /// orelhas de abano e rosto comprido. Como os cabelos 3D acompanham exatamente o mesmo campo espacial
    /// da cabeça, o crânio nunca atravessa cortes curtos/raspados na testa ou nas têmporas.
    /// </summary>
    public static class FaceShapes
    {
        public static readonly string[] Nomes =
        {
            "MaxilarLargo", "MaxilarFino", "QueixoLongo", "QueixoCurto", "NarizGrande", "Bochechas", "TestaAlta", "Orelhas", "RostoLongo"
        };
        static readonly string[] Rotulos =
        {
            "maxilar largo", "maxilar fino", "queixo longo", "queixo curto", "nariz grande", "bochechas cheias", "testa alta", "orelhas de abano", "rosto comprido"
        };

        struct HeadFrame
        {
            public bool valid;
            public Vector3 hp, right, up, fwd;
            public float chin, U, noseX, noseZ, mx;
        }

        static readonly Dictionary<Mesh, Mesh> cache = new Dictionary<Mesh, Mesh>();
        static readonly Dictionary<Mesh, HeadFrame> frames = new Dictionary<Mesh, HeadFrame>();

        /// <summary>Libera as malhas criadas (fim da partida).</summary>
        public static void Limpar()
        {
            foreach (var m in cache.Values) if (m != null) Object.Destroy(m);
            cache.Clear();
            frames.Clear();
        }

        /// <summary>Sorteia e aplica um formato de rosto na cabeça e nos cabelos 3D; devolve a descrição (para o HUD).</summary>
        public static string Aplicar(GameObject go, System.Random rng)
        {
            SkinnedMeshRenderer smr = null;
            var hairs = new List<SkinnedMeshRenderer>();
            foreach (var x in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (x.name == "Cabeca") smr = x;
                else if (x.name.StartsWith("Cabelo_")) hairs.Add(x);
            }
            if (smr == null) smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            var anim = go.GetComponentInChildren<Animator>();
            if (smr == null || smr.sharedMesh == null || anim == null || !anim.isHuman) return "";

            Mesh src = smr.sharedMesh;
            Mesh m;
            HeadFrame frame;
            if (!cache.TryGetValue(src, out m))
            {
                m = BuildHead(smr, anim, go.transform, out frame);
                cache[src] = m;
                frames[src] = frame;
                if (m != null)
                {
                    cache[m] = m;
                    frames[m] = frame;
                }
            }
            else
            {
                frames.TryGetValue(src, out frame);
            }

            if (m == null || m.blendShapeCount < Nomes.Length) return "";
            smr.sharedMesh = m;
            for (int k = 0; k < Nomes.Length; k++) smr.SetBlendShapeWeight(k, 0f);

            // Propaga o mesmo conjunto de blend shapes para os cabelos 3D deste modelo (cacheados por malha)
            if (frame.valid)
            {
                for (int h = 0; h < hairs.Count; h++)
                {
                    var hs = hairs[h];
                    if (hs == null || hs.sharedMesh == null) continue;
                    Mesh hSrc = hs.sharedMesh;
                    Mesh hMesh;
                    if (!cache.TryGetValue(hSrc, out hMesh))
                    {
                        hMesh = BuildHair(hs, anim, frame);
                        cache[hSrc] = hMesh;
                        if (hMesh != null) cache[hMesh] = hMesh;
                    }
                    if (hMesh != null && hMesh.blendShapeCount >= Nomes.Length)
                    {
                        hs.sharedMesh = hMesh;
                        for (int k = 0; k < Nomes.Length; k++) hs.SetBlendShapeWeight(k, 0f);
                    }
                }
            }

            // 2 a 4 traços, sem combinar opostos (largo/fino, longo/curto)
            var picked = new List<int>();
            int n = 2 + rng.Next(3);
            for (int t = 0; t < 20 && picked.Count < n; t++)
            {
                int i = rng.Next(Nomes.Length);
                if (picked.Contains(i)) continue;
                if ((i == 0 && picked.Contains(1)) || (i == 1 && picked.Contains(0))) continue;
                if ((i == 2 && picked.Contains(3)) || (i == 3 && picked.Contains(2))) continue;
                picked.Add(i);
            }
            var desc = new List<string>();
            foreach (int i in picked)
            {
                float w = 35f + (float)rng.NextDouble() * 65f;
                smr.SetBlendShapeWeight(i, w);
                for (int h = 0; h < hairs.Count; h++)
                {
                    var hs = hairs[h];
                    if (hs != null && hs.sharedMesh != null && i < hs.sharedMesh.blendShapeCount)
                        hs.SetBlendShapeWeight(i, w);
                }
                if (w > 55f) desc.Add(Rotulos[i]);
            }
            return desc.Count > 0 ? string.Join(", ", desc) : "traços suaves";
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static Vector3 ComputeDelta(int shapeIdx, float x, float y, float z, float mx, Vector3 right, Vector3 up, Vector3 fwd, float scale)
        {
            float front = Smooth(-0.6f, -0.3f, z);
            float dx = 0f, dy = 0f, dz = 0f;
            switch (shapeIdx)
            {
                case 0: // maxilar largo
                    dx = x * 0.16f * (1f - Smooth(0.25f, 0.5f, y)) * front;
                    break;
                case 1: // maxilar fino
                    dx = -x * 0.13f * (1f - Smooth(0.25f, 0.5f, y)) * front;
                    break;
                case 2: // queixo longo
                    {
                        float c = (1f - Smooth(0.08f, 0.25f, y)) * Smooth(-0.4f, -0.2f, z) * (1f - Smooth(0.12f, 0.22f, Mathf.Abs(x)));
                        dy = -0.06f * c; dz = 0.025f * c;
                        break;
                    }
                case 3: // queixo curto
                    {
                        float c = (1f - Smooth(0.08f, 0.25f, y)) * Smooth(-0.4f, -0.2f, z) * (1f - Smooth(0.12f, 0.22f, Mathf.Abs(x)));
                        dy = 0.04f * c; dz = -0.02f * c;
                        break;
                    }
                case 4: // nariz grande
                    {
                        float r2 = (x * x + (y - 0.47f) * (y - 0.47f) * 0.6f) / (0.09f * 0.09f);
                        float c = Mathf.Exp(-r2) * Smooth(-0.15f, -0.03f, z);
                        dx = x * 0.35f * c; dy = (y - 0.47f) * 0.2f * c; dz = 0.045f * c;
                        break;
                    }
                case 5: // bochechas cheias
                    {
                        float c = Smooth(0.22f, 0.32f, y) * (1f - Smooth(0.45f, 0.55f, y)) * Smooth(0.1f, 0.2f, Mathf.Abs(x)) * front;
                        dx = Mathf.Sign(x) * 0.03f * c; dz = 0.012f * c;
                        break;
                    }
                case 6: // testa alta / crânio mais longo
                    dy = Mathf.Max(0f, y - 0.7f) * 0.2f;
                    break;
                case 7: // orelhas de abano
                    {
                        float c = Smooth(mx * 0.82f, mx * 0.96f, Mathf.Abs(x)) * Smooth(0.36f, 0.44f, y) * (1f - Smooth(0.6f, 0.68f, y));
                        dx = Mathf.Sign(x) * 0.06f * c; dz = -0.015f * c;
                        break;
                    }
                case 8: // rosto comprido
                    dy = -Mathf.Max(0f, 0.5f - y) * 0.12f * front;
                    break;
            }
            return (right * dx + up * dy + fwd * dz) * scale;
        }

        static Mesh BuildHead(SkinnedMeshRenderer smr, Animator anim, Transform root, out HeadFrame frame)
        {
            frame = default;
            Mesh src = smr.sharedMesh;
            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            var bones = smr.bones;
            int hi = System.Array.IndexOf(bones, head);
            var bind = src.bindposes;
            if (hi < 0 || hi >= bind.Length) return null;

            Vector3[] v = src.vertices;
            var bw = src.boneWeights;
            if (bw == null || bw.Length != v.Length) return null;

            // referencial da cabeça no espaço da malha (pose de repouso)
            Vector3 hp = bind[hi].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 up = smr.transform.InverseTransformDirection(Vector3.up).normalized;
            Vector3 fwd = smr.transform.InverseTransformDirection(root.forward);
            fwd = Vector3.ProjectOnPlane(fwd, up).normalized;
            Vector3 right = Vector3.Cross(up, fwd).normalized;

            int n = v.Length;
            var w = new float[n];
            var lx = new float[n]; var ly = new float[n]; var lz = new float[n];
            float top = 0f;
            for (int i = 0; i < n; i++)
            {
                var b = bw[i];
                float wh = (b.boneIndex0 == hi ? b.weight0 : 0f) + (b.boneIndex1 == hi ? b.weight1 : 0f) +
                           (b.boneIndex2 == hi ? b.weight2 : 0f) + (b.boneIndex3 == hi ? b.weight3 : 0f);
                w[i] = wh;
                Vector3 d = v[i] - hp;
                lx[i] = Vector3.Dot(d, right); ly[i] = Vector3.Dot(d, up); lz[i] = Vector3.Dot(d, fwd);
                if (wh > 0.5f) top = Mathf.Max(top, ly[i]);
            }
            if (top <= 1e-5f) return null;

            float medZ = 0f; int cnt = 0;
            for (int i = 0; i < n; i++) if (w[i] > 0.5f) { medZ += lz[i]; cnt++; }
            medZ /= Mathf.Max(1, cnt);
            float chin = top;
            for (int i = 0; i < n; i++) if (w[i] > 0.5f && lz[i] > medZ && Mathf.Abs(lx[i]) < top * 0.25f) chin = Mathf.Min(chin, ly[i]);
            float U = top - chin;
            if (U <= 1e-5f) return null;

            float noseZ = -9f, noseX = 0f;
            for (int i = 0; i < n; i++)
            {
                if (w[i] < 0.5f) continue;
                float yy = (ly[i] - chin) / U;
                if (yy > 0.35f && yy < 0.65f && Mathf.Abs(lx[i]) < U * 0.12f && lz[i] > noseZ) { noseZ = lz[i]; noseX = lx[i]; }
            }
            float mx = 0f;
            for (int i = 0; i < n; i++) if (w[i] > 0.5f) mx = Mathf.Max(mx, Mathf.Abs(lx[i] - noseX) / U);

            frame = new HeadFrame
            {
                valid = true, hp = hp, right = right, up = up, fwd = fwd,
                chin = chin, U = U, noseX = noseX, noseZ = noseZ, mx = mx
            };

            var res = Object.Instantiate(src);
            res.name = src.name + "_Rostos";
            var deltas = new Vector3[n];
            for (int s = 0; s < Nomes.Length; s++)
            {
                for (int i = 0; i < n; i++)
                {
                    float wh = w[i];
                    if (wh <= 0.02f) { deltas[i] = Vector3.zero; continue; }
                    float x = (lx[i] - noseX) / U, y = (ly[i] - chin) / U, z = (lz[i] - noseZ) / U;
                    deltas[i] = ComputeDelta(s, x, y, z, mx, right, up, fwd, U * wh);
                }
                res.AddBlendShapeFrame(Nomes[s], 100f, deltas, null, null);
            }
            return res;
        }

        static Mesh BuildHair(SkinnedMeshRenderer hairSmr, Animator anim, HeadFrame f)
        {
            Mesh src = hairSmr.sharedMesh;
            if (src == null || !src.isReadable) return null;
            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            var bones = hairSmr.bones;
            int hi = System.Array.IndexOf(bones, head);
            var bind = src.bindposes;
            Vector3 hp = (hi >= 0 && hi < bind.Length) ? bind[hi].inverse.MultiplyPoint3x4(Vector3.zero) : f.hp;

            Vector3[] v = src.vertices;
            int n = v.Length;
            var res = Object.Instantiate(src);
            res.name = src.name + "_Rostos";
            var deltas = new Vector3[n];
            for (int s = 0; s < Nomes.Length; s++)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector3 d = v[i] - hp;
                    float lx = Vector3.Dot(d, f.right), ly = Vector3.Dot(d, f.up), lz = Vector3.Dot(d, f.fwd);
                    float x = (lx - f.noseX) / f.U, y = (ly - f.chin) / f.U, z = (lz - f.noseZ) / f.U;
                    deltas[i] = ComputeDelta(s, x, y, z, f.mx, f.right, f.up, f.fwd, f.U);
                }
                res.AddBlendShapeFrame(Nomes[s], 100f, deltas, null, null);
            }
            return res;
        }
    }
}
