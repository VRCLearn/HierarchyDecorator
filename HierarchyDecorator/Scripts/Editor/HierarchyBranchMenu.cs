using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        private const string ActionName = "Collapse Other Branches";
        private const string MenuPath = "Tools/HierarchyDecorator/" + ActionName;
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type HierarchyWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
        private static readonly PropertyInfo SceneHierarchy = HierarchyWindowType?.GetProperty("sceneHierarchy", InstanceMembers);
        private static readonly PropertyInfo LastHierarchy = HierarchyWindowType?.GetProperty("lastInteractedHierarchyWindow", BindingFlags.Static | BindingFlags.Public);
        private static readonly MethodInfo ExpandItem = SceneHierarchy?.PropertyType.GetMethod("ExpandTreeViewItem", InstanceMembers, null,
            new[] { typeof(HierarchyObjectId), typeof(bool) }, null);

#if UNITY_2019_1_OR_NEWER
        private const string ToolbarButtonName = "hierarchy-decorator-collapse-branches";
        private const float SearchToolbarWidth = 330f;
        private const float ButtonWidth = 22f;
        private const int IconSize = 16;
        private static readonly MethodInfo GetHierarchyWindows = HierarchyWindowType?.GetMethod("GetAllSceneHierarchyWindows", BindingFlags.Static | BindingFlags.Public);
        private static readonly MethodInfo CollapseAll = SceneHierarchy?.PropertyType.GetMethod("CollapseAll", InstanceMembers, null, Type.EmptyTypes, null);
        private static double nextToolbarUpdate;
        private static Texture2D collapseIcon;

        static HierarchyBranchMenu()
        {
            EditorApplication.update += UpdateToolbars;
        }

        private static void UpdateToolbars()
        {
            if (EditorApplication.timeSinceStartup < nextToolbarUpdate || ExpandItem == null || GetHierarchyWindows == null)
                return;

            nextToolbarUpdate = EditorApplication.timeSinceStartup + 0.25;
            foreach (EditorWindow window in (IEnumerable)GetHierarchyWindows.Invoke(null, null))
            {
                VisualElement root = window.rootVisualElement;
                if (root.panel == null)
                    continue;

                ToolbarButton button = root.Q<ToolbarButton>(ToolbarButtonName);
                if (button == null)
                {
                    int clickCount = 1;
                    button = new ToolbarButton(() =>
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
                }

                // The classic search field can take the full toolbar width in a narrow dock.
                // Use the tab strip there so the search field and Create menu both remain clickable.
                bool useTabStrip = root.layout.width < 420f && root.layout.y >= 18f;
                button.style.top = useTabStrip ? -18f : 1f;
                float rightInset = useTabStrip ? 48f : SearchToolbarWidth;
                button.style.left = Mathf.Max(37f, root.layout.width - rightInset - ButtonWidth);
                button.tooltip = SettingsLocalization.Text("Click to collapse other branches. Double-click to collapse all.");
                button.SetEnabled(CollapseAll != null || CanCollapse(Selection.activeGameObject));
            }
        }

        private static void CollapseToSceneRoots(EditorWindow window)
        {
            if (window == null || CollapseAll == null)
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

        [MenuItem(MenuPath)]
        private static void CollapseSelectedBranch() => CollapseOtherBranches(GetHierarchyWindow(), Selection.activeGameObject);

        [MenuItem(MenuPath, true)]
        private static bool ValidateCollapseSelectedBranch() => CanCollapse(Selection.activeGameObject) && GetHierarchyWindow() != null;

        private static bool CanCollapse(GameObject target) => ExpandItem != null && target != null && target.scene.isLoaded;

        internal static void HandleRowShortcut(Rect row, GameObject target)
        {
            Event evt = Event.current;
            if (evt.type != EventType.KeyDown || evt.keyCode != KeyCode.E || !evt.shift ||
                evt.alt || evt.control || evt.command || EditorGUIUtility.editingTextField)
                return;

            row.xMin = 0f;
            if (!row.Contains(evt.mousePosition) || !CanCollapse(target))
                return;

            EditorWindow window = EditorWindow.focusedWindow;
            if (window == null || HierarchyWindowType?.IsInstanceOfType(window) != true)
                return;

            evt.Use();
            // Updating Unity's tree while it draws the rows can interrupt iteration.
            EditorApplication.delayCall += () => CollapseOtherBranches(window, target);
        }

        private static EditorWindow GetHierarchyWindow()
        {
            EditorWindow focused = EditorWindow.focusedWindow;
            return focused != null && HierarchyWindowType?.IsInstanceOfType(focused) == true
                ? focused : LastHierarchy?.GetValue(null) as EditorWindow;
        }

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
