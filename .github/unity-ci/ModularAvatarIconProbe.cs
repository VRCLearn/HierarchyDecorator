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
    public static class ModularAvatarIconProbe
    {
        private static readonly string[] Names =
        {
            "ModularAvatarMenuInstaller", "ModularAvatarMenuItem", "ModularAvatarMenuGroup", "ModularAvatarParameters",
            "ModularAvatarMergeAnimator", "ModularAvatarMergeBlendTree", "ModularAvatarMaterialSetter", "ModularAvatarMaterialSwap",
            "ModularAvatarMeshSettings", "ModularAvatarObjectToggle", "ModularAvatarBoneProxy", "ModularAvatarMergeArmature",
            "ModularAvatarBlendshapeSync", "ModularAvatarShapeChanger"
        };
        private static readonly string[] Badges =
        {
            "VRCExpressionsMenu", "VRCExpressionsMenu", "VRCExpressionsMenu", "VRCExpressionParameters",
            "AnimatorController", "AnimationClip", "Material", "Material", "Mesh", "GameObject", "BoneProxy",
            "MergeArmature", "BlendshapeSync", "ShapeChanger"
        };
        private static readonly List<string> Checks = new List<string>();
        private static readonly MethodInfo GetIcon = typeof(ComponentType).Assembly.GetType("HierarchyDecorator.Editor.ModularAvatarComponentIcons")
            .GetMethod("GetIcon", BindingFlags.Static | BindingFlags.NonPublic);
        private static EditorWindow hierarchy;
        private static ModularAvatarIconPreview preview;
        private static double ready;
        private static string output;
        private static Texture2D[] combined;
        private static Texture2D original;
        private static readonly List<GameObject> Fixtures = new List<GameObject>();
        private static readonly HashSet<int> Rows = new HashSet<int>();
        private static bool expanded;

        [Serializable]
        private class Report { public bool success; public string error; public string unityVersion; public string[] checks; }

        public static void Run()
        {
            output = Path.GetFullPath("Logs/ModularAvatarIconProbe");
            Directory.CreateDirectory(output);
            try
            {
                bool expect = Environment.GetCommandLineArgs().Contains("-maIconProbePackages");
                Require((FindType(Names[0]) != null) == expect, "Expected MA/SDK installation: " + expect);
                Require(Resolve(typeof(BoxCollider), null) == null, "Ordinary Unity icons use their existing path");
                if (!expect) { Finish(null); return; }
                var scripts = MonoImporter.GetAllRuntimeMonoScripts();
                var settings = (Settings)typeof(Settings).Assembly.GetType("HierarchyDecorator.Editor.HierarchyDecorator")
                    .GetProperty("Settings", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                typeof(Settings).GetProperty("NeedsPresetSelection", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, false);
                var serialized = new SerializedObject(settings);
                serialized.FindProperty("components").FindPropertyRelative("showAll").intValue = (int)DisplayMode.Custom;
                serialized.FindProperty("components").FindPropertyRelative("enableIcons").boolValue = true;
                serialized.FindProperty("components").FindPropertyRelative("stackDuplicateIcons").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                combined = new Texture2D[Names.Length];
                var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("HierarchyDecorator.MALookalikes"), AssemblyBuilderAccess.Run).DefineDynamicModule("Types");
                bool headless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
                for (int i = 0; i < Names.Length; i++)
                {
                    Type type = FindType(Names[i]);
                    Require(type != null, "Real component available: " + Names[i]);
                    MonoScript script = scripts.First(candidate => candidate != null && candidate.GetClass() == type);
                    Texture2D source = AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(script)) as Texture2D;
                    Require(source != null, "Original MA icon available: " + Names[i]);
                    original = source;
                    Type fake = module.DefineType(type.FullName, TypeAttributes.Public).CreateType();
                    Require(Resolve(fake, script) == null, "Unrelated assembly is not recognized: " + Names[i]);
                    Texture2D badgeSource = BadgeSource(i);
                    if (i < 10) Require(badgeSource != null, "Original SDK/Unity icon available: " + Names[i]);
                    Color32[] sourcePixels = headless ? null : Read(source);
                    Color32[] badgeSourcePixels = headless || badgeSource == null ? null : Read(badgeSource);
                    var go = new GameObject((i + 1).ToString("00") + " " + Names[i]);
                    Fixtures.Add(go);
                    var component = new ComponentItem(go.AddComponent(type));
                    if (headless)
                    {
                        Require(Resolve(type, script) == null && component.Content.image == source,
                            "Headless mode keeps the original MA icon: " + Names[i]);
                        continue;
                    }
                    combined[i] = Resolve(type, script);
                    string expected = "MA_" + Badges[i];
                    Require(combined[i] != null && combined[i].name == expected && component.Content.image == combined[i],
                        "Combined icon used: " + Names[i]);
                    Require(component.Content.tooltip == Names[i], "Tooltip retains the component type: " + Names[i]);
                    Require(Resolve(type, script) == combined[i], "Combined icon is cached: " + Names[i]);
                    Require(Read(source).SequenceEqual(sourcePixels) && (badgeSource == null || Read(badgeSource).SequenceEqual(badgeSourcePixels)),
                        "Original MA, SDK and Unity source pixels remain unchanged: " + Names[i]);
                    Color32[] pixels = Read(combined[i]);
                    Require(pixels.Any(pixel => pixel.a == 0), "Combined icon retains transparent background: " + Names[i]);
                    bool ma = false, badge = false;
                    for (int y = 0; y < 64; y++)
                        for (int x = 0; x < 64; x++)
                        {
                            Color32 pixel = pixels[y * 64 + x];
                            if (x < 28 && y > 36 && pixel.a > 100 && pixel.b > pixel.g && pixel.g > pixel.r + 10) ma = true;
                            if (i < 4)
                            {
                                if (x >= 28 && y < 36 && pixel.a > 100 && pixel.r > 100 && pixel.r > pixel.g * 2 && pixel.g > pixel.b) badge = true;
                            }
                            else if (x >= 28 && y < 16 && pixel.a > 50) badge = true;
                        }
                    Save(combined[i], Path.Combine(output, Names[i] + ".png"));
                    Save(source, Path.Combine(output, "MA-source.png"));
                    Require(ma && badge, "Both MA mark and function badge remain visible: " + Names[i] + " MA=" + ma + " badge=" + badge);
                }
                Type other = FindType("ModularAvatarWorldFixedObject");
                MonoScript otherScript = scripts.First(candidate => candidate != null && candidate.GetClass() == other);
                var otherGo = new GameObject("15 Other MA component unchanged");
                Fixtures.Add(otherGo);
                var otherItem = new ComponentItem(otherGo.AddComponent(other));
                Require(Resolve(other, otherScript) == null && otherItem.Content.image == AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(otherScript)),
                    "Other MA component icons remain unchanged");
                if (headless) { Finish(null); return; }
                Require(combined[0] == combined[1] && combined[1] == combined[2] && combined[3] != combined[0],
                    "Three menu components share the menu icon; parameters use a distinct icon");
                Require(combined[6] == combined[7] && combined.Distinct().Count() == 11,
                    "Material components share a material badge; other function badges are distinct");
                if (!AssetDatabase.IsValidFolder("Assets/MAIconProbe")) AssetDatabase.CreateFolder("Assets", "MAIconProbe");
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/MAIconProbe/Fixture.unity");
                settings.styleData.UpdateStyles(EditorGUIUtility.isProSkin);
                hierarchy = CreateHierarchy();
                preview = ScriptableObject.CreateInstance<ModularAvatarIconPreview>();
                preview.Labels = Names;
                preview.Icons = combined;
                preview.ShowUtility();
                preview.position = new Rect(900, 80, 780, 530);
                preview.Repaint();
                EditorApplication.hierarchyWindowItemOnGUI += TrackRow;
                HierarchyManager.Initialize();
                ready = EditorApplication.timeSinceStartup + 3;
                EditorApplication.update += Capture;
            }
            catch (Exception error) { Finish(error); }
        }

        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.GetType("nadena.dev.modular_avatar.core." + name)).FirstOrDefault(type => type != null);
        }
        private static Texture2D Resolve(Type type, MonoScript script) { return (Texture2D)GetIcon.Invoke(null, new object[] { type, script }); }
        private static Texture2D BadgeSource(int index)
        {
            if (index < 4) return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(index == 3
                ? "b54460437079b5a4fb73ecebc0aef4dc" : "576142c89b11bc84e974b3740e480a63"));
            Type type = index == 4 ? typeof(UnityEditor.Animations.AnimatorController)
                : index == 5 ? typeof(AnimationClip) : index < 8 ? typeof(Material) : index == 8 ? typeof(Mesh)
                : index == 9 ? typeof(GameObject) : null;
            return type == null ? null : EditorGUIUtility.ObjectContent(null, type).image as Texture2D;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Checks.Add(message); }
        private static EditorWindow CreateHierarchy()
        {
            var window = ScriptableObject.CreateInstance(typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow")) as EditorWindow;
            window.ShowUtility(); window.position = new Rect(80, 80, 800, 350); window.Focus(); window.Repaint(); return window;
        }
        private static Color32[] Read(Texture icon)
        {
            var target = RenderTexture.GetTemporary(icon.width, icon.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var copy = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, false, true);
            try { Graphics.Blit(icon, target); RenderTexture.active = target; copy.ReadPixels(new Rect(0, 0, icon.width, icon.height), 0, 0); return copy.GetPixels32(); }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(copy); }
        }
        private static void Save(Texture icon, string path)
        {
            // Convert render-space RGB to sRGB for a viewable PNG, retaining alpha.
            Color32[] pixels = Read(icon);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                for (int i = 0; i < pixels.Length; i++) { Color gamma = ((Color)pixels[i]).gamma; gamma.a = pixels[i].a / 255f; pixels[i] = gamma; }
            var copy = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, false); copy.SetPixels32(pixels); copy.Apply();
            File.WriteAllBytes(path, copy.EncodeToPNG()); Object.DestroyImmediate(copy);
        }
        private static void Capture()
        {
            if (EditorApplication.timeSinceStartup < ready) return;
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                if (!expanded)
                {
                    var sceneHierarchy = hierarchy.GetType().GetProperty("sceneHierarchy", flags).GetValue(hierarchy);
                    sceneHierarchy.GetType().GetMethod("SetExpandedRecursive", flags).Invoke(sceneHierarchy,
                        new object[] { UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle, true });
                    expanded = true;
                    Rows.Clear();
                    hierarchy.Repaint();
                    ready = EditorApplication.timeSinceStartup + 1;
                    return;
                }
                EditorApplication.update -= Capture;
                Require(Fixtures.All(go => Rows.Contains(go.GetInstanceID())), "All component rows rendered in the captured Hierarchy");
                Require(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty, "Hierarchy rendering leaves the scene unchanged");
                SaveWindow(hierarchy, Path.Combine(output, "hierarchy.png"));
                SaveWindow(preview, Path.Combine(output, "preview.png"));
                GetIcon.DeclaringType.GetMethod("Clear", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                Require(combined.All(icon => icon == null) && original != null, "Cleanup destroys only composite copies");
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }
        private static void SaveWindow(EditorWindow window, string path)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var view = typeof(EditorWindow).GetField("m_Parent", flags).GetValue(window);
            int width = Mathf.RoundToInt(window.position.width * EditorGUIUtility.pixelsPerPoint), height = Mathf.RoundToInt(window.position.height * EditorGUIUtility.pixelsPerPoint);
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active; var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView").GetMethod("GrabPixels", flags).Invoke(view, new object[] { target, new Rect(0, 0, width, height) });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                var pixels = image.GetPixels32(); var flipped = new Color32[pixels.Length];
                for (int row = 0; row < height; row++) Array.Copy(pixels, row * width, flipped, (height - row - 1) * width, width);
                image.SetPixels32(flipped); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image); }
        }
        private static void TrackRow(int id, Rect rect)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            if (Event.current.type == EventType.Repaint && typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView")
                .GetProperty("current", BindingFlags.Static | BindingFlags.Public).GetValue(null) == typeof(EditorWindow).GetField("m_Parent", flags).GetValue(hierarchy))
                Rows.Add(id);
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Capture;
            EditorApplication.hierarchyWindowItemOnGUI -= TrackRow;
            var report = new Report { success = error == null, error = error?.ToString(), unityVersion = Application.unityVersion, checks = Checks.ToArray() };
            Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            if (hierarchy != null) hierarchy.Close();
            if (preview != null) preview.Close();
            if (error != null) Debug.LogException(error); else Debug.Log("MA_ICON_PROBE_OK " + Checks.Count);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class ModularAvatarIconPreview : EditorWindow
    {
        internal string[] Labels;
        internal Texture2D[] Icons;

        private void OnGUI()
        {
            GUI.Label(new Rect(18, 8, 740, 22), "Modular Avatar component icons — 48 px preview / 16 px actual size", EditorStyles.boldLabel);
            if (Icons == null) return;
            for (int i = 0; i < Icons.Length; i++)
            {
                float x = 12 + (i % 2) * 390, y = 40 + (i / 2) * 69;
                GUI.DrawTexture(new Rect(x + 4, y + 6, 48, 48), Icons[i], ScaleMode.ScaleToFit, true);
                string label = Labels[i].Replace("ModularAvatar", "");
                if (label == "MergeBlendTree") label = "Merge Motion (Blend Tree)";
                GUI.Label(new Rect(x + 68, y + 4, 310, 22), label, EditorStyles.boldLabel);
                GUI.DrawTexture(new Rect(x + 70, y + 32, 16, 16), Icons[i], ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(x + 96, y + 30, 200, 20), "16 px", EditorStyles.miniLabel);
            }
        }
    }
}
