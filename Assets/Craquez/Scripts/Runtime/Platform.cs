using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Identifica se o jogo está rodando numa TV (Android TV / Google TV 4K ou APK da versão TV).
    /// Na TV não há tela de toque: os menus e a partida funcionam pelo controle remoto (setas + OK + Voltar)
    /// ou por um controle Bluetooth/USB, e o 3D é desenhado em resolução escalada (720p/1080p/1440p) com
    /// menus e placar nítidos na resolução nativa do painel.
    /// </summary>
    public static class Platform
    {
        static int _tv = -1;
        static string _flavor;

        /// <summary>
        /// Versão do pacote: "" (celular/PC) ou "tv" (APK Craquez 3D TV, 32 e 64 bits, pacote próprio).
        /// Vem de Resources/flavor.txt, gravado pelo workflow na build da TV.
        /// </summary>
        public static string Flavor
        {
            get
            {
                if (_flavor == null)
                {
                    var t = Resources.Load<TextAsset>("flavor");
                    _flavor = t != null ? t.text.Trim().ToLowerInvariant() : "";
                }
                return _flavor;
            }
        }

        public static bool TvBuild { get { return Flavor == "tv"; } }

        /// <summary>Android TV / Google TV (detectado pelo UiModeManager do Android ou pelo APK de TV).</summary>
        public static bool IsTv
        {
            get
            {
                if (TvBuild) return true;
                if (_tv < 0)
                {
                    _tv = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
                    try
                    {
                        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var act = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        using (var ui = act.Call<AndroidJavaObject>("getSystemService", "uimode"))
                            if (ui != null && ui.Call<int>("getCurrentModeType") == 4) _tv = 1; // UI_MODE_TYPE_TELEVISION
                    }
                    catch (System.Exception e) { Debug.LogWarning("[Craquez 3D] Plataforma TV: " + e.Message); }
#endif
                }
                return _tv == 1;
            }
        }

        /// <summary>Altura máxima do 3D na TV (o HUD e os menus continuam na resolução nativa da TV).</summary>
        public static int TvMaxRenderHeight(QualidadeGrafica q)
        {
            return q == QualidadeGrafica.Alta ? 1440 : (q == QualidadeGrafica.Media ? 1080 : 720);
        }
    }
}
