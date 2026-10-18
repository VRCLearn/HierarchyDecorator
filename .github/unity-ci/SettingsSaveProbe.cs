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
    // Mutates the settings file. Run only in an explicitly marked disposable project.
    public static class SettingsSaveProbe
    {
        private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const int RapidSaves = 300;
        private const int TotalSaves = 600;
        private static readonly List<string> Checks = new List<string>();
        private static ScriptableObject settings;
        private static PropertyInfo settingsProperty;
        private static MethodInfo saveMethod;
        private static MonoScript script;
        private static Font font;
        private static string settingsPath;
        private static int saveCount;
        private static int updateCount;
        private static int lockFailureErrors;
        private static bool finished;

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public bool passed;
            public int verifiedSaves;
            public int updateCallbacks;
            public int lockFailureErrors;
            public string[] checks;
            public string error;
        }

        public static void Run()
        {
            try
            {
                Require(Application.isBatchMode && File.Exists("ProjectSettings/SettingsSaveProbe.enabled") &&
                    Path.GetFullPath(".").IndexOf(".unity-ci~", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Settings-save tests require an explicitly marked disposable .unity-ci~ batch project.");
                Type type = Type.GetType("VRCLearn.HierarchyDecorator.Settings, VRCLearn.HierarchyDecorator.Editor", true);
                Type owner = type.Assembly.GetType("VRCLearn.HierarchyDecorator.HierarchyDecorator", true);
                settingsProperty = owner.GetProperty("Settings", AnyStatic);
                saveMethod = owner.GetMethod("SaveSettings", AnyStatic);
                settings = (ScriptableObject)settingsProperty.GetValue(null);
                Type paths = type.Assembly.GetType("VRCLearn.HierarchyDecorator.Constants+Paths", true);
                settingsPath = (string)paths.GetField("SETTINGS_FILE_PATH", AnyStatic).GetRawConstantValue();
                Require(Path.GetFullPath(settingsPath).StartsWith(Path.GetFullPath("UserSettings") + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase), "Unexpected settings file outside UserSettings.");
                InitializeReferences();

                for (int i = 0; i < RapidSaves; i++)
                    SaveAndVerify("::rapid-" + i + "::", i);
                Checks.Add("300 consecutive saves persist values, font and script references and clear dirty state");
                EditorApplication.update += ContinueSaves;
            }
            catch (Exception exception)
            {
                Finish(false, exception);
            }
        }

        private static void InitializeReferences()
        {
            script = MonoScript.FromScriptableObject(settings);
#if UNITY_2022_2_OR_NEWER
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            Require(script != null && font != null, "Settings reference fixtures are unavailable.");
            using (var serialized = new SerializedObject(settings))
            {
                serialized.FindProperty("components.customGroups").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            object components = settings.GetType().GetProperty("Components").GetValue(settings);
            components.GetType().GetMethod("AddCustomGroup").Invoke(components, new object[] { "Settings save probe" });
            var groups = (Array)components.GetType().GetProperty("CustomGroups").GetValue(components);
            object entry = groups.GetValue(0).GetType().GetMethod("AddEmpty").Invoke(groups.GetValue(0), new object[] { false });
            entry.GetType().GetMethod("UpdateType", new[] { typeof(MonoScript) }).Invoke(entry, new object[] { script });
        }

        private static void ContinueSaves()
        {
            try
            {
                updateCount++;
                for (int i = 0; i < 20 && saveCount < TotalSaves; i++)
                    SaveAndVerify("::update-" + saveCount + "::", saveCount);
                if (saveCount < TotalSaves)
                    return;

                EditorApplication.update -= ContinueSaves;
                Checks.Add("300 further saves across editor updates persist values and references without dirty-state leaks");
                CheckDirectoryFailure();
                CheckReadOnlyFailure();
                CheckLockedFileFailure();
                Finish(true, null);
            }
            catch (Exception exception)
            {
                Finish(false, exception);
            }
        }

        private static void SetValues(string prefix, int value)
        {
            using (var serialized = new SerializedObject(settings))
            {
                serialized.FindProperty("globalData.showAllComponents").boolValue = value % 2 == 0;
                serialized.FindProperty("globalData.depthMode").intValue = value % 4;
                serialized.FindProperty("styleData.styles.Array.data[0].prefix").stringValue = prefix;
                serialized.FindProperty("styleData.styles.Array.data[0].font").objectReferenceValue = font;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(settings);
        }

        private static bool Save()
        {
            return (bool)saveMethod.Invoke(null, new object[] { settings });
        }

        private static void SaveAndVerify(string prefix, int value)
        {
            SetValues(prefix, value);
            Require(Save(), "Saving failed at " + prefix);
            Require(!EditorUtility.IsDirty(settings), "Successful save retained dirty state at " + prefix);
            Require(ReferenceEquals(settingsProperty.GetValue(null), settings), "Saving replaced the settings singleton.");
            VerifyFile(prefix, value);
            saveCount++;
        }

        private static void VerifyFile(string prefix, int value)
        {
            Object[] loaded = InternalEditorUtility.LoadSerializedFileAndForget(settingsPath);
            try
            {
                Require(loaded != null && loaded.Length == 1 && loaded[0] != null && loaded[0].GetType() == settings.GetType(),
                    "Saved settings have the wrong identity.");
                using (var serialized = new SerializedObject(loaded[0]))
                {
                    Require(serialized.FindProperty("globalData.showAllComponents").boolValue == (value % 2 == 0) &&
                        serialized.FindProperty("globalData.depthMode").intValue == value % 4 &&
                        serialized.FindProperty("styleData.styles.Array.data[0].prefix").stringValue == prefix &&
                        serialized.FindProperty("styleData.styles.Array.data[0].font").objectReferenceValue == font &&
                        serialized.FindProperty("m_Script").objectReferenceValue == script &&
                        serialized.FindProperty("components.customGroups.Array.data[0].components.Array.data[0].script").objectReferenceValue == script,
                        "Saved fields or font/script references differ at " + prefix);
                }
            }
            finally
            {
                if (loaded != null)
                    foreach (Object item in loaded)
                        if (item != null && item != settings)
                            Object.DestroyImmediate(item);
            }
        }

        private static void CheckDirectoryFailure()
        {
            byte[] previous = File.ReadAllBytes(settingsPath);
            string heldFile = settingsPath + ".probe-" + Guid.NewGuid().ToString("N");
            File.Move(settingsPath, heldFile);
            try
            {
                Directory.CreateDirectory(settingsPath);
                SetValues("::directory-failure::", 1);
                Require(!Save(), "A directory target was reported as a successful save.");
                Require(EditorUtility.IsDirty(settings) && Directory.Exists(settingsPath) && !File.Exists(settingsPath) &&
                    File.ReadAllBytes(heldFile).SequenceEqual(previous), "Directory failure changed prior contents or cleared dirty state.");
            }
            finally
            {
                if (Directory.Exists(settingsPath))
                    Directory.Delete(settingsPath);
                File.Move(heldFile, settingsPath);
            }
            SaveAndVerify("::directory-recovered::", 2);
            Checks.Add("directory target returns false, preserves prior contents and dirty state, and saves after recovery");
        }

        private static void CheckReadOnlyFailure()
        {
            byte[] previous = File.ReadAllBytes(settingsPath);
            FileAttributes attributes = File.GetAttributes(settingsPath);
            try
            {
                File.SetAttributes(settingsPath, attributes | FileAttributes.ReadOnly);
                SetValues("::readonly-failure::", 3);
                Require(!Save(), "A read-only target was reported as a successful save.");
                Require(EditorUtility.IsDirty(settings) && File.ReadAllBytes(settingsPath).SequenceEqual(previous) &&
                    (File.GetAttributes(settingsPath) & FileAttributes.ReadOnly) != 0,
                    "Read-only failure changed prior contents, attributes or dirty state.");
            }
            finally
            {
                File.SetAttributes(settingsPath, attributes);
            }
            SaveAndVerify("::readonly-recovered::", 4);
            Checks.Add("read-only target returns false, preserves contents and dirty state, and saves after recovery");
        }

        private static void CheckLockedFileFailure()
        {
            byte[] previous = File.ReadAllBytes(settingsPath);
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    lockFailureErrors++;
            };
            Application.logMessageReceived += capture;
            try
            {
                // One attempt exercises native failure reporting beyond the preflight checks.
                using (new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    SetValues("::locked-file-failure::", 5);
                    Require(!Save(), "Native save failure on a locked target was reported as success.");
                    Require(EditorUtility.IsDirty(settings) && File.ReadAllBytes(settingsPath).SequenceEqual(previous),
                        "Locked-file failure changed prior contents or cleared dirty state.");
                }
                Require(lockFailureErrors > 0, "The locked-file fixture did not exercise native error reporting.");
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }
            SaveAndVerify("::locked-file-recovered::", 6);
            Checks.Add("native locked-file failure returns false and preserves contents and dirty state; releasing the lock restores saving");
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
            EditorApplication.update -= ContinueSaves;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/settings-save-result.json", JsonUtility.ToJson(new Report
            {
                unityVersion = Application.unityVersion,
                passed = passed,
                verifiedSaves = saveCount,
                updateCallbacks = updateCount,
                lockFailureErrors = lockFailureErrors,
                checks = Checks.ToArray(),
                error = error == null ? null : error.ToString()
            }, true));
            if (error != null)
                Debug.LogException(error);
            else
                Debug.Log("HIERARCHY_DECORATOR_SETTINGS_SAVE_OK");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
