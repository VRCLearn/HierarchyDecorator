using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HierarchyDecorator.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HierarchyDecoratorCI
{
    // Run only in a disposable project; fixture scripts belong outside Assets/Editor.
    public static class ComponentToggleProbe
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly List<string> Checks = new List<string>();
        private static readonly List<ComponentItem> Items = new List<ComponentItem>();
        private static readonly List<int> InitialStates = new List<int>();
        private static ComponentToggleProbeWindow window;
        private static Settings settings;
        private static double nextStep;
        private static int nextItem;
        private const string Output = "Logs/ComponentToggleProbe";

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public bool success;
            public string error;
            public string[] checks;
        }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                settings = (Settings)typeof(Settings).Assembly.GetType("HierarchyDecorator.Editor.HierarchyDecorator", true)
                    .GetProperty("Settings", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                typeof(Settings).GetProperty("NeedsPresetSelection", Instance).SetValue(settings, false);
                SetClickToToggle(true);
                var names = new[]
                {
                    "UnityEngine.Transform", "UnityEngine.Rigidbody", "UnityEngine.BoxCollider",
                    "UnityEngine.MeshRenderer", "UnityEngine.AudioSource", "EmptyToggleProbe", "ActiveToggleProbe", "ShadowedEnabledToggleProbe",
                    "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor",
                    "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone",
                    "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider"
                };
                foreach (string name in names)
                {
                    Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
                    if (type == null)
                    {
                        Require(name.StartsWith("VRC.", StringComparison.Ordinal), "Only optional SDK fixtures may be absent: " + name);
                        continue;
                    }
                    var go = new GameObject(name);
                    Component component = type == typeof(Transform) ? go.transform : go.AddComponent(type);
                    int native = EditorUtility.GetObjectEnabled(component);
                    var item = new ComponentItem(component);
                    Require(item.HasToggle == (native != -1), "Toggle eligibility matches Inspector: " + name + " (" + native + ")");
                    if (name == "EmptyToggleProbe" || name.EndsWith("VRCAvatarDescriptor", StringComparison.Ordinal))
                    {
                        Require(native == -1, "No Inspector checkbox despite inherited enabled property: " + name);
                        Require(item.Type.HasToggle && item.Type.ToggleProperty != null,
                            "Type-level enabled metadata remains available without allowing an Inspector toggle: " + name);
                    }
                    Items.Add(item);
                    InitialStates.Add(native);
                }
                CheckUndoAndPrefab();
                window = ScriptableObject.CreateInstance<ComponentToggleProbeWindow>();
                window.Settings = settings;
                window.ShowUtility();
                window.position = new Rect(80, 80, 240, 100);
                EditorApplication.update += Tick;
                nextStep = EditorApplication.timeSinceStartup + 2;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void CheckUndoAndPrefab()
        {
            var collider = new GameObject("Undo collider").AddComponent<BoxCollider>();
            var item = new ComponentItem(collider);
            Undo.ClearAll();
            Undo.IncrementCurrentGroup();
            item.SetActive(false);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Require(!collider.enabled && !item.Active, "Component disables through native API");
            Undo.PerformUndo();
            item.UpdateActiveState();
            Require(collider.enabled && item.Active, "Undo restores enabled state");
            Undo.PerformRedo();
            item.UpdateActiveState();
            Require(!collider.enabled && !item.Active, "Redo restores disabled state");
            collider.enabled = true;
            item.ToggleActive();
            Require(!collider.enabled, "Toggle reads current state after an external Inspector change");

            if (!AssetDatabase.IsValidFolder("Assets/ComponentToggleProbe"))
                AssetDatabase.CreateFolder("Assets", "ComponentToggleProbe");
            var source = new GameObject("Source");
            source.AddComponent<BoxCollider>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, "Assets/ComponentToggleProbe/Collider.prefab");
            Object.DestroyImmediate(source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var instanceCollider = instance.GetComponent<BoxCollider>();
            var prefabItem = new ComponentItem(instanceCollider);
            Undo.ClearAll();
            prefabItem.SetActive(false);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Require(!instanceCollider.enabled && PrefabUtility.GetPropertyModifications(instance)
                .Any(p => p.propertyPath == "m_Enabled" && p.value == "0"), "Prefab enabled override recorded");
            Undo.PerformUndo();
            Require(instanceCollider.enabled, "Prefab toggle can be undone");
            Undo.PerformRedo();
            Require(!instanceCollider.enabled, "Prefab toggle can be redone");
            string scene = "Assets/ComponentToggleProbe/Fixture.unity";
            Type[] fixtureTypes = Items.Select(item => item.Component.GetType()).ToArray();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scene);
            EditorSceneManager.OpenScene(scene);
            var restoredInstance = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .Single(go => PrefabUtility.IsPartOfPrefabInstance(go));
            Require(!restoredInstance.GetComponent<BoxCollider>().enabled,
                "Prefab enabled override survives scene save and reopen");
            // Reacquire fixtures because reopening the scene invalidates their old objects.
            for (int i = 0; i < Items.Count; i++)
            {
                Type type = fixtureTypes[i];
                Items[i] = new ComponentItem(GameObject.Find(type.FullName).GetComponent(type));
            }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextStep) return;
            try
            {
                if (nextItem == Items.Count) { Finish(null); return; }
                var item = Items[nextItem];
                int initial = InitialStates[nextItem++];
                window.Item = item;
                window.InputColor = new Color(0.6f, 0.7f, 0.8f, 0.9f);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(24, 24) });
                if (window.Error != null) throw new InvalidOperationException("IMGUI click probe failed", window.Error);
                Require(EditorUtility.GetObjectEnabled(item.Component) == (initial == -1 ? -1 : 0),
                    "Non-stacked click respects native toggle eligibility: " + item.DisplayName);
                Require(window.ClickConsumed == (initial != -1), "Only toggleable icons consume left click: " + item.DisplayName);
                Require(window.OutputColor == window.InputColor, "Icon drawing restores GUI color: " + item.DisplayName);
                if (initial == -1)
                {
                    string before = EditorJsonUtility.ToJson(item.Component);
                    item.ToggleActive();
                    item.SetActive(false);
                    Require(EditorJsonUtility.ToJson(item.Component) == before, "Unsupported components cannot be toggled programmatically: " + item.DisplayName);
                }
                else
                {
                    SetClickToToggle(false);
                    window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(24, 24) });
                    Require(EditorUtility.GetObjectEnabled(item.Component) == 0 && !window.ClickConsumed,
                        "Click-to-toggle setting remains respected: " + item.DisplayName);
                    SetClickToToggle(true);
                    window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(24, 24) });
                    Require(EditorUtility.GetObjectEnabled(item.Component) == 1, "Second click re-enables component: " + item.DisplayName);
                }
                nextStep = EditorApplication.timeSinceStartup + 0.1;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void SetClickToToggle(bool value)
        {
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("components").FindPropertyRelative("clickToToggleComponent").boolValue = value;
            serialized.FindProperty("components").FindPropertyRelative("stackDuplicateIcons").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
            Checks.Add(check);
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            File.WriteAllText(Path.Combine(Output, "result.json"), JsonUtility.ToJson(new Report
            {
                unityVersion = Application.unityVersion, success = error == null,
                error = error?.ToString(), checks = Checks.ToArray()
            }, true));
            if (window != null) window.Close();
            if (error != null) Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public class ComponentToggleProbeWindow : EditorWindow
    {
        private static readonly MethodInfo Draw = typeof(ComponentIconInfo).GetMethod("DrawComponentIcon", BindingFlags.Instance | BindingFlags.NonPublic);
        private ComponentIconInfo drawer;
        public ComponentItem Item;
        public Settings Settings;
        public Color InputColor;
        public Color OutputColor;
        public bool ClickConsumed;
        public Exception Error;

        private void OnGUI()
        {
            if (Item == null) return;
            Color previous = GUI.color;
            bool click = Event.current.type == EventType.MouseDown;
            try
            {
                if (drawer == null) drawer = new ComponentIconInfo();
                GUI.color = InputColor;
                Draw.Invoke(drawer, new object[] { new Rect(16, 16, 16, 16), Item, Settings });
                if (click)
                {
                    ClickConsumed = Event.current.type == EventType.Used;
                    OutputColor = GUI.color;
                }
            }
            catch (Exception error) { Error = error; }
            finally { GUI.color = previous; }
        }
    }
}
