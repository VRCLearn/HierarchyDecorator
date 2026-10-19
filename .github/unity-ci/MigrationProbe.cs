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
        private const string SettingsPath = "Assets/MigrationSettings/CurrentRenamed.asset";
        private const string InitialSettingsPath = "Assets/MigrationSettings/BeforeMove.asset";
        private const string DefaultSettingsPath = "Assets/HierarchyDecorator/Settings.asset";
        private const string WrongTypePath = "Assets/MigrationSettings/NotSettings.txt";
        private const string OtherSettingsPath = "Assets/MigrationSettings/Unselected.asset";
        private const string CurrentSettingsPath = "UserSettings/HierarchyDecoratorSettings.asset";
        private const string UserContent = "This file belongs to the user, not the plugin.\n";
        private static readonly List<string> Checks = new List<string>();
        private static Dictionary<string, byte[]> originalFiles;
        private static Dictionary<string, byte[]> externalFiles;
        private static Configuration configuration;
        private static Type newSettingsType;
        private static Font fixtureFont;
        private static MonoScript fixtureScript;
        private static byte[] selectedSettings;
        private static byte[] selectedSettingsMeta;
        private static byte[] otherSettings;
        private static byte[] otherSettingsMeta;
        private static byte[] previousVpmManifest;
        private static byte[] previousUnityManifest;
        private static double deadline;
        private static bool finished;
        private static string preferenceKey;
        private static string previousPreference;
        private static bool hadPreference;
        private static bool preferenceCaptured;
        private static string activeGuid;
        private static byte[] defaultSettings;
        private static byte[] defaultSettingsMeta;

        [Serializable]
        private class Configuration
        {
            public string mode;
            public string upstreamRoot;
            public bool failSave;
            public bool vpm;
            public string externalSource;
            public string regeneratedAsset;
            public string regeneratedGuid;
        }

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public string mode;
            public bool failSave;
            public bool vpm;
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
                preferenceKey = Application.productName + "_HD_GUID";
                hadPreference = EditorPrefs.HasKey(preferenceKey);
                previousPreference = EditorPrefs.GetString(preferenceKey, string.Empty);
                preferenceCaptured = true;
                configuration = JsonUtility.FromJson<Configuration>(File.ReadAllText(Marker));
                Require(new[] { "assets", "local", "embedded" }.Contains(configuration.mode), "Unknown installation fixture.");
                Require(Directory.Exists(configuration.upstreamRoot), "Upstream fixture is absent.");
                Type oldType = Type.GetType("HierarchyDecorator.Settings, Wooshii.HierarchyDecorator.Editor", true);
                newSettingsType = Type.GetType("HierarchyDecorator.Editor.Settings, VRCLearn.HierarchyDecorator.Editor", true);
                CreateSettings(oldType);
                selectedSettings = File.ReadAllBytes(SettingsPath);
                selectedSettingsMeta = File.ReadAllBytes(SettingsPath + ".meta");
                otherSettings = File.ReadAllBytes(OtherSettingsPath);
                otherSettingsMeta = File.ReadAllBytes(OtherSettingsPath + ".meta");
                defaultSettings = File.ReadAllBytes(DefaultSettingsPath);
                defaultSettingsMeta = File.ReadAllBytes(DefaultSettingsPath + ".meta");
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
                Finish(false, exception);
            }
        }

        private static void BeginMigration()
        {
            try
            {
                Type migration = newSettingsType.Assembly.GetType("HierarchyDecorator.Editor.UpstreamMigration", true);
                Require((bool)migration.GetProperty("IsVpmManaged", AnyStatic).GetValue(null) == configuration.vpm,
                    "VPM ownership was not detected correctly.");
                CheckSingleSourceEntry(migration);
                var callback = new Action<Exception>(Completed);
                migration.GetMethod("Start", AnyStatic).Invoke(null, new object[] { callback });
            }
            catch (Exception exception)
            {
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
            Require(!File.Exists(SettingsPath) && !File.Exists(InitialSettingsPath), "Refusing to overwrite an existing settings fixture.");
            if (!AssetDatabase.IsValidFolder("Assets/MigrationSettings"))
                AssetDatabase.CreateFolder("Assets", "MigrationSettings");
            var source = ScriptableObject.CreateInstance(oldType);
            AssetDatabase.CreateAsset(source, InitialSettingsPath);
            activeGuid = AssetDatabase.AssetPathToGUID(InitialSettingsPath);
            EditorPrefs.SetString(preferenceKey, activeGuid);
            Require(string.IsNullOrEmpty(AssetDatabase.MoveAsset(InitialSettingsPath, SettingsPath)),
                "Could not move and rename the active configuration fixture.");
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
            Require(!File.Exists(OtherSettingsPath) && AssetDatabase.CopyAsset(SettingsPath, OtherSettingsPath),
                "Could not create independent unselected-settings fixture.");
            if (!AssetDatabase.IsValidFolder("Assets/HierarchyDecorator"))
                AssetDatabase.CreateFolder("Assets", "HierarchyDecorator");
            if (!File.Exists(DefaultSettingsPath))
                Require(AssetDatabase.CopyAsset(SettingsPath, DefaultSettingsPath), "Could not create the excluded default-path configuration.");
            foreach (string path in new[] { OtherSettingsPath, DefaultSettingsPath })
            {
                var excluded = AssetDatabase.LoadAssetAtPath(path, oldType);
                Require(excluded != null, "Excluded settings fixture has an unexpected type.");
                using (var serialized = new SerializedObject(excluded))
                {
                    serialized.FindProperty("styleData.styles.Array.data[0].prefix").stringValue = "::excluded::";
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(excluded);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(WrongTypePath, "Not a Settings asset.");
            AssetDatabase.ImportAsset(WrongTypePath);
            EditorPrefs.SetString(preferenceKey, activeGuid);
            if (configuration.mode == "assets")
            {
                Require(File.ReadAllText(Path.Combine(configuration.upstreamRoot, "UserNote.txt")).Replace("\r\n", "\n") == UserContent,
                    "Expected user-owned file in the Assets fixture.");
                Require(File.Exists(configuration.regeneratedAsset) &&
                    AssetDatabase.AssetPathToGUID(configuration.regeneratedAsset).Equals(configuration.regeneratedGuid, StringComparison.OrdinalIgnoreCase),
                    "Regenerated-GUID fixture was not imported with its new identity.");
            }
        }

        private static void CheckSingleSourceEntry(Type migration)
        {
            byte[] previousCurrent = File.Exists(CurrentSettingsPath) ? File.ReadAllBytes(CurrentSettingsPath) : null;
            foreach (string invalidGuid in new[] { null, "ffffffffffffffffffffffffffffffff", AssetDatabase.AssetPathToGUID(WrongTypePath) })
            {
                if (invalidGuid == null)
                    EditorPrefs.DeleteKey(preferenceKey);
                else
                    EditorPrefs.SetString(preferenceKey, invalidGuid);
                Exception error = null;
                try
                {
                    migration.GetMethod("Start", AnyStatic).Invoke(null, new object[] { new Action<Exception>(exception => error = exception) });
                }
                catch (TargetInvocationException exception)
                {
                    error = exception.InnerException;
                }
                Require(error is InvalidDataException,
                    "The real migration entry must reject a missing, stale or wrong-type active GUID without selecting a fallback: " + error);
                CheckOriginalInstallation();
                CheckOldSettingsAndPackageRecords();
                CheckExcludedSettings();
                Require(previousCurrent == null ? !File.Exists(CurrentSettingsPath) :
                    File.ReadAllBytes(CurrentSettingsPath).SequenceEqual(previousCurrent),
                    "Rejected migration changed the new configuration.");
            }
            EditorPrefs.SetString(preferenceKey, activeGuid);
            Type importer = newSettingsType.Assembly.GetType("HierarchyDecorator.Editor.SettingsImport", true);
            Require((string)importer.GetMethod("FindSource", AnyStatic).Invoke(null, null) == SettingsPath,
                "The active GUID did not follow the moved and renamed configuration.");
            Checks.Add("real migration entry rejects missing, stale and wrong-type GUIDs without fallback; valid GUID follows the moved active configuration");
        }

        private static void CheckExcludedSettings()
        {
            Require(File.ReadAllBytes(OtherSettingsPath).SequenceEqual(otherSettings) &&
                File.ReadAllBytes(OtherSettingsPath + ".meta").SequenceEqual(otherSettingsMeta) &&
                File.ReadAllBytes(DefaultSettingsPath).SequenceEqual(defaultSettings) &&
                File.ReadAllBytes(DefaultSettingsPath + ".meta").SequenceEqual(defaultSettingsMeta),
                "Import or cleanup modified excluded configuration copies.");
        }

        private static void Completed(Exception error)
        {
            try
            {
                CheckExcludedSettings();
                if (configuration.failSave)
                {
                    Require(error != null, "Expected save failure was reported as migration success.");
                    CheckOriginalInstallation();
                    CheckOldSettingsAndPackageRecords();
                    Checks.Add("save failure leaves all upstream files and selected old settings untouched");
                    Finish(true, null);
                    return;
                }

                Require(error == null, "Migration failed: " + error);
                Require(File.Exists(CurrentSettingsPath), "New settings were not persisted before cleanup.");
                CheckSavedSettings();
                if (configuration.vpm)
                {
                    CheckOriginalInstallation();
                    CheckOldSettingsAndPackageRecords();
                    Checks.Add("VPM import preserves the old package, both manifests and selected old settings byte-for-byte");
                    Finish(true, null);
                    return;
                }
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
                    Require(!string.IsNullOrEmpty(configuration.regeneratedAsset) &&
                        !File.Exists(configuration.regeneratedAsset) && !File.Exists(configuration.regeneratedAsset + ".meta"),
                        "Cleanup left the distributed asset with a regenerated GUID behind.");
                    Checks.Add("Assets distribution files, including the regenerated-GUID file, are removed while user content remains");
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

        private static void CheckOldSettingsAndPackageRecords()
        {
            Require(File.ReadAllBytes(SettingsPath).SequenceEqual(selectedSettings) &&
                File.ReadAllBytes(SettingsPath + ".meta").SequenceEqual(selectedSettingsMeta) &&
                File.ReadAllBytes("Packages/manifest.json").SequenceEqual(previousUnityManifest) &&
                (previousVpmManifest == null || File.ReadAllBytes("Packages/vpm-manifest.json").SequenceEqual(previousVpmManifest)),
                "The selected upstream settings or package records changed.");
        }

        private static void CheckSavedSettings()
        {
            Object[] loaded = InternalEditorUtility.LoadSerializedFileAndForget(CurrentSettingsPath);
            try
            {
                Require(loaded != null && loaded.Length == 1 && loaded[0].GetType() == newSettingsType &&
                    loaded[0].name == "HierarchyDecorator Settings",
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
            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/migration-result.json", JsonUtility.ToJson(new Report
                {
                    unityVersion = Application.unityVersion,
                    mode = configuration == null ? "unknown" : configuration.mode,
                    failSave = configuration != null && configuration.failSave,
                    vpm = configuration != null && configuration.vpm,
                    passed = passed,
                    checks = Checks.ToArray(),
                    error = error == null ? null : error.ToString()
                }, true));
                if (error != null)
                    Debug.LogException(error);
                else
                    Debug.Log("HIERARCHY_DECORATOR_MIGRATION_OK");
            }
            finally
            {
                if (preferenceCaptured)
                {
                    if (hadPreference)
                        EditorPrefs.SetString(preferenceKey, previousPreference);
                    else
                        EditorPrefs.DeleteKey(preferenceKey);
                }
            }
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
