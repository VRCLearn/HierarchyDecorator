using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

#if UNITY_6000_3_OR_NEWER
using HierarchyObjectId = UnityEngine.EntityId;
#else
using HierarchyObjectId = System.Int32;
#endif

namespace HierarchyDecorator.Editor
{
    public enum PrefabInfo { None, Root, Part }

    public static class HierarchyManager
    {
        // --- Scene Data

        private static Dictionary<HierarchyObjectId, HierarchyItem> lookup = new Dictionary<HierarchyObjectId, HierarchyItem>();
        public static IReadOnlyDictionary<HierarchyObjectId, HierarchyItem> Items => lookup;
        public static int Count => lookup.Count;
        private static Settings s_Settings => HierarchyDecorator.Settings;

        public static HierarchyItem Current { get; private set; }
        public static HierarchyItem Previous { get; private set; }

        // --- Drawers 

        private static HierarchyDrawer[] Drawers = new HierarchyDrawer[]
        {
            new StyleDrawer(),
        };

        private static HierarchyDrawer[] OverlayDrawers = new HierarchyDrawer[]
        {
            new StateDrawer(),
            new ToggleDrawer(),
            new BreadcrumbsDrawer()
        };

        private static HierarchyInfo[] Info = new HierarchyInfo[]
        {
            new TagLayerInfo(),
            new ComponentIconInfo()
        };

        // --- Methods

        public static void Initialize()
        {
            lookup.Clear();


#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnGUI;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= OnGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
#endif

            EditorSceneManager.sceneOpened -= OnSceneOpen;
            EditorSceneManager.sceneOpened += OnSceneOpen;
            EditorSceneManager.sceneClosed -= OnSceneClose;
            EditorSceneManager.sceneClosed += OnSceneClose;

            EditorApplication.hierarchyChanged -= OnHierarchyChange;
            EditorApplication.hierarchyChanged += OnHierarchyChange;
        }

        private static void OnHierarchyChange()
        {
            if (lookup.Count < 100)
            {
                return;
            }

            ResetLookup();
        }

        private static void OnSceneOpen(Scene scene, OpenSceneMode mode)
        {
            ResetLookup();
        }

        private static void OnSceneClose(Scene scene)
        {
            ResetLookup();
        }

        private static void ResetLookup()
        {
            lookup.Clear();
            Current = null;
        }

        // - GUI

#if UNITY_6000_4_OR_NEWER
        public static void OnGUI(EntityId id, Rect rect)
#else
        public static void OnGUI(int id, Rect rect)
#endif
        {
            if (EditorApplication.isUpdating)
            {
                return;
            }

            if (!TryGetValidInstance(id, out HierarchyItem item))
            {
                DrawSceneItemHighlight(rect, id);
                return;
            }

            // Need to check IsValid() as the last GO in hierarchy doees not call OnHierarchyChange() in time
            
            if (Current != null && Current.IsValid())
            {
                Current.OnGUIEnd(item);
                Previous = Current;
            }

            Current = item;
            Current.OnGUIBegin();

            DrawItem(rect, item, s_Settings);
        }

        private static void DrawItem(Rect rect, HierarchyItem item, Settings settings)
        {
#if UNITY_2019_1_OR_NEWER
            rect.height = 16f;

            // Unity draws prefab override markers in the first two pixels after
            // the visibility column. Preserve that native margin for every row,
            // including selection overlays, toggles and custom header styles.
            Rect contentRect = rect;
            contentRect.xMin = 34f;
            contentRect.xMax += rect.height;
            // Keep the existing viewport's vertical clipping and text overflow.
            contentRect.yMin = 0f;
            contentRect.height = float.MaxValue;
            GUI.BeginClip(contentRect, -contentRect.position, Vector2.zero, false);
#endif
            try
            {
                for (int i = 0; i < Drawers.Length; i++)
                {
                    Drawers[i].Draw(rect, item, settings);
                }

                for (int i = 0; i < Info.Length; i++)
                {
                    Info[i].Draw(rect, item, settings);
                }

                GameObjectIconWindow.HandleRowInput(rect, item.GameObject);

                for (int i = 0; i < OverlayDrawers.Length; i++)
                {
                    OverlayDrawers[i].Draw(rect, item, settings);
                }
            }
            finally
            {
#if UNITY_2019_1_OR_NEWER
                GUI.EndClip();
#endif
                HierarchyInfo.ResetIndent();
            }
        }

