using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_2019_1_OR_NEWER
using UnityEditor.UIElements;
using UnityEngine.UIElements;
#endif

#if UNITY_6000_3_OR_NEWER
using HierarchyObjectId = UnityEngine.EntityId;
#else
using HierarchyObjectId = System.Int32;
#endif

namespace HierarchyDecorator.Editor
{
#if UNITY_2019_1_OR_NEWER
    [InitializeOnLoad]
#endif
    internal static class HierarchyBranchMenu
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type HierarchyWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
        private static readonly PropertyInfo SceneHierarchy = HierarchyWindowType?.GetProperty("sceneHierarchy", InstanceMembers);
        private static readonly MethodInfo ExpandItem = SceneHierarchy?.PropertyType.GetMethod("ExpandTreeViewItem", InstanceMembers, null,
            new[] { typeof(HierarchyObjectId), typeof(bool) }, null);

#if UNITY_2019_1_OR_NEWER
        private const string ToolbarButtonName = "hierarchy-decorator-collapse-branches";
        private const string ToolbarTooltip = "Click to collapse other branches. Double-click to collapse all.";
        private const float SearchToolbarWidth = 330f;
        private const float ButtonWidth = 22f;
        private const float SceneButtonWidth = HierarchySceneMenu.ButtonWidth;
        private const int IconSize = 16;
        private static readonly Func<IList> GetHierarchyWindows = BindHierarchyWindowList();
        private static readonly MethodInfo CollapseAll = SceneHierarchy?.PropertyType.GetMethod("CollapseAll", InstanceMembers, null, Type.EmptyTypes, null);
        private static readonly ConditionalWeakTable<VisualElement, ToolbarControls> Toolbars = new ConditionalWeakTable<VisualElement, ToolbarControls>();
        private static double nextToolbarUpdate;
        private static Texture2D collapseIcon;

        private sealed class ToolbarControls
        {
            public ToolbarButton Collapse;
            public ToolbarButton Scene;
            public IMGUIContainer NativeToolbar;
            public StyleLength NativeMarginTop;
        }

        static HierarchyBranchMenu()
        {
            if (GetHierarchyWindows == null)
                return;

            EditorApplication.update += UpdateToolbars;
            SettingsLocalization.LanguageChanged += UpdateToolbarTooltips;
        }

        private static Func<IList> BindHierarchyWindowList()
        {
            MethodInfo method = HierarchyWindowType?.GetMethod("GetAllSceneHierarchyWindows", BindingFlags.Static | BindingFlags.Public);
            return method == null ? null : Delegate.CreateDelegate(typeof(Func<IList>), method, false) as Func<IList>;
        }

