using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Craquez.EditorTools
{
    /// <summary>
    /// Build automático (GitHub Actions / linha de comando, sem abrir a Unity):
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Craquez.EditorTools.CraquezCI.BuildAndroid
    ///         [-customBuildPath Build/Craquez3D.apk] [-androidKeystoreName x.keystore -androidKeystorePass ... -androidKeyaliasName ... -androidKeyaliasPass ...]
    ///   Unity -batchmode -quit -projectPath . -buildTarget StandaloneWindows64 -executeMethod Craquez.EditorTools.CraquezCI.BuildWindows
    ///         [-customBuildPath Build/Windows/Craquez3D.exe]
    /// Configura o projeto (cena, prefabs, URP...), assina o APK com a keystore informada e sai com código 0 (sucesso) ou 1 (falha).
    /// </summary>
    public static class CraquezCI
    {
        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return string.IsNullOrEmpty(args[i + 1]) || args[i + 1].StartsWith("-") ? null : args[i + 1];
            return null;
        }

        public static void BuildAndroid()
        {
            int exitCode = 1;
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

                if (!CraquezSetup.Configure(false)) throw new Exception("o setup do projeto falhou (veja o log acima)");
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(CraquezSetup.IntroVideo) == null)
                    throw new Exception("vídeo de abertura não importado: " + CraquezSetup.IntroVideo);

                string keystore = Arg("-androidKeystoreName");
                if (!string.IsNullOrEmpty(keystore) && File.Exists(keystore))
                {
                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                    PlayerSettings.Android.keystorePass = Arg("-androidKeystorePass") ?? "";
                    PlayerSettings.Android.keyaliasName = Arg("-androidKeyaliasName") ?? "";
                    PlayerSettings.Android.keyaliasPass = Arg("-androidKeyaliasPass") ?? "";
                    Debug.Log("[Craquez 3D] APK assinado com a keystore " + Path.GetFileName(keystore) + " (alias " + PlayerSettings.Android.keyaliasName + ")");
                }
                else
                {
                    PlayerSettings.Android.useCustomKeystore = false;
                    Debug.LogWarning("[Craquez 3D] Sem keystore: APK assinado com chave de depuração. Ele NÃO poderá ser atualizado por versões assinadas com outra chave.");
                }

                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.development = false;

                string path = Arg("-customBuildPath") ?? "Build/Android/Craquez3D.apk";
                if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(path, "Craquez3D.apk");
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { CraquezSetup.Scene },
                    locationPathName = path,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };
                Debug.Log("[Craquez 3D] Gerando APK v" + PlayerSettings.bundleVersion + " (versionCode " + PlayerSettings.Android.bundleVersionCode + ") em " + path);
                BuildReport report = BuildPipeline.BuildPlayer(options);
                var s = report.summary;
                Debug.Log("[Craquez 3D] Resultado: " + s.result + ", " + s.totalErrors + " erro(s), " + (s.totalSize / (1024f * 1024f)).ToString("0.0") + " MB, " + s.totalTime);
                exitCode = s.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[Craquez 3D] Build falhou: " + e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        public static void BuildWindows()
        {
            int exitCode = 1;
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

                if (!CraquezSetup.Configure(false)) throw new Exception("o setup do projeto falhou (veja o log acima)");
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(CraquezSetup.IntroVideo) == null)
                    throw new Exception("vídeo de abertura não importado: " + CraquezSetup.IntroVideo);

                EditorUserBuildSettings.development = false;

                string path = Arg("-customBuildPath") ?? "Build/Windows/Craquez3D.exe";
                if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(path, "Craquez3D.exe");
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { CraquezSetup.Scene },
                    locationPathName = path,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None
                };
                Debug.Log("[Craquez 3D] Gerando build Windows v" + PlayerSettings.bundleVersion + " em " + path);
                BuildReport report = BuildPipeline.BuildPlayer(options);
                var s = report.summary;
                Debug.Log("[Craquez 3D] Resultado: " + s.result + ", " + s.totalErrors + " erro(s), " + (s.totalSize / (1024f * 1024f)).ToString("0.0") + " MB, " + s.totalTime);
                exitCode = s.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[Craquez 3D] Build falhou: " + e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Build Linux de diagnóstico (roda a partida sozinho com -autoteste e mostra os erros no log).</summary>
        public static void BuildLinuxTeste()
        {
            int exitCode = 1;
            try
            {
                if (!CraquezSetup.Configure(false)) throw new Exception("o setup do projeto falhou (veja o log acima)");
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { CraquezSetup.Scene },
                    locationPathName = "Build/Linux/Craquez3D.x86_64",
                    target = BuildTarget.StandaloneLinux64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None
                };
                var r = BuildPipeline.BuildPlayer(options);
                Debug.Log("[Craquez 3D] Build Linux: " + r.summary.result + ", " + r.summary.totalErrors + " erro(s)");
                exitCode = r.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[Craquez 3D] Build Linux falhou: " + e);
            }
            EditorApplication.Exit(exitCode);
        }
    }
}
