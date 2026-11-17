using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KnotAndSpool.EditorTools
{
    /// Creates the main scene and applies Android player settings the first time the project opens,
    /// and adds a "Knot & Spool" menu with Android build commands.
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ApkPath = "Builds/KnotAndSpool.apk";
        const string AppId = "com.elkuku.knotandspool";

        static ProjectSetup()
        {
            EditorApplication.delayCall += EnsureSetup;
        }

        [MenuItem("Knot & Spool/Apply Project Setup")]
        static void EnsureSetup()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[Knot & Spool] Created {ScenePath}");
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "elkuku";
            PlayerSettings.productName = "Knot & Spool";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        [MenuItem("Knot & Spool/Build Android APK")]
        static void BuildApk() => Build(BuildOptions.None);

        [MenuItem("Knot & Spool/Build and Run on Device")]
        static void BuildAndRun() => Build(BuildOptions.AutoRunPlayer);

        static void Build(BuildOptions options)
        {
            EnsureSetup();
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = options,
            });
            Debug.Log($"[Knot & Spool] Build {report.summary.result}: {ApkPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
        }

        /// For command-line builds: Unity -batchmode -quit -projectPath . -executeMethod KnotAndSpool.EditorTools.ProjectSetup.BuildApkBatch
        public static void BuildApkBatch()
        {
            BuildApk();
        }
    }
}
