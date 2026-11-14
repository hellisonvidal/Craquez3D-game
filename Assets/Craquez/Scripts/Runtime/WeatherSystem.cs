using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Craquez
{
    /// <summary>
    /// Clima completo da arena: Sol / Chuva / Tempestade.
    /// Controla luz do sol, céu, neblina, nuvens, chuva (partículas que seguem a câmera),
    /// respingos, poças, vento com rajadas (afeta a bola e a chuva), relâmpagos e trovões,
    /// refletores, gramado molhado, pós-processamento e o comportamento físico do jogo.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }
        public static event Action<float> OnLightning; // intensidade (para tremer a câmera)

        [Serializable]
        public class Perfil
        {
            public float sunIntensity; public Color sunColor; public float sunPitch; public float shadowStrength;
            public Color ambientSky, ambientEquator, ambientGround;
            public float fogDensity; public Color fogColor;
            public Color skyTint; public float skyExposure, atmosphere; public Color skyGround;
            public float clouds;
            public float rainRate; public float rainSpeed;
            public float windBase, windGust;
            public float rollDecel, bounce;
            public int puddles, soakedAreas; public float puddleDepth, puddleSize, rippleSpeed, ripplesPerSecond;
            public float wetness;
            public bool lightning; public float floodlights;
            public float rainVolume, windVolume;
            public float gkReaction, gkFumble, gkCatchSpeed, playerGrip;
            public string postProcess;
        }

        public static Perfil Get(Clima c)
        {
            switch (c)
            {
                case Clima.Sol:
                    return new Perfil
                    {
                        sunIntensity = 1.7f, sunColor = new Color(1f, 0.95f, 0.86f), sunPitch = 47f, shadowStrength = 1f,
                        ambientSky = new Color(0.56f, 0.66f, 0.82f), ambientEquator = new Color(0.46f, 0.49f, 0.45f), ambientGround = new Color(0.2f, 0.21f, 0.16f),
                        fogDensity = 0.0012f, fogColor = new Color(0.68f, 0.78f, 0.9f),
                        skyTint = new Color(0.5f, 0.55f, 0.62f), skyExposure = 1.25f, atmosphere = 1.0f, skyGround = new Color(0.37f, 0.35f, 0.33f),
                        clouds = 0.22f, rainRate = 0f, rainSpeed = 0f,
                        windBase = 1.0f, windGust = 1.4f, rollDecel = 1.15f, bounce = 0.62f,
                        puddles = 0, soakedAreas = 0, puddleDepth = 0f, puddleSize = 1f, rippleSpeed = 0f, ripplesPerSecond = 0f, wetness = 0f,
                        lightning = false, floodlights = 0f, rainVolume = 0f, windVolume = 0.07f,
                        gkReaction = 0f, gkFumble = 0.04f, gkCatchSpeed = 21f, playerGrip = 1f, postProcess = "PP_Sol"
                    };
                case Clima.Chuva:
                    return new Perfil
                    {
                        sunIntensity = 0.6f, sunColor = new Color(0.8f, 0.85f, 0.95f), sunPitch = 58f, shadowStrength = 0.45f,
                        ambientSky = new Color(0.42f, 0.46f, 0.53f), ambientEquator = new Color(0.34f, 0.36f, 0.38f), ambientGround = new Color(0.14f, 0.15f, 0.14f),
                        fogDensity = 0.0065f, fogColor = new Color(0.5f, 0.53f, 0.57f),
                        skyTint = new Color(0.33f, 0.35f, 0.39f), skyExposure = 0.62f, atmosphere = 2.4f, skyGround = new Color(0.28f, 0.29f, 0.3f),
                        clouds = 0.85f, rainRate = 4500f, rainSpeed = 15f,
                        windBase = 4.5f, windGust = 3f, rollDecel = 0.85f, bounce = 0.52f,
                        puddles = 14, soakedAreas = 10, puddleDepth = 0.65f, puddleSize = 1f, rippleSpeed = 0.06f, ripplesPerSecond = 70f, wetness = 0.7f,
                        lightning = false, floodlights = 2f, rainVolume = 0.55f, windVolume = 0.25f,
                        gkReaction = 0.04f, gkFumble = 0.14f, gkCatchSpeed = 18f, playerGrip = 0.85f, postProcess = "PP_Chuva"
                    };
                default:
                    return new Perfil
                    {
                        sunIntensity = 0.22f, sunColor = new Color(0.62f, 0.68f, 0.85f), sunPitch = 62f, shadowStrength = 0.25f,
                        ambientSky = new Color(0.22f, 0.25f, 0.31f), ambientEquator = new Color(0.17f, 0.19f, 0.22f), ambientGround = new Color(0.07f, 0.08f, 0.08f),
                        fogDensity = 0.013f, fogColor = new Color(0.24f, 0.27f, 0.31f),
                        skyTint = new Color(0.2f, 0.21f, 0.25f), skyExposure = 0.3f, atmosphere = 3.6f, skyGround = new Color(0.12f, 0.12f, 0.13f),
                        clouds = 1f, rainRate = 9500f, rainSpeed = 19f,
                        windBase = 10.5f, windGust = 6.5f, rollDecel = 0.95f, bounce = 0.45f,
                        puddles = 26, soakedAreas = 18, puddleDepth = 1f, puddleSize = 1.35f, rippleSpeed = 0.14f, ripplesPerSecond = 190f, wetness = 1f,
                        lightning = true, floodlights = 5f, rainVolume = 1f, windVolume = 0.7f,
                        gkReaction = 0.08f, gkFumble = 0.25f, gkCatchSpeed = 15f, playerGrip = 0.72f, postProcess = "PP_Tempestade"
                    };
            }
        }

        // ------------------------------------------------------------ referências (preenchidas pelo bootstrap)
        public Light sun;
        public Material skyboxTemplate;
        public Material rainMaterial, splashMaterial, dropletMaterial, cloudMaterial;
        public Transform postProcessRoot;
        public ReflectionProbe probe;
        public PuddleField puddleField;
        public Transform followCamera;
        public List<Material> grassMaterials = new List<Material>();
        public List<Color> grassBaseColors = new List<Color>();
        public List<Material> characterMaterials = new List<Material>();

        public Clima Current { get; private set; }
        public Perfil P { get; private set; }
        public Vector3 Wind { get; private set; }
        public float WindSpeed { get { return Wind.magnitude; } }
        public float RollingDeceleration { get { return P != null ? P.rollDecel : 1.1f; } }

        ParticleSystem rain, groundSplash, droplets;
        Material sky;
        GameObject cloudLayer;
        Light flash;
        readonly List<Light> floods = new List<Light>();
        float windAngle, windSeed;
        Coroutine lightningRoutine;
        float baseSkyExposure;

        void Awake()
        {
            Instance = this;
            windSeed = UnityEngine.Random.value * 100f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Build()
        {
            if (skyboxTemplate != null)
            {
                sky = new Material(skyboxTemplate);
                RenderSettings.skybox = sky;
            }
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            rain = CreateRain();
            groundSplash = CreateGroundSplash();
            droplets = CreateDroplets();
            cloudLayer = CreateClouds();

            var fgo = new GameObject("Relampago");
            fgo.transform.SetParent(transform, false);
            fgo.transform.rotation = Quaternion.Euler(70f, 120f, 0f);
            flash = fgo.AddComponent<Light>();
            flash.type = LightType.Directional;
            flash.color = new Color(0.78f, 0.85f, 1f);
            flash.intensity = 0f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;

            // refletores nas 4 torres de iluminação
            Vector3[] towers = { new Vector3(58f, 27f, 42f), new Vector3(-58f, 27f, 42f), new Vector3(58f, 27f, -42f), new Vector3(-58f, 27f, -42f) };
            foreach (var t in towers)
            {
                var go = new GameObject("Refletor");
                go.transform.SetParent(transform, false);
                go.transform.position = t;
                go.transform.LookAt(new Vector3(t.x * 0.45f, 0f, t.z * 0.2f));
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = 120f;
                l.spotAngle = 62f;
                l.color = new Color(1f, 0.97f, 0.9f);
                l.shadows = LightShadows.None;
                l.enabled = false;
                floods.Add(l);
            }
        }

        public void Apply(Clima c)
        {
            Current = c;
            P = Get(c);
            var p = P;

            sun.intensity = p.sunIntensity;
            sun.color = p.sunColor;
            sun.shadowStrength = p.shadowStrength;
            sun.transform.rotation = Quaternion.Euler(p.sunPitch, -32f, 0f);

            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogDensity = p.fogDensity;

            if (sky != null)
            {
                sky.SetColor("_SkyTint", p.skyTint);
                sky.SetColor("_GroundColor", p.skyGround);
                sky.SetFloat("_Exposure", p.skyExposure);
                sky.SetFloat("_AtmosphereThickness", p.atmosphere);
                sky.SetFloat("_SunSize", c == Clima.Sol ? 0.04f : 0.0f);
            }
            baseSkyExposure = p.skyExposure;

            if (cloudMaterial != null)
            {
                Color cc = Color.Lerp(new Color(1f, 1f, 1f), new Color(0.32f, 0.34f, 0.38f), Mathf.Clamp01((p.clouds - 0.3f) / 0.7f));
                cc.a = p.clouds;
                cloudMaterial.SetColor("_BaseColor", cc);
            }

            var em = rain.emission; em.rateOverTime = p.rainRate * GameSettings.Particulas;
            var main = rain.main; main.startSpeed = p.rainSpeed;
            if (p.rainRate > 0f) { if (!rain.isPlaying) rain.Play(); } else rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var em2 = groundSplash.emission; em2.rateOverTime = p.rainRate * 0.12f * GameSettings.Particulas;
            if (p.rainRate > 0f) { if (!groundSplash.isPlaying) groundSplash.Play(); } else groundSplash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            foreach (var l in floods) { l.enabled = p.floodlights > 0f; l.intensity = p.floodlights; }

            for (int i = 0; i < grassMaterials.Count; i++)
            {
                var m = grassMaterials[i];
                Color b = i < grassBaseColors.Count ? grassBaseColors[i] : Color.white;
                float k = Mathf.Lerp(1f, 0.7f, p.wetness);
                m.SetColor("_BaseColor", new Color(b.r * k, b.g * k, b.b * k, 1f));
                m.SetFloat("_Smoothness", Mathf.Lerp(0.12f, 0.58f, p.wetness));
            }
            foreach (var m in characterMaterials)
                m.SetFloat("_Smoothness", Mathf.Lerp(0.55f, 0.85f, p.wetness));

            if (puddleField != null)
                puddleField.Generate(p.puddles, p.soakedAreas, p.puddleDepth, p.puddleSize, p.rippleSpeed, p.ripplesPerSecond, UnityEngine.Random.Range(0, 100000));

            if (postProcessRoot != null)
                foreach (Transform t in postProcessRoot) t.gameObject.SetActive(t.name == p.postProcess);

            if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.SetAmbience(p.rainVolume, p.windVolume, 1f);

            windAngle = UnityEngine.Random.Range(0f, 360f);
            if (lightningRoutine != null) StopCoroutine(lightningRoutine);
            if (p.lightning) lightningRoutine = StartCoroutine(LightningLoop());
            flash.enabled = false;

            StartCoroutine(RefreshProbe());
        }

        IEnumerator RefreshProbe()
        {
            yield return null;
            yield return null;
            if (probe != null) probe.RenderProbe();
        }

        void Update()
        {
            if (P == null) return;
            float t = Time.time;
            // vento: direção que deriva lentamente + rajadas (ruído de Perlin)
            float dirDeg = windAngle + 28f * (Mathf.PerlinNoise(windSeed, t * 0.02f) - 0.5f) * 2f;
            float gust = Mathf.PerlinNoise(windSeed + 7.7f, t * 0.35f);
            gust = gust * gust * 1.6f;
            float speed = P.windBase + P.windGust * gust;
            Wind = Quaternion.Euler(0f, dirDeg, 0f) * Vector3.forward * speed;

            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.SetAmbience(P.rainVolume, P.windVolume * Mathf.Lerp(0.6f, 1.3f, gust), Mathf.Lerp(0.85f, 1.15f, gust));

            if (followCamera != null)
            {
                Vector3 c = followCamera.position;
                Vector3 upwind = -Wind * 0.9f; // compensa a deriva lateral das gotas
                rain.transform.position = new Vector3(c.x + upwind.x, 20f, c.z + upwind.z) + followCamera.forward * 10f;
                groundSplash.transform.position = new Vector3(c.x, 0.02f, c.z) + Vector3.ProjectOnPlane(followCamera.forward, Vector3.up).normalized * 14f;
            }
            var vel = rain.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(Wind.x * 0.9f);
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(Wind.z * 0.9f);

            if (cloudMaterial != null)
            {
                Vector2 off = cloudMaterial.GetTextureOffset("_BaseMap");
                off += new Vector2(Wind.x, Wind.z) * Time.deltaTime * 0.00025f;
                cloudMaterial.SetTextureOffset("_BaseMap", off);
            }
        }

        /// <summary>Respingo de água (bola, pés) — usado pelas poças.</summary>
        public void Splash(Vector3 pos, float strength)
        {
            int n = Mathf.RoundToInt(Mathf.Lerp(8, 34, strength));
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle;
                ep.position = new Vector3(pos.x + r.x * 0.15f, 0.03f, pos.z + r.y * 0.15f);
                ep.velocity = new Vector3(r.x * 1.6f, UnityEngine.Random.Range(1.2f, 3.2f) * Mathf.Lerp(0.6f, 1.3f, strength), r.y * 1.6f);
                ep.startSize = UnityEngine.Random.Range(0.025f, 0.06f);
                ep.startLifetime = UnityEngine.Random.Range(0.35f, 0.7f);
                droplets.Emit(ep, 1);
            }
            if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.Splash(pos, strength);
        }

        IEnumerator LightningLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 13f));
                float power = UnityEngine.Random.Range(0.5f, 1f);
                flash.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(35f, 75f), UnityEngine.Random.Range(0f, 360f), 0f);
                flash.enabled = true;
                int pulses = UnityEngine.Random.Range(2, 4);
                for (int i = 0; i < pulses; i++)
                {
                    float peak = power * UnityEngine.Random.Range(2.2f, 4f);
                    flash.intensity = peak;
                    if (sky != null) sky.SetFloat("_Exposure", baseSkyExposure + peak * 0.9f);
                    RenderSettings.ambientSkyColor = P.ambientSky * (1f + peak);
                    yield return new WaitForSeconds(UnityEngine.Random.Range(0.04f, 0.09f));
                    flash.intensity = peak * 0.15f;
                    if (sky != null) sky.SetFloat("_Exposure", baseSkyExposure + peak * 0.15f);
                    yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.16f));
                }
                flash.intensity = 0f;
                flash.enabled = false;
                if (sky != null) sky.SetFloat("_Exposure", baseSkyExposure);
                RenderSettings.ambientSkyColor = P.ambientSky;

                // trovão chega depois (distância / velocidade do som)
                float distance = UnityEngine.Random.Range(300f, 2500f);
                yield return new WaitForSeconds(distance / 343f);
                float vol = Mathf.Lerp(1f, 0.45f, (distance - 300f) / 2200f) * power;
                if (ProceduralAudio.Instance != null) ProceduralAudio.Instance.Thunder(vol, UnityEngine.Random.Range(0.8f, 1.05f));
                if (OnLightning != null) OnLightning(vol);
            }
        }

        // ------------------------------------------------------------ construção das partículas

        ParticleSystem NewPS(string n, Material mat, int max)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        ParticleSystem CreateRain()
        {
            var ps = NewPS("Chuva", rainMaterial, 14000);
            var main = ps.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 16f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.startColor = new Color(0.8f, 0.85f, 0.95f, 0.55f);
            main.gravityModifier = 0.3f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(90f, 0f, 0f);    // emite para baixo
            shape.scale = new Vector3(46f, 46f, 1f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2f;
            r.velocityScale = 0.035f;
            return ps;
        }

        ParticleSystem CreateGroundSplash()
        {
            var ps = NewPS("RespingosChao", splashMaterial, 3000);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.6f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.scale = new Vector3(34f, 34f, 0.01f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 1f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            return ps;
        }

        ParticleSystem CreateDroplets()
        {
            var ps = NewPS("Borrifos", dropletMaterial, 1500);
            var main = ps.main;
            main.startLifetime = 0.6f;
            main.startSpeed = 0f;
            main.gravityModifier = 1f;
            main.startColor = new Color(0.8f, 0.86f, 0.95f, 0.75f);
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ps.Play();
            return ps;
        }

        GameObject CreateClouds()
        {
            if (cloudMaterial == null) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CamadaNuvens";
            var qc = go.GetComponent<Collider>();
            if (qc != null) Destroy(qc);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 140f, 0f);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // face voltada para baixo => visível do campo
            go.transform.localScale = new Vector3(1400f, 1400f, 1f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = cloudMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            cloudMaterial.SetTextureScale("_BaseMap", new Vector2(5f, 5f));
            return go;
        }
    }
}