        private static void UpdateToolbars()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextToolbarUpdate)
                return;

            // Unity provides no public creation event for these windows.
            nextToolbarUpdate = now + 0.25;
            IList windows = GetHierarchyWindows();
            if (windows.Count == 0)
                return;

            GlobalData settings = HierarchyDecorator.Settings.globalData;
            bool showCollapse = settings.showCollapseBranchesButton && ExpandItem != null;
            bool showScene = settings.showSceneSwitchButton;
            bool canCollapse = showCollapse && (CollapseAll != null || CanCollapse(Selection.activeGameObject));
            bool canSwitch = showScene && HierarchySceneMenu.CanSwitch;
            for (int i = 0; i < windows.Count; i++)
            {
                var window = (EditorWindow)windows[i];
                VisualElement root = window.rootVisualElement;
                if (root.panel == null)
                    continue;

                if (!Toolbars.TryGetValue(root, out ToolbarControls controls))
                {
                    if (!showCollapse && !showScene)
                        continue;

                    controls = new ToolbarControls();
                    Toolbars.Add(root, controls);
                    root.RegisterCallback<GeometryChangedEvent>(OnToolbarGeometryChanged);
                }

                bool reposition = false;
                if (showCollapse && controls.Collapse?.parent != root)
                {
                    controls.Collapse = root.Q<ToolbarButton>(ToolbarButtonName) ?? CreateToolbarButton(window, root);
                    controls.Collapse.tooltip = SettingsLocalization.Text(ToolbarTooltip);
                    reposition = true;
                }

                if (showScene && controls.Scene?.parent != root)
                {
                    controls.Scene = HierarchySceneMenu.CreateButton();
                    root.Add(controls.Scene);
                    reposition = true;
                }

                reposition |= UpdateToolbarButton(controls.Collapse, showCollapse, canCollapse);
                reposition |= UpdateToolbarButton(controls.Scene, showScene, canSwitch);
                if (reposition)
                    PositionToolbar(root, controls, root.layout);
            }
        }

        private static bool UpdateToolbarButton(ToolbarButton button, bool show, bool enabled)
        {
            if (button == null)
                return false;

            if (button.enabledSelf != enabled)
                button.SetEnabled(enabled);

            DisplayStyle display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (button.style.display.value == display)
                return false;

            button.style.display = display;
            return true;
        }

        private static ToolbarButton CreateToolbarButton(EditorWindow window, VisualElement root)
        {
            int clickCount = 1;
            var button = new ToolbarButton(() =>
            {
                if (clickCount == 2)
                    CollapseToSceneRoots(window);
                else if (clickCount <= 1)
                    CollapseOtherBranches(window, Selection.activeGameObject);
            })
            {
                name = ToolbarButtonName,
                focusable = false
            };
            button.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                    clickCount = evt.clickCount;
            }, TrickleDown.TrickleDown);
            button.style.position = Position.Absolute;
            button.style.width = ButtonWidth;
            button.style.height = 18f;
            button.style.marginLeft = button.style.marginRight = 0f;
            button.style.paddingLeft = button.style.paddingRight = 0f;
            button.style.borderLeftWidth = button.style.borderRightWidth = 0f;
            button.style.borderTopWidth = button.style.borderBottomWidth = 0f;
            button.style.backgroundColor = Color.clear;
            button.style.justifyContent = Justify.Center;
            button.style.alignItems = Align.Center;
            var icon = new Image
            {
                image = GetCollapseIcon(),
                tintColor = EditorGUIUtility.isProSkin ? new Color(0.70f, 0.70f, 0.70f) : new Color(0.30f, 0.30f, 0.30f),
                pickingMode = PickingMode.Ignore
            };
            icon.style.width = icon.style.height = IconSize;
            button.Add(icon);
            button.RegisterCallback<MouseEnterEvent>(_ => button.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.16f));
            button.RegisterCallback<MouseLeaveEvent>(_ => button.style.backgroundColor = Color.clear);
            root.Add(button);
            return button;
        }

        private static void OnToolbarGeometryChanged(GeometryChangedEvent evt)
        {
            var root = (VisualElement)evt.currentTarget;
            if (Toolbars.TryGetValue(root, out ToolbarControls controls))
                PositionToolbar(root, controls, evt.newRect);
        }

        private static void PositionToolbar(VisualElement root, ToolbarControls controls, Rect layout)
        {
            const float buttonGap = 2f;
            bool showScene = IsVisible(controls.Scene, root);
            bool showCollapse = IsVisible(controls.Collapse, root);
            bool narrow = layout.width < 420f;
            bool useTabStrip = narrow && layout.y >= 18f;
            SetNativeToolbarInset(root, controls, (showScene || showCollapse) && narrow && !useTabStrip);

            float width = (showScene ? SceneButtonWidth : 0f) + (showCollapse ? ButtonWidth : 0f);
            if (showScene && showCollapse)
                width += buttonGap;

            float top = useTabStrip ? -18f : 1f;
            float rightInset = useTabStrip ? 48f : SearchToolbarWidth;
            float right = Mathf.Max(39f + SceneButtonWidth + buttonGap + ButtonWidth, layout.width - rightInset);
            float left = right - width;
            if (showScene)
            {
                controls.Scene.style.top = top;
                controls.Scene.style.left = left;
                left += SceneButtonWidth + buttonGap;
            }

            if (showCollapse)
            {
                controls.Collapse.style.top = top;
                controls.Collapse.style.left = left;
            }
        }

        private static bool IsVisible(ToolbarButton button, VisualElement root) =>
            button?.parent == root && button.style.display.value != DisplayStyle.None;

        private static void SetNativeToolbarInset(VisualElement root, ToolbarControls controls, bool reserveSpace)
        {
            // A narrow floating window has no tab strip, so reserve a row above its native controls.
            if (controls.NativeToolbar != null && (!reserveSpace || controls.NativeToolbar.parent != root.parent))
            {
                controls.NativeToolbar.style.marginTop = controls.NativeMarginTop;
                controls.NativeToolbar = null;
            }

            if (!reserveSpace || controls.NativeToolbar != null || root.parent == null)
                return;

            foreach (VisualElement sibling in root.parent.Children())
            {
                if (sibling is IMGUIContainer nativeToolbar)
                {
                    controls.NativeToolbar = nativeToolbar;
                    controls.NativeMarginTop = nativeToolbar.style.marginTop;
                    float baseMargin = nativeToolbar.resolvedStyle.marginTop;
                    nativeToolbar.style.marginTop = float.IsNaN(baseMargin) ? 20f : baseMargin + 20f;
                    break;
                }
            }
        }

        private static void UpdateToolbarTooltips()
        {
            string collapseTooltip = SettingsLocalization.Text(ToolbarTooltip);
            string sceneTooltip = SettingsLocalization.Text("Switch Scene");
            IList windows = GetHierarchyWindows();
            for (int i = 0; i < windows.Count; i++)
            {
                VisualElement root = ((EditorWindow)windows[i]).rootVisualElement;
                if (Toolbars.TryGetValue(root, out ToolbarControls controls))
                {
                    if (controls.Collapse != null)
                        controls.Collapse.tooltip = collapseTooltip;
                    if (controls.Scene != null)
                        controls.Scene.tooltip = sceneTooltip;
                }
            }
        }

        private static void CollapseToSceneRoots(EditorWindow window)
        {
            if (window == null || CollapseAll == null || ExpandItem == null)
                return;

            object hierarchy = SceneHierarchy.GetValue(window);
            if (hierarchy == null)
                return;

            CollapseAll.Invoke(hierarchy, null);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

#if UNITY_6000_4_OR_NEWER
                HierarchyObjectId id = EntityId.FromULong(scene.handle.GetRawData());
#elif UNITY_6000_3_OR_NEWER
                HierarchyObjectId id = (int)scene.handle;
#else
                HierarchyObjectId id = scene.handle;
#endif
                ExpandItem.Invoke(hierarchy, new object[] { id, true });
            }
            window.Repaint();
        }

        private static Texture2D GetCollapseIcon()
        {
            if (collapseIcon != null)
                return collapseIcon;

            // The native CreateAddNew icon has a 12-point glyph with 2-point strokes.
            const int size = IconSize * 2;
            const float strokeRadius = 2f;
            collapseIcon = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            var barStart = new Vector2(6f, 26f);
            var barEnd = new Vector2(26f, 26f);
            var stemStart = new Vector2(16f, 6f);
            var tip = new Vector2(16f, 20f);
            var leftWing = new Vector2(9f, 13f);
            var rightWing = new Vector2(23f, 13f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    float bar = DistanceToStroke(point, barStart, barEnd);
                    float stem = DistanceToStroke(point, stemStart, tip);
                    float left = DistanceToStroke(point, leftWing, tip);
                    float right = DistanceToStroke(point, rightWing, tip);
                    float distance = Mathf.Min(Mathf.Min(bar, stem), Mathf.Min(left, right));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(strokeRadius + 0.5f - distance));
                }
            }
            collapseIcon.SetPixels(pixels);
            collapseIcon.Apply(false, true);
            return collapseIcon;
        }

        private static float DistanceToStroke(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 stroke = end - start;
            float along = Mathf.Clamp01(Vector2.Dot(point - start, stroke) / stroke.sqrMagnitude);
            return Vector2.Distance(point, start + along * stroke);
        }
