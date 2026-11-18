using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Craquez
{
    /// <summary>Layouts de botões do controle (PlayStation / Xbox / genérico).</summary>
    public enum LayoutControle
    {
        /// <summary>Chute no ○ (B), colocado no □ (X), cavadinha no △ (Y).</summary>
        ChuteNoCirculo = 0,
        /// <summary>Chute no □ (X), colocado no ○ (B), cavadinha no △ (Y).</summary>
        ChuteNoQuadrado = 1
    }

    public enum QualidadeGrafica { Baixa = 0, Media = 1, Alta = 2 }

    public enum PapelControle { Atacante = 0, Goleiro = 1 }

    /// <summary>Analógico de toque: flutuante (nasce onde o dedo encosta), fixo (sempre no mesmo canto) ou ancorado (nasce onde o dedo encosta e não sai do lugar).</summary>
    public enum ModoJoystick { Flutuante = 0, Fixo = 1, Ancorado = 2 }

    /// <summary>Configurações do jogador, salvas no aparelho (PlayerPrefs).</summary>
    public static class GameSettings
    {
        const string KLayout = "craquez.layout", KCam = "craquez.camera", KQual = "craquez.qualidade",
                     KVol = "craquez.volume", KPapel = "craquez.papel", KVib = "craquez.vibracao", KInv = "craquez.inverterX",
                     KMapa = "craquez.minimapa", KNomes = "craquez.nomes", KTempo = "craquez.tempo", KDif = "craquez.dificuldade",
                     KCamPartida = "craquez.cameraPartida", KTime = "craquez.timeUsuario",
                     KJoy = "craquez.joystick", KTam = "craquez.tamanhoControles", KCorrerAnalog = "craquez.correrAnalogico";

        public static ModoJoystick Joystick
        {
            get { return (ModoJoystick)Mathf.Clamp(PlayerPrefs.GetInt(KJoy, 0), 0, 2); }
            set { PlayerPrefs.SetInt(KJoy, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>Tamanho dos controles de toque: 0 = pequeno, 1 = médio, 2 = grande.</summary>
        public static int TamanhoControles
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(KTam, 1), 0, 2); }
            set { PlayerPrefs.SetInt(KTam, Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); }
        }
        public static float EscalaControles { get { return TamanhoControles == 0 ? 0.85f : (TamanhoControles == 2 ? 1.17f : 1f); } }

        /// <summary>Arrastar o analógico até a borda externa também corre (sprint).</summary>
        public static bool CorrerNoAnalogico
        {
            get { return PlayerPrefs.GetInt(KCorrerAnalog, 1) == 1; }
            set { PlayerPrefs.SetInt(KCorrerAnalog, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Partida: duração de cada tempo (minutos reais).</summary>
        public static readonly int[] DuracoesTempo = { 2, 3, 5, 8, 12 };

        public static bool MiniMapa
        {
            get { return PlayerPrefs.GetInt(KMapa, 1) == 1; }
            set { PlayerPrefs.SetInt(KMapa, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool NomesJogadores
        {
            get { return PlayerPrefs.GetInt(KNomes, 1) == 1; }
            set { PlayerPrefs.SetInt(KNomes, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Índice em <see cref="DuracoesTempo"/>.</summary>
        public static int DuracaoTempo
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(KTempo, 1), 0, DuracoesTempo.Length - 1); }
            set { PlayerPrefs.SetInt(KTempo, Mathf.Clamp(value, 0, DuracoesTempo.Length - 1)); PlayerPrefs.Save(); }
        }

        /// <summary>0 = Amador, 1 = Profissional, 2 = Lenda (IA adversária).</summary>
        public static int Dificuldade
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(KDif, 1), 0, 2); }
            set { PlayerPrefs.SetInt(KDif, Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); }
        }

        /// <summary>Índice em CameraRig.ModosPartida.</summary>
        public static int CameraPartida
        {
            get { return PlayerPrefs.GetInt(KCamPartida, 0); }
            set { PlayerPrefs.SetInt(KCamPartida, value); PlayerPrefs.Save(); }
        }

        /// <summary>0 = Craquez FC (casa), 1 = Atlético Celeste (visitante).</summary>
        public static int TimeUsuario
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(KTime, 0), 0, 1); }
            set { PlayerPrefs.SetInt(KTime, Mathf.Clamp(value, 0, 1)); PlayerPrefs.Save(); }
        }

        public static LayoutControle Layout
        {
            get { return (LayoutControle)PlayerPrefs.GetInt(KLayout, 0); }
            set { PlayerPrefs.SetInt(KLayout, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>Câmera padrão ao entrar na arena (0 = atrás do atacante rumo ao gol).</summary>
        public static int CameraPadrao
        {
            get { return PlayerPrefs.GetInt(KCam, 0); }
            set { PlayerPrefs.SetInt(KCam, value); PlayerPrefs.Save(); }
        }

        public static QualidadeGrafica Qualidade
        {
            get { return (QualidadeGrafica)PlayerPrefs.GetInt(KQual, Application.isMobilePlatform ? 1 : 2); }
            set { PlayerPrefs.SetInt(KQual, (int)value); PlayerPrefs.Save(); Aplicar(); }
        }

        public static float Volume
        {
            get { return PlayerPrefs.GetFloat(KVol, 1f); }
            set { PlayerPrefs.SetFloat(KVol, Mathf.Clamp01(value)); PlayerPrefs.Save(); AudioListener.volume = Mathf.Clamp01(value); }
        }

        public static PapelControle Papel
        {
            get { return (PapelControle)PlayerPrefs.GetInt(KPapel, 0); }
            set { PlayerPrefs.SetInt(KPapel, (int)value); PlayerPrefs.Save(); }
        }

        public static bool Vibracao
        {
            get { return PlayerPrefs.GetInt(KVib, 1) == 1; }
            set { PlayerPrefs.SetInt(KVib, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        static int baseW, baseH;

        /// <summary>Aplica qualidade gráfica e volume (chamado na inicialização e ao mudar).</summary>
        public static void Aplicar()
        {
            AudioListener.volume = Volume;
            var q = Qualidade;
            Application.targetFrameRate = q == QualidadeGrafica.Baixa ? 30 : 60;
            // qualidade baixa: texturas na metade da resolução (menos memória e banda nos celulares fracos)
            QualitySettings.globalTextureMipmapLimit = q == QualidadeGrafica.Baixa ? 1 : 0;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (Platform.IsTv)
            {
                // TV (Google TV / Android TV 4K): mantém a tela na resolução nativa (menus e placar nítidos em 4K)
                // e desenha o 3D em 720p/1080p/1440p pelo renderScale do URP (leve para o processador da TV)
                int h = Screen.height > 0 ? Screen.height : 1080;
                if (urp != null) urp.renderScale = Mathf.Clamp((float)Platform.TvMaxRenderHeight(q) / h, 0.35f, 1f);
            }
            else if (Application.isMobilePlatform)
            {
                if (baseW == 0) { baseW = Screen.width; baseH = Screen.height; }
                float s = q == QualidadeGrafica.Baixa ? 0.7f : (q == QualidadeGrafica.Media ? 0.85f : 1f);
                if (urp != null) urp.renderScale = 1f;
                Screen.SetResolution(Mathf.RoundToInt(baseW * s), Mathf.RoundToInt(baseH * s), true);
            }
            if (RenderSettings.sun != null)
                RenderSettings.sun.shadows = q == QualidadeGrafica.Baixa ? LightShadows.None : (q == QualidadeGrafica.Media ? LightShadows.Hard : LightShadows.Soft);
        }

        /// <summary>Multiplicador da densidade de chuva/partículas.</summary>
        public static float Particulas
        {
            get { var q = Qualidade; return q == QualidadeGrafica.Baixa ? 0.45f : (q == QualidadeGrafica.Media ? 0.75f : 1f); }
        }

        public static void Vibrar()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Vibracao) return;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var h = new AndroidJavaClass("com.craquez.updater.Haptics"))
                    h.CallStatic("vibrate", activity, 35L);
            }
            catch (System.Exception) { }
#endif
        }
    }
}
