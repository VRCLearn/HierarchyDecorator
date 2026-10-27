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
    // Run in a disposable graphical project. Events go through the production drawer.
    public static class SelectionToggleProbe
    {
        private static readonly List<string> Checks = new List<string>();
        private static readonly List<string> Observations = new List<string>();
        private static SelectionToggleProbeWindow window;
        private static Settings settings;
        private static GameObject[] objects;
        private static bool[] initialStates;
        private static int scenario;
        private static double nextStep;
        private static string output;

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public bool success;
            public string error;
            public string[] observations;
            public string[] checks;
        }

        public static void Run()
        {
            output = Path.GetFullPath("Logs/SelectionToggleProbe");
            Directory.CreateDirectory(output);
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                settings = (Settings)typeof(Settings).Assembly.GetType("HierarchyDecorator.Editor.HierarchyDecorator", true)
                    .GetProperty("Settings", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                typeof(Settings).GetProperty("NeedsPresetSelection", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(settings, false);
                settings.globalData.showActiveToggles = true;
                objects = new[] { "Selected A", "Selected B", "Unselected C", "Selected offscreen D" }
                    .Select(name => new GameObject(name)).ToArray();
                window = ScriptableObject.CreateInstance<SelectionToggleProbeWindow>();
                window.Settings = settings;
                window.Items = objects.Take(3).Select(go => new HierarchyItem(go.GetInstanceID(), go)).ToArray();
                window.ShowUtility();
                window.position = new Rect(80, 80, 300, 150);
                PrepareScenario();
                EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void PrepareScenario()
        {
            bool active = (scenario & 1) == 0;
            foreach (var go in objects) go.SetActive(active);
            if ((scenario & 16) != 0) objects[1].SetActive(!active);
            initialStates = objects.Select(go => go.activeSelf).ToArray();
            Selection.objects = new Object[] { objects[0], objects[1], objects[3] };
            settings.globalData.activeSwiping = (scenario & 2) == 0;
            settings.globalData.activeToggleType = (scenario & 4) == 0 ? GlobalData.ToggleType.Checkbox : GlobalData.ToggleType.Dot;
            window.Drawer = new ToggleDrawer();
            window.Repaint();
            Undo.ClearAll();
            Undo.IncrementCurrentGroup();
            nextStep = EditorApplication.timeSinceStartup + 0.2;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextStep) return;
            try
            {
                bool initial = (scenario & 1) == 0;
                bool keyOverToggle = (scenario & 8) != 0;
                Vector2 keyPosition = keyOverToggle ? new Vector2(40, 24) : new Vector2(-10, -10);
                KeyCode shift = keyOverToggle ? KeyCode.RightShift : KeyCode.LeftShift;
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = shift, modifiers = EventModifiers.Shift, mousePosition = keyPosition });
                Require(objects.Select(go => go.activeSelf).SequenceEqual(initialStates), "Holding Shift without clicking leaves all objects unchanged: " + scenario);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, keyCode = KeyCode.None,
                    modifiers = EventModifiers.Shift, mousePosition = new Vector2(40, 24) });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0,
                    modifiers = EventModifiers.Shift, mousePosition = new Vector2(40, 24) });
                window.SendEvent(new Event { type = EventType.KeyUp, keyCode = shift, mousePosition = keyPosition });
                if (window.Error != null) throw new InvalidOperationException("Production toggle drawer failed", window.Error);
                Observations.Add("scenario=" + scenario + ", swipe=" + settings.globalData.activeSwiping
                    + ", style=" + settings.globalData.activeToggleType + ", initiallyActive=" + initial
                    + ", states[A,B,C,D]=" + string.Join(",", objects.Select(go => go.activeSelf)));
                Require(objects[0].activeSelf == !initial && objects[1].activeSelf == !initial
                    && objects[2].activeSelf == initialStates[2] && objects[3].activeSelf == !initial,
                    "Shift-click changes all and only selected objects, including offscreen selection: " + scenario);
                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();
                Undo.PerformUndo();
                Require(objects.Select(go => go.activeSelf).SequenceEqual(initialStates), "One Undo restores the whole selection: " + scenario);
                Undo.PerformRedo();
                Require(objects[0].activeSelf == !initial && objects[1].activeSelf == !initial && objects[3].activeSelf == !initial,
                    "One Redo reapplies the whole selection: " + scenario);
                if (++scenario == 32)
                {
                    CheckSelectionBoundaries();
                    CheckPrefabPersistence();
                    Finish(null);
                    return;
                }
                PrepareScenario();
            }
            catch (Exception error) { Finish(error); }
        }

        private static void SendMouse(EventType type, int row, bool shift = false, int button = 0)
        {
            window.SendEvent(new Event { type = type, button = button, keyCode = KeyCode.None,
                modifiers = shift ? EventModifiers.Shift : EventModifiers.None,
                mousePosition = new Vector2(40, 24 + row * 24) });
            if (window.Error != null) throw new InvalidOperationException("Production toggle drawer failed", window.Error);
        }

        private static void ResetObjects()
        {
            foreach (var go in objects) go.SetActive(true);
            Selection.objects = new Object[] { objects[0], objects[1], objects[3] };
            settings.globalData.activeSwiping = true;
            settings.globalData.activeToggleType = GlobalData.ToggleType.Checkbox;
            window.Drawer = new ToggleDrawer();
        }

        private static void Click(int row, bool shift = false)
        {
            SendMouse(EventType.MouseDown, row, shift);
            SendMouse(EventType.MouseUp, row, shift);
        }

        private static void CheckSelectionBoundaries()
        {
            ResetObjects();
            Click(2, true);
            Require(objects[0].activeSelf && objects[1].activeSelf && !objects[2].activeSelf && objects[3].activeSelf,
                "Shift-click outside selection only toggles the clicked object");
            foreach (bool selected in new[] { false, true })
            {
                ResetObjects();
                Selection.objects = selected ? new Object[] { objects[0] } : Array.Empty<Object>();
                Click(0, true);
                Require(!objects[0].activeSelf && objects.Skip(1).All(go => go.activeSelf),
                    "Shift-click works with zero or one selected object: " + selected);
            }
            ResetObjects();
            SendMouse(EventType.MouseDown, 0, true, 1);
            SendMouse(EventType.MouseUp, 0, true, 1);
            Require(objects.All(go => go.activeSelf), "Shift plus right mouse does not toggle objects");

            ResetObjects();
            SendMouse(EventType.MouseDown, 0, true);
            window.SendEvent(new Event { type = EventType.KeyUp, keyCode = KeyCode.LeftShift, mousePosition = new Vector2(40, 24) });
            SendMouse(EventType.MouseDrag, 2);
            SendMouse(EventType.MouseUp, 2);
            Require(!objects[0].activeSelf && !objects[1].activeSelf && objects[2].activeSelf && !objects[3].activeSelf,
                "Releasing Shift during a click gesture does not start swiping unselected objects");
            Click(1);
            Require(!objects[0].activeSelf && objects[1].activeSelf && objects[2].activeSelf && !objects[3].activeSelf,
                "A subsequent ordinary click on a selected object is not stuck in Shift mode");

            ResetObjects();
            SendMouse(EventType.MouseDown, 0, true);
            window.SendEvent(new Event { type = EventType.MouseLeaveWindow });
            // The mouse can be released outside this window without delivering MouseUp.
            Click(1);
            Require(!objects[0].activeSelf && objects[1].activeSelf && objects[2].activeSelf && !objects[3].activeSelf,
                "A fresh click starts a new gesture after MouseUp was missed outside the window");

            ResetObjects();
            Click(0);
            Require(!objects[0].activeSelf && objects.Skip(1).All(go => go.activeSelf),
                "Ordinary click with active swiping still toggles only one object");
            ResetObjects();
            SendMouse(EventType.MouseDown, 0);
            SendMouse(EventType.MouseDrag, 1);
            SendMouse(EventType.MouseUp, 1);
            Require(!objects[0].activeSelf && !objects[1].activeSelf && objects[2].activeSelf && objects[3].activeSelf,
                "Ordinary selected-object swiping remains available");
            ResetObjects();
            SendMouse(EventType.MouseDown, 0);
            SendMouse(EventType.MouseDrag, 2);
            SendMouse(EventType.MouseUp, 2);
            Require(!objects[0].activeSelf && objects[1].activeSelf && objects[2].activeSelf && objects[3].activeSelf,
                "Ordinary swiping continues to respect selection-only filtering");
        }

        private static void CheckPrefabPersistence()
        {
            if (!AssetDatabase.IsValidFolder("Assets/SelectionToggleProbe"))
                AssetDatabase.CreateFolder("Assets", "SelectionToggleProbe");
            var source = new GameObject("Selection prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, "Assets/SelectionToggleProbe/Active.prefab");
            Object.DestroyImmediate(source);
            var first = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var second = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            window.Items = new[] { first, second }.Select(go => new HierarchyItem(go.GetInstanceID(), go)).ToArray();
            window.Drawer = new ToggleDrawer();
            Selection.objects = new Object[] { first, second, prefab };
            Undo.ClearAll();
            Undo.IncrementCurrentGroup();
            Click(0, true);
            Require(!first.activeSelf && !second.activeSelf && prefab.activeSelf,
                "Selected prefab instances toggle while selected project prefab assets remain unchanged");
            Require(PrefabUtility.GetPropertyModifications(first).Any(p => p.propertyPath == "m_IsActive" && p.value == "0")
                && PrefabUtility.GetPropertyModifications(second).Any(p => p.propertyPath == "m_IsActive" && p.value == "0"),
                "Both selected prefab instances record active-state overrides");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();
            Require(first.activeSelf && second.activeSelf, "One Undo restores both selected prefab instances");
            Undo.PerformRedo();
            Require(!first.activeSelf && !second.activeSelf, "One Redo disables both selected prefab instances");
            string path = "Assets/SelectionToggleProbe/Fixture.unity";
            Require(EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path), "Fixture scene saved");
            window.Items = Array.Empty<HierarchyItem>();
            EditorSceneManager.OpenScene(path);
            var restored = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .Where(go => PrefabUtility.IsPartOfPrefabInstance(go)).ToArray();
            Require(restored.Length == 2 && restored.All(go => !go.activeSelf),
                "Both selected prefab active-state overrides survive scene save and reopen");
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
            Checks.Add(check);
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Report
            {
                unityVersion = Application.unityVersion, success = error == null, error = error?.ToString(),
                observations = Observations.ToArray(), checks = Checks.ToArray()
            }, true));
            if (window != null) window.Close();
            Selection.objects = Array.Empty<Object>();
            if (error != null) Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public class SelectionToggleProbeWindow : EditorWindow
    {
        public Settings Settings;
        public HierarchyItem[] Items;
        public ToggleDrawer Drawer;
        public Exception Error;

        private void OnGUI()
        {
            if (Drawer == null || Items == null) return;
            try
            {
                for (int i = 0; i < Items.Length; i++)
                {
                    var rect = new Rect(0, 16 + i * 24, 280, 16);
                    Drawer.Draw(rect, Items[i], Settings);
                    GUI.Label(new Rect(56, rect.y, 220, 16), Items[i].DisplayName);
                }
            }
            catch (Exception error) { Error = error; }
        }
    }
}
