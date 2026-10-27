using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HierarchyDecorator.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HierarchyDecoratorCI
{
    // Integration checks in Unity's own Hierarchy, independent of the drawer test window.
    public static class NativeActiveToggleProbe
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Dictionary<int, Vector2> Rows = new Dictionary<int, Vector2>();
        private static readonly List<string> Checks = new List<string>();
        private static GameObject[] objects;
        private static EditorWindow hierarchy;
        private static Settings settings;
        private static object view;
        private static double runAt;
        private static int scenario;
        private static int step;

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
            Directory.CreateDirectory("Logs/NativeActiveToggleProbe");
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                settings = (Settings)typeof(Settings).Assembly.GetType("HierarchyDecorator.Editor.HierarchyDecorator", true)
                    .GetProperty("Settings", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                typeof(Settings).GetProperty("NeedsPresetSelection", Instance).SetValue(settings, false);
                settings.globalData.showActiveToggles = true;
                settings.styleData.UpdateStyles(EditorGUIUtility.isProSkin);
                objects = new[] { "A selected", "B selected", "C unselected", "D selected" }
                    .Select(n => new GameObject(n)).ToArray();
                hierarchy = ScriptableObject.CreateInstance(typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow", true)) as EditorWindow;
                hierarchy.ShowUtility();
                hierarchy.position = new Rect(80, 80, 600, 280);
                view = typeof(EditorWindow).GetField("m_Parent", Instance).GetValue(hierarchy);
                HierarchyManager.Initialize();
                EditorApplication.hierarchyWindowItemOnGUI += Track;
                EditorApplication.update += Tick;
                hierarchy.Focus();
                PrepareScenario();
                runAt = EditorApplication.timeSinceStartup + 2;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void PrepareScenario()
        {
            foreach (var go in objects) go.SetActive(true);
            Selection.objects = new UnityEngine.Object[] { objects[0], objects[1], objects[3] };
            settings.globalData.activeSwiping = (scenario & 2) == 0;
            settings.globalData.activeToggleType = (scenario & 4) == 0 ? GlobalData.ToggleType.Checkbox : GlobalData.ToggleType.Dot;
            Undo.ClearAll();
            Undo.IncrementCurrentGroup();
            Rows.Clear();
            hierarchy.Repaint();
            runAt = EditorApplication.timeSinceStartup + 0.3;
        }

        private static void Track(int id, Rect rect)
        {
            if (Event.current.type == EventType.Repaint && typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView", true)
                .GetProperty("current", BindingFlags.Static | BindingFlags.Public).GetValue(null) == view)
                Rows[id] = GUIUtility.GUIToScreenPoint(new Vector2(40, rect.center.y)) - hierarchy.position.position;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < runAt || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (step++ == 0)
                {
                    var sceneHierarchy = hierarchy.GetType().GetProperty("sceneHierarchy", Instance).GetValue(hierarchy);
                    var expand = sceneHierarchy.GetType().GetMethod("SetExpandedRecursive", Instance);
                    foreach (var go in objects) expand.Invoke(sceneHierarchy, new object[] { go.GetInstanceID(), true });
                    hierarchy.Repaint();
                    runAt = EditorApplication.timeSinceStartup + 1;
                    return;
                }
                bool shift = (scenario & 1) != 0;
                EventModifiers modifiers = shift ? EventModifiers.Shift : EventModifiers.None;
                Vector2 point = Rows[objects[0].GetInstanceID()];
                hierarchy.SendEvent(new Event { type = EventType.MouseDown, button = 0, modifiers = modifiers, mousePosition = point });
                hierarchy.SendEvent(new Event { type = EventType.MouseUp, button = 0, modifiers = modifiers, mousePosition = point });
                Require(ExpectedState(shift), "Native Hierarchy click has the correct scope and final state: " + scenario);
                Require(Selection.gameObjects.Length == 3 && Selection.Contains(objects[0]) && Selection.Contains(objects[1]) && Selection.Contains(objects[3]),
                    "Native Hierarchy toggle preserves selection: " + scenario);
                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();
                Undo.PerformUndo();
                Require(objects.All(go => go.activeSelf), "Native Hierarchy click can be undone: " + scenario);
                Undo.PerformRedo();
                Require(ExpectedState(shift), "Native Hierarchy click can be redone: " + scenario);
                if (++scenario == 8) { Finish(null); return; }
                PrepareScenario();
            }
            catch (Exception error) { Finish(error); }
        }

        private static bool ExpectedState(bool shift)
        {
            return !objects[0].activeSelf && objects[1].activeSelf == !shift && objects[2].activeSelf && objects[3].activeSelf == !shift;
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
            Checks.Add(check);
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            EditorApplication.hierarchyWindowItemOnGUI -= Track;
            File.WriteAllText("Logs/NativeActiveToggleProbe/result.json", JsonUtility.ToJson(new Report
            {
                unityVersion = Application.unityVersion, success = error == null,
                error = error?.ToString(), checks = Checks.ToArray()
            }, true));
            if (hierarchy != null) hierarchy.Close();
            if (error != null) Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
