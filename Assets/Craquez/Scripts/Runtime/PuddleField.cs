using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    public enum Superficie { Grama = 0, Encharcado = 1, Poca = 2 }

    /// <summary>
    /// Terreno molhado: poças d'água (freiam e podem parar a bola) com marcas dos pingos de chuva caindo,
    /// e áreas encharcadas do gramado (película d'água: a bola derrapa e ganha velocidade ao quicar).
    /// </summary>
    public class PuddleField : MonoBehaviour
    {
        public static PuddleField Instance { get; private set; }

        class Area
        {
            public Vector2 center;
            public float radius;
            public float rotation;
            public float[] shape;
            public GameObject go;
            public float areaM2;
        }

        readonly List<Area> puddles = new List<Area>();
        readonly List<Area> soaked = new List<Area>();
        Material puddleMat, soakedMat, rippleMat;
        float depth = 1f;
        float rippleSpeed = 0.05f;
        float ripplesPerSecond;
        float rippleAccum;
        Vector2 offA, offB;
        ParticleSystem ripples;
        float totalPuddleArea;
        System.Random rng = new System.Random(5);
        const int ShapeSamples = 48;

        public int Count { get { return puddles.Count; } }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Init(Material puddleMaterial, Material soakedMaterial, Material rippleMaterial)
        {
            puddleMat = puddleMaterial;
            soakedMat = soakedMaterial;
            rippleMat = rippleMaterial;
            ripples = CreateRipples();
        }

        /// <summary>Recria poças e áreas encharcadas.</summary>
        public void Generate(int puddleCount, int soakedCount, float depth01, float sizeScale, float ripple, float ripplesPerSec, int seed)
        {
            Clear();
            depth = depth01;
            rippleSpeed = ripple;
            ripplesPerSecond = ripplesPerSec;
            rng = new System.Random(seed);
            // áreas encharcadas primeiro (maiores, mais rasas), poças depois
            Place(soaked, soakedCount, 2.2f, 5.0f, sizeScale, soakedMat, 0.005f, 0.6f);
            Place(puddles, puddleCount, 0.9f, 2.6f, sizeScale, puddleMat, 0.008f, 0.9f);
            totalPuddleArea = 0f;
            foreach (var p in puddles) totalPuddleArea += p.areaM2;
        }

        void Place(List<Area> list, int count, float rMin, float rMax, float sizeScale, Material mat, float y, float overlap)
        {
            int attempts = 0;
            while (list.Count < count && attempts < count * 40)
            {
                attempts++;
                Vector2 c;
                if (rng.NextDouble() < 0.7)
                    c = new Vector2(L(14f, 51f), L(-24f, 24f));
                else
                    c = new Vector2(L(-50f, 50f), L(-32f, 32f));
                float radius = L(rMin, rMax) * sizeScale;
                if (rng.NextDouble() < 0.15) radius *= 1.6f;
                bool ok = true;
                foreach (var p in list)
                    if ((p.center - c).magnitude < (p.radius + radius) * overlap) { ok = false; break; }
                if (!ok) continue;
                var a = new Area { center = c, radius = radius, rotation = L(0f, 360f), shape = new float[ShapeSamples] };
                float a1 = L(0f, 6.28f), a2 = L(0f, 6.28f), a3 = L(0f, 6.28f);
                float stretch = L(0.6f, 1f);
                float sum = 0f;
                for (int i = 0; i < ShapeSamples; i++)
                {
                    float ang = i * Mathf.PI * 2f / ShapeSamples;
                    float m = 1f + 0.22f * Mathf.Sin(2f * ang + a1) + 0.12f * Mathf.Sin(3f * ang + a2) + 0.07f * Mathf.Sin(5f * ang + a3);
                    float ell = 1f / Mathf.Sqrt(Mathf.Pow(Mathf.Cos(ang), 2) + Mathf.Pow(Mathf.Sin(ang) / stretch, 2));
                    a.shape[i] = Mathf.Max(0.35f, m * Mathf.Min(1f, ell));
                    sum += a.shape[i] * a.shape[i];
                }
                a.areaM2 = Mathf.PI * radius * radius * sum / ShapeSamples;
                a.go = BuildMesh(a, mat, y);
                list.Add(a);
            }
        }

        public void Clear()
        {
            foreach (var p in puddles) if (p.go != null) Destroy(p.go);
            foreach (var p in soaked) if (p.go != null) Destroy(p.go);
            puddles.Clear();
            soaked.Clear();
            totalPuddleArea = 0f;
            if (ripples != null) ripples.Clear();
        }

        float L(float a, float b) { return a + (b - a) * (float)rng.NextDouble(); }

        static float RadiusAt(Area p, Vector2 local)
        {
            float ang = Mathf.Atan2(local.y, local.x) - p.rotation * Mathf.Deg2Rad;
            float f = Mathf.Repeat(ang / (Mathf.PI * 2f), 1f) * ShapeSamples;
            int i0 = Mathf.FloorToInt(f) % ShapeSamples;
            int i1 = (i0 + 1) % ShapeSamples;
            return p.radius * Mathf.Lerp(p.shape[i0], p.shape[i1], f - Mathf.Floor(f));
        }

        static bool Inside(List<Area> list, Vector2 q, out float edge01)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                Vector2 d = q - p.center;
                if (d.sqrMagnitude > p.radius * p.radius * 2.7f) continue;
                float r = RadiusAt(p, d);
                float dist = d.magnitude;
                if (dist < r) { edge01 = Mathf.Clamp01(1f - dist / r); return true; }
            }
            edge01 = 0f;
            return false;
        }

        /// <summary>Retorna true se o ponto está numa poça; depthFactor (0..1) maior no centro.</summary>
        public bool Query(Vector3 worldPos, out float depthFactor)
        {
            float e;
            if (Inside(puddles, new Vector2(worldPos.x, worldPos.z), out e))
            {
                depthFactor = depth * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(e * 1.6f));
                return true;
            }
            depthFactor = 0f;
            return false;
        }

        /// <summary>Tipo de superfície no ponto (poça tem prioridade sobre encharcado).</summary>
        public Superficie SurfaceAt(Vector3 worldPos, out float depthFactor)
        {
            if (Query(worldPos, out depthFactor)) return Superficie.Poca;
            float e;
            if (Inside(soaked, new Vector2(worldPos.x, worldPos.z), out e)) { depthFactor = e; return Superficie.Encharcado; }
            depthFactor = 0f;
            return Superficie.Grama;
        }

        public bool IsNearPuddle(Vector3 worldPos, float margin)
        {
            Vector2 q = new Vector2(worldPos.x, worldPos.z);
            foreach (var p in puddles)
                if ((q - p.center).magnitude < p.radius * 1.35f + margin) return true;
            return false;
        }

        bool RandomPointInPuddle(out Vector3 pos)
        {
            pos = Vector3.zero;
            if (puddles.Count == 0 || totalPuddleArea <= 0f) return false;
            float pick = Random.value * totalPuddleArea;
            Area a = puddles[puddles.Count - 1];
            foreach (var p in puddles) { pick -= p.areaM2; if (pick <= 0f) { a = p; break; } }
            for (int i = 0; i < 6; i++)
            {
                Vector2 c = a.center + Random.insideUnitCircle * a.radius;
                float r = RadiusAt(a, c - a.center);
                if ((c - a.center).magnitude < r * 0.92f) { pos = new Vector3(c.x, 0.012f, c.y); return true; }
            }
            return false;
        }

        GameObject BuildMesh(Area p, Material mat, float y)
        {
            var go = new GameObject(mat == puddleMat ? "Poca" : "GramadoEncharcado");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(p.center.x, y, p.center.y);
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var n = new List<Vector3>();
            var col = new List<Color>();
            var t = new List<int>();

            // Centro (índice 0) + 3 anéis concêntricos para suavizar a transição radial de transparência e normais
            float[] ringFracs = { 0.38f, 0.72f, 1.0f };
            v.Add(Vector3.zero);
            uv.Add(new Vector2(0.5f, 0.5f));
            n.Add(Vector3.up);
            col.Add(Color.white);

            for (int rIdx = 0; rIdx < ringFracs.Length; rIdx++)
            {
                float frac = ringFracs[rIdx];
                float uvRad = frac * 0.49f;
                float alpha = rIdx == 0 ? 1f : (rIdx == 1 ? 0.82f : 0f);
                for (int i = 0; i < ShapeSamples; i++)
                {
                    float baseAng = i * Mathf.PI * 2f / ShapeSamples;
                    float ang = baseAng + p.rotation * Mathf.Deg2Rad;
                    float r = p.radius * p.shape[i] * frac;
                    Vector3 pos = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                    v.Add(pos);
                    // UV radial [0..1] centrado em (0.5, 0.5) para usar a máscara suave Poca_Mascara sem bordas poligonais duras
                    uv.Add(new Vector2(0.5f + Mathf.Cos(baseAng) * uvRad, 0.5f + Mathf.Sin(baseAng) * uvRad));
                    n.Add(Vector3.up);
                    col.Add(new Color(1f, 1f, 1f, alpha));
                }
            }

            // Triângulos do centro para o anel 0
            for (int i = 0; i < ShapeSamples; i++)
            {
                int a = 1 + i;
                int b = 1 + (i + 1) % ShapeSamples;
                t.Add(0); t.Add(b); t.Add(a);
            }
            // Triângulos entre os anéis concêntricos
            for (int rIdx = 0; rIdx < ringFracs.Length - 1; rIdx++)
            {
                int baseInner = 1 + rIdx * ShapeSamples;
                int baseOuter = 1 + (rIdx + 1) * ShapeSamples;
                for (int i = 0; i < ShapeSamples; i++)
                {
                    int next = (i + 1) % ShapeSamples;
                    int i0 = baseInner + i, i1 = baseInner + next;
                    int o0 = baseOuter + i, o1 = baseOuter + next;
                    t.Add(i0); t.Add(i1); t.Add(o1);
                    t.Add(i0); t.Add(o1); t.Add(o0);
                }
            }

            var m = new Mesh { name = go.name };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetNormals(n); m.SetColors(col); m.SetTriangles(t, 0);
            m.RecalculateTangents(); m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        ParticleSystem CreateRipples()
        {
            var go = new GameObject("MarcasDeChuva");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.maxParticles = 2500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.7f);
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f));
            var c = ps.colorOverLifetime;
            c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
            c.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = rippleMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (puddleMat != null && (puddles.Count > 0 || soaked.Count > 0))
            {
                offA += new Vector2(0.7f, 0.4f) * rippleSpeed * dt;
                offB += new Vector2(-0.3f, 0.8f) * rippleSpeed * dt;
                puddleMat.SetTextureOffset("_BumpMap", offA);
                if (soakedMat != null) soakedMat.SetTextureOffset("_BumpMap", offB * 0.5f);
            }
            // marcas dos pingos caindo sobre as poças (anéis que se expandem)
            if (ripples != null && ripplesPerSecond > 0f && totalPuddleArea > 0f)
            {
                rippleAccum += ripplesPerSecond * Mathf.Min(1f, totalPuddleArea / 40f + 0.25f) * GameSettings.Particulas * dt;
                var ep = new ParticleSystem.EmitParams();
                int guard = 0;
                while (rippleAccum >= 1f && guard++ < 200)
                {
                    rippleAccum -= 1f;
                    Vector3 p;
                    if (!RandomPointInPuddle(out p)) continue;
                    ep.position = p;
                    ep.startSize = Random.Range(0.14f, 0.4f);
                    ep.startLifetime = Random.Range(0.35f, 0.6f);
                    ripples.Emit(ep, 1);
                }
            }
        }
    }
}
