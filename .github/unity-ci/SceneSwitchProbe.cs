using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HierarchyDecoratorCI
{
    // Run only in a disposable project. In graphical mode, cancel then discard each dirty-scene prompt.
    public static class SceneSwitchProbe
    {
        private const string Folder = "Assets/SceneSwitchFixture";
        private static readonly List<string> Checks = new List<string>();
        private static readonly MethodInfo Open = typeof(HierarchyDecorator.Editor.Settings).Assembly
            .GetType("HierarchyDecorator.Editor.HierarchySceneMenu", true)
            .GetMethod("OpenScene", BindingFlags.Static | BindingFlags.NonPublic);

        [Serializable]
        private class Report
        {
            public bool success;
            public string unityVersion;
            public string error;
            public string[] checks;
        }

        public static void Run()
        {
            if (!File.Exists(Path.Combine(Application.dataPath, "../ProjectSettings/SceneSwitchProbe.enabled")))
                throw new InvalidOperationException("Use an explicitly enabled disposable project.");
            if (Application.isBatchMode) Check();
            else EditorApplication.update += WaitForEditor;
        }

        private static void WaitForEditor()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitForEditor;
            Check();
        }

        private static void Check()
        {
            Exception failure = null;
            try
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "SceneSwitchFixture");
                foreach (string name in new[] { "A", "B", "C" })
                {
                    Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    Require(EditorSceneManager.SaveScene(fixture, PathFor(name)), "Saved fixture " + name);
                }

                foreach (NewSceneSetup setup in new[] { NewSceneSetup.DefaultGameObjects, NewSceneSetup.EmptyScene })
                {
                    Scene source = EditorSceneManager.NewScene(setup, NewSceneMode.Single);
                    Require(source.path == string.Empty && !source.isDirty, "Clean Untitled: " + setup);
                    Switch(source, "A");
                    Require(!source.IsValid() && SceneManager.sceneCount == 1 &&
                        SceneManager.GetActiveScene().path == PathFor("A"), "Replaced clean Untitled without a prompt: " + setup);
                }

                Scene saved = SceneManager.GetActiveScene();
                Switch(saved, "B");
                Require(!saved.IsValid() && SceneManager.GetActiveScene().path == PathFor("B"), "Replaced a clean saved scene");

                Scene other = EditorSceneManager.OpenScene(PathFor("A"), OpenSceneMode.Additive);
                var marker = new GameObject("Unrelated unsaved object");
                SceneManager.MoveGameObjectToScene(marker, other);
                EditorSceneManager.MarkSceneDirty(other);
                Scene sourceB = SceneManager.GetSceneByPath(PathFor("B"));
                SceneManager.SetActiveScene(sourceB);
                Switch(sourceB, "C");
                Require(other.IsValid() && other.isDirty && marker != null && SceneManager.sceneCount == 2 &&
                    SceneManager.GetActiveScene().path == PathFor("C"), "Other dirty scene and its objects survive replacement");
                EditorSceneManager.CloseScene(other, true);

                if (!Application.isBatchMode)
                {
                    Scene dirtyUntitled = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    CheckDirtyScene(dirtyUntitled, "Untitled");
                    CheckDirtyScene(SceneManager.GetActiveScene(), "saved");
                }
            }
            catch (Exception error) { failure = error; }

            string output = Path.Combine(Application.dataPath, "../Logs/SceneSwitchProbe");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Report
            {
                success = failure == null,
                unityVersion = Application.unityVersion,
                error = failure == null ? null : failure.ToString(),
                checks = Checks.ToArray()
            }, true));
            if (failure != null) Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static void CheckDirtyScene(Scene source, string label)
        {
            var marker = new GameObject("Unsaved " + label + " object");
            EditorSceneManager.MarkSceneDirty(source);
            Debug.Log("SCENE_SWITCH_PROBE_CANCEL " + label);
            Switch(source, "B");
            Require(source.IsValid() && source.isDirty && marker != null, "Cancel preserves dirty " + label + " scene");
            Debug.Log("SCENE_SWITCH_PROBE_DISCARD " + label);
            Switch(source, "B");
            Require(!source.IsValid() && SceneManager.GetActiveScene().path == PathFor("B"), "Don't Save replaces dirty " + label + " scene");
            // Use C for the next source, so the following switch to B exercises replacement.
            Switch(SceneManager.GetActiveScene(), "C");
        }

        private static string PathFor(string name) => Folder + "/" + name + ".unity";

        private static void Switch(Scene source, string name)
        {
            Debug.Log("SCENE_SWITCH_PROBE_OPEN source=" + source.path + " dirty=" + source.isDirty + " target=" + name);
            Open.Invoke(null, new object[] { source, AssetDatabase.AssetPathToGUID(PathFor(name)) });
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
            Checks.Add(check);
            Debug.Log("SCENE_SWITCH_PROBE_OK " + check);
        }
    }
}
