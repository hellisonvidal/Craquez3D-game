using UnityEngine;

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

    /// <summary>Configurações do jogador, salvas no aparelho (PlayerPrefs).</summary>
    public static class GameSettings
    {
        const string KLayout = "craquez.layout", KCam = "craquez.camera", KQual = "craquez.qualidade",
                     KVol = "craquez.volume", KPapel = "craquez.papel", KVib = "craquez.vibracao", KInv = "craquez.inverterX";

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
            if (Application.isMobilePlatform)
            {
                if (baseW == 0) { baseW = Screen.width; baseH = Screen.height; }
                float s = q == QualidadeGrafica.Baixa ? 0.7f : (q == QualidadeGrafica.Media ? 0.85f : 1f);
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
