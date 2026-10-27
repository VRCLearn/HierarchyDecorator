using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HierarchyDecorator.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HierarchyDecoratorCI
{
    // Run in a disposable graphical Editor. Compare the real Hierarchy's native
    // margin with and without the plugin, rather than synthesizing override data.
    public static class PrefabOverrideProbe
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string FixtureFolder = "Assets/PrefabOverrideProbe";
        private static readonly Dictionary<int, Vector2> Rows = new Dictionary<int, Vector2>();
        private static readonly Dictionary<int, Color[]> NativeMargins = new Dictionary<int, Color[]>();
        private static readonly List<GameObject> Roots = new List<GameObject>();
        private static readonly List<string> Checks = new List<string>();
        private static readonly Type ViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView", true);
        private static readonly PropertyInfo SkinIndex = typeof(EditorGUIUtility).GetProperty("skinIndex", BindingFlags.Static | BindingFlags.NonPublic);
        private static int originalSkin;
        private static object hierarchyView;
        private static Color nativeBackground;
        private static EditorWindow hierarchy;
        private static EditorWindow otherWindow;
        private static Settings settings;
        private static double nextStep;
        private static int step;
        private static int mode;
        private static string output;
        private static bool finished;

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public bool darkTheme;
            public float pixelsPerPoint;
            public bool passed;
            public string[] checks;
            public string error;
        }

        public static void Run()
        {
            if (Application.isBatchMode || !File.Exists("ProjectSettings/PrefabOverrideProbe.enabled"))
                throw new InvalidOperationException("Prepare a disposable project with --prefab-override-probe and launch a graphical Editor.");
            output = Path.GetFullPath("Logs/PrefabOverrideProbe");
            Directory.CreateDirectory(output);
            originalSkin = (int)SkinIndex.GetValue(null);
            if (Environment.GetCommandLineArgs().Contains("-prefabProbeLight"))
                SkinIndex.SetValue(null, 0);
            EditorApplication.delayCall += Setup;
        }

        private static void Setup()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Type owner = typeof(Settings).Assembly.GetType("HierarchyDecorator.Editor.HierarchyDecorator", true);
                settings = (Settings)owner.GetProperty("Settings", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                typeof(Settings).GetProperty("NeedsPresetSelection", Instance).SetValue(settings, false);
                settings.styleData.UpdateStyles(EditorGUIUtility.isProSkin);
                settings.globalData.showActiveToggles = true;
                if (!AssetDatabase.IsValidFolder(FixtureFolder))
                    AssetDatabase.CreateFolder("Assets", "PrefabOverrideProbe");

                CreateFixture("01 Unmodified", null);
                CreateFixture("02 Property", go => SetProperty(go.GetComponent<BoxCollider>()));
                CreateFixture("03 Child property", go => SetProperty(go.transform.GetChild(0).GetComponent<BoxCollider>()));
                CreateFixture("04 Added component", go => go.AddComponent<Rigidbody>());
                CreateFixture("05 Removed component", go => Object.DestroyImmediate(go.GetComponent<BoxCollider>()));
                CreateFixture("06 Added child", go => new GameObject("Added child").transform.SetParent(go.transform));
                CreateFixture("07 Removed child", go => Object.DestroyImmediate(go.transform.GetChild(0).gameObject));
                CreateFixture("08 Inactive", go => { go.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(go); });
                CreateFixture("= Styled prefab", go => SetProperty(go.GetComponent<BoxCollider>()));

                var nestedSource = new GameObject("09 Nested prefab");
                GameObject nested = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FixtureFolder + "/01 Unmodified.prefab"));
                nested.transform.SetParent(nestedSource.transform);
                GameObject nestedAsset = PrefabUtility.SaveAsPrefabAsset(nestedSource, FixtureFolder + "/09 Nested prefab.prefab");
                Object.DestroyImmediate(nestedSource);
                var nestedInstance = (GameObject)PrefabUtility.InstantiatePrefab(nestedAsset);
                Roots.Add(nestedInstance);
                SetProperty(nestedInstance.transform.GetChild(0).GetComponent<BoxCollider>());
                new GameObject("10 Ordinary object");

                hierarchy = ScriptableObject.CreateInstance(typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow", true)) as EditorWindow;
                hierarchy.titleContent = new GUIContent("Prefab Override Regression");
                hierarchy.ShowUtility();
                hierarchy.position = new Rect(80, 80, 850, 760);
                hierarchyView = typeof(EditorWindow).GetField("m_Parent", Instance).GetValue(hierarchy);
                var sceneHierarchy = hierarchy.GetType().GetProperty("sceneHierarchy", Instance).GetValue(hierarchy);
                MethodInfo expand = sceneHierarchy.GetType().GetMethod("SetExpandedRecursive", Instance);
                foreach (GameObject root in Roots)
                    expand.Invoke(sceneHierarchy, new object[] { root.GetInstanceID(), true });

                otherWindow = ScriptableObject.CreateInstance<PrefabOverrideFocusWindow>();
                otherWindow.ShowUtility();
                otherWindow.position = new Rect(960, 80, 200, 100);
                HierarchyManager.Initialize();
                EditorApplication.hierarchyWindowItemOnGUI += TrackRow;
                EditorApplication.update += Tick;
                nextStep = EditorApplication.timeSinceStartup + 2;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void CreateFixture(string name, Action<GameObject> modify)
        {
            var source = new GameObject(name, typeof(BoxCollider));
            new GameObject("Child", typeof(BoxCollider)).transform.SetParent(source.transform);
            var asset = PrefabUtility.SaveAsPrefabAsset(source, FixtureFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            Roots.Add(instance);
            modify?.Invoke(instance);
        }

        private static void SetProperty(BoxCollider collider)
        {
            collider.center = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }

        private static void TrackRow(int id, Rect rect)
        {
            if (Event.current.type == EventType.Repaint &&
                ViewType.GetProperty("current", BindingFlags.Static | BindingFlags.Public).GetValue(null) == hierarchyView)
                Rows[id] = GUIUtility.GUIToScreenPoint(new Vector2(32, rect.y + 2)) - hierarchy.position.position;
        }

        private static void Tick()
        {
            if (finished || EditorApplication.timeSinceStartup < nextStep || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            nextStep = EditorApplication.timeSinceStartup + 0.5;
            try
            {
                if (step == 0)
                {
                    Selection.objects = mode == 1 || mode == 2 ? Roots.Cast<Object>().ToArray() : new Object[0];
                    settings.styleData.twoToneBackground = mode != 3;
                    settings.styleData.styles[0].fontSize = mode == 5 ? 24 : 11;
                    settings.styleData.UpdateStyles(EditorGUIUtility.isProSkin);
                    settings.globalData.activeToggleType = mode == 4 ? GlobalData.ToggleType.Dot : GlobalData.ToggleType.Checkbox;
                    EditorApplication.hierarchyWindowItemOnGUI -= HierarchyManager.OnGUI;
                    Rows.Clear();
                    FocusWindow();
                    hierarchy.Repaint();
                }
                else if (step == 1)
                {
                    if (Rows.Count < 20)
                        throw new InvalidOperationException("Expected expanded fixture rows, got " + Rows.Count);
                    NativeMargins.Clear();
                    var reference = CaptureWindow();
                    SaveScreenshot(reference, "native-" + mode);
                    foreach (var row in Rows)
                        NativeMargins.Add(row.Key, ReadMargin(reference, row.Value));
                    nativeBackground = ReadBackground(reference);
                    if (mode == 0)
                    {
                        foreach (var root in Roots)
                        {
                            Color pixel = NativeMargins[root.GetInstanceID()][0];
                            bool blueMarker = pixel.b > pixel.r + 0.2f;
                            if (blueMarker != (root.name != "01 Unmodified"))
                                throw new InvalidOperationException("Native reference marker missing or unexpected: " + root.name + " " + pixel);
                        }
                        Checks.Add("native reference distinguishes unmodified instances from property, structural and nested overrides");
                    }
                    Object.DestroyImmediate(reference);
                    EditorApplication.hierarchyWindowItemOnGUI -= TrackRow;
                    EditorApplication.hierarchyWindowItemOnGUI += HierarchyManager.OnGUI;
                    EditorApplication.hierarchyWindowItemOnGUI += TrackRow;
                    hierarchy.Repaint();
                }
                else
                {
                    var actualWindow = CaptureWindow();
                    SaveScreenshot(actualWindow, "decorated-" + mode);
                    var changedRows = new List<string>();
                    foreach (var native in NativeMargins)
                    {
                        Color[] actual = ReadMargin(actualWindow, Rows[native.Key]);
                        if (!native.Value.SequenceEqual(actual))
                            changedRows.Add(EditorUtility.InstanceIDToObject(native.Key).name);
                    }
                    if (changedRows.Count > 0)
                        throw new InvalidOperationException("Native margin changed in mode " + mode + " for: " + string.Join(", ", changedRows));
                    if (mode != 3 && ReadBackground(actualWindow) == nativeBackground)
                        throw new InvalidOperationException("Plugin background was not drawn in mode " + mode);
                    Checks.Add("native margin unchanged for " + NativeMargins.Count + " rows: " + new[] { "unselected", "selected and focused", "selected and unfocused", "header styles without alternating background", "dot active toggles", "large header font", "toggle input" }[mode]);
                    Object.DestroyImmediate(actualWindow);
                    if (mode == 6)
                    {
                        Vector2 point = Rows[Roots[0].GetInstanceID()] + new Vector2(8, 4);
                        hierarchy.SendEvent(new Event { type = EventType.MouseDown, mousePosition = point, button = 0 });
                        hierarchy.SendEvent(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 });
                        if (Roots[0].activeSelf)
                            throw new InvalidOperationException("Active toggle did not respond at its original coordinates.");
                        Checks.Add("active toggle responds at its original mouse coordinates");
                    }
                    mode++;
                    if (mode == 7) { Finish(null); return; }
                    step = -1;
                }
                step++;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void FocusWindow()
        {
            if (mode == 2) otherWindow.Focus();
            else hierarchy.Focus();
        }

        private static Color[] ReadMargin(Texture2D texture, Vector2 point)
        {
            float scale = EditorGUIUtility.pixelsPerPoint;
            int height = Mathf.RoundToInt(10 * scale);
            return texture.GetPixels(Mathf.RoundToInt(point.x * scale), texture.height - Mathf.RoundToInt(point.y * scale) - height,
                Mathf.RoundToInt(2 * scale), height);
        }

        private static Color ReadBackground(Texture2D texture)
        {
            Vector2 point = Rows[Roots[0].GetInstanceID()];
            float scale = EditorGUIUtility.pixelsPerPoint;
            return texture.GetPixel(Mathf.RoundToInt(600 * scale), texture.height - Mathf.RoundToInt((point.y + 3) * scale));
        }

        private static Texture2D CaptureWindow()
        {
            float scale = EditorGUIUtility.pixelsPerPoint;
            Rect rect = hierarchy.position;
            int width = Mathf.RoundToInt(rect.width * scale);
            int height = Mathf.RoundToInt(rect.height * scale);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                ViewType.GetMethod("GrabPixels", Instance).Invoke(hierarchyView, new object[] { target, new Rect(0, 0, width, height) });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                // GUIView's backing surface is top-down; Texture2D/PNG is bottom-up.
                Color32[] pixels = texture.GetPixels32();
                var flipped = new Color32[pixels.Length];
                for (int row = 0; row < height; row++)
                    Array.Copy(pixels, row * width, flipped, (height - row - 1) * width, width);
                texture.SetPixels32(flipped);
                texture.Apply();
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void SaveScreenshot(Texture2D texture, string name)
        {
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
        }

        private static void Finish(Exception error)
        {
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.hierarchyWindowItemOnGUI -= TrackRow;
            var report = new Report
            {
                unityVersion = Application.unityVersion,
                darkTheme = EditorGUIUtility.isProSkin,
                pixelsPerPoint = EditorGUIUtility.pixelsPerPoint,
                passed = error == null,
                checks = Checks.ToArray(),
                error = error?.ToString()
            };
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            if (error == null) Debug.Log("PREFAB_OVERRIDE_PROBE_OK " + JsonUtility.ToJson(report));
            else Debug.LogException(error);
            SkinIndex.SetValue(null, originalSkin);
            InternalEditorUtility.RepaintAllViews();
            if (hierarchy != null) hierarchy.Close();
            if (otherWindow != null) otherWindow.Close();
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public class PrefabOverrideFocusWindow : EditorWindow
    {
        private void OnGUI() { GUILayout.Label("Unfocused Hierarchy check"); }
    }
}
