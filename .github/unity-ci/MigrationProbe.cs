using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HierarchyDecoratorCI
{
    // This probe deletes fixture installations. Never copy its marker into a real project.
    public static class MigrationProbe
    {
        private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const string Marker = "ProjectSettings/MigrationProbe.enabled";
        private const string SettingsPath = "Assets/MigrationSettings/Selected.asset";
        private const string CurrentSettingsPath = "UserSettings/VRCLearnHierarchyDecoratorSettings.asset";
        private const string UserContent = "This file belongs to the user, not the plugin.\n";
        private static readonly List<string> Checks = new List<string>();
        private static Dictionary<string, byte[]> originalFiles;
        private static Dictionary<string, byte[]> externalFiles;
        private static Configuration configuration;
        private static Type newSettingsType;
        private static Font fixtureFont;
        private static MonoScript fixtureScript;
        private static byte[] previousCurrentSettings;
        private static byte[] selectedSettings;
        private static byte[] selectedSettingsMeta;
        private static byte[] previousVpmManifest;
        private static byte[] previousUnityManifest;
        private static double deadline;
        private static bool finished;

        [Serializable]
        private class Configuration
        {
            public string mode;
            public string upstreamRoot;
            public bool failSave;
            public bool vpm;
            public bool expectPreflightFailure;
            public string externalSource;
        }

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public string mode;
            public bool failSave;
            public bool vpm;
            public bool expectPreflightFailure;
            public bool passed;
            public string[] checks;
            public string error;
        }

        public static void Run()
        {
            try
            {
                Require(Application.isBatchMode && File.Exists(Marker),
                    "Migration tests require a disposable batch project and explicit marker.");
                configuration = JsonUtility.FromJson<Configuration>(File.ReadAllText(Marker));
                Require(new[] { "assets", "local", "embedded" }.Contains(configuration.mode), "Unknown installation fixture.");
                Require(Directory.Exists(configuration.upstreamRoot), "Upstream fixture is absent.");
                Type oldType = Type.GetType("HierarchyDecorator.Settings, Wooshii.HierarchyDecorator.Editor", true);
                newSettingsType = Type.GetType("VRCLearn.HierarchyDecorator.Settings, VRCLearn.HierarchyDecorator.Editor", true);
                CreateSettings(oldType);
                selectedSettings = File.ReadAllBytes(SettingsPath);
                selectedSettingsMeta = File.ReadAllBytes(SettingsPath + ".meta");
                previousUnityManifest = File.ReadAllBytes("Packages/manifest.json");
                if (File.Exists("Packages/vpm-manifest.json"))
                    previousVpmManifest = File.ReadAllBytes("Packages/vpm-manifest.json");
                originalFiles = Directory.GetFiles(configuration.upstreamRoot, "*", SearchOption.AllDirectories)
                    .ToDictionary(path => path, File.ReadAllBytes);
                if (!string.IsNullOrEmpty(configuration.externalSource))
                    externalFiles = Directory.GetFiles(configuration.externalSource, "*", SearchOption.AllDirectories)
                        .ToDictionary(path => path, File.ReadAllBytes);
                Require(originalFiles.Count > 0, "Empty upstream installation fixture.");
                Require(AssetDatabase.LoadAssetAtPath(SettingsPath, oldType) != null, "Settings fixture was not imported.");
                Checks.Add("both plugins load and the selected upstream Settings asset is available");

                if (configuration.failSave)
                {
                    // A directory at the target file path makes saving fail on every OS.
                    Directory.CreateDirectory("UserSettings");
                    if (File.Exists(CurrentSettingsPath))
                        File.Delete(CurrentSettingsPath);
                    Directory.CreateDirectory(CurrentSettingsPath);
                }

                deadline = EditorApplication.timeSinceStartup + 180;
                EditorApplication.update += CheckTimeout;
                EditorApplication.update += WaitForFixture;
            }
            catch (Exception exception)
            {
                if (configuration != null && configuration.expectPreflightFailure && originalFiles != null && previousCurrentSettings != null)
                    CheckPreflightFailure(exception);
                else
                    Finish(false, exception);
            }
        }

        private static void BeginMigration()
        {
            try
            {
                if (configuration.expectPreflightFailure)
                {
                    Type owner = newSettingsType.Assembly.GetType("VRCLearn.HierarchyDecorator.HierarchyDecorator", true);
                    var current = (ScriptableObject)owner.GetProperty("Settings", AnyStatic).GetValue(null);
                    using (var serialized = new SerializedObject(current))
                    {
                        serialized.FindProperty("styleData.styles.Array.data[0].prefix").stringValue = "::before-preflight::";
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    Require((bool)owner.GetMethod("SaveSettings", AnyStatic).Invoke(null, new object[] { current }),
                        "Could not establish the unchanged-current-settings preflight fixture.");
                    previousCurrentSettings = File.ReadAllBytes(CurrentSettingsPath);
                }

                Type migration = newSettingsType.Assembly.GetType("VRCLearn.HierarchyDecorator.UpstreamMigration", true);
                var callback = new Action<Exception>(Completed);
                migration.GetMethod("Start", AnyStatic).Invoke(null, new object[] { SettingsPath, callback });
            }
            catch (Exception exception)
            {
                if (configuration.expectPreflightFailure)
                    CheckPreflightFailure(exception);
                else
                    Finish(false, exception);
            }
        }

        private static void WaitForFixture()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            EditorApplication.update -= WaitForFixture;
            BeginMigration();
        }

        private static void CreateSettings(Type oldType)
        {
            Require(!File.Exists(SettingsPath), "Refusing to overwrite an existing settings fixture.");
            if (!AssetDatabase.IsValidFolder("Assets/MigrationSettings"))
                AssetDatabase.CreateFolder("Assets", "MigrationSettings");
            var source = ScriptableObject.CreateInstance(oldType);
            AssetDatabase.CreateAsset(source, SettingsPath);
            var independent = ScriptableObject.CreateInstance(newSettingsType);
            fixtureScript = MonoScript.FromScriptableObject(independent);
            Object.DestroyImmediate(independent);
#if UNITY_2022_2_OR_NEWER
            fixtureFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            fixtureFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            Require(fixtureFont != null && fixtureScript != null, "Reference fixtures are missing.");
            object components = oldType.GetProperty("Components").GetValue(source);
            components.GetType().GetMethod("AddCustomGroup").Invoke(components, new object[] { "Migration fixture group" });
            var groups = (Array)components.GetType().GetProperty("CustomGroups").GetValue(components);
            object entry = groups.GetValue(0).GetType().GetMethod("AddEmpty").Invoke(groups.GetValue(0), new object[] { false });
            entry.GetType().GetMethod("UpdateType", new[] { typeof(MonoScript) }).Invoke(entry, new object[] { fixtureScript });
            using (var serialized = new SerializedObject(source))
            {
                serialized.FindProperty("globalData.showAllComponents").boolValue = false;
                serialized.FindProperty("globalData.depthMode").intValue = 3;
                serialized.FindProperty("styleData.styles.Array.data[0].prefix").stringValue = "::migration::";
                serialized.FindProperty("styleData.styles.Array.data[0].font").objectReferenceValue = fixtureFont;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();
            if (configuration.mode == "assets")
            {
                Require(File.ReadAllText(Path.Combine(configuration.upstreamRoot, "UserNote.txt")).Replace("\r\n", "\n") == UserContent,
                    "Expected user-owned file in the renamed Assets fixture.");
            }
        }

        private static void Completed(Exception error)
        {
            try
            {
                if (configuration.expectPreflightFailure)
                {
                    CheckPreflightFailure(error);
                    return;
                }
                if (configuration.failSave)
                {
                    Require(error != null, "Expected save failure was reported as migration success.");
                    CheckOriginalInstallation();
                    Require(File.Exists(SettingsPath) && File.Exists(SettingsPath + ".meta"),
                        "Failed import deleted the selected old settings.");
                    Checks.Add("save failure leaves all upstream files and selected old settings untouched");
                    Finish(true, null);
                    return;
                }

                Require(error == null, "Migration failed: " + error);
                Require(File.Exists(CurrentSettingsPath), "New settings were not persisted before cleanup.");
                CheckSavedSettings();
                Require(!File.Exists(SettingsPath) && !File.Exists(SettingsPath + ".meta"),
                    "Selected upstream settings were not removed after successful import.");
                Require(Directory.Exists("Assets/MigrationSettings"), "Cleanup deleted the user's settings directory.");
                Checks.Add("successful migration persists settings and references before deleting selected old settings");
                if (configuration.mode == "assets")
                {
                    string userFile = Path.Combine(configuration.upstreamRoot, "UserNote.txt");
                    Require(File.ReadAllBytes(userFile).SequenceEqual(originalFiles[userFile]),
                        "Cleanup deleted user-owned content from the renamed plugin folder.");
                    Require(!Directory.Exists(Path.Combine(configuration.upstreamRoot, "Scripts")),
                        "Known upstream scripts or empty plugin directories remain.");
                    Require(Directory.GetFiles(configuration.upstreamRoot, "*.cs", SearchOption.AllDirectories).Length == 0 &&
                        Directory.GetFiles(configuration.upstreamRoot, "*.asmdef", SearchOption.AllDirectories).Length == 0,
                        "Upstream source files remain after cleanup.");
                    Checks.Add("renamed Assets installation is removed while user content remains");
                }
                else if (configuration.mode == "local")
                {
                    CheckOriginalInstallation();
                    Require(!File.ReadAllText("Packages/manifest.json").Contains("com.wooshii.hierarchydecorator"),
                        "UPM still contains the upstream dependency.");
                    Checks.Add("UPM dependency is removed and external local package source remains byte-for-byte intact");
                }
                else
                {
                    Require(!Directory.Exists(configuration.upstreamRoot), "Embedded upstream package directory remains.");
                    Require(!File.ReadAllText("Packages/manifest.json").Contains("com.wooshii.hierarchydecorator"),
                        "The removed embedded package can be restored by a remaining UPM reference.");
                    if (externalFiles != null)
                    {
                        foreach (var pair in externalFiles)
                            Require(File.Exists(pair.Key) && pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key)),
                                "Embedded removal changed external dependency source: " + pair.Key);
                    }
                    Checks.Add("project-owned embedded upstream package directory is removed");
                    if (externalFiles != null)
                        Checks.Add("remaining UPM reference is removed without changing its external source");
                }
                if (configuration.vpm)
                {
                    Require(!File.ReadAllText("Packages/vpm-manifest.json").Contains("\"com.wooshii.hierarchydecorator\""),
                        "VPM still contains an upstream locked entry or direct dependency.");
                    Require(!File.ReadAllText("Packages/manifest.json").Contains("com.wooshii.hierarchydecorator"),
                        "UPM still contains an upstream package reference after VPM removal.");
                    Checks.Add("VPM removal updates both locked/direct records and Unity package references");
                }
                Finish(true, null);
            }
            catch (Exception exception)
            {
                Finish(false, exception);
            }
        }

        private static void CheckOriginalInstallation()
        {
            foreach (var pair in originalFiles)
                Require(File.Exists(pair.Key) && pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key)),
                    "Original installation changed: " + pair.Key);
        }

        private static void CheckPreflightFailure(Exception error)
        {
            try
            {
                Require(error != null, "Expected VPM preflight rejection was reported as success.");
                string expectedReason = Directory.Exists("Packages/dev.vrclearn.migration-consumer")
                    ? "required by dev.vrclearn.migration-consumer"
                    : "available VPM Resolver";
                Require(error.ToString().Contains(expectedReason), "Unexpected preflight failure: " + error);
                CheckOriginalInstallation();
                Require(File.ReadAllBytes(SettingsPath).SequenceEqual(selectedSettings) &&
                    File.ReadAllBytes(SettingsPath + ".meta").SequenceEqual(selectedSettingsMeta) &&
                    File.ReadAllBytes(CurrentSettingsPath).SequenceEqual(previousCurrentSettings) &&
                    File.ReadAllBytes("Packages/manifest.json").SequenceEqual(previousUnityManifest) &&
                    (previousVpmManifest == null || File.ReadAllBytes("Packages/vpm-manifest.json").SequenceEqual(previousVpmManifest)),
                    "Preflight failure changed selected old settings, current new settings or package records.");
                Checks.Add("VPM preflight failure preserves old installation and both old and new settings byte-for-byte");
                Finish(true, null);
            }
            catch (Exception exception)
            {
                Finish(false, exception);
            }
        }

        private static void CheckSavedSettings()
        {
            Object[] loaded = InternalEditorUtility.LoadSerializedFileAndForget(CurrentSettingsPath);
            try
            {
                Require(loaded != null && loaded.Length == 1 && loaded[0].GetType() == newSettingsType,
                    "Persisted settings have the wrong identity.");
                using (var serialized = new SerializedObject(loaded[0]))
                {
                    Require(!serialized.FindProperty("globalData.showAllComponents").boolValue &&
                        serialized.FindProperty("globalData.depthMode").intValue == 3 &&
                        serialized.FindProperty("styleData.styles.Array.data[0].prefix").stringValue == "::migration::" &&
                        serialized.FindProperty("styleData.styles.Array.data[0].font").objectReferenceValue == fixtureFont &&
                        serialized.FindProperty("components.customGroups.Array.data[0].components.Array.data[0].script").objectReferenceValue == fixtureScript &&
                        !serialized.FindProperty("needsPresetSelection").boolValue,
                        "Imported setting values, references or setup state were not preserved.");
                }
            }
            finally
            {
                if (loaded != null)
                    foreach (Object item in loaded)
                        if (item != null)
                            Object.DestroyImmediate(item);
            }
        }

        private static void CheckTimeout()
        {
            if (EditorApplication.timeSinceStartup > deadline)
                Finish(false, new TimeoutException("Migration callback did not complete within 180 seconds."));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void Finish(bool passed, Exception error)
        {
            if (finished)
                return;
            finished = true;
            EditorApplication.update -= CheckTimeout;
            EditorApplication.update -= WaitForFixture;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/migration-result.json", JsonUtility.ToJson(new Report
            {
                unityVersion = Application.unityVersion,
                mode = configuration == null ? "unknown" : configuration.mode,
                failSave = configuration != null && configuration.failSave,
                vpm = configuration != null && configuration.vpm,
                expectPreflightFailure = configuration != null && configuration.expectPreflightFailure,
                passed = passed,
                checks = Checks.ToArray(),
                error = error == null ? null : error.ToString()
            }, true));
            if (error != null)
                Debug.LogException(error);
            else
                Debug.Log("HIERARCHY_DECORATOR_MIGRATION_OK");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
