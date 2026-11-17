using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Sons sintetizados em tempo de execução (não depende de arquivos de áudio):
    /// chuva, vento, trovão, chute, trave, rede e respingo.
    /// </summary>
    public class ProceduralAudio : MonoBehaviour
    {
        public static ProceduralAudio Instance { get; private set; }
        const int Rate = 22050;

        AudioSource rain, wind, ui, crowd, crowdFx;
        AudioClip rainClip, windClip, thunderClip, kickClip, postClip, netClip, splashClip, bounceClip;
        AudioClip whistleClip, whistleLongClip, crowdClip, cheerClip;
        float crowdTarget, crowdBoost;
        System.Random rng = new System.Random(1234);

        void Awake()
        {
            Instance = this;
            rainClip = MakeLoop("Chuva", 4f, RainSample);
            windClip = MakeLoop("Vento", 6f, WindSample);
            thunderClip = MakeOneShot("Trovao", 6f, ThunderSample);
            kickClip = MakeOneShot("Chute", 0.18f, KickSample);
            postClip = MakeOneShot("Trave", 0.9f, PostSample);
            netClip = MakeOneShot("Rede", 0.35f, NetSample);
            splashClip = MakeOneShot("Respingo", 0.3f, SplashSample);
            bounceClip = MakeOneShot("Quique", 0.12f, BounceSample);
            whistleClip = MakeOneShot("Apito", 0.32f, WhistleSample);
            whistleLongClip = MakeOneShot("ApitoLongo", 0.9f, WhistleSample);
            crowdClip = MakeLoop("Torcida", 6f, CrowdSample);
            cheerClip = MakeOneShot("Festa", 3.5f, CheerSample);

            rain = NewSource("FonteChuva", true, rainClip);
            wind = NewSource("FonteVento", true, windClip);
            ui = NewSource("FonteUI", false, null);
            crowd = NewSource("FonteTorcida", true, crowdClip);
            crowdFx = NewSource("FonteTorcidaReacao", false, null);
            rain.volume = 0f; wind.volume = 0f; crowd.volume = 0f;
            rain.Play(); wind.Play(); crowd.Play();
        }

        void Update()
        {
            crowdBoost = Mathf.MoveTowards(crowdBoost, 0f, Time.unscaledDeltaTime * 0.25f);
            if (crowd != null) crowd.volume = Mathf.MoveTowards(crowd.volume, crowdTarget * (1f + crowdBoost), Time.unscaledDeltaTime * 0.5f);
        }

        /// <summary>Murmúrio da torcida (0 = estádio vazio, como no treino).</summary>
        public void SetCrowd(float volume) { crowdTarget = Mathf.Clamp01(volume); }

        /// <summary>Explosão da torcida: gol (1) ou grande defesa/lance perigoso (menor).</summary>
        public void CrowdReaction(float intensity)
        {
            if (crowdTarget <= 0f) return;
            crowdBoost = Mathf.Max(crowdBoost, intensity * 0.8f);
            crowdFx.pitch = Random.Range(0.92f, 1.06f);
            crowdFx.PlayOneShot(cheerClip, Mathf.Clamp01(0.35f + intensity * 0.65f));
        }

        /// <summary>Apito do árbitro: 1 = curto, 2 = intervalo, 3 = fim de jogo (o último é longo).</summary>
        public void Whistle(int times)
        {
            StartCoroutine(WhistleRoutine(Mathf.Max(1, times)));
        }

        System.Collections.IEnumerator WhistleRoutine(int n)
        {
            for (int i = 0; i < n; i++)
            {
                bool last = i == n - 1 && n > 1;
                ui.pitch = Random.Range(0.98f, 1.03f);
                ui.PlayOneShot(last ? whistleLongClip : whistleClip, 0.75f);
                yield return new WaitForSecondsRealtime(last ? 1f : 0.42f);
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        AudioSource NewSource(string n, bool loop, AudioClip clip)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = loop; s.clip = clip; s.playOnAwake = false; s.spatialBlend = 0f;
            return s;
        }

        public void SetAmbience(float rainVolume, float windVolume, float windPitch)
        {
            rain.volume = rainVolume;
            wind.volume = windVolume;
            wind.pitch = windPitch;
        }

        public void Thunder(float volume, float pitch)
        {
            ui.pitch = pitch;
            ui.PlayOneShot(thunderClip, volume);
        }

        public void Kick(Vector3 at, float strength) { Play3D(kickClip, at, Mathf.Lerp(0.35f, 1f, strength), Random.Range(0.9f, 1.1f)); }
        public void Post(Vector3 at, float strength) { Play3D(postClip, at, Mathf.Clamp01(strength), Random.Range(0.97f, 1.03f)); }
        public void Net(Vector3 at, float strength) { Play3D(netClip, at, Mathf.Clamp01(strength), Random.Range(0.9f, 1.1f)); }
        public void Splash(Vector3 at, float strength) { Play3D(splashClip, at, Mathf.Lerp(0.2f, 0.9f, strength), Random.Range(0.85f, 1.2f)); }
        public void Bounce(Vector3 at, float strength) { Play3D(bounceClip, at, Mathf.Clamp01(strength), Random.Range(0.9f, 1.1f)); }

        void Play3D(AudioClip clip, Vector3 at, float vol, float pitch)
        {
            var go = new GameObject("Som");
            go.transform.position = at;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.volume = vol; s.pitch = pitch;
            s.spatialBlend = 0.85f; s.minDistance = 4f; s.maxDistance = 80f; s.rolloffMode = AudioRolloffMode.Linear;
            s.Play();
            Destroy(go, clip.length / pitch + 0.1f);
        }

        // ------------------------------------------------------------ síntese

        delegate float Sampler(int i, int n, ref float s1, ref float s2, ref float s3);

        AudioClip MakeOneShot(string name, float seconds, Sampler f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float a = 0, b = 0, c = 0;
            for (int i = 0; i < n; i++) data[i] = f(i, n, ref a, ref b, ref c);
            Normalize(data, 0.9f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip MakeLoop(string name, float seconds, Sampler f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            int fade = Rate / 2;
            var raw = new float[n + fade];
            float a = 0, b = 0, c = 0;
            for (int i = 0; i < raw.Length; i++) raw[i] = f(i, n, ref a, ref b, ref c);
            // crossfade do final com o início => loop sem emenda
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float t = (float)i / fade;
                data[i] = raw[i] * t + raw[n + i] * (1f - t);
            }
            Normalize(data, 0.8f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 1e-5f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        float Noise() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

        float RainSample(int i, int n, ref float lp, ref float hp, ref float drops)
        {
            float w = Noise();
            lp += 0.35f * (w - lp);           // passa-baixa
            float h = w - lp;                  // componente aguda (chiado de chuva)
            drops *= 0.995f;
            if (rng.NextDouble() < 0.0025) drops += Noise() * 0.8f; // gotas isoladas
            return 0.55f * lp + 0.35f * h + drops * 0.4f;
        }

        float WindSample(int i, int n, ref float lp, ref float lp2, ref float ph)
        {
            float w = Noise();
            lp += 0.02f * (w - lp);
            lp2 += 0.004f * (lp - lp2);
            float t = (float)i / Rate;
            float gust = 0.6f + 0.4f * Mathf.Sin(t * 0.9f) * Mathf.Sin(t * 0.37f + 1.3f);
            return (lp * 3f + lp2 * 6f) * gust;
        }

        float ThunderSample(int i, int n, ref float lp, ref float lp2, ref float crack)
        {
            float t = (float)i / Rate;
            float w = Noise();
            lp += 0.015f * (w - lp);
            lp2 += 0.05f * (w - lp2);
            float env = Mathf.Exp(-t * 0.8f) * (1f - Mathf.Exp(-t * 30f));
            float rumble = lp * 6f * (0.7f + 0.3f * Mathf.Sin(t * 3.1f) * Mathf.Sin(t * 1.7f));
            float crk = t < 0.35f ? lp2 * 2.5f * Mathf.Exp(-t * 9f) : 0f;
            return (rumble + crk) * env;
        }

        float KickSample(int i, int n, ref float lp, ref float b, ref float c)
        {
            float t = (float)i / Rate;
            float thump = Mathf.Sin(2f * Mathf.PI * (95f - 120f * t) * t) * Mathf.Exp(-t * 32f);
            lp += 0.3f * (Noise() - lp);
            float slap = lp * Mathf.Exp(-t * 90f);
            return thump * 0.9f + slap * 0.7f;
        }

        float PostSample(int i, int n, ref float a, ref float b, ref float c)
        {
            float t = (float)i / Rate;
            float env = Mathf.Exp(-t * 5f);
            float s = Mathf.Sin(2f * Mathf.PI * 820f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 1310f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * 2245f * t);
            float hit = Noise() * Mathf.Exp(-t * 120f);
            return s * env * 0.5f + hit * 0.6f;
        }

        float NetSample(int i, int n, ref float lp, ref float b, ref float c)
        {
            float t = (float)i / Rate;
            lp += 0.12f * (Noise() - lp);
            return lp * Mathf.Exp(-t * 11f) * (1f - Mathf.Exp(-t * 200f));
        }

        float SplashSample(int i, int n, ref float lp, ref float b, ref float c)
        {
            float t = (float)i / Rate;
            float w = Noise();
            lp += 0.4f * (w - lp);
            float bubbles = Mathf.Sin(2f * Mathf.PI * (500f + 900f * t) * t) * 0.15f;
            return ((w - lp) * 0.8f + bubbles) * Mathf.Exp(-t * 14f) * (1f - Mathf.Exp(-t * 300f));
        }

        float WhistleSample(int i, int n, ref float ph, ref float lp, ref float c)
        {
            // apito de árbitro: tom agudo com trinado (bolinha dentro do apito) e sopro
            float t = (float)i / Rate;
            float trill = 1f + 0.035f * Mathf.Sin(2f * Mathf.PI * 28f * t);
            ph += 2f * Mathf.PI * 2850f * trill / Rate;
            float env = Mathf.Clamp01(t / 0.015f) * Mathf.Clamp01((n - i) / (Rate * 0.05f));
            lp += 0.5f * (Noise() - lp);
            float am = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 28f * t);
            return (Mathf.Sin(ph) * 0.8f + Mathf.Sin(ph * 2f) * 0.12f) * am * env + lp * 0.08f * env;
        }

        float CrowdSample(int i, int n, ref float lp, ref float lp2, ref float voice)
        {
            // multidão: ruído filtrado na faixa da voz com ondulações lentas
            float t = (float)i / Rate;
            float w = Noise();
            lp += 0.09f * (w - lp);
            lp2 += 0.012f * (lp - lp2);
            float band = lp - lp2;
            float swell = 0.7f + 0.3f * Mathf.Sin(t * 0.7f) * Mathf.Sin(t * 0.23f + 2f);
            voice *= 0.9995f;
            if (rng.NextDouble() < 0.0004) voice += 0.6f;
            return band * 3f * swell * (1f + voice * 0.3f);
        }

        float CheerSample(int i, int n, ref float lp, ref float lp2, ref float c)
        {
            float t = (float)i / Rate;
            float w = Noise();
            lp += 0.16f * (w - lp);
            lp2 += 0.02f * (lp - lp2);
            float env = Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-Mathf.Max(0f, t - 0.8f) * 1.1f);
            return (lp - lp2) * 3.5f * env;
        }

        float BounceSample(int i, int n, ref float a, ref float b, ref float c)
        {
            float t = (float)i / Rate;
            return Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 45f);
        }
    }
}
