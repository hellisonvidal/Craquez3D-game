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
                        sunIntensity = 1.0f, sunColor = new Color(0.86f, 0.9f, 0.98f), sunPitch = 58f, shadowStrength = 0.5f,
                        ambientSky = new Color(0.6f, 0.66f, 0.76f), ambientEquator = new Color(0.47f, 0.5f, 0.54f), ambientGround = new Color(0.22f, 0.23f, 0.22f),
                        fogDensity = 0.0045f, fogColor = new Color(0.58f, 0.62f, 0.67f),
                        skyTint = new Color(0.42f, 0.46f, 0.52f), skyExposure = 1.0f, atmosphere = 0.9f, skyGround = new Color(0.36f, 0.37f, 0.39f),
                        clouds = 0.85f, rainRate = 4500f, rainSpeed = 15f,
                        windBase = 4.5f, windGust = 3f, rollDecel = 0.85f, bounce = 0.52f,
                        puddles = 14, soakedAreas = 10, puddleDepth = 0.65f, puddleSize = 1f, rippleSpeed = 0.06f, ripplesPerSecond = 70f, wetness = 0.7f,
                        lightning = false, floodlights = 0.9f, rainVolume = 0.55f, windVolume = 0.25f,
                        gkReaction = 0.04f, gkFumble = 0.14f, gkCatchSpeed = 18f, playerGrip = 0.85f, postProcess = "PP_Chuva"
                    };
                default:
                    return new Perfil
                    {
                        sunIntensity = 0.65f, sunColor = new Color(0.72f, 0.78f, 0.92f), sunPitch = 62f, shadowStrength = 0.3f,
                        ambientSky = new Color(0.46f, 0.51f, 0.62f), ambientEquator = new Color(0.36f, 0.39f, 0.45f), ambientGround = new Color(0.17f, 0.18f, 0.19f),
                        fogDensity = 0.0075f, fogColor = new Color(0.38f, 0.42f, 0.49f),
                        skyTint = new Color(0.3f, 0.34f, 0.42f), skyExposure = 0.7f, atmosphere = 0.75f, skyGround = new Color(0.22f, 0.23f, 0.26f),
                        clouds = 1f, rainRate = 9500f, rainSpeed = 19f,
                        windBase = 10.5f, windGust = 6.5f, rollDecel = 0.95f, bounce = 0.45f,
                        puddles = 26, soakedAreas = 18, puddleDepth = 1f, puddleSize = 1.35f, rippleSpeed = 0.14f, ripplesPerSecond = 190f, wetness = 1f,
                        lightning = true, floodlights = 1.6f, rainVolume = 1f, windVolume = 0.7f,
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

        ParticleSystem rain, groundSplash, droplets, mist;
        LineRenderer bolt;
        Material boltMaterial;
        float wetLevel = 1f;          // 0..1 do encharcamento alvo: o campo vai molhando com o tempo
        float wetApplied = -1f;
        float rainIntensity = 1f;     // rajadas de chuva (Perlin)
        float ballSprayTimer;
        float boltUntil;
        readonly List<Vector4> lensDrops = new List<Vector4>(); // x, y (0..1), raio, nascimento
        float lensSpawn;

        /// <summary>Encharcamento atual do gramado (0 = seco, 1 = o máximo deste clima).</summary>
        public float WetLevel { get { return wetLevel; } }
        public float RainIntensity { get { return P != null && P.rainRate > 0f ? rainIntensity : 0f; } }
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
            mist = CreateMist();
            cloudLayer = CreateClouds();
            bolt = CreateBolt();

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
                l.range = 160f;
                l.spotAngle = 70f;
                l.innerSpotAngle = 40f;
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
                Color cc = Color.Lerp(new Color(1f, 1f, 1f), new Color(0.5f, 0.53f, 0.6f), Mathf.Clamp01((p.clouds - 0.3f) / 0.7f));
                cc.a = p.clouds;
                cloudMaterial.SetColor("_BaseColor", cc);
            }

            var em = rain.emission; em.rateOverTime = p.rainRate * GameSettings.Particulas;
            var main = rain.main; main.startSpeed = p.rainSpeed;
            if (p.rainRate > 0f) { if (!rain.isPlaying) rain.Play(); } else rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var em2 = groundSplash.emission; em2.rateOverTime = p.rainRate * 0.12f * GameSettings.Particulas;
            if (p.rainRate > 0f) { if (!groundSplash.isPlaying) groundSplash.Play(); } else groundSplash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // refletores: o URP usa queda física (1/d²); a ~70 m do campo precisamos de intensidade alta
            foreach (var l in floods) { l.enabled = p.floodlights > 0f; l.intensity = p.floodlights * 1800f; }

            // começa parcialmente molhado e vai encharcando com a chuva (jogo de verdade: o campo piora com o tempo)
            wetLevel = p.rainRate > 0f ? 0.55f : 1f;
            wetApplied = -1f;
            ApplyWetness();
            var me = mist.emission; me.rateOverTime = c == Clima.Tempestade ? 26f * GameSettings.Particulas : (c == Clima.Chuva ? 7f * GameSettings.Particulas : 0f);
            if (c != Clima.Sol) { if (!mist.isPlaying) mist.Play(); } else mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            lensDrops.Clear();

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

        /// <summary>Molhado efetivo agora (perfil do clima × encharcamento acumulado).</summary>
        public float Wetness { get { return P != null ? P.wetness * wetLevel : 0f; } }

        void ApplyWetness()
        {
            if (P == null) return;
            float w = Wetness;
            if (Mathf.Abs(w - wetApplied) < 0.01f) return;
            wetApplied = w;
            for (int i = 0; i < grassMaterials.Count; i++)
            {
                var m = grassMaterials[i];
                Color b = i < grassBaseColors.Count ? grassBaseColors[i] : Color.white;
                float k = Mathf.Lerp(1f, 0.85f, w);
                m.SetColor("_BaseColor", new Color(b.r * k, b.g * k, b.b * k, 1f));
                m.SetFloat("_Smoothness", Mathf.Lerp(0.12f, 0.36f, w)); // brilho molhado sem "estourar" nos refletores
            }
            foreach (var m in characterMaterials) ApplyWetCharacter(m, w);
        }

        static void ApplyWetCharacter(Material m, float w)
        {
            if (m == null) return;
            bool hasPbrMap = m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap") != null;
            // uniforme e pele ficam levemente mais escuros e reflexivos quando molhados pela chuva
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.68f, 0.72f, 0.78f, 1f), w));
            if (m.HasProperty("_Metallic"))
                m.SetFloat("_Metallic", Mathf.Lerp(0f, 0.18f, w));
            if (m.HasProperty("_Smoothness"))
                m.SetFloat("_Smoothness", hasPbrMap ? Mathf.Lerp(0.90f, 1.0f, w) : Mathf.Lerp(0.50f, 0.90f, w));
        }

        /// <summary>Materiais criados depois (uniformes da partida) também ficam molhados.</summary>
        public void RegisterCharacterMaterial(Material m)
        {
            if (m == null || characterMaterials.Contains(m)) return;
            characterMaterials.Add(m);
            ApplyWetCharacter(m, Wetness);
        }

        public void UnregisterCharacterMaterial(Material m) { characterMaterials.Remove(m); }

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

            if (P.rainRate > 0f)
            {
                // a chuva vem em ondas: mais forte nas rajadas de vento, nunca constante
                float wave = Mathf.PerlinNoise(windSeed + 31f, t * 0.06f);
                rainIntensity = Mathf.Lerp(0.55f, 1.3f, wave) * Mathf.Lerp(0.9f, 1.15f, gust);
                var em = rain.emission; em.rateOverTime = P.rainRate * rainIntensity * GameSettings.Particulas;
                var em2 = groundSplash.emission; em2.rateOverTime = P.rainRate * 0.12f * rainIntensity * GameSettings.Particulas;
                var rm = rain.main; rm.startSpeed = P.rainSpeed * Mathf.Lerp(0.95f, 1.15f, gust);
                // o campo encharca aos poucos (cerca de 3 minutos de chuva forte até o máximo)
                wetLevel = Mathf.MoveTowards(wetLevel, 1f, Time.deltaTime * rainIntensity / (Current == Clima.Tempestade ? 120f : 200f));
                ApplyWetness();
                if (followCamera != null)
                {
                    Vector3 c = followCamera.position;
                    mist.transform.position = new Vector3(c.x, 0.4f, c.z) + Vector3.ProjectOnPlane(followCamera.forward, Vector3.up).normalized * 18f;
                    var mv = mist.velocityOverLifetime;
                    mv.x = new ParticleSystem.MinMaxCurve(Wind.x * 0.6f);
                    mv.z = new ParticleSystem.MinMaxCurve(Wind.z * 0.6f);
                }
                UpdateBallSpray();
                UpdateLensDrops();
            }
            if (bolt != null && bolt.enabled && Time.time > boltUntil) bolt.enabled = false;

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
                float yaw = UnityEngine.Random.Range(0f, 360f);
                flash.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(35f, 75f), yaw, 0f);
                ShowBolt(yaw, power);
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

        // ------------------------------------------------------------ efeitos da chuva no jogo

        /// <summary>A bola rolando ou quicando no gramado molhado levanta um rastro de água.</summary>
        void UpdateBallSpray()
        {
            var b = BallPhysics.Instance;
            if (b == null || b.IsHeld || Wetness < 0.25f) return;
            Vector3 v = b.Velocity;
            Vector3 p = b.transform.position;
            float sp = new Vector2(v.x, v.z).magnitude;
            if (p.y > Arena.BallRadius + 0.06f || sp < 3.5f) return;
            ballSprayTimer -= Time.deltaTime;
            if (ballSprayTimer > 0f) return;
            ballSprayTimer = Mathf.Lerp(0.09f, 0.03f, Mathf.Clamp01(sp / 25f));
            var ep = new ParticleSystem.EmitParams();
            Vector3 back = -new Vector3(v.x, 0f, v.z).normalized;
            int n = Mathf.RoundToInt(Mathf.Lerp(2f, 7f, Mathf.Clamp01(sp / 22f)) * Wetness);
            for (int i = 0; i < n; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle;
                ep.position = new Vector3(p.x, 0.04f, p.z) + back * 0.1f;
                ep.velocity = back * UnityEngine.Random.Range(0.5f, 1.6f) + new Vector3(r.x * 0.8f, UnityEngine.Random.Range(0.8f, 2.2f) * Mathf.Clamp01(sp / 15f), r.y * 0.8f);
                ep.startSize = UnityEngine.Random.Range(0.02f, 0.045f);
                ep.startLifetime = UnityEngine.Random.Range(0.25f, 0.5f);
                droplets.Emit(ep, 1);
            }
        }

        /// <summary>Gotas na lente da câmera (como nas transmissões debaixo de chuva).</summary>
        void UpdateLensDrops()
        {
            if (GameSettings.Qualidade == QualidadeGrafica.Baixa) { lensDrops.Clear(); return; }
            float rate = (Current == Clima.Tempestade ? 2.6f : 0.9f) * rainIntensity;
            // a lente só pega chuva quando olha para o lado de onde o vento traz as gotas
            if (followCamera != null && Wind.sqrMagnitude > 0.1f)
                rate *= Mathf.Lerp(0.35f, 1.3f, Mathf.Clamp01(0.5f - 0.5f * Vector3.Dot(followCamera.forward, Wind.normalized)));
            lensSpawn += Time.deltaTime * rate;
            while (lensSpawn > 1f && lensDrops.Count < 26)
            {
                lensSpawn -= 1f;
                lensDrops.Add(new Vector4(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.Range(0.006f, 0.02f), Time.time));
            }
            for (int i = lensDrops.Count - 1; i >= 0; i--)
            {
                var d = lensDrops[i];
                float age = Time.time - d.w;
                if (age > 3.5f) { lensDrops.RemoveAt(i); continue; }
                // as maiores escorrem
                if (d.z > 0.013f) { d.y += Time.deltaTime * 0.02f * (d.z / 0.02f); lensDrops[i] = d; }
            }
        }

        void OnGUI()
        {
            if (lensDrops.Count == 0 || P == null || P.rainRate <= 0f) return;
            GUI.depth = 3;
            float H = Screen.height;
            foreach (var d in lensDrops)
            {
                float age = Time.time - d.w;
                float a = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((3.5f - age) / 1.2f);
                float r = d.z * H;
                Vector2 c = new Vector2(d.x * Screen.width, d.y * H);
                UiKit.Disc(c, r, new Color(0.75f, 0.82f, 0.9f, 0.16f * a));
                UiKit.RingAt(c, r, new Color(1f, 1f, 1f, 0.22f * a));
                UiKit.Disc(c + new Vector2(-r * 0.3f, -r * 0.35f), r * 0.25f, new Color(1f, 1f, 1f, 0.35f * a));
            }
        }

        /// <summary>Desenha o raio no céu, longe do estádio, na direção do clarão.</summary>
        void ShowBolt(float yaw, float power)
        {
            if (bolt == null) return;
            Vector3 dir = Quaternion.Euler(0f, yaw + 180f, 0f) * Vector3.forward;
            Vector3 top = dir * UnityEngine.Random.Range(300f, 520f) + Vector3.up * UnityEngine.Random.Range(150f, 210f);
            Vector3 bottom = new Vector3(top.x, 0f, top.z) + UnityEngine.Random.insideUnitSphere * 40f;
            bottom.y = UnityEngine.Random.Range(20f, 60f);
            int n = bolt.positionCount;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / (n - 1);
                Vector3 p = Vector3.Lerp(top, bottom, k);
                if (i > 0 && i < n - 1) p += new Vector3(UnityEngine.Random.Range(-14f, 14f), UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(-14f, 14f));
                bolt.SetPosition(i, p);
            }
            float w = Mathf.Lerp(1.6f, 3.2f, power);
            bolt.startWidth = w;
            bolt.endWidth = w * 0.35f;
            bolt.enabled = true;
            boltUntil = Time.time + UnityEngine.Random.Range(0.18f, 0.32f);
        }

        // ------------------------------------------------------------ construção das partículas

        ParticleSystem CreateMist()
        {
            // névoa de respingos rente ao gramado, levada pelo vento (aparece mais na tempestade)
            var ps = NewPS("NevoaChuva", splashMaterial, 400);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startColor = new Color(0.82f, 0.86f, 0.92f, 0.07f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 0.5f, 60f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            return ps;
        }

        LineRenderer CreateBolt()
        {
            var go = new GameObject("Raio");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 9;
            lr.useWorldSpace = true;
            if (splashMaterial != null)
            {
                boltMaterial = new Material(splashMaterial);
                if (boltMaterial.HasProperty("_BaseColor")) boltMaterial.SetColor("_BaseColor", new Color(0.92f, 0.95f, 1f, 1f));
                lr.sharedMaterial = boltMaterial;
            }
            lr.startColor = lr.endColor = new Color(0.95f, 0.97f, 1f, 1f);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }


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
