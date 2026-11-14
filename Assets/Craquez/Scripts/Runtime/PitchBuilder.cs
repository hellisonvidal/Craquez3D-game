using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Constrói o gramado em escala real (105 x 68 m) com faixas de corte, todas as marcações oficiais,
    /// o collider do chão e o "alambrado" invisível que segura a bola dentro do complexo.
    /// </summary>
    public static class PitchBuilder
    {
        public const float LineWidth = 0.12f;
        const float LineY = 0.004f;

        public static GameObject Build(Transform parent, Material grassLight, Material grassDark, Material lines, out Renderer[] grassRenderers)
        {
            var root = new GameObject("Campo");
            root.transform.SetParent(parent, false);

            // ---------- gramado com faixas (2 submeshes alternados)
            const float sizeX = 136f, sizeZ = 100f;
            const int bands = 24;                       // 105 m / 18 faixas ≈ 5,8 m, estendido além das linhas
            float bandW = Arena.Length / 18f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tris0 = new List<int>();
            var tris1 = new List<int>();
            float x0 = -bands * 0.5f * bandW;
            for (int b = 0; b < bands; b++)
            {
                float xa = Mathf.Max(-sizeX * 0.5f, x0 + b * bandW);
                float xb = Mathf.Min(sizeX * 0.5f, x0 + (b + 1) * bandW);
                if (b == 0) xa = -sizeX * 0.5f;
                if (b == bands - 1) xb = sizeX * 0.5f;
                if (xb <= xa) continue;
                // subdivide em Z para iluminação por vértice e precisão de neblina
                const int segZ = 10;
                int start = verts.Count;
                for (int j = 0; j <= segZ; j++)
                {
                    float z = -sizeZ * 0.5f + sizeZ * j / segZ;
                    verts.Add(new Vector3(xa, 0f, z)); verts.Add(new Vector3(xb, 0f, z));
                    uvs.Add(new Vector2(xa / 4f, z / 4f)); uvs.Add(new Vector2(xb / 4f, z / 4f));
                    normals.Add(Vector3.up); normals.Add(Vector3.up);
                }
                var t = (b % 2 == 0) ? tris0 : tris1;
                for (int j = 0; j < segZ; j++)
                {
                    int i0 = start + j * 2;
                    t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1);
                    t.Add(i0 + 1); t.Add(i0 + 2); t.Add(i0 + 3);
                }
            }
            var mesh = new Mesh { name = "Gramado" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tris0, 0);
            mesh.SetTriangles(tris1, 1);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var grass = new GameObject("Gramado");
            grass.transform.SetParent(root.transform, false);
            grass.AddComponent<MeshFilter>().sharedMesh = mesh;
            var gr = grass.AddComponent<MeshRenderer>();
            gr.sharedMaterials = new[] { grassLight, grassDark };
            grassRenderers = new Renderer[] { gr };

            // ---------- marcações
            var lineGo = new GameObject("Marcacoes");
            lineGo.transform.SetParent(root.transform, false);
            lineGo.AddComponent<MeshFilter>().sharedMesh = BuildLines();
            var lr = lineGo.AddComponent<MeshRenderer>();
            lr.sharedMaterial = lines;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ---------- chão físico
            var ground = new GameObject("ChaoFisico");
            ground.transform.SetParent(root.transform, false);
            var box = ground.AddComponent<BoxCollider>();
            box.size = new Vector3(300f, 2f, 300f);
            box.center = new Vector3(0f, -1f, 0f);
            // o contato bola–gramado (quique, atrito, rolamento) é resolvido em BallPhysics; aqui só sustenta
            Compat.SetPhysicsMaterial(box, "Gramado", 0f, 0f, 0f, 2);

            // ---------- alambrado invisível (impede a bola de sair do complexo)
            AddWall(root.transform, new Vector3(Arena.FenceX + 0.5f, 3f, 0f), new Vector3(1f, 6f, Arena.FenceZ * 2f + 2f));
            AddWall(root.transform, new Vector3(-Arena.FenceX - 0.5f, 3f, 0f), new Vector3(1f, 6f, Arena.FenceZ * 2f + 2f));
            AddWall(root.transform, new Vector3(0f, 3f, Arena.FenceZ + 0.5f), new Vector3(Arena.FenceX * 2f + 2f, 6f, 1f));
            AddWall(root.transform, new Vector3(0f, 3f, -Arena.FenceZ - 0.5f), new Vector3(Arena.FenceX * 2f + 2f, 6f, 1f));
            return root;
        }

        static void AddWall(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Alambrado");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
            Compat.SetPhysicsMaterial(c, "Alambrado", 0.5f, 0.5f, 0.25f, false);
        }

        // ------------------------------------------------------------------ linhas

        static Mesh BuildLines()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            float L = Arena.HalfLength, W = Arena.HalfWidth, h = LineWidth * 0.5f;

            // linhas laterais e de fundo (a largura da linha fica dentro do campo)
            Rect(v, t, -L, -W, L, -W + LineWidth);
            Rect(v, t, -L, W - LineWidth, L, W);
            Rect(v, t, -L, -W, -L + LineWidth, W);
            Rect(v, t, L - LineWidth, -W, L, W);
            // linha do meio
            Rect(v, t, -h, -W, h, W);
            // círculo central e marca
            Arc(v, t, Vector2.zero, 9.15f, 0f, 360f);
            Disc(v, t, Vector2.zero, 0.15f);

            for (int s = -1; s <= 1; s += 2)
            {
                float gx = s * L;
                // grande área: 16,5 m de profundidade, 40,32 m de largura
                Box(v, t, gx, s, 16.5f, 20.16f);
                // pequena área: 5,5 m x 18,32 m
                Box(v, t, gx, s, 5.5f, 9.16f);
                // marca do pênalti (11 m)
                Vector2 spot = new Vector2(gx - s * 11f, 0f);
                Disc(v, t, spot, 0.12f);
                // meia-lua: arco de 9,15 m fora da grande área
                float ang = Mathf.Acos(5.5f / 9.15f) * Mathf.Rad2Deg;
                float baseAng = s > 0 ? 180f : 0f;
                Arc(v, t, spot, 9.15f, baseAng - ang, baseAng + ang);
                // escanteios (raio 1 m)
                Arc(v, t, new Vector2(gx, W), 1f, s > 0 ? 180f : 270f, s > 0 ? 270f : 360f);
                Arc(v, t, new Vector2(gx, -W), 1f, s > 0 ? 90f : 0f, s > 0 ? 180f : 90f);
            }

            var m = new Mesh { name = "Marcacoes" };
            m.SetVertices(v);
            var n = new List<Vector3>(v.Count);
            var uv = new List<Vector2>(v.Count);
            for (int i = 0; i < v.Count; i++) { n.Add(Vector3.up); uv.Add(new Vector2(v[i].x * 0.25f, v[i].z * 0.25f)); }
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        static void Box(List<Vector3> v, List<int> t, float gx, int s, float depth, float halfW)
        {
            float inner = gx - s * depth;
            float xa = Mathf.Min(gx, inner), xb = Mathf.Max(gx, inner);
            Rect(v, t, xa, halfW - LineWidth, xb, halfW);
            Rect(v, t, xa, -halfW, xb, -halfW + LineWidth);
            float lx = inner - (s > 0 ? 0f : LineWidth);
            Rect(v, t, lx, -halfW, lx + LineWidth, halfW);
        }

        static void Rect(List<Vector3> v, List<int> t, float x0, float z0, float x1, float z1)
        {
            int i = v.Count;
            v.Add(new Vector3(x0, LineY, z0)); v.Add(new Vector3(x1, LineY, z0));
            v.Add(new Vector3(x0, LineY, z1)); v.Add(new Vector3(x1, LineY, z1));
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i + 1); t.Add(i + 2); t.Add(i + 3);
        }

        static void Arc(List<Vector3> v, List<int> t, Vector2 c, float r, float a0, float a1)
        {
            int seg = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 3f));
            float ri = r - LineWidth * 0.5f, ro = r + LineWidth * 0.5f;
            int start = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, (float)i / seg) * Mathf.Deg2Rad;
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                v.Add(new Vector3(c.x + d.x * ri, LineY, c.y + d.y * ri));
                v.Add(new Vector3(c.x + d.x * ro, LineY, c.y + d.y * ro));
            }
            for (int i = 0; i < seg; i++)
            {
                int k = start + i * 2;
                // ordem que gera normal para cima independentemente do sentido
                AddQuadUp(v, t, k, k + 1, k + 2, k + 3);
            }
        }

        static void Disc(List<Vector3> v, List<int> t, Vector2 c, float r)
        {
            int seg = 16;
            int ci = v.Count;
            v.Add(new Vector3(c.x, LineY, c.y));
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v.Add(new Vector3(c.x + Mathf.Cos(a) * r, LineY, c.y + Mathf.Sin(a) * r));
            }
            for (int i = 0; i < seg; i++) AddTriUp(v, t, ci, ci + 1 + i, ci + 2 + i);
        }

        static void AddTriUp(List<Vector3> v, List<int> t, int a, int b, int c)
        {
            Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (n.y >= 0f) { t.Add(a); t.Add(b); t.Add(c); }
            else { t.Add(a); t.Add(c); t.Add(b); }
        }

        static void AddQuadUp(List<Vector3> v, List<int> t, int a, int b, int c, int d)
        {
            AddTriUp(v, t, a, c, b);
            AddTriUp(v, t, b, c, d);
        }
    }
}
