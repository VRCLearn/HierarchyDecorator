using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HierarchyDecorator.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HierarchyDecoratorCI
{
    // Run only in a disposable project, with or without the real VRChat SDK.
    public static class VrcComponentIconProbe
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly string[] TypeNames =
        {
            "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor",
            "VRC.SDK3.Components.VRCMirrorReflection",
            "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone",
            "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider"
        };
        private static readonly string[] IconNames =
        {
            "AvatarDescriptor", "MirrorReflection", "VRCPhysBone", "VRCPhysBone"
        };
        private static readonly List<string> Checks = new List<string>();
        private static readonly List<GameObject> Fixtures = new List<GameObject>();
        private static readonly GameObject[] ComponentHosts = new GameObject[4];
        private static readonly Dictionary<int, Rect> Rows = new Dictionary<int, Rect>();
        private static readonly MethodInfo GetIcon = typeof(ComponentType).Assembly
            .GetType("HierarchyDecorator.Editor.VrcComponentIcons", true)
            .GetMethod("GetIcon", BindingFlags.Static | BindingFlags.NonPublic);
        private static Settings settings;
        private static EditorWindow hierarchy;
        private static object hierarchyView;
        private static double captureAt;
        private static int captureStep;
        private static string output;
        private static bool sdkPresent;

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public bool sdkPresent;
            public bool success;
            public string error;
            public string[] checks;
        }

        public static void Run()
        {
            output = Path.GetFullPath("Logs/VrcComponentIconProbe");
            Directory.CreateDirectory(output);
            try
            {
                settings = (Settings)typeof(Settings).Assembly.GetType("HierarchyDecorator.Editor.HierarchyDecorator", true)
                    .GetProperty("Settings", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                typeof(Settings).GetProperty("NeedsPresetSelection", Instance).SetValue(settings, false);
                var serialized = new SerializedObject(settings);
                serialized.FindProperty("components").FindPropertyRelative("showAll").intValue = (int)DisplayMode.Custom;
                serialized.FindProperty("components").FindPropertyRelative("enableIcons").boolValue = true;
                serialized.FindProperty("components").FindPropertyRelative("stackDuplicateIcons").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                Require(ResolveIcon(typeof(BoxCollider)) == null, "Built-in components retain their existing icon path");
                var ordinary = new ComponentType(typeof(BoxCollider), true);
                ordinary.UpdateContent();
                Require(ordinary.Content.image == EditorGUIUtility.ObjectContent(null, typeof(BoxCollider)).image,
                    "Built-in component icon unchanged");

                var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("HierarchyDecorator.IconLookalikes"),
                    AssemblyBuilderAccess.Run).DefineDynamicModule("Lookalikes");
                foreach (string name in TypeNames.Concat(new[] { "Example.VRCPhysBone", "Example.NotVRCPhysBoneCollider" }))
                    Require(ResolveIcon(module.DefineType(name, TypeAttributes.Public).CreateType()) == null,
                        "No VRC icon for unrelated type: " + name);

                sdkPresent = TypeNames.Any(name => FindType(name) != null);
                bool expectSdk = Environment.GetCommandLineArgs().Contains("-vrcIconProbeSdk");
                Require(sdkPresent == expectSdk, "Expected SDK installation state: " + expectSdk);
                if (sdkPresent)
                {
                    int primary = Environment.GetCommandLineArgs().Contains("-vrcIconProbeWorlds") ? 1 : 0;
                    Require(FindType(TypeNames[primary]) != null, "Expected SDK component available: " + TypeNames[primary]);
                    CheckSdkComponents();
                }

                if (Environment.GetCommandLineArgs().Contains("-vrcIconProbeGraphic"))
                    StartGraphicalCheck();
                else
                    Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private static void CheckSdkComponents()
        {
            var textures = new HashSet<Texture>();
            for (int i = 0; i < TypeNames.Length; i++)
            {
                Type type = FindType(TypeNames[i]);
                if (type == null) continue;
                Require(!type.IsAbstract, "Real attachable SDK type available: " + TypeNames[i]);
                var go = new GameObject((i + 1).ToString("00") + " Attached " + type.Name);
                Fixtures.Add(go);
                ComponentHosts[i] = go;
                var attached = go.AddComponent(type);
                var item = new ComponentItem(attached);
                Require(!item.IsNullComponent && item.Type.IsValid(), "Attached component registered: " + type.Name);
                Require(item.Content.image == ResolveIcon(type) && item.Content.image.name == IconNames[i],
                    "Dedicated icon used: " + type.Name);
                Require(item.Content.tooltip == type.Name, "Tooltip names the attached component: " + type.Name);
                textures.Add(item.Content.image);

                var drawer = new ComponentIconInfo();
                MethodInfo canShow = typeof(ComponentIconInfo).GetMethod("CanShow", Instance);
                item.Type.Excluded = true;
                Require(!(bool)canShow.Invoke(drawer, new object[] { item, settings }), "Existing exclusion respected: " + type.Name);
                item.Type.Excluded = false;
                Require((bool)canShow.Invoke(drawer, new object[] { item, settings }), "Show custom components respected: " + type.Name);

                var serialized = new SerializedObject(settings);
                var showAll = serialized.FindProperty("components").FindPropertyRelative("showAll");
                showAll.intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                item.Type.Shown = false;
                Require(!(bool)canShow.Invoke(drawer, new object[] { item, settings }), "Hidden custom component stays hidden: " + type.Name);
                item.Type.Shown = true;
                Require((bool)canShow.Invoke(drawer, new object[] { item, settings }), "Explicit custom selection respected: " + type.Name);
                item.Type.Shown = false;
                showAll.intValue = (int)DisplayMode.Custom;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Require(textures.Count == ComponentHosts.Count(host => host != null) - 1,
                "PhysBone and Collider share one blue bone icon");

            var restored = ScriptableObject.CreateInstance<Settings>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(settings), restored);
                restored.Components.UpdateComponents(true);
                for (int i = 0; i < TypeNames.Length; i++)
                {
                    if (ComponentHosts[i] == null) continue;
                    ComponentType restoredType;
                    Require(restored.Components.TryGetCustomComponent(FindType(TypeNames[i]), out restoredType)
                        && restoredType.Content.image == ResolveIcon(FindType(TypeNames[i])),
                        "Serialized settings restore the VRC component icon: " + IconNames[i]);
                }
            }
            finally { Object.DestroyImmediate(restored); }

            var target = new GameObject("05 Referenced root without components");
            var child = new GameObject("06 Child without components");
            Fixtures.Add(target);
            Fixtures.Add(child);
            child.transform.SetParent(target.transform);
            var physBone = ComponentHosts[2].GetComponent(FindType(TypeNames[2]));
            physBone.GetType().GetField("rootTransform").SetValue(physBone, target.transform);
            Require(VrcIconsOn(target) == 0 && VrcIconsOn(child) == 0, "Referenced root and child receive no VRC icons");
            Require(VrcIconsOn(ComponentHosts[2]) == 1, "PhysBone stays on its component host after root assignment");
            Texture physBoneIcon = new ComponentItem(physBone).Content.image;
            ((Behaviour)physBone).enabled = false;
            Require(new ComponentItem(physBone).Content.image == physBoneIcon,
                "PhysBone icon identity does not depend on enabled state or root configuration");
            ((Behaviour)physBone).enabled = true;
            ComponentHosts[2].SetActive(false);
            Require(VrcIconsOn(ComponentHosts[2]) == 1, "Inactive host still describes its attached component");
            ComponentHosts[2].SetActive(true);

            var changing = new GameObject("07 Add remove undo redo");
            Fixtures.Add(changing);
            var cached = new HierarchyItem(changing.GetInstanceID(), changing);
            var collider = Undo.AddComponent(changing, FindType(TypeNames[3]));
            cached.OnGUIBegin();
            Require(VrcIconsOn(cached) == 1, "Added component appears in existing hierarchy cache");
            Undo.IncrementCurrentGroup();
            Undo.DestroyObjectImmediate(collider);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            cached.OnGUIBegin();
            Require(VrcIconsOn(cached) == 0, "Removed component disappears from existing hierarchy cache");
            Undo.PerformUndo();
            cached.OnGUIBegin();
            Require(VrcIconsOn(cached) == 1, "Undo restores the component icon");
            Undo.PerformRedo();
            cached.OnGUIBegin();
            Require(VrcIconsOn(cached) == 0, "Redo removes the component icon");

            if (!AssetDatabase.IsValidFolder("Assets/VrcIconProbe"))
                AssetDatabase.CreateFolder("Assets", "VrcIconProbe");
            var prefab = PrefabUtility.SaveAsPrefabAsset(ComponentHosts[3], "Assets/VrcIconProbe/Collider.prefab");
            var prefabInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            prefabInstance.name = "08 Prefab attached collider";
            Fixtures.Add(prefabInstance);
            Require(VrcIconsOn(prefabInstance) == 1, "Prefab instance uses attached component icon");

            var multiple = new GameObject("09 Multiple attached VRC components");
            Fixtures.Add(multiple);
            multiple.AddComponent(FindType(TypeNames[2]));
            multiple.AddComponent(FindType(TypeNames[2]));
            multiple.AddComponent(FindType(TypeNames[3]));
            var multipleItem = new HierarchyItem(multiple.GetInstanceID(), multiple);
            var iconDrawer = new ComponentIconInfo();
            typeof(ComponentIconInfo).GetMethod("OnDrawInit", Instance).Invoke(iconDrawer, new object[] { multipleItem, settings });
            var drawn = (IList<ComponentItem>)typeof(ComponentIconInfo).GetField("items", Instance).GetValue(iconDrawer);
            Require(drawn.Count == 3 && drawn.Select(component => component.Content.image).Distinct().Count() == 1,
                "All attached PhysBone components use the same bone graphic");
            Require(drawn.Select(component => component.DisplayName).Distinct().Count() == 2,
                "Shared graphic preserves the distinct component names in tooltips");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/VrcIconProbe/Fixture.unity");
            Require(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty, "Fixture scene saved before drawing");
        }

        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.GetType(name)).FirstOrDefault(type => type != null);
        }

        private static Texture ResolveIcon(Type type) { return (Texture)GetIcon.Invoke(null, new object[] { type }); }

        private static int VrcIconsOn(GameObject go)
        {
            return VrcIconsOn(new HierarchyItem(go.GetInstanceID(), go));
        }

        private static int VrcIconsOn(HierarchyItem item)
        {
            return item.Components.GetItems().Count(component => !component.IsNullComponent && ResolveIcon(component.Component.GetType()) != null);
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
            Checks.Add(check);
        }

        private static void StartGraphicalCheck()
        {
            if (!sdkPresent) throw new InvalidOperationException("Graphical probe requires the real SDK fixtures.");
            settings.styleData.UpdateStyles(EditorGUIUtility.isProSkin);
            hierarchy = ScriptableObject.CreateInstance(typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow", true)) as EditorWindow;
            hierarchy.ShowUtility();
            hierarchy.position = new Rect(80, 80, 780, 440);
            hierarchyView = typeof(EditorWindow).GetField("m_Parent", Instance).GetValue(hierarchy);
            HierarchyManager.Initialize();
            EditorApplication.hierarchyWindowItemOnGUI += TrackRow;
            EditorApplication.update += Tick;
            hierarchy.Focus();
            hierarchy.Repaint();
            captureStep = -1;
            captureAt = EditorApplication.timeSinceStartup + 3;
        }

        private static void TrackRow(int id, Rect rect)
        {
            if (Event.current.type == EventType.Repaint && typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView", true)
                .GetProperty("current", BindingFlags.Static | BindingFlags.Public).GetValue(null) == hierarchyView)
            {
                Rows[id] = rect;
            }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < captureAt) return;
            try
            {
                if (captureStep == -1)
                {
                    var sceneHierarchy = hierarchy.GetType().GetProperty("sceneHierarchy", Instance).GetValue(hierarchy);
                    var expand = sceneHierarchy.GetType().GetMethod("SetExpandedRecursive", Instance);
                    foreach (var fixture in Fixtures)
                        expand.Invoke(sceneHierarchy, new object[] { fixture.GetInstanceID(), true });
                    Rows.Clear();
                    hierarchy.Repaint();
                    captureStep = 0;
                    captureAt = EditorApplication.timeSinceStartup + 1;
                    return;
                }
                Require(Fixtures.All(go => Rows.ContainsKey(go.GetInstanceID())), "All fixture rows drawn in the real Hierarchy; missing: "
                    + string.Join(", ", Fixtures.Where(go => !Rows.ContainsKey(go.GetInstanceID())).Select(go => go.name)));
                Require(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty, "Hierarchy rendering leaves the scene unchanged");
                var physBone = ComponentHosts[2].GetComponent(FindType(TypeNames[2]));
                Require(new ComponentItem(physBone).Content.image.name == "VRCPhysBone",
                    "PhysBone uses the single shared bone asset");
                CheckBonePixels(ResolveIcon(physBone.GetType()));
                var view = typeof(EditorWindow).GetField("m_Parent", Instance).GetValue(hierarchy);
                float scale = EditorGUIUtility.pixelsPerPoint;
                int width = Mathf.RoundToInt(hierarchy.position.width * scale);
                int height = Mathf.RoundToInt(hierarchy.position.height * scale);
                var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var previous = RenderTexture.active;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView", true).GetMethod("GrabPixels", Instance)
                        .Invoke(view, new object[] { target, new Rect(0, 0, width, height) });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    var pixels = image.GetPixels32();
                    var flipped = new Color32[pixels.Length];
                    for (int row = 0; row < height; row++)
                        Array.Copy(pixels, row * width, flipped, (height - row - 1) * width, width);
                    image.SetPixels32(flipped);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, captureStep == 0 ? "icons.png" : "stacked.png"), image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                    Object.DestroyImmediate(image);
                }
                if (captureStep++ == 0)
                {
                    var serialized = new SerializedObject(settings);
                    serialized.FindProperty("components").FindPropertyRelative("stackDuplicateIcons").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    hierarchy.Repaint();
                    captureAt = EditorApplication.timeSinceStartup + 2;
                }
                else Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private static void CheckBonePixels(Texture icon)
        {
            var target = RenderTexture.GetTemporary(icon.width, icon.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var sample = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(icon, target);
                RenderTexture.active = target;
                sample.ReadPixels(new Rect(icon.width / 2, icon.height / 2, 1, 1), 0, 0);
                Color center = sample.GetPixel(0, 0);
                Require(center.a > 0.9f && center.b > 0.7f && center.r < 0.2f && center.b > center.g + 0.15f,
                    "Imported diagonal bone icon has an opaque blue center");
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(sample);
            }
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            EditorApplication.hierarchyWindowItemOnGUI -= TrackRow;
            var report = new Report { unityVersion = Application.unityVersion, sdkPresent = sdkPresent,
                success = error == null, error = error?.ToString(), checks = Checks.ToArray() };
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            if (hierarchy != null)
            {
                hierarchy.Close();
            }
            if (error == null) Debug.Log("VRC_COMPONENT_ICON_PROBE_OK " + JsonUtility.ToJson(report));
            else Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