        private static bool TryGetValidInstance(HierarchyObjectId id, out HierarchyItem item)
        {
            if (lookup.TryGetValue(id, out item) && item.IsValid())
            {
                // Adding uGUI components can replace the Transform.
                if (item.Transform == null)
                    item.Transform = item.GameObject.transform;
                return true;
            }

#if UNITY_6000_3_OR_NEWER
            GameObject instance = EditorUtility.EntityIdToObject(id) as GameObject;
#else
            GameObject instance = EditorUtility.InstanceIDToObject(id) as GameObject;
#endif

            if (instance == null)
            {
                item = null;
                return false;
            }

            item = new HierarchyItem(id, instance);
            lookup[id] = item;
            return true;
        }

        private static void DrawSceneItemHighlight(Rect rect, HierarchyObjectId id)
        {
            if (Event.current.type != EventType.Repaint || !s_Settings.styleData.showSceneItemHighlight)
                return;
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                var scene = SceneManager.GetSceneAt(i);
#if UNITY_6000_4_OR_NEWER
                if (scene.handle.GetRawData() == EntityId.ToULong(id))
#else
                if (scene.handle == id)
#endif
                {
                    var max_height = rect.height;
                    var center_y = max_height * 0.5f;
                    var thickness = s_Settings.styleData.sceneItemHighlight.lineThickness;
                    rect.x -= 48;
                    rect.y += center_y - thickness * 0.5f;
                    rect.width = 34;
                    rect.height = thickness;
                    EditorGUI.DrawRect(rect, s_Settings.styleData.sceneItemHighlight.color);
                    break;
                }
            }
        }

        public static bool IsPreviousParent()
        {
            if (Previous == null)
            {
                return false;
            }

            return Current.Transform.parent == Previous.Transform;
        }
    }

    public class ComponentList
    {
        private static readonly List<Component> currentComponents = new List<Component>();
        private static readonly Dictionary<Component, ComponentItem> previousComponents = new Dictionary<Component, ComponentItem>();
        private readonly List<ComponentItem> items = new List<ComponentItem>();

        internal int Count => items.Count;
        internal ComponentItem this[int index] => items[index];

        public ComponentList(GameObject instance)
        {
            Refresh(instance);
        }

        public void Validate(GameObject instance)
        {
            Refresh(instance);
            RefreshActiveStates();
        }

        internal void RefreshActiveStates()
        {
            for (int i = 0; i < items.Count; i++)
                items[i].UpdateActiveState();
        }

        internal void Refresh(GameObject instance)
        {
            instance.GetComponents(currentComponents);
            try
            {
                int unchanged = 0;
                int sharedCount = Mathf.Min(items.Count, currentComponents.Count);
                for (; unchanged < sharedCount; unchanged++)
                {
                    ComponentItem item = items[unchanged];
                    Component component = currentComponents[unchanged];
                    if (!ReferenceEquals(item.Component, component) || (item.IsNullComponent && !ReferenceEquals(component, null)))
                        break;
                }

                if (unchanged != items.Count || unchanged != currentComponents.Count)
                    UpdateCache(unchanged);
            }
            finally
            {
                currentComponents.Clear();
            }
        }

        private void UpdateCache(int unchanged)
        {
            for (int i = unchanged; i < items.Count; i++)
            {
                ComponentItem item = items[i];
                if (item.Component != null && item.IsValid())
                    previousComponents[item.Component] = item;
            }

            items.RemoveRange(unchanged, items.Count - unchanged);
            try
            {
                for (int i = unchanged; i < currentComponents.Count; i++)
                {
                    Component component = currentComponents[i];
                    if (component == null || !previousComponents.TryGetValue(component, out ComponentItem item))
                        item = new ComponentItem(component);

                    items.Add(item);
                }
            }
            finally
            {
                previousComponents.Clear();
            }
        }

        public IEnumerable<ComponentItem> GetItems()
        {
            for (int i = 0; i < items.Count; i++)
            {
                yield return items[i];
            }
        }
    }
}
