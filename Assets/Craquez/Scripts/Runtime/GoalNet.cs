using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>Marca um collider como painel da rede.</summary>
    public class GoalNetPanel : MonoBehaviour
    {
        public GoalNet net;
    }

    /// <summary>
    /// Gol oficial (7,32 x 2,44 m) com traves cilíndricas, rede em malha deformável
    /// e volume interno que amortece a bola como uma rede real.
    /// O gol é construído no espaço local: +X aponta para FORA do campo (profundidade da rede).
    /// </summary>
    public class GoalNet : MonoBehaviour
    {
        static readonly List<GoalNet> all = new List<GoalNet>();

        // geometria da rede (local): teto até 1,0 m de profundidade, fundo inclinado até 2,0 m no chão
        const float RoofDepth = 1.0f;
        const float BottomDepth = Arena.NetDepth;
        const float H = Arena.GoalHeight + Arena.PostRadius;
        const float W = Arena.GoalHalfWidth + Arena.PostRadius;

        class Panel
        {
            public Mesh mesh;
            public Vector3[] rest;
            public Vector3[] offset;
            public Vector3[] vel;
            public Vector3[] work;
            public Vector3 outward; // normal para fora do gol (local)
            public bool dirty;
        }

        readonly List<Panel> panels = new List<Panel>();
        float sleepTimer;

        public static GoalNet Build(Transform parent, string name, Vector3 position, float yaw, Material postMat, Material netMat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var g = go.AddComponent<GoalNet>();
            g.BuildFrame(postMat);
            g.BuildNet(netMat);
            all.Add(g);
            return g;
        }

        void OnDestroy() { all.Remove(this); }

        // -------------------------------------------------------------- estrutura

        void BuildFrame(Material postMat)
        {
            float r = Arena.PostRadius;
            // traves (centro do poste sobre a linha de gol, face interna a 3,66 m do centro)
            Cyl("TraveEsq", new Vector3(0f, H * 0.5f, W), new Vector3(0f, 0f, 0f), H, r, postMat, true);
            Cyl("TraveDir", new Vector3(0f, H * 0.5f, -W), new Vector3(0f, 0f, 0f), H, r, postMat, true);
            Cyl("Travessao", new Vector3(0f, Arena.GoalHeight + r, 0f), new Vector3(90f, 0f, 0f), W * 2f + r * 2f, r, postMat, true);
            // suportes traseiros (finos, não colidem com a bola de forma relevante)
            float sr = 0.025f;
            Cyl("SuporteTetoE", new Vector3(RoofDepth * 0.5f, H, W), new Vector3(0f, 0f, 90f), RoofDepth, sr, postMat, false);
            Cyl("SuporteTetoD", new Vector3(RoofDepth * 0.5f, H, -W), new Vector3(0f, 0f, 90f), RoofDepth, sr, postMat, false);
            float dx = BottomDepth - RoofDepth;
            float len = Mathf.Sqrt(dx * dx + H * H);
            float ang = Mathf.Atan2(dx, H) * Mathf.Rad2Deg;
            Cyl("SuporteFundoE", new Vector3(RoofDepth + dx * 0.5f, H * 0.5f, W), new Vector3(0f, 0f, ang), len, sr, postMat, false);
            Cyl("SuporteFundoD", new Vector3(RoofDepth + dx * 0.5f, H * 0.5f, -W), new Vector3(0f, 0f, ang), len, sr, postMat, false);
            Cyl("BaseFundo", new Vector3(BottomDepth, sr, 0f), new Vector3(90f, 0f, 0f), W * 2f, sr, postMat, false);
            Cyl("BaseE", new Vector3(BottomDepth * 0.5f, sr, W), new Vector3(0f, 0f, 90f), BottomDepth, sr, postMat, false);
            Cyl("BaseD", new Vector3(BottomDepth * 0.5f, sr, -W), new Vector3(0f, 0f, 90f), BottomDepth, sr, postMat, false);
        }

        void Cyl(string n, Vector3 localPos, Vector3 euler, float length, float radius, Material mat, bool collide)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.name = n;
            c.transform.SetParent(transform, false);
            c.transform.localPosition = localPos;
            c.transform.localRotation = Quaternion.Euler(euler);
            c.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            c.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = c.GetComponent<Collider>();
            if (!collide) { if (col != null) Destroy(col); return; }
            // a primitiva costuma vir com um CapsuleCollider (raio 0,5 / altura 2 no eixo Y) que escala junto;
            // no APK ele pode ter sido removido pelo "strip" de código, então garantimos um.
            if (col == null)
            {
                var cap = c.AddComponent<CapsuleCollider>();
                cap.radius = 0.5f;
                cap.height = 2f;
                cap.direction = 1;
                col = cap;
            }
            Compat.SetPhysicsMaterial(col, "Trave", 0.2f, 0.2f, 0.75f, false);
        }

        // -------------------------------------------------------------- rede

        void BuildNet(Material netMat)
        {
            // teto
            AddPanel("RedeTeto", new Vector3(0f, H, -W), new Vector3(0f, H, W), new Vector3(RoofDepth, H, -W), new Vector3(RoofDepth, H, W), 16, 6, Vector3.up, netMat);
            // fundo inclinado
            Vector3 outBack = new Vector3(H, 0f, 0f) + new Vector3(0f, BottomDepth - RoofDepth, 0f);
            AddPanel("RedeFundo", new Vector3(RoofDepth, H, -W), new Vector3(RoofDepth, H, W), new Vector3(BottomDepth, 0f, -W), new Vector3(BottomDepth, 0f, W), 16, 10, outBack.normalized, netMat);
            // laterais (quadriláteros: frente-alto, fundo-alto, frente-chão, fundo-chão)
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * W;
                AddSidePanel(s > 0 ? "RedeLateralE" : "RedeLateralD", z, new Vector3(0f, 0f, s), netMat);
            }
            // colliders levemente por fora da malha visual (a bola afunda um pouco antes de parar)
            AddBoxCollider("ColTeto", new Vector3(RoofDepth * 0.5f + 0.1f, H + 0.12f, 0f), new Vector3(RoofDepth + 0.2f, 0.06f, W * 2f + 0.3f), Quaternion.identity);
            float dx = BottomDepth - RoofDepth;
            float len = Mathf.Sqrt(dx * dx + H * H);
            float ang = Mathf.Atan2(dx, H) * Mathf.Rad2Deg;
            AddBoxCollider("ColFundo", new Vector3(RoofDepth + dx * 0.5f + 0.15f, H * 0.5f, 0f), new Vector3(0.06f, len + 0.2f, W * 2f + 0.3f), Quaternion.Euler(0f, 0f, ang));
            AddBoxCollider("ColLatE", new Vector3(BottomDepth * 0.5f + 0.05f, H * 0.5f, W + 0.15f), new Vector3(BottomDepth + 0.1f, H + 0.1f, 0.06f), Quaternion.identity);
            AddBoxCollider("ColLatD", new Vector3(BottomDepth * 0.5f + 0.05f, H * 0.5f, -W - 0.15f), new Vector3(BottomDepth + 0.1f, H + 0.1f, 0.06f), Quaternion.identity);
        }

        void AddBoxCollider(string n, Vector3 pos, Vector3 size, Quaternion rot)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var b = go.AddComponent<BoxCollider>();
            b.size = size;
            Compat.SetPhysicsMaterial(b, "Rede", 1f, 1f, 0.05f, false);
            go.AddComponent<GoalNetPanel>().net = this;
        }

        void AddPanel(string n, Vector3 a, Vector3 b, Vector3 c, Vector3 d, int nu, int nv, Vector3 outward, Material mat)
        {
            // a-b: borda "u" de cima/frente; c-d: borda oposta
            var verts = new Vector3[(nu + 1) * (nv + 1)];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= nv; j++)
                for (int i = 0; i <= nu; i++)
                {
                    float u = (float)i / nu, v = (float)j / nv;
                    Vector3 p = Vector3.Lerp(Vector3.Lerp(a, b, u), Vector3.Lerp(c, d, u), v);
                    verts[j * (nu + 1) + i] = p;
                }
            FinishPanel(n, verts, uvs, nu, nv, outward, mat);
        }

        void AddSidePanel(string n, float z, Vector3 outward, Material mat)
        {
            int nu = 8, nv = 10;
            var verts = new Vector3[(nu + 1) * (nv + 1)];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= nv; j++)
                for (int i = 0; i <= nu; i++)
                {
                    float u = (float)i / nu, v = (float)j / nv;
                    float y = Mathf.Lerp(H, 0f, v);
                    // profundidade máxima nesta altura (teto até 1 m, fundo inclinado até 2 m no chão)
                    float maxD = RoofDepth + (BottomDepth - RoofDepth) * (1f - y / H);
                    verts[j * (nu + 1) + i] = new Vector3(u * maxD, y, z);
                }
            FinishPanel(n, verts, uvs, nu, nv, outward, mat);
        }

        void FinishPanel(string n, Vector3[] verts, Vector2[] uvs, int nu, int nv, Vector3 outward, Material mat)
        {
            // UV em metros (a textura tem 8 células por repetição => malha de ~12 cm)
            Vector3 axisA = Mathf.Abs(outward.z) > 0.5f ? Vector3.right : Vector3.forward;
            Vector3 axisB = Vector3.Cross(outward, axisA).normalized;
            for (int i = 0; i < verts.Length; i++)
                uvs[i] = new Vector2(Vector3.Dot(verts[i], axisA), Vector3.Dot(verts[i], axisB));
            var tris = new List<int>();
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    int k = j * (nu + 1) + i;
                    tris.Add(k); tris.Add(k + nu + 1); tris.Add(k + 1);
                    tris.Add(k + 1); tris.Add(k + nu + 1); tris.Add(k + nu + 2);
                }
            var m = new Mesh { name = n };
            m.MarkDynamic();
            m.vertices = verts;
            m.uv = uvs;
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var p = new Panel
            {
                mesh = m, rest = (Vector3[])verts.Clone(), offset = new Vector3[verts.Length],
                vel = new Vector3[verts.Length], work = new Vector3[verts.Length], outward = outward
            };
            panels.Add(p);
        }

        /// <summary>Estufa a rede no ponto de impacto (efeito visual).</summary>
        public void Bulge(Vector3 worldPoint, Vector3 worldVelocity)
        {
            Vector3 lp = transform.InverseTransformPoint(worldPoint);
            Vector3 lv = transform.InverseTransformDirection(worldVelocity);
            float strength = Mathf.Clamp(lv.magnitude * 0.06f, 0.05f, 1.4f);
            foreach (var p in panels)
            {
                for (int i = 0; i < p.rest.Length; i++)
                {
                    float d = Vector3.Distance(p.rest[i] + p.offset[i], lp);
                    if (d > 1.1f) continue;
                    float f = 1f - d / 1.1f;
                    f = f * f * (3f - 2f * f);
                    // empurra para fora do gol (na direção do movimento da bola)
                    Vector3 push = p.outward * Mathf.Max(0.2f, Vector3.Dot(lv.normalized, p.outward));
                    p.vel[i] += push * strength * f * 9f;
                }
                p.dirty = true;
            }
            sleepTimer = 3f;
        }

        void Update()
        {
            if (sleepTimer <= 0f) return;
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            sleepTimer -= dt;
            const float k = 60f, damp = 7f;
            foreach (var p in panels)
            {
                if (!p.dirty) continue;
                for (int i = 0; i < p.rest.Length; i++)
                {
                    Vector3 acc = -k * p.offset[i] - damp * p.vel[i];
                    p.vel[i] += acc * dt;
                    p.offset[i] += p.vel[i] * dt;
                    // limita a deformação máxima
                    if (p.offset[i].sqrMagnitude > 0.36f) p.offset[i] = p.offset[i].normalized * 0.6f;
                    p.work[i] = p.rest[i] + p.offset[i];
                }
                p.mesh.vertices = p.work;
                p.mesh.RecalculateBounds();
                if (sleepTimer <= 0f) p.dirty = false;
            }
        }

        // -------------------------------------------------------------- física

        /// <summary>Retorna true se a posição (mundo) está dentro do volume interno do gol (atrás da linha).</summary>
        public bool InsideVolume(Vector3 world, out Vector3 local)
        {
            local = transform.InverseTransformPoint(world);
            if (local.x < Arena.BallRadius * 0.5f) return false;
            if (Mathf.Abs(local.z) > Arena.GoalHalfWidth + 0.2f) return false;
            if (local.y > Arena.GoalHeight + 0.2f) return false;
            float maxD = RoofDepth + (BottomDepth - RoofDepth) * (1f - Mathf.Clamp01(local.y / H));
            return local.x < maxD + 0.25f;
        }

        public static void ConstrainAll(BallPhysics ball, float dt)
        {
            for (int i = 0; i < all.Count; i++) all[i].Constrain(ball, dt);
        }

        void Constrain(BallPhysics ball, float dt)
        {
            Vector3 local;
            if (!InsideVolume(ball.Body.position, out local)) return;
            // dentro da rede o ar e o contato com a malha dissipam energia rapidamente
            Vector3 v = Compat.GetVelocity(ball.Body);
            Vector3 lv = transform.InverseTransformDirection(v);
            float depth01 = Mathf.Clamp01(local.x / RoofDepth);
            float k = Mathf.Lerp(1.5f, 6f, depth01);
            lv.x *= Mathf.Exp(-k * dt);
            lv.z *= Mathf.Exp(-k * 0.6f * dt);
            if (lv.y > 0f) lv.y *= Mathf.Exp(-k * dt);
            Compat.SetVelocity(ball.Body, transform.TransformDirection(lv));
        }
    }
}
