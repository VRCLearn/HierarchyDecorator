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
        private static string SettingsPath => configuration.sourcePath;
        private static string InitialSettingsPath => Path.GetDirectoryName(SettingsPath).Replace('\\', '/') + "/BeforeMove.asset";
        private const string DefaultSettingsPath = "Assets/HierarchyDecorator/Settings.asset";
        private const string WrongTypePath = "Assets/MigrationSettings/NotSettings.txt";
        private const string OtherSettingsPath = "Assets/MigrationSettings/Unselected.asset";
        private const string CurrentSettingsPath = "UserSettings/Settings.asset";
        private const string OldSettingsScriptGuid = "00668fd727de9bb4081a8a202ce24c3b";
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
        private static byte[] previousUnityManifest;
        private static byte[] previousCurrent;
        private static double deadline;
        private static bool finished;
        private static bool exporting;
        private static string preferenceKey;
        private static string previousPreference;
        private static bool hadPreference;
        private static bool preferenceCaptured;
        private static string activeGuid;
        private static byte[] defaultSettings;
        private static byte[] defaultSettingsMeta;
        private static byte[] installationParentMeta;

        [Serializable]
        private class Configuration
        {
            public string mode;
            public string upstreamRoot;
            public bool failSave;
            public bool binarySettings;
            public bool emptyAssets;
            public string activeGuid;
            public string sourcePath;
            public string externalSource;
            public string regeneratedAsset;
            public string regeneratedGuid;
        }

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public string stage;
            public string mode;
            public bool failSave;
            public bool binarySettings;
            public string activeGuid;
            public string sourcePath;
            public bool passed;
            public string[] checks;
            public string error;
        }

        public static void Run()
        {
            try
            {
                Initialize();
                if (configuration.mode == "orphan")
                {
                    Require(!Directory.Exists(configuration.upstreamRoot) &&
                        Type.GetType("HierarchyDecorator.Settings, Wooshii.HierarchyDecorator.Editor", false) == null &&
                        string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(OldSettingsScriptGuid)),
                        "Orphan migration must run after removal of the actual old package, Settings type and script GUID.");
                    activeGuid = AssetDatabase.AssetPathToGUID(SettingsPath);
                    Require(!string.IsNullOrEmpty(activeGuid), "Exported orphan configuration was not imported.");
                    Checks.Add("the original Settings asset remains after its old package, Settings type and script GUID are removed");
                }
                else
                {
                    Require(Directory.Exists(configuration.upstreamRoot), "Upstream fixture is absent.");
                    Type oldType = Type.GetType("HierarchyDecorator.Settings, Wooshii.HierarchyDecorator.Editor", true);
                    CreateSettings(oldType);
                    Require(AssetDatabase.LoadAssetAtPath(SettingsPath, oldType) != null, "Settings fixture was not imported.");
                    Checks.Add("both plugins load and the selected upstream Settings asset is available");
                }
                selectedSettings = File.ReadAllBytes(SettingsPath);
                selectedSettingsMeta = File.ReadAllBytes(SettingsPath + ".meta");
                otherSettings = File.ReadAllBytes(OtherSettingsPath);
                otherSettingsMeta = File.ReadAllBytes(OtherSettingsPath + ".meta");
                defaultSettings = File.ReadAllBytes(DefaultSettingsPath);
                defaultSettingsMeta = File.ReadAllBytes(DefaultSettingsPath + ".meta");
                previousUnityManifest = File.ReadAllBytes("Packages/manifest.json");
                originalFiles = configuration.mode == "orphan" ? new Dictionary<string, byte[]>() :
                    Directory.GetFiles(configuration.upstreamRoot, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
                if (configuration.emptyAssets)
                    installationParentMeta = File.ReadAllBytes(Path.GetDirectoryName(configuration.upstreamRoot) + ".meta");
                if (!string.IsNullOrEmpty(configuration.externalSource))
                    externalFiles = Directory.GetFiles(configuration.externalSource, "*", SearchOption.AllDirectories)
                        .ToDictionary(path => path, File.ReadAllBytes);
                Require(configuration.mode == "orphan" || originalFiles.Count > 0, "Empty upstream installation fixture.");

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

        public static void ExportOrphan()
        {
            try
            {
                exporting = true;
                Initialize();
                Require(configuration.mode == "orphan", "Orphan export requires --mode orphan.");
                Type oldType = Type.GetType("HierarchyDecorator.Settings, Wooshii.HierarchyDecorator.Editor", true);
                // Keep the requested mode through shutdown: saving dirty fixtures must not revert binary to text.
                EditorSettings.serializationMode = configuration.binarySettings ? SerializationMode.ForceBinary : SerializationMode.ForceText;
                CreateSettings(oldType);
                AssetDatabase.ForceReserializeAssets(new[] { SettingsPath }, ForceReserializeAssetsOptions.ReserializeAssets);
                byte[] exported = File.ReadAllBytes(SettingsPath);
                Require(exported.Length > 0 && (exported[0] != '%') == configuration.binarySettings,
                    "The exported configuration does not use the requested serialization format.");
                Checks.Add("exported the original configuration with Unity-native serialization and retained source metadata and excluded copies");
                Finish(true, null);
            }
            catch (Exception exception)
            {
                Finish(false, exception);
            }
        }

        private static void Initialize()
        {
            Require(Application.isBatchMode && File.Exists(Marker),
                "Migration tests require a disposable batch project and explicit marker.");
            preferenceKey = Application.productName + "_HD_GUID";
            hadPreference = EditorPrefs.HasKey(preferenceKey);
            previousPreference = EditorPrefs.GetString(preferenceKey, string.Empty);
            preferenceCaptured = true;
            configuration = JsonUtility.FromJson<Configuration>(File.ReadAllText(Marker));
            Require(new[] { "assets", "local", "embedded", "settings-only", "empty", "orphan" }.Contains(configuration.mode), "Unknown installation fixture.");
            newSettingsType = Type.GetType("HierarchyDecorator.Editor.Settings, VRCLearn.HierarchyDecorator.Editor", true);
            var independent = ScriptableObject.CreateInstance(newSettingsType);
            fixtureScript = MonoScript.FromScriptableObject(independent);
            Object.DestroyImmediate(independent);
#if UNITY_2022_2_OR_NEWER
            fixtureFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            fixtureFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            Require(fixtureFont != null && fixtureScript != null, "Reference fixtures are missing.");
        }

        private static void BeginMigration()
        {
            try
            {
                Type migration = newSettingsType.Assembly.GetType("HierarchyDecorator.Editor.UpstreamMigration", true);
                bool hasInstallation = configuration.mode != "settings-only" && configuration.mode != "empty" && configuration.mode != "orphan";
                Require((bool)migration.GetProperty("HasInstallation", AnyStatic).GetValue(null) == hasInstallation,
                    "Installation detection does not match the fixture.");
                Checks.Add("installation detection is independent of the selected configuration");
                CheckSingleSourceEntry();
                SelectActiveGuid(configuration.activeGuid);
                CheckReadOnlyImport();
                previousCurrent = File.Exists(CurrentSettingsPath) ? File.ReadAllBytes(CurrentSettingsPath) : null;
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
                if (!configuration.emptyAssets)
                    Require(File.ReadAllText(Path.Combine(configuration.upstreamRoot, "UserNote.txt")).Replace("\r\n", "\n") == UserContent,
                        "Expected user-owned file in the Assets fixture.");
                Require(File.Exists(configuration.regeneratedAsset) &&
                    AssetDatabase.AssetPathToGUID(configuration.regeneratedAsset).Equals(configuration.regeneratedGuid, StringComparison.OrdinalIgnoreCase),
                    "Regenerated-GUID fixture was not imported with its new identity.");
            }
        }

        private static void SelectActiveGuid(string selection)
        {
            if (selection == "missing")
                EditorPrefs.DeleteKey(preferenceKey);
            else
                EditorPrefs.SetString(preferenceKey, selection == "stale" ? "ffffffffffffffffffffffffffffffff" :
                    selection == "wrong-type" ? AssetDatabase.AssetPathToGUID(WrongTypePath) : activeGuid);
        }

        private static void CheckSingleSourceEntry()
        {
            Type importer = newSettingsType.Assembly.GetType("HierarchyDecorator.Editor.SettingsImport", true);
            foreach (string selection in new[] { "missing", "stale", "wrong-type" })
            {
                SelectActiveGuid(selection);
                Require(importer.GetMethod("FindSource", AnyStatic).Invoke(null, null) == null,
                    "Missing, stale or wrong-type active GUID must return no source without selecting a fallback.");
            }
            EditorPrefs.SetString(preferenceKey, activeGuid);
            Require((string)importer.GetMethod("FindSource", AnyStatic).Invoke(null, null) == SettingsPath,
                "The active GUID did not follow the moved and renamed configuration.");
            Checks.Add("missing, stale and wrong-type GUIDs return no source without fallback; valid GUID follows the moved active configuration");
        }

        private static void CheckExcludedSettings()
        {
            Require(File.ReadAllBytes(OtherSettingsPath).SequenceEqual(otherSettings) &&
                File.ReadAllBytes(OtherSettingsPath + ".meta").SequenceEqual(otherSettingsMeta) &&
                File.ReadAllBytes(DefaultSettingsPath).SequenceEqual(defaultSettings) &&
                File.ReadAllBytes(DefaultSettingsPath + ".meta").SequenceEqual(defaultSettingsMeta),
                "Import or cleanup modified excluded configuration copies.");
        }

        private static void CheckReadOnlyImport()
        {
            Type importer = newSettingsType.Assembly.GetType("HierarchyDecorator.Editor.SettingsImport", true);
            var temporaryFiles = new HashSet<string>(Directory.GetFiles(Path.GetTempPath(), "HierarchyDecorator-*.asset"));
            Object copy = null;
            try
            {
                copy = (Object)importer.GetMethod("Read", AnyStatic).Invoke(null, new object[] { SettingsPath });
                CheckImportedSettings(copy);
                CheckSelectedSettings();
            }
            finally
            {
                if (copy != null)
                    Object.DestroyImmediate(copy);
                Require(temporaryFiles.SetEquals(Directory.GetFiles(Path.GetTempPath(), "HierarchyDecorator-*.asset")),
                    "Successful settings read leaked a temporary asset.");
            }
            bool rejected = false;
            try
            {
                importer.GetMethod("Read", AnyStatic).Invoke(null, new object[] { WrongTypePath });
            }
            catch (TargetInvocationException exception)
            {
                Require(exception.InnerException is InvalidDataException, "Wrong-type read reported an unexpected error: " + exception);
                rejected = true;
            }
            Require(rejected && temporaryFiles.SetEquals(Directory.GetFiles(Path.GetTempPath(), "HierarchyDecorator-*.asset")),
                "Failed settings read was accepted or leaked a temporary asset.");
            if (configuration.mode == "orphan")
                CheckUnreadableSource(importer, temporaryFiles);
            CheckSelectedSettings();
            CheckExcludedSettings();
            Checks.Add("read-only import retains selected source bytes and metadata, preserves references, rejects wrong types and leaves no temporary assets");
        }

        private static void CheckUnreadableSource(Type importer, HashSet<string> temporaryFiles)
        {
            string path = Path.Combine(Path.GetTempPath(), "Unreadable-" + Guid.NewGuid().ToString("N") + ".asset");
            try
            {
                File.WriteAllText(path, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &1\nMonoBehaviour:\n" +
                    "  m_Script: {fileID: 11500000, guid: " + OldSettingsScriptGuid + ", type: 3}\n" +
                    "  globalData:\n    depthMode: [\n  styleData:\n  components:\n");
                bool rejected = false;
                try
                {
                    importer.GetMethod("Read", AnyStatic).Invoke(null, new object[] { path });
                }
                catch (TargetInvocationException exception)
                {
                    Require(exception.InnerException is InvalidDataException,
                        "Unreadable settings reported an unexpected error: " + exception);
                    rejected = true;
                }
                Require(rejected, "Malformed settings were imported as defaults.");
            }
            finally
            {
                File.Delete(path);
                Require(temporaryFiles.SetEquals(Directory.GetFiles(Path.GetTempPath(), "HierarchyDecorator-*.asset")),
                    "Native settings load failure leaked a temporary asset.");
            }
            Checks.Add("native load failure rejects malformed data and removes its temporary asset");
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
                if (configuration.activeGuid == "valid")
                {
                    Require(File.Exists(CurrentSettingsPath), "New settings were not persisted before cleanup.");
                    CheckSavedSettings();
                    Checks.Add("successful import persists settings and references before installation cleanup");
                }
                else
                {
                    Require(previousCurrent == null ? !File.Exists(CurrentSettingsPath) :
                        File.ReadAllBytes(CurrentSettingsPath).SequenceEqual(previousCurrent),
                        "Skipping an unavailable configuration modified the new settings.");
                    Checks.Add("unavailable active configuration is skipped without importing excluded copies");
                }
                bool sourceRemoved = configuration.activeGuid == "valid" && SettingsPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    configuration.mode == "embedded" && SettingsPath.StartsWith("Packages/", StringComparison.Ordinal);
                if (sourceRemoved)
                    Require(!File.Exists(SettingsPath) && !File.Exists(SettingsPath + ".meta"),
                        "Selected upstream settings remain after successful cleanup.");
                else
                    CheckSelectedSettings();
                Require(Directory.Exists("Assets/MigrationSettings"), "Cleanup deleted the user's settings directory.");
                if (configuration.mode == "settings-only" || configuration.mode == "empty" || configuration.mode == "orphan")
                {
                    CheckOriginalInstallation();
                    Require(File.ReadAllBytes("Packages/manifest.json").SequenceEqual(previousUnityManifest),
                        "Migration without an old installation changed registered packages.");
                    Checks.Add(configuration.mode == "orphan" ?
                        "configuration imports after removal of its real old scripts, preserves references and excluded copies, then removes only the selected source" :
                        configuration.mode == "settings-only" ?
                        "settings import succeeds without a recognized old installation; unrelated package is preserved" :
                        "no source and no old installation completes as a no-op");
                    Finish(true, null);
                    return;
                }
                if (configuration.mode == "assets")
                {
                    if (configuration.emptyAssets)
                    {
                        string parent = Path.GetDirectoryName(configuration.upstreamRoot);
                        Require(!Directory.Exists(configuration.upstreamRoot) && !File.Exists(configuration.upstreamRoot + ".meta"),
                            "Empty identified installation root remains after cleanup.");
                        Require(Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any() &&
                            File.ReadAllBytes(parent + ".meta").SequenceEqual(installationParentMeta),
                            "Cleanup removed or changed the user's empty parent folder or its metadata.");
                        Checks.Add("empty installation root is removed while its empty user-owned parent and metadata remain unchanged");
                        Finish(true, null);
                        return;
                    }
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
            if (configuration.mode == "orphan")
                Require(!Directory.Exists(configuration.upstreamRoot), "Migration restored the absent old package.");
            foreach (var pair in originalFiles)
                Require(File.Exists(pair.Key) && pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key)),
                    "Original installation changed: " + pair.Key);
        }

        private static void CheckOldSettingsAndPackageRecords()
        {
            CheckSelectedSettings();
            Require(File.ReadAllBytes("Packages/manifest.json").SequenceEqual(previousUnityManifest),
                "The upstream package records changed.");
        }

        private static void CheckSelectedSettings()
        {
            Require(File.ReadAllBytes(SettingsPath).SequenceEqual(selectedSettings) &&
                File.ReadAllBytes(SettingsPath + ".meta").SequenceEqual(selectedSettingsMeta),
                "The selected upstream settings changed.");
        }

        private static void CheckSavedSettings()
        {
            Object[] loaded = InternalEditorUtility.LoadSerializedFileAndForget(CurrentSettingsPath);
            try
            {
                Require(loaded != null && loaded.Length == 1, "Persisted settings have the wrong object count.");
                CheckImportedSettings(loaded[0]);
            }
            finally
            {
                if (loaded != null)
                    foreach (Object item in loaded)
                        if (item != null)
                            Object.DestroyImmediate(item);
            }
        }

        private static void CheckImportedSettings(Object settings)
        {
            Require(settings != null && settings.GetType() == newSettingsType && settings.name == "Settings" &&
                settings.hideFlags == (HideFlags.HideAndDontSave & ~HideFlags.NotEditable),
                "Imported settings have the wrong identity or hide flags.");
            using (var serialized = new SerializedObject(settings))
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
                    stage = exporting ? "export" : "migration",
                    mode = configuration == null ? "unknown" : configuration.mode,
                    failSave = configuration != null && configuration.failSave,
                    binarySettings = configuration != null && configuration.binarySettings,
                    activeGuid = configuration == null ? null : configuration.activeGuid,
                    sourcePath = configuration == null ? null : configuration.sourcePath,
                    passed = passed,
                    checks = Checks.ToArray(),
                    error = error == null ? null : error.ToString()
                }, true));
                if (error != null)
                    Debug.LogException(error);
                else
                    Debug.Log(exporting ? "HIERARCHY_DECORATOR_ORPHAN_EXPORT_OK" : "HIERARCHY_DECORATOR_MIGRATION_OK");
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