#endif

        private static bool CanCollapse(GameObject target) => ExpandItem != null && target != null && target.scene.isLoaded;

        private static void CollapseOtherBranches(EditorWindow window, GameObject target)
        {
            if (window == null || !CanCollapse(target))
                return;

            object hierarchy = SceneHierarchy.GetValue(window);
            if (hierarchy == null)
                return;

            var path = new Stack<Transform>();
            for (Transform current = target.transform; current != null; current = current.parent)
                path.Push(current);

            Transform root = path.Peek();
            foreach (GameObject sceneRoot in target.scene.GetRootGameObjects())
            {
                if (sceneRoot != root.gameObject)
                    SetExpanded(hierarchy, sceneRoot, false);
            }

            while (path.Count > 0)
            {
                Transform current = path.Pop();
                SetExpanded(hierarchy, current.gameObject, true);
                if (path.Count == 0)
                    break;

                Transform next = path.Peek();
                foreach (Transform sibling in current)
                {
                    if (sibling != next)
                        SetExpanded(hierarchy, sibling.gameObject, false);
                }
            }

            foreach (Transform child in target.transform)
                SetExpanded(hierarchy, child.gameObject, false);

            window.Repaint();
        }

        private static void SetExpanded(object hierarchy, GameObject target, bool expanded)
        {
            if (target.transform.childCount == 0)
                return;

#if UNITY_6000_3_OR_NEWER
            HierarchyObjectId id = target.GetEntityId();
#else
            HierarchyObjectId id = target.GetInstanceID();
#endif
            ExpandItem.Invoke(hierarchy, new object[] { id, expanded });
        }
    }
}
