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
            "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider",
            "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver",
            "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender",
            "VRC.SDK3.Dynamics.Constraint.Components.VRCAimConstraint",
            "VRC.SDK3.Dynamics.Constraint.Components.VRCLookAtConstraint",
            "VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint",
            "VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint",
            "VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint",
            "VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint"
        };
        private static readonly string[] IconNames =
        {
            "AvatarDescriptor", "MirrorReflection", "VRCPhysBone", "VRCPhysBoneCollider",
            "VRCContactReceiver", "VRCContactSender", "VRCAimConstraint", "VRCLookAtConstraint",
            "VRCParentConstraint", "VRCPositionConstraint", "VRCRotationConstraint", "VRCScaleConstraint"
        };
        private static readonly List<string> Checks = new List<string>();
        private static readonly List<GameObject> Fixtures = new List<GameObject>();
        private static readonly GameObject[] ComponentHosts = new GameObject[TypeNames.Length];
        private static readonly Dictionary<int, Rect> Rows = new Dictionary<int, Rect>();
        private static readonly Dictionary<int, float> ScreenRowTops = new Dictionary<int, float>();
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
                    Require(FindType(TypeNames[4]) != null && FindType(TypeNames[5]) != null,
                        "Real Contact Receiver and Sender components are available");
                    Require(TypeNames.Skip(6).All(name => FindType(name) != null),
                        "All six real VRC constraint components are available");
                    if (Environment.GetCommandLineArgs().Contains("-vrcIconProbeHeadless"))
                    {
                        CheckHeadlessConstraints();
                        Finish(null);
                        return;
                    }
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
                if (i == 1)
                {
                    // A mirror without a renderer/material disables itself in the SDK's Start().
                    if (!AssetDatabase.IsValidFolder("Assets/VrcIconProbe"))
                        AssetDatabase.CreateFolder("Assets", "VrcIconProbe");
                    const string materialPath = "Assets/VrcIconProbe/Mirror.mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        Shader shader = Shader.Find("FX/MirrorReflection");
                        Require(shader != null, "SDK mirror shader is available for a valid enabled fixture");
                        material = new Material(shader);
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    go.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                var attached = go.AddComponent(type);
                var item = new ComponentItem(attached);
                Require(!item.IsNullComponent && item.Type.IsValid(), "Attached component registered: " + type.Name);
                Require(item.Content.image == ResolveIcon(type) && item.Content.image.name == IconNames[i],
                    "Dedicated icon used: " + type.Name);
                Require(item.Content.tooltip == type.Name, "Tooltip names the attached component: " + type.Name);
                textures.Add(item.Content.image);
                if (i >= 6) CheckConstraintIcon(type, item.Content.image);

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
            Require(textures.Count == ComponentHosts.Count(host => host != null),
                "Every supported VRC component type has a distinct icon");

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

            var target = new GameObject("13 Referenced root without components");
            var child = new GameObject("14 Child without components");
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

            var changing = new GameObject("15 Add remove undo redo");
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
            prefabInstance.name = "16 Prefab attached collider";
            Fixtures.Add(prefabInstance);
            Require(VrcIconsOn(prefabInstance) == 1, "Prefab instance uses attached component icon");

            var multiple = new GameObject("17 Multiple attached VRC components");
            Fixtures.Add(multiple);
            multiple.AddComponent(FindType(TypeNames[2]));
            multiple.AddComponent(FindType(TypeNames[2]));
            multiple.AddComponent(FindType(TypeNames[3]));
            multiple.AddComponent(FindType(TypeNames[4]));
            multiple.AddComponent(FindType(TypeNames[5]));
            var multipleItem = new HierarchyItem(multiple.GetInstanceID(), multiple);
            var iconDrawer = new ComponentIconInfo();
            typeof(ComponentIconInfo).GetMethod("OnDrawInit", Instance).Invoke(iconDrawer, new object[] { multipleItem, settings });
            var drawn = (IList<ComponentItem>)typeof(ComponentIconInfo).GetField("items", Instance).GetValue(iconDrawer);
            Require(drawn.Count == 5 && drawn.Select(component => component.Content.image).Distinct().Count() == 4,
                "Duplicate PhysBones share a graphic; collider and contacts have distinct graphics");
            Require(drawn.Select(component => component.DisplayName).Distinct().Count() == 4,
                "Distinct graphics preserve the component names in tooltips");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/VrcIconProbe/Fixture.unity");
            Require(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty, "Fixture scene saved before drawing");
        }

        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.GetType(name)).FirstOrDefault(type => type != null);
        }

        private static void CheckConstraintIcon(Type type, Texture icon)
        {
            string unityName = type.Name.Substring(3);
            Type unityType = FindType("UnityEngine.Animations." + unityName);
            Require(unityType != null, "Native constraint type available: " + unityName);
            Texture original = EditorGUIUtility.ObjectContent(null, unityType).image;
            var ordinary = new ComponentType(unityType, true);
            ordinary.UpdateContent();
            Require(original != null && ordinary.Content.image == original && icon != original,
                "Ordinary Unity constraint keeps its original icon: " + unityName);
            Require(ResolveIcon(type) == icon, "Recolored constraint icon is cached: " + unityName);
            Color32[] before = ReadIconPixels(original);
            Color32[] after = ReadIconPixels(icon);
            Require(before.Length == after.Length, "Constraint icon dimensions preserved: " + unityName);
            int pinPixels = 0;
            int unchanged = 0;
            Color32 brand = new Color32(43, 170, 193, 255);
            Color32 expected = QualitySettings.activeColorSpace == ColorSpace.Linear ? (Color32)((Color)brand).linear : brand;
            for (int i = 0; i < before.Length; i++)
            {
                Color32 source = before[i];
                Color32 result = after[i];
                if (source.a > 0 && source.r > source.g && source.b > source.g)
                {
                    if (result.r != expected.r || result.g != expected.g || result.b != expected.b || result.a != source.a)
                        throw new InvalidOperationException("Incorrect brand-color pin pixel: " + unityName + " at " + i);
                    pinPixels++;
                }
                else
                {
                    if (!source.Equals(result))
                        throw new InvalidOperationException("Non-pin pixel changed: " + unityName + " at " + i);
                    if (source.a > 0) unchanged++;
                }
            }
            Require(pinPixels > 0 && unchanged > 0,
                "Only pin pixels change to #2BAAC1; body and alpha preserved: " + unityName);
        }

        private static void CheckHeadlessConstraints()
        {
            Require(SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null,
                "Headless constraint check has no graphics device");
            MonoScript[] scripts = MonoImporter.GetAllRuntimeMonoScripts();
            foreach (string name in TypeNames.Skip(6))
            {
                Type type = FindType(name);
                MonoScript script = scripts.First(candidate => candidate != null && candidate.GetClass() == type);
                var component = new ComponentType(type, false);
                component.UpdateType(script);
                Require(ResolveIcon(type) == null && component.IsValid()
                    && component.Content.image == AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(script)),
                    "Headless runs retain the existing icon path without GPU readback: " + type.Name);
            }
        }

        private static Color32[] ReadIconPixels(Texture icon)
        {
            var target = RenderTexture.GetTemporary(icon.width, icon.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var sample = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(icon, target);
                RenderTexture.active = target;
                sample.ReadPixels(new Rect(0, 0, icon.width, icon.height), 0, 0);
                return sample.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(sample);
            }
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
                ScreenRowTops[id] = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y)).y - hierarchy.position.y;
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
                if (ComponentHosts[1] != null)
                    Require(new ComponentItem(ComponentHosts[1].GetComponent(FindType(TypeNames[1]))).Active,
                        "Mirror remains enabled for like-for-like icon color comparison");
                var physBone = ComponentHosts[2].GetComponent(FindType(TypeNames[2]));
                Require(new ComponentItem(physBone).Content.image.name == "VRCPhysBone",
                    "PhysBone retains the blue bone asset");
                CheckBonePixels(ResolveIcon(physBone.GetType()));
                CheckIconPalette(ResolveIcon(FindType(TypeNames[3])),
                    color => color.g > 0.9f && color.r < 0.1f && color.b < 0.1f,
                    "Imported collider icon uses the SDK's green instead of PhysBone blue");
                CheckIconPalette(ResolveIcon(FindType(TypeNames[4])),
                    color => color.r < 0.1f && color.g > 0.9f && color.b > 0.9f,
                    "Imported Contact Receiver icon uses the SDK's cyan");
                CheckIconPalette(ResolveIcon(FindType(TypeNames[5])),
                    color => color.r > 0.9f && color.g > 0.85f && color.b < 0.1f,
                    "Imported Contact Sender icon uses the SDK's Unity yellow");
                for (int i = 0; i < 2; i++)
                    if (ComponentHosts[i] != null)
                    {
                        Color gray = new Color(196f / 255f, 196f / 255f, 196f / 255f);
                        if (QualitySettings.activeColorSpace == ColorSpace.Linear) gray = gray.linear;
                        CheckIconPalette(ResolveIcon(FindType(TypeNames[i])),
                            color => Mathf.Abs(color.r - gray.r) < 0.005f
                                && Mathf.Abs(color.g - gray.g) < 0.005f
                                && Mathf.Abs(color.b - gray.b) < 0.005f,
                            "Imported icon matches native Unity body gray in " + QualitySettings.activeColorSpace + ": " + IconNames[i]);
                    }
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
                    CheckRenderedConstraintPins(flipped, width, height, scale);
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
                else
                {
                    CheckConstraintCacheCleanup();
                    Finish(null);
                }
            }
            catch (Exception error) { Finish(error); }
        }

        private static void CheckIconPalette(Texture icon, Func<Color, bool> matches, string check)
        {
            // Sample the imported texture's RGB values without a render-target gamma conversion.
            var target = RenderTexture.GetTemporary(icon.width, icon.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var sample = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(icon, target);
                RenderTexture.active = target;
                sample.ReadPixels(new Rect(0, 0, icon.width, icon.height), 0, 0);
                Color[] strokes = sample.GetPixels().Where(color => color.a > 0.9f).ToArray();
                Require(strokes.Length > 0 && strokes.All(matches), check);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(sample);
            }
        }

        private static void CheckRenderedConstraintPins(Color32[] pixels, int width, int height, float scale)
        {
            for (int i = 6; i < TypeNames.Length; i++)
            {
                int top = Mathf.RoundToInt(ScreenRowTops[ComponentHosts[i].GetInstanceID()] * scale);
                bool found = false;
                for (int y = Mathf.Max(0, top); y < Mathf.Min(height, top + Mathf.CeilToInt(16 * scale)); y++)
                    for (int x = 0; x < width; x++)
                    {
                        Color32 pixel = pixels[(height - 1 - y) * width + x];
                        // Alpha blending and 16 px filtering slightly shift edge colors.
                        if (Mathf.Abs(pixel.r - 43) <= 5 && Mathf.Abs(pixel.g - 170) <= 5 && Mathf.Abs(pixel.b - 193) <= 5)
                            found = true;
                    }
                Require(found, "Rendered pin matches sRGB #2BAAC1 in " + QualitySettings.activeColorSpace + ": " + IconNames[i] + " at row " + top);
            }
        }

        private static void CheckConstraintCacheCleanup()
        {
            Texture[] icons = TypeNames.Skip(6).Select(name => ResolveIcon(FindType(name))).ToArray();
            Require(icons.All(icon => icon != null), "All six constraint copies are cached before cleanup");
            GetIcon.DeclaringType.GetMethod("ClearConstraintIcons", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
            Require(icons.All(icon => icon == null), "Constraint cleanup destroys the generated textures");
            Require(TypeNames.Skip(6).All(name => EditorGUIUtility.ObjectContent(null,
                    FindType("UnityEngine.Animations." + FindType(name).Name.Substring(3))).image != null),
                "Constraint cleanup leaves Unity's original textures intact");
            Require(ResolveIcon(FindType(TypeNames[6])) != null,
                "Constraint icon can be regenerated after cleanup");
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
