using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Craquez
{
    /// <summary>
    /// Atualização pelo próprio app, a partir das Releases publicadas do GitHub do projeto.
    /// - Verifica a última release publicada (rascunhos e pré-lançamentos são ignorados);
    /// - Se a versão for mais nova que a instalada, a atualização é OBRIGATÓRIA: o jogo fica bloqueado
    ///   na tela de atualização até instalar (não há opção de pular);
    /// - Android: baixa o .apk com barra de progresso e abre o instalador do sistema
    ///   (o Android sempre pede a confirmação final do usuário — é uma proteção do sistema que nenhum app pode pular);
    /// - Windows/Editor: abre a página da release.
    /// Para publicar uma atualização: crie uma Release com tag "vX.Y.Z" e anexe o APK (assinado com a MESMA keystore).
    /// </summary>
    public class AppUpdater : MonoBehaviour
    {
        public const string Repository = "hellisonvidal/Craquez-3D";

        public enum Estado { Ocioso, Verificando, EmDia, Disponivel, Baixando, Pronto, Instalando, Erro, SemConexao }

        public static AppUpdater Instance { get; private set; }

        public Estado State { get; private set; }
        public string LatestVersion { get; private set; }
        public string ReleaseNotes { get; private set; }
        public string ErrorMessage { get; private set; }
        public float Progress { get; private set; }
        public string CurrentVersion { get { return Application.version; } }

        /// <summary>Existe versão nova => o jogo deve bloquear até atualizar.</summary>
        public bool UpdateRequired
        {
            get { return State == Estado.Disponivel || State == Estado.Baixando || State == Estado.Pronto || State == Estado.Instalando || (State == Estado.Erro && downloadUrl != null); }
        }

        string downloadUrl;
        string pageUrl;
        string apkPath;

        [Serializable] class AssetJson { public string name; public string browser_download_url; public long size; }
        [Serializable] class ReleaseJson { public string tag_name; public string name; public string body; public string html_url; public bool draft; public bool prerelease; public AssetJson[] assets; }

        void Awake()
        {
            Instance = this;
            apkPath = Path.Combine(Application.persistentDataPath, "updates", "craquez-update.apk");
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Check()
        {
            if (State == Estado.Verificando || State == Estado.Baixando) return;
            StartCoroutine(CheckRoutine());
        }

        IEnumerator CheckRoutine()
        {
            State = Estado.Verificando;
            ErrorMessage = null;
            string url = "https://api.github.com/repos/" + Repository + "/releases/latest";
            using (var req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.SetRequestHeader("User-Agent", "Craquez3D-Updater");
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    // 404 = ainda não há release publicada
                    if (req.responseCode == 404) { State = Estado.EmDia; yield break; }
                    State = Estado.SemConexao;
                    ErrorMessage = req.error;
                    yield break;
                }
                ReleaseJson rel = null;
                try { rel = JsonUtility.FromJson<ReleaseJson>(req.downloadHandler.text); }
                catch (Exception e) { ErrorMessage = e.Message; }
                if (rel == null || string.IsNullOrEmpty(rel.tag_name)) { State = Estado.Erro; ErrorMessage = ErrorMessage ?? "Resposta inválida"; yield break; }

                LatestVersion = rel.tag_name.TrimStart('v', 'V');
                ReleaseNotes = rel.body;
                pageUrl = rel.html_url;
                if (CompareVersions(LatestVersion, CurrentVersion) <= 0) { State = Estado.EmDia; yield break; }

                downloadUrl = null;
                if (rel.assets != null)
                {
                    foreach (var a in rel.assets)
                    {
                        if (a == null || string.IsNullOrEmpty(a.name)) continue;
                        string n = a.name.ToLowerInvariant();
#if UNITY_ANDROID
                        // a versão de TV se atualiza com o APK de TV (32 e 64 bits); o celular, com o APK de celular
                        if (n.EndsWith(".apk") && Platform.TvBuild == n.Contains("-tv")) { downloadUrl = a.browser_download_url; break; }
#else
                        if (n.EndsWith(".zip") || n.EndsWith(".exe")) { downloadUrl = a.browser_download_url; break; }
#endif
                    }
                }
                // Android e Windows dividem a Release: sem o arquivo desta plataforma (build ainda em andamento
                // ou falhou) não há o que instalar. Antes o Android baixava a página HTML como se fosse o APK.
                if (downloadUrl == null)
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    State = Estado.EmDia;
                    yield break;
#else
                    downloadUrl = pageUrl;
#endif
                }
                State = Estado.Disponivel;
            }
        }

        /// <summary>Baixa e instala (Android) ou abre a página da versão (outras plataformas).</summary>
        public void DownloadAndInstall()
        {
            if (downloadUrl == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (State == Estado.Pronto && File.Exists(apkPath)) { Install(); return; }
            if (State != Estado.Baixando) StartCoroutine(DownloadRoutine());
#else
            Application.OpenURL(downloadUrl.EndsWith(".apk") ? pageUrl : downloadUrl);
#endif
        }

        IEnumerator DownloadRoutine()
        {
            State = Estado.Baixando;
            Progress = 0f;
            Directory.CreateDirectory(Path.GetDirectoryName(apkPath));
            if (File.Exists(apkPath)) File.Delete(apkPath);
            using (var req = new UnityWebRequest(downloadUrl, UnityWebRequest.kHttpVerbGET))
            {
                req.downloadHandler = new DownloadHandlerFile(apkPath) { removeFileOnAbort = true };
                req.SetRequestHeader("User-Agent", "Craquez3D-Updater");
                var op = req.SendWebRequest();
                while (!op.isDone) { Progress = req.downloadProgress; yield return null; }
                if (req.result != UnityWebRequest.Result.Success)
                {
                    State = Estado.Erro;
                    ErrorMessage = req.error;
                    yield break;
                }
            }
            Progress = 1f;
            State = Estado.Pronto;
            Install();
        }

        void Install()
        {
            State = Estado.Instalando;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var installer = new AndroidJavaClass("com.craquez.updater.ApkInstaller"))
                {
                    int r = installer.CallStatic<int>("install", activity, apkPath);
                    if (r == 2)
                    {
                        // o usuário precisa permitir "instalar apps desta fonte" (exigência do Android 8+);
                        // ao voltar ao jogo, o botão ATUALIZAR instala direto
                        State = Estado.Pronto;
                        ErrorMessage = "Permita a instalação de apps do Craquez 3D e toque em ATUALIZAR novamente.";
                    }
                    else if (r == 0) { State = Estado.Erro; ErrorMessage = "Não foi possível abrir o instalador."; }
                }
            }
            catch (Exception e)
            {
                State = Estado.Erro;
                ErrorMessage = e.Message;
            }
#endif
        }

        void OnApplicationPause(bool paused)
        {
            // ao voltar das configurações de permissão, reabre o instalador automaticamente
            if (!paused && State == Estado.Pronto && File.Exists(apkPath)) Install();
        }

        /// <summary>Compara versões "1.2.3" (componentes numéricos). Retorna &gt;0 se a &gt; b.</summary>
        public static int CompareVersions(string a, string b)
        {
            var pa = (a ?? "0").Split('.', '-', '+');
            var pb = (b ?? "0").Split('.', '-', '+');
            int n = Mathf.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int x = 0, y = 0;
                if (i < pa.Length) int.TryParse(pa[i], out x);
                if (i < pb.Length) int.TryParse(pb[i], out y);
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }
    }
}
