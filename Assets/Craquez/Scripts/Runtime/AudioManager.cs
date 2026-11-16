using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Craquez
{
    /// <summary>
    /// Gerenciador de áudio expandido usando assets gratuitos do Kenney (UI + Interface sounds).
    /// Suporta: sons de UI, chute, apito, torcida, impacto, passos, etc.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup uiGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;

        [Header("Kenney UI Sounds")]
        [SerializeField] private AudioClip[] uiClicks;           // click1-5
        [SerializeField] private AudioClip uiMouseClick;         // mouseclick1
        [SerializeField] private AudioClip uiMouseRelease;       // mouserelease1
        [SerializeField] private AudioClip[] uiRollovers;        // rollover1-6
        [SerializeField] private AudioClip[] uiSwitches;         // switch1-38

        [Header("Kenney Interface Sounds")]
        [SerializeField] private AudioClip[] ifxBack;            // back_001-004
        [SerializeField] private AudioClip ifxBong;              // bong_001
        [SerializeField] private AudioClip[] ifxClick;           // click_001-005
        [SerializeField] private AudioClip[] ifxClose;           // close_001-004
        [SerializeField] private AudioClip[] ifxConfirmation;    // confirmation_001-004
        [SerializeField] private AudioClip[] ifxDrop;            // drop_001-004
        [SerializeField] private AudioClip[] ifxError;           // error_001-008
        [SerializeField] private AudioClip[] ifxGlass;           // glass_001-006
        [SerializeField] private AudioClip[] ifxGlitch;          // glitch_001-004
        [SerializeField] private AudioClip[] ifxMaximize;        // maximize_001-009
        [SerializeField] private AudioClip[] ifxMinimize;        // minimize_001-009
        [SerializeField] private AudioClip[] ifxOpen;            // open_001-004
        [SerializeField] private AudioClip[] ifxPluck;           // pluck_001-002
        [SerializeField] private AudioClip[] ifxQuestion;        // question_001-004
        [SerializeField] private AudioClip[] ifxScratch;         // scratch_001-005
        [SerializeField] private AudioClip[] ifxScroll;          // scroll_001-005
        [SerializeField] private AudioClip[] ifxSelect;          // select_001-008
        [SerializeField] private AudioClip[] ifxSwitch;          // switch_001-007
        [SerializeField] private AudioClip[] ifxTick;            // tick_001,002,004
        [SerializeField] private AudioClip[] ifxToggle;          // toggle_001-003

        [Header("Gameplay Sounds (to be added from freesound/pixabay)")]
        [SerializeField] private AudioClip[] kickSounds;         // chute bola
        [SerializeField] private AudioClip[] whistleSounds;      // apito
        [SerializeField] private AudioClip[] crowdCheerSounds;   // torcida gol
        [SerializeField] private AudioClip[] crowdAmbienceSounds; // torcida ambiente
        [SerializeField] private AudioClip[] impactSounds;       // impacto bola/jogador
        [SerializeField] private AudioClip[] footstepSounds;     // passos grama
        [SerializeField] private AudioClip[] slideSounds;        // carrinho/deslize
        [SerializeField] private AudioClip[] goalkeeperSaveSounds; // defesa goleiro
        [SerializeField] private AudioClip[] ballBounceSounds;   // quique bola
        [SerializeField] private AudioClip[] netHitSounds;       // bola na rede

        [Header("Settings")]
        [SerializeField] private int maxConcurrentSources = 32;
        [SerializeField] private float defaultVolume = 0.8f;
        [SerializeField] private float pitchVariation = 0.1f;

        private readonly Queue<AudioSource> _sourcePool = new();
        private readonly Dictionary<string, AudioClip[]> _soundGroups = new();
        private AudioSource _musicSource;
        private AudioSource _ambienceSource;
        private float _masterVolume = 1f;
        private float _sfxVolume = 1f;
        private float _musicVolume = 1f;
        private float _uiVolume = 1f;
        private float _ambienceVolume = 1f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        void Initialize()
        {
            // Criar pool de AudioSources
            for (int i = 0; i < maxConcurrentSources; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f; // 3D por padrão
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 1f;
                source.maxDistance = 50f;
                _sourcePool.Enqueue(source);
            }

            // Sources dedicados para música e ambiente
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f; // 2D
            if (musicGroup != null) _musicSource.outputAudioMixerGroup = musicGroup;

            _ambienceSource = gameObject.AddComponent<AudioSource>();
            _ambienceSource.playOnAwake = false;
            _ambienceSource.loop = true;
            _ambienceSource.spatialBlend = 0f;
            if (ambienceGroup != null) _ambienceSource.outputAudioMixerGroup = ambienceGroup;

            // Registrar grupos de sons
            RegisterSoundGroups();

            // Carregar volumes salvos
            LoadVolumes();
        }

        void RegisterSoundGroups()
        {
            // UI Sounds
            _soundGroups["UIClick"] = uiClicks;
            _soundGroups["UIMouseClick"] = new[] { uiMouseClick };
            _soundGroups["UIMouseRelease"] = new[] { uiMouseRelease };
            _soundGroups["UIRollover"] = uiRollovers;
            _soundGroups["UISwitch"] = uiSwitches;

            // Interface Sounds
            _soundGroups["IFXBack"] = ifxBack;
            _soundGroups["IFXBong"] = new[] { ifxBong };
            _soundGroups["IFXClick"] = ifxClick;
            _soundGroups["IFXClose"] = ifxClose;
            _soundGroups["IFXConfirmation"] = ifxConfirmation;
            _soundGroups["IFXDrop"] = ifxDrop;
            _soundGroups["IFXError"] = ifxError;
            _soundGroups["IFXGlass"] = ifxGlass;
            _soundGroups["IFXGlitch"] = ifxGlitch;
            _soundGroups["IFXMaximize"] = ifxMaximize;
            _soundGroups["IFXMinimize"] = ifxMinimize;
            _soundGroups["IFXOpen"] = ifxOpen;
            _soundGroups["IFXPluck"] = ifxPluck;
            _soundGroups["IFXQuestion"] = ifxQuestion;
            _soundGroups["IFXScratch"] = ifxScratch;
            _soundGroups["IFXScroll"] = ifxScroll;
            _soundGroups["IFXSelect"] = ifxSelect;
            _soundGroups["IFXSwitch"] = ifxSwitch;
            _soundGroups["IFXTick"] = ifxTick;
            _soundGroups["IFXToggle"] = ifxToggle;

            // Gameplay
            _soundGroups["Kick"] = kickSounds;
            _soundGroups["Whistle"] = whistleSounds;
            _soundGroups["CrowdCheer"] = crowdCheerSounds;
            _soundGroups["CrowdAmbience"] = crowdAmbienceSounds;
            _soundGroups["Impact"] = impactSounds;
            _soundGroups["Footstep"] = footstepSounds;
            _soundGroups["Slide"] = slideSounds;
            _soundGroups["GoalkeeperSave"] = goalkeeperSaveSounds;
            _soundGroups["BallBounce"] = ballBounceSounds;
            _soundGroups["NetHit"] = netHitSounds;
        }

        // =====================================================================
        // MÉTODOS PÚBLICOS - UI
        // =====================================================================

        public void PlayUIClick() => PlayRandom("UIClick", uiGroup, 0.5f);
        public void PlayUIMouseClick() => PlayRandom("UIMouseClick", uiGroup, 0.6f);
        public void PlayUIMouseRelease() => PlayRandom("UIMouseRelease", uiGroup, 0.4f);
        public void PlayUIRollover() => PlayRandom("UIRollover", uiGroup, 0.3f);
        public void PlayUISwitch() => PlayRandom("UISwitch", uiGroup, 0.5f);

        // =====================================================================
        // MÉTODOS PÚBLICOS - INTERFACE (Feedback geral)
        // =====================================================================

        public void PlayIFXBack() => PlayRandom("IFXBack", uiGroup, 0.5f);
        public void PlayIFXBong() => PlayRandom("IFXBong", uiGroup, 0.6f);
        public void PlayIFXClick() => PlayRandom("IFXClick", uiGroup, 0.5f);
        public void PlayIFXClose() => PlayRandom("IFXClose", uiGroup, 0.5f);
        public void PlayIFXConfirmation() => PlayRandom("IFXConfirmation", uiGroup, 0.6f);
        public void PlayIFXDrop() => PlayRandom("IFXDrop", uiGroup, 0.5f);
        public void PlayIFXError() => PlayRandom("IFXError", uiGroup, 0.7f);
        public void PlayIFXGlass() => PlayRandom("IFXGlass", uiGroup, 0.5f);
        public void PlayIFXGlitch() => PlayRandom("IFXGlitch", uiGroup, 0.6f);
        public void PlayIFXMaximize() => PlayRandom("IFXMaximize", uiGroup, 0.5f);
        public void PlayIFXMinimize() => PlayRandom("IFXMinimize", uiGroup, 0.5f);
        public void PlayIFXOpen() => PlayRandom("IFXOpen", uiGroup, 0.5f);
        public void PlayIFXPluck() => PlayRandom("IFXPluck", uiGroup, 0.5f);
        public void PlayIFXQuestion() => PlayRandom("IFXQuestion", uiGroup, 0.5f);
        public void PlayIFXScratch() => PlayRandom("IFXScratch", uiGroup, 0.5f);
        public void PlayIFXScroll() => PlayRandom("IFXScroll", uiGroup, 0.4f);
        public void PlayIFXSelect() => PlayRandom("IFXSelect", uiGroup, 0.5f);
        public void PlayIFXSwitch() => PlayRandom("IFXSwitch", uiGroup, 0.5f);
        public void PlayIFXTick() => PlayRandom("IFXTick", uiGroup, 0.4f);
        public void PlayIFXToggle() => PlayRandom("IFXToggle", uiGroup, 0.5f);

        // =====================================================================
        // MÉTODOS PÚBLICOS - GAMEPLAY
        // =====================================================================

        /// <summary>Som de chute com variação baseada na força</summary>
        public void PlayKick(Vector3 position, float power = 1f)
        {
            var clip = GetRandomClip("Kick");
            if (clip == null) return;

            var source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _sfxVolume * _masterVolume * defaultVolume * Mathf.Lerp(0.6f, 1f, power);
            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation) * power;
            source.spatialBlend = 1f;
            source.maxDistance = 40f;
            if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
            source.Play();
            ReturnToPoolWhenDone(source, clip.length);
        }

        /// <summary>Apito do árbitro</summary>
        public void PlayWhistle(Vector3 position, float volume = 1f)
        {
            var clip = GetRandomClip("Whistle");
            if (clip == null) return;

            var source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _sfxVolume * _masterVolume * volume;
            source.pitch = 1f + Random.Range(-0.05f, 0.05f);
            source.spatialBlend = 0.3f; // Parcialmente 2D para ser ouvido em todo campo
            source.maxDistance = 100f;
            if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
            source.Play();
            ReturnToPoolWhenDone(source, clip.length);
        }

        /// <summary>Torcida comemorando gol</summary>
        public void PlayCrowdCheer(Vector3 position, float intensity = 1f)
        {
            var clip = GetRandomClip("CrowdCheer");
            if (clip == null) return;

            var source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _sfxVolume * _masterVolume * defaultVolume * intensity;
            source.pitch = 1f + Random.Range(-0.1f, 0.1f);
            source.spatialBlend = 0.5f;
            source.maxDistance = 80f;
            if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
            source.Play();
            ReturnToPoolWhenDone(source, clip.length);
        }

        /// <summary>Ambiente de torcida contínuo</summary>
        public void SetCrowdAmbience(bool playing, float volume = 1f)
        {
            if (_ambienceSource == null) return;

            var clip = GetRandomClip("CrowdAmbience");
            if (clip == null) return;

            if (playing && !_ambienceSource.isPlaying)
            {
                _ambienceSource.clip = clip;
                _ambienceSource.volume = _ambienceVolume * _masterVolume * volume;
                _ambienceSource.Play();
            }
            else if (!playing && _ambienceSource.isPlaying)
            {
                _ambienceSource.Stop();
            }
        }

        /// <summary>Impacto bola/jogador/trave</summary>
        public void PlayImpact(Vector3 position, float force = 1f, bool isMetal = false)
        {
            var clip = GetRandomClip("Impact");
            if (clip == null) return;

            var source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _sfxVolume * _masterVolume * defaultVolume * Mathf.Lerp(0.4f, 1f, force);
            source.pitch = isMetal ? 1.2f + Random.Range(-0.1f, 0.1f) : 1f + Random.Range(-pitchVariation, pitchVariation);
            source.spatialBlend = 1f;
            source.maxDistance = 30f;
            if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
            source.Play();
            ReturnToPoolWhenDone(source, clip.length);
        }

        /// <summary>Passos na grama (varia com velocidade)</summary>
        public void PlayFootstep(Vector3 position, float speed, bool isSprinting = false)
        {
            var clip = GetRandomClip("Footstep");
            if (clip == null) return;

            var source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _sfxVolume * _masterVolume * 0.4f * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(speed / 10f));
            source.pitch = isSprinting ? 1.1f + Random.Range(-0.05f, 0.05f) : 1f + Random.Range(-0.1f, 0.1f);
            source.spatialBlend = 1f;
            source.maxDistance = 15f;
            if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
            source.Play();
            ReturnToPoolWhenDone(source, clip.length);
        }

        /// <summary>Som de carrinho/deslize</summary>
        public void PlaySlide(Vector3 position, float speed)
        {
            var clip = GetRandomClip("Slide");
            if (clip == null) return;

            var source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _sfxVolume * _masterVolume * 0.6f * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(speed / 12f));
            source.pitch = 1f + Random.Range(-0.15f, 0.15f);
            source.spatialBlend = 1f;
            source.maxDistance = 20f;
            if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
            source.Play();
            ReturnToPoolWhenDone(source, clip.length);
        }

        /// <summary>Defesa do goleiro</summary>
        public void PlayGoalkeeperSave(Vector3 position, float power = 1f)
        {
            var clip = GetRandomClip("GoalkeeperSave");
            if (clip == null) return;

            var source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _sfxVolume * _masterVolume * defaultVolume * Mathf.Lerp(0.7f, 1f, power);
            source.pitch = 1f + Random.Range(-0.1f, 0.1f);
            source.spatialBlend = 1f;
            source.maxDistance = 35f;
            if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
            source.Play();
            ReturnToPoolWhenDone(source, clip.length);
        }

        /// <summary>Quique da bola no chão</summary>
        public void PlayBallBounce(Vector3 position, float velocity)
        {
            var clip = GetRandomClip("BallBounce");
            if (clip == null) return;

            var source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _sfxVolume * _masterVolume * 0.5f * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(velocity / 15f));
            source.pitch = 1f + Random.Range(-0.2f, 0.2f);
            source.spatialBlend = 1f;
            source.maxDistance = 25f;
            if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
            source.Play();
            ReturnToPoolWhenDone(source, clip.length);
        }

        /// <summary>Bola batendo na rede</summary>
        public void PlayNetHit(Vector3 position, float power = 1f)
        {
            var clip = GetRandomClip("NetHit");
            if (clip == null) return;

            var source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _sfxVolume * _masterVolume * defaultVolume * Mathf.Lerp(0.5f, 1f, power);
            source.pitch = 1f + Random.Range(-0.1f, 0.1f);
            source.spatialBlend = 1f;
            source.maxDistance = 30f;
            if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
            source.Play();
            ReturnToPoolWhenDone(source, clip.length);
        }

        // =====================================================================
        // MÚSICA
        // =====================================================================

        public void PlayMusic(AudioClip clip, float volume = 1f, bool fadeIn = true)
        {
            if (_musicSource == null || clip == null) return;

            if (fadeIn)
            {
                StartCoroutine(FadeMusic(clip, volume));
            }
            else
            {
                _musicSource.clip = clip;
                _musicSource.volume = _musicVolume * _masterVolume * volume;
                _musicSource.Play();
            }
        }

        public void StopMusic(bool fadeOut = true)
        {
            if (_musicSource == null) return;

            if (fadeOut && _musicSource.isPlaying)
            {
                StartCoroutine(FadeOutMusic());
            }
            else
            {
                _musicSource.Stop();
            }
        }

        System.Collections.IEnumerator FadeMusic(AudioClip clip, float targetVolume)
        {
            float startVolume = _musicSource.volume;
            float duration = 1f;
            float elapsed = 0f;

            _musicSource.clip = clip;
            _musicSource.volume = 0f;
            _musicSource.Play();

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(startVolume, _musicVolume * _masterVolume * targetVolume, elapsed / duration);
                yield return null;
            }
            _musicSource.volume = _musicVolume * _masterVolume * targetVolume;
        }

        System.Collections.IEnumerator FadeOutMusic()
        {
            float startVolume = _musicSource.volume;
            float duration = 1f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }
            _musicSource.Stop();
            _musicSource.volume = startVolume;
        }

        // =====================================================================
        // CONTROLE DE VOLUME
        // =====================================================================

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            if (audioMixer != null) audioMixer.SetFloat("MasterVolume", Mathf.Log10(_masterVolume) * 20f);
            SaveVolumes();
        }

        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            if (audioMixer != null) audioMixer.SetFloat("SFXVolume", Mathf.Log10(_sfxVolume) * 20f);
            SaveVolumes();
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            if (audioMixer != null) audioMixer.SetFloat("MusicVolume", Mathf.Log10(_musicVolume) * 20f);
            SaveVolumes();
        }

        public void SetUIVolume(float volume)
        {
            _uiVolume = Mathf.Clamp01(volume);
            if (audioMixer != null) audioMixer.SetFloat("UIVolume", Mathf.Log10(_uiVolume) * 20f);
            SaveVolumes();
        }

        public void SetAmbienceVolume(float volume)
        {
            _ambienceVolume = Mathf.Clamp01(volume);
            if (audioMixer != null) audioMixer.SetFloat("AmbienceVolume", Mathf.Log10(_ambienceVolume) * 20f);
            SaveVolumes();
        }

        void LoadVolumes()
        {
            _masterVolume = PlayerPrefs.GetFloat("Volume_Master", 1f);
            _sfxVolume = PlayerPrefs.GetFloat("Volume_SFX", 1f);
            _musicVolume = PlayerPrefs.GetFloat("Volume_Music", 1f);
            _uiVolume = PlayerPrefs.GetFloat("Volume_UI", 1f);
            _ambienceVolume = PlayerPrefs.GetFloat("Volume_Ambience", 1f);

            SetMasterVolume(_masterVolume);
            SetSFXVolume(_sfxVolume);
            SetMusicVolume(_musicVolume);
            SetUIVolume(_uiVolume);
            SetAmbienceVolume(_ambienceVolume);
        }

        void SaveVolumes()
        {
            PlayerPrefs.SetFloat("Volume_Master", _masterVolume);
            PlayerPrefs.SetFloat("Volume_SFX", _sfxVolume);
            PlayerPrefs.SetFloat("Volume_Music", _musicVolume);
            PlayerPrefs.SetFloat("Volume_UI", _uiVolume);
            PlayerPrefs.SetFloat("Volume_Ambience", _ambienceVolume);
            PlayerPrefs.Save();
        }

        // =====================================================================
        // POOLING INTERNO
        // =====================================================================

        AudioSource GetPooledSource()
        {
            if (_sourcePool.Count > 0)
            {
                var source = _sourcePool.Dequeue();
                source.gameObject.SetActive(true);
                return source;
            }

            // Pool esgotado - criar novo (deve ser raro)
            var newSource = gameObject.AddComponent<AudioSource>();
            newSource.playOnAwake = false;
            newSource.spatialBlend = 1f;
            newSource.rolloffMode = AudioRolloffMode.Logarithmic;
            return newSource;
        }

        void ReturnToPoolWhenDone(AudioSource source, float clipLength)
        {
            StartCoroutine(ReturnToPoolCoroutine(source, clipLength + 0.1f));
        }

        System.Collections.IEnumerator ReturnToPoolCoroutine(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);
            source.Stop();
            source.clip = null;
            source.gameObject.SetActive(false);
            _sourcePool.Enqueue(source);
        }

        AudioClip GetRandomClip(string groupName)
        {
            if (_soundGroups.TryGetValue(groupName, out var clips) && clips != null && clips.Length > 0)
            {
                return clips[Random.Range(0, clips.Length)];
            }
            return null;
        }

        void PlayRandom(string groupName, AudioMixerGroup mixerGroup, float volumeMultiplier = 1f, float pitchVar = 0.1f)
        {
            var clip = GetRandomClip(groupName);
            if (clip == null) return;

            var source = GetPooledSource();
            source.clip = clip;
            source.volume = _sfxVolume * _masterVolume * volumeMultiplier * defaultVolume;
            source.pitch = 1f + Random.Range(-pitchVar, pitchVar);
            source.spatialBlend = 0f; // UI sounds são 2D
            if (mixerGroup != null) source.outputAudioMixerGroup = mixerGroup;
            source.Play();
            ReturnToPoolWhenDone(source, clip.length);
        }
    }
}