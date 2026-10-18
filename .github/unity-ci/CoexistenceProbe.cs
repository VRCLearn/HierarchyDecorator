using System;
using System.Collections;
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
    // Run only in a disposable project prepared with both packages installed.
    public static class CoexistenceProbe
    {
        private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const string AssetPath = "Assets/HierarchyDecorator/Settings.asset";
        private static readonly List<string> Checks = new List<string>();

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public bool passed;
            public string[] checks;
            public string error;
        }

        public static void Run()
        {
            try
            {
                Require(Application.isBatchMode && File.Exists("ProjectSettings/CoexistenceProbe.enabled"),
                    "The coexistence probe requires a disposable batch project with an explicit marker.");
                Type oldType = Type.GetType("HierarchyDecorator.Settings, Wooshii.HierarchyDecorator.Editor", true);
                Type newType = Type.GetType("VRCLearn.HierarchyDecorator.Settings, VRCLearn.HierarchyDecorator.Editor", true);
                Require(oldType != newType && oldType.Assembly != newType.Assembly, "Settings identities overlap.");
                Checks.Add("independent assemblies and Settings types");

                CheckPaths(oldType.Assembly, newType.Assembly);
                ScriptableObject source = CreateFixture(oldType);
                byte[] originalAsset = File.ReadAllBytes(AssetPath);
                byte[] originalMeta = File.ReadAllBytes(AssetPath + ".meta");
                string originalValues = EditorJsonUtility.ToJson(source);
                Type importer = newType.Assembly.GetType("VRCLearn.HierarchyDecorator.SettingsImport", true);
                MonoScript originalScript = MonoScript.FromScriptableObject(source);
                Font font = ReadReference(source, "styleData.styles.Array.data[0].font") as Font;

                ScriptableObject assetCopy = Import(importer, AssetPath);
                CheckImported(source, assetCopy, newType, originalScript, font);
                Require(originalAsset.SequenceEqual(File.ReadAllBytes(AssetPath)) &&
                    originalMeta.SequenceEqual(File.ReadAllBytes(AssetPath + ".meta")) &&
                    originalValues == EditorJsonUtility.ToJson(source), "Asset import modified its source.");
                Checks.Add("asset import preserves nested values, references, original bytes and script identities");

                CheckImportAndSave(importer, source, newType, originalScript, font, originalAsset, originalMeta);
                CheckCallbacksAndState(oldType.Assembly, newType.Assembly, source, assetCopy);
                Object.DestroyImmediate(assetCopy);
                WriteReport(true, null);
                Debug.Log("HIERARCHY_DECORATOR_COEXISTENCE_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                WriteReport(false, exception.ToString());
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                throw;
            }
        }

        private static ScriptableObject CreateFixture(Type type)
        {
            if (!AssetDatabase.IsValidFolder("Assets/HierarchyDecorator"))
                AssetDatabase.CreateFolder("Assets", "HierarchyDecorator");
            var source = AssetDatabase.LoadAssetAtPath(AssetPath, type) as ScriptableObject;
            if (source == null)
            {
                Require(!File.Exists(AssetPath), "Unexpected settings type at the fixture path.");
                source = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(source, AssetPath);
            }
            using (var serialized = new SerializedObject(source))
            {
                serialized.FindProperty("components.customGroups").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            object components = type.GetProperty("Components").GetValue(source);
            components.GetType().GetMethod("AddCustomGroup").Invoke(components, new object[] { "Coexistence custom group" });
            var groups = (Array)components.GetType().GetProperty("CustomGroups").GetValue(components);
            Require(groups.Length == 1, "Expected one custom fixture group in the disposable project.");
            object entry = groups.GetValue(0).GetType().GetMethod("AddEmpty").Invoke(groups.GetValue(0), new object[] { false });
            entry.GetType().GetMethod("UpdateType", new[] { typeof(MonoScript) })
                .Invoke(entry, new object[] { MonoScript.FromScriptableObject(source) });
#if UNITY_2022_2_OR_NEWER
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            Require(font != null, "Built-in font fixture is unavailable.");
            using (var serialized = new SerializedObject(source))
            {
                serialized.FindProperty("globalData.showAllComponents").boolValue = false;
                serialized.FindProperty("globalData.activeSwiping").boolValue = false;
                serialized.FindProperty("globalData.depthMode").intValue = 3;
                serialized.FindProperty("globalData.tagSettings.colorSettings.solidColor").colorValue = new Color(0.2f, 0.3f, 0.7f, 0.8f);
                serialized.FindProperty("styleData.styles.Array.data[0].prefix").stringValue = "::coexistence::";
                serialized.FindProperty("styleData.styles.Array.data[0].fontSize").intValue = 23;
                serialized.FindProperty("styleData.styles.Array.data[0].font").objectReferenceValue = font;
                serialized.FindProperty("components.enableIcons").boolValue = false;
                serialized.FindProperty("components.customGroups.Array.data[0].components.Array.data[0].shown").boolValue = true;
                serialized.FindProperty("components.customGroups.Array.data[0].components.Array.data[0].excluded").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();
            return source;
        }

        private static ScriptableObject Import(Type importer, string path)
        {
            return (ScriptableObject)importer.GetMethod("Read", AnyStatic).Invoke(null, new object[] { FindCandidate(importer, path) });
        }

        private static string FindCandidate(Type importer, string path)
        {
            var candidates = (IEnumerable)importer.GetMethod("FindCandidates", AnyStatic).Invoke(null, null);
            string selected = null;
            foreach (string candidate in candidates)
            {
                if (Path.GetFullPath(candidate) == Path.GetFullPath(path))
                    selected = candidate;
            }
            Require(selected != null, "Import candidate was not detected: " + path);
            return selected;
        }

        private static void CheckImportAndSave(Type importer, ScriptableObject source, Type newType, MonoScript originalScript,
            Font font, byte[] originalAsset, byte[] originalMeta)
        {
            const string currentPath = "UserSettings/VRCLearnHierarchyDecoratorSettings.asset";
            Type owner = newType.Assembly.GetType("VRCLearn.HierarchyDecorator.HierarchyDecorator", true);
            PropertyInfo currentProperty = owner.GetProperty("Settings", AnyStatic);
            var current = (ScriptableObject)currentProperty.GetValue(null);
            using (var serialized = new SerializedObject(current))
            {
                serialized.FindProperty("needsPresetSelection").boolValue = true;
                serialized.FindProperty("styleData.styles.Array.data[0].prefix").stringValue = "::before-import::";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Require((bool)owner.GetMethod("SaveSettings", AnyStatic).Invoke(null, new object[] { current }),
                "Could not save the pre-import current settings fixture.");
            string sourceValues = EditorJsonUtility.ToJson(source);
            owner.GetMethod("ImportSettings", AnyStatic).Invoke(null, new object[] { FindCandidate(importer, AssetPath) });
            Require((ScriptableObject)currentProperty.GetValue(null) == current, "Import replaced the current Settings instance.");
            CheckImported(source, current, newType, originalScript, font);
            CheckImportState(current);
            Require(File.Exists(currentPath) && Path.GetFullPath(currentPath) != Path.GetFullPath(AssetPath),
                "Imported settings were not saved to their independent local file.");
            Require(!File.Exists(currentPath + ".tmp"), "Saving imported settings leaked its temporary file.");
            Object[] persisted = InternalEditorUtility.LoadSerializedFileAndForget(currentPath);
            try
            {
                Require(persisted != null && persisted.Length == 1 && persisted[0].GetType() == newType,
                    "Saved settings could not be loaded with the new Settings type.");
                CheckImported(source, (ScriptableObject)persisted[0], newType, originalScript, font);
                CheckImportState(persisted[0]);
            }
            finally
            {
                if (persisted != null)
                {
                    foreach (Object loaded in persisted)
                    {
                        if (loaded != null && !EditorUtility.IsPersistent(loaded))
                            Object.DestroyImmediate(loaded);
                    }
                }
            }
            Require(originalAsset.SequenceEqual(File.ReadAllBytes(AssetPath)) &&
                originalMeta.SequenceEqual(File.ReadAllBytes(AssetPath + ".meta")) &&
                sourceValues == EditorJsonUtility.ToJson(source),
                "The complete import/save flow modified original settings.");
            Checks.Add("complete import reuses current instance, clears setup and persists independently");
        }

        private static void CheckImportState(Object settings)
        {
            using (var serialized = new SerializedObject(settings))
            {
                Require(!serialized.FindProperty("needsPresetSelection").boolValue,
                    "Import did not clear the initial preset selection marker.");
            }
        }

        private static void CheckImported(ScriptableObject source, ScriptableObject copy, Type newType, MonoScript oldScript, Font font)
        {
            Require(copy != null && copy.GetType() == newType && copy != source, "Importer did not create independent new settings.");
            using (var actual = new SerializedObject(copy))
            {
                Require(!actual.FindProperty("globalData.showAllComponents").boolValue &&
                    !actual.FindProperty("globalData.activeSwiping").boolValue &&
                    actual.FindProperty("globalData.depthMode").intValue == 3, "Global settings were not preserved.");
                Require(actual.FindProperty("globalData.tagSettings.colorSettings.solidColor").colorValue == new Color(0.2f, 0.3f, 0.7f, 0.8f),
                    "Nested color was not preserved.");
                Require(actual.FindProperty("styleData.styles.Array.data[0].prefix").stringValue == "::coexistence::" &&
                    actual.FindProperty("styleData.styles.Array.data[0].fontSize").intValue == 23 &&
                    actual.FindProperty("styleData.styles.Array.data[0].font").objectReferenceValue == font, "Style or font reference was not preserved.");
                Require(!actual.FindProperty("components.enableIcons").boolValue &&
                    actual.FindProperty("components.customGroups").arraySize == 1 &&
                    actual.FindProperty("components.customGroups.Array.data[0].name").stringValue == "Coexistence custom group" &&
                    actual.FindProperty("components.customGroups.Array.data[0].components").arraySize == 1 &&
                    actual.FindProperty("components.customGroups.Array.data[0].components.Array.data[0].shown").boolValue &&
                    actual.FindProperty("components.customGroups.Array.data[0].components.Array.data[0].excluded").boolValue &&
                    actual.FindProperty("components.customGroups.Array.data[0].components.Array.data[0].script").objectReferenceValue == oldScript,
                    "Custom component group, flags or script reference was not preserved.");
                var newScript = actual.FindProperty("m_Script").objectReferenceValue as MonoScript;
                Require(newScript != null && newScript != oldScript && newScript.GetClass() == newType,
                    "Imported m_Script does not point to the independent new Settings script.");
            }
            Require(MonoScript.FromScriptableObject(source) == oldScript, "Original settings script changed.");
        }

        private static Object ReadReference(Object target, string path)
        {
            using (var serialized = new SerializedObject(target))
                return serialized.FindProperty(path).objectReferenceValue;
        }

        private static void CheckPaths(Assembly oldAssembly, Assembly newAssembly)
        {
            Func<Assembly, HashSet<string>> menus = assembly => new HashSet<string>(assembly.GetTypes()
                .SelectMany(type => type.GetMethods(AnyStatic)).SelectMany(method => method.GetCustomAttributes(typeof(MenuItem), false))
                .Cast<MenuItem>().Select(item => item.menuItem));
            HashSet<string> oldMenus = menus(oldAssembly);
            HashSet<string> newMenus = menus(newAssembly);
            Require(newMenus.Count > 0 && !newMenus.Overlaps(oldMenus) &&
                newMenus.All(path => path.StartsWith("Tools/VRCLearn/", StringComparison.Ordinal)), "Tools menu paths overlap.");
            Func<Assembly, string, string[]> preferences = (assembly, prefix) =>
                ((SettingsProvider[])assembly.GetType(prefix + ".HierarchyDecoratorProvider", true)
                    .GetMethod("Load", AnyStatic).Invoke(null, null)).Select(provider => provider.settingsPath).ToArray();
            string[] oldPaths = preferences(oldAssembly, "HierarchyDecorator");
            string[] newPaths = preferences(newAssembly, "VRCLearn.HierarchyDecorator");
            Require(oldPaths.Length > 0 && newPaths.Length > 0 && !oldPaths.Intersect(newPaths).Any() &&
                newPaths.All(path => path.StartsWith("VRCLearn/", StringComparison.Ordinal)), "Preferences paths overlap.");
            Checks.Add("independent Tools menus and Preferences paths");
        }

        private static void CheckCallbacksAndState(Assembly oldAssembly, Assembly newAssembly, ScriptableObject oldSettings, ScriptableObject newSettings)
        {
            Type oldManager = oldAssembly.GetType("HierarchyDecorator.HierarchyManager", true);
            Type newManager = newAssembly.GetType("VRCLearn.HierarchyDecorator.HierarchyManager", true);
            oldManager.GetMethod("Initialize").Invoke(null, null);
            newManager.GetMethod("Initialize").Invoke(null, null);
            newManager.GetMethod("Initialize").Invoke(null, null);
            foreach (string callback in new[] { "hierarchyWindowItemOnGUI" })
            {
                var handlers = (Delegate)typeof(EditorApplication).GetField(callback, AnyStatic).GetValue(null);
                Require(handlers != null && handlers.GetInvocationList().Count(handler => handler.Method.DeclaringType == oldManager) == 1 &&
                    handlers.GetInvocationList().Count(handler => handler.Method.DeclaringType == newManager) == 1,
                    "Both managers must retain exactly one independent callback: " + callback);
            }
            oldAssembly.GetType("HierarchyDecorator.HierarchyDecorator", true).GetField("s_Settings", AnyStatic).SetValue(null, oldSettings);
            newAssembly.GetType("VRCLearn.HierarchyDecorator.HierarchyDecorator", true).GetField("s_Settings", AnyStatic).SetValue(null, newSettings);
            using (var oldSerialized = new SerializedObject(oldSettings))
            using (var newSerialized = new SerializedObject(newSettings))
            {
                oldSerialized.FindProperty("globalData.showAllComponents").boolValue = false;
                oldSerialized.ApplyModifiedPropertiesWithoutUndo();
                newSerialized.FindProperty("globalData.showAllComponents").boolValue = true;
                newSerialized.ApplyModifiedPropertiesWithoutUndo();
                oldSerialized.Update();
                Require(!oldSerialized.FindProperty("globalData.showAllComponents").boolValue &&
                    newSerialized.FindProperty("globalData.showAllComponents").boolValue, "Old state changed new state.");
                oldSerialized.FindProperty("globalData.showAllComponents").boolValue = true;
                oldSerialized.ApplyModifiedPropertiesWithoutUndo();
                newSerialized.FindProperty("globalData.showAllComponents").boolValue = false;
                newSerialized.ApplyModifiedPropertiesWithoutUndo();
                oldSerialized.Update();
                Require(oldSerialized.FindProperty("globalData.showAllComponents").boolValue &&
                    !newSerialized.FindProperty("globalData.showAllComponents").boolValue, "New state changed old state.");
            }
            Checks.Add("both hierarchy callback sets and independent component visibility");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void WriteReport(bool passed, string error)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/coexistence-result.json", JsonUtility.ToJson(new Report
            {
                unityVersion = Application.unityVersion,
                passed = passed,
                checks = Checks.ToArray(),
                error = error
            }, true));
        }
    }
}
