using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OneMoreMove.EditorTools
{
    /// <summary>
    /// Player settings in code (reproducible, reviewable in git) and batch builds for every target platform.
    /// Batch usage: <c>-executeMethod OneMoreMove.EditorTools.BuildTools.BuildIosSimulator -buildPath Builds/iOS</c>.
    /// Start Unity with a matching <c>-buildTarget</c> (iOS, Android, Win64) to avoid a platform switch.
    /// </summary>
    public static class BuildTools
    {
        public const string BundleId = "com.gvnai.birhamledaha";

        [MenuItem("One More Move/Configure Player Settings")]
        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "gvnai";
            PlayerSettings.productName = "Bir Hamle Daha";
            foreach (var target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.iOS, NamedBuildTarget.Android })
            {
                PlayerSettings.SetApplicationIdentifier(target, BundleId);
            }

            // Phones and tablets play in portrait; ScreenLayout adapts the HUD to any aspect ratio.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Store requirements: 64-bit Android (IL2CPP + ARM64). iOS is always IL2CPP.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Apple silicon simulators (iOS 26+) no longer run x86_64 apps.
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;

            AssetDatabase.SaveAssets();
        }

        public static void BuildMac() => BuildBatch(BuildTarget.StandaloneOSX, "Builds/macOS/BirHamleDaha.app");

        public static void BuildWindows() => BuildBatch(BuildTarget.StandaloneWindows64, "Builds/Windows/BirHamleDaha.exe");

        public static void BuildAndroid() => BuildBatch(BuildTarget.Android, "Builds/Android/BirHamleDaha.apk");

        /// <summary>Xcode project for a device; sign and archive it in Xcode.</summary>
        public static void BuildIos() => BuildBatch(BuildTarget.iOS, "Builds/iOS", iOSSdkVersion.DeviceSDK);

        /// <summary>Xcode project for the iOS Simulator (no signing needed).</summary>
        public static void BuildIosSimulator() => BuildBatch(BuildTarget.iOS, "Builds/iOS-Simulator", iOSSdkVersion.SimulatorSDK);

        private static void BuildBatch(BuildTarget target, string defaultPath, iOSSdkVersion? iosSdk = null)
        {
            var path = ArgumentValue("-buildPath") ?? defaultPath;
            var previousSdk = PlayerSettings.iOS.sdkVersion;
            var exitCode = 1;
            try
            {
                ConfigurePlayerSettings();
                if (iosSdk.HasValue) PlayerSettings.iOS.sdkVersion = iosSdk.Value;

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    target = target,
                    targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                    locationPathName = Path.GetFullPath(path),
                    options = BuildOptions.None
                });

                var summary = report.summary;
                Debug.Log($"[BuildTools] {target}: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors → {summary.outputPath}");
                exitCode = summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                // A simulator build must not leave the project configured for the simulator.
                PlayerSettings.iOS.sdkVersion = previousSdk;
                AssetDatabase.SaveAssets();
            }

            // Exit terminates immediately, so it must come after the settings are restored.
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        private static string ArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
