
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Object = UnityEngine.Object;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Editor
{
    public class ComponentIconInfo : HierarchyInfo
    {
        private static readonly Color s_DisabledColor = new Color(1f, 1f, 1f, 0.4f);
        private readonly Dictionary<Texture, List<ComponentItem>> stackedComponents = new Dictionary<Texture, List<ComponentItem>>();
        private readonly List<ComponentItem> items = new List<ComponentItem>();

        private bool requireWarning;
        private int iconLimit;
        private bool stackIcons;

#if UNITY_2019_1_OR_NEWER
        private readonly GUIContent warningGUI = EditorGUIUtility.IconContent("warning");
#else
        private readonly GUIContent warningGUI = EditorGUIUtility.IconContent("console.warnicon");
#endif
        protected override void OnDrawInit(HierarchyItem item, Settings settings)
        {
            requireWarning = false;
            iconLimit = settings.Components.ComponentIconLimit;
            stackIcons = settings.Components.StackScripts;
            items.Clear();
            stackedComponents.Clear();

            foreach (ComponentItem component in item.Components.GetItems())
            {
                if (component.IsNullComponent && settings.Components.ShowMissingScriptWarning)
                {
                    requireWarning = true;
                    continue;
                }

                if (!CanShow(component, settings))
                {
                    continue;
                }

                Texture texture = stackIcons ? component.Content.image : null;
                if (texture != null)
                {
                    if (stackedComponents.TryGetValue(texture, out List<ComponentItem> group))
                    {
                        group.Add(component);
                        continue;
                    }

                    stackedComponents.Add(texture, new List<ComponentItem> { component });
                }
                items.Add(component);
            }
        }

        protected override int CalculateGridCount()
        {
            int visible = iconLimit > 0 ? Mathf.Min(items.Count, iconLimit) : items.Count;
            int hidden = items.Count - visible;
            int overflowSlots = hidden > 0 ? OverflowSlots(hidden) : 0;
            return visible + overflowSlots + (requireWarning ? 1 : 0);
        }

        protected override bool DrawerIsEnabled(HierarchyItem item, Settings settings)
        {
            if (!settings.styleData.displayIcons && settings.styleData.HasStyle(item.DisplayName))
            {
                return false;
            }

            return settings.Components.Enabled;
        }

        protected override void DrawInfo(Rect rect, HierarchyItem item, Settings settings)
        {
            int available = GridCount - (requireWarning ? 1 : 0);
            int visible = GetVisibleIconCount(available);
            rect = GetIconPosition(rect);

            for (int i = 0; i < visible; i++)
            {
                ComponentItem component = items[i];
                List<ComponentItem> group = GetStack(component);
                if (group != null)
                {
                    var content = new GUIContent(string.Empty, component.Content.image,
                        string.Join("\n", group.Select(member => member.DisplayName)));
                    DrawStackedComponentIcon(rect, content, group, settings);
                }
                else
                {
                    DrawComponentIcon(rect, component, settings);
                }
                rect.x -= INDENT_SIZE;
            }

            int hidden = items.Count - visible;
            if (iconLimit > 0 && hidden > 0 && available > visible)
            {
                int slots = Mathf.Min(OverflowSlots(hidden), available - visible);
                Rect overflowRect = rect;
                overflowRect.xMin -= (slots - 1) * INDENT_SIZE;
                DrawOverflow(overflowRect, visible, hidden, slots);
                rect.x -= slots * INDENT_SIZE;
            }

            if (requireWarning)
            {
                DrawIcon(rect, warningGUI);
            }
        }

        private int GetVisibleIconCount(int available)
        {
            int visible = Mathf.Min(available, items.Count);
            if (iconLimit == 0)
            {
                return visible;
            }

            visible = Mathf.Min(visible, iconLimit);
            while (visible > 0 && visible < items.Count && visible + OverflowSlots(items.Count - visible) > available)
            {
                visible--;
            }
            return visible;
        }

        private static int OverflowSlots(int hidden) => hidden > 9 ? 2 : 1;

        private List<ComponentItem> GetStack(ComponentItem component)
        {
            Texture texture = stackIcons ? component.Content.image : null;
            return texture != null && stackedComponents.TryGetValue(texture, out List<ComponentItem> group)
                ? group : null;
        }

        private void DrawOverflow(Rect rect, int visible, int hidden, int slots)
        {
            string count = "+" + hidden;
            string label = slots < OverflowSlots(hidden) ? "…" : count;
            if (!rect.Contains(Event.current.mousePosition))
            {
                GUI.Label(rect, label, EditorStyles.miniLabel);
                return;
            }

            var tooltip = new StringBuilder(count);
            for (int i = visible; i < items.Count; i++)
            {
                ComponentItem component = items[i];
                List<ComponentItem> group = GetStack(component);
                if (group == null)
                {
                    tooltip.Append('\n').Append(component.DisplayName);
                    continue;
                }

                foreach (ComponentItem member in group)
                {
                    tooltip.Append('\n').Append(member.DisplayName);
                }
            }

            GUI.Label(rect, new GUIContent(label, tooltip.ToString()), EditorStyles.miniLabel);
        }

        // GUI

        private void DrawStackedComponentIcon(Rect rect, GUIContent content, List<ComponentItem> components, Settings settings)
        {
            ComponentItem firstToggleable = null;
            if (settings.Components.ClickToToggleComponent)
            {
                firstToggleable = components.Find(component => component.HasToggle);
            }

            if (firstToggleable == null)
            {
                DrawIcon(rect, content);
                return;
            }

            Event ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
            {
                bool targetState = !firstToggleable.Active;
                foreach (ComponentItem component in components)
                {
                    component.SetActive(targetState);
                }
                ev.Use();
            }

            DrawActiveIcon(rect, content, firstToggleable.Active);
        }

        private void DrawComponentIcon(Rect rect, ComponentItem item, Settings settings)
        {
            DrawContextMenu(rect, item);
            
            if (item.HasToggle && settings.Components.ClickToToggleComponent)
            {
                DrawComponentToggle(rect, item);
            }
            else
            {
                DrawIcon(rect, item.Content);
            }
        }

        private void DrawComponentToggle(Rect rect, ComponentItem item)
        {
            Event ev = Event.current;

            if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
            {
                item.ToggleActive();
                ev.Use();
            }

            DrawActiveIcon(rect, item.Content, item.Active);
        }

        private void DrawActiveIcon(Rect rect, GUIContent content, bool active)
        {
            Color previousColor = GUI.color;
            try
            {
                if (!active)
                {
                    GUI.color *= s_DisabledColor;
                }
                DrawIcon(rect, content);
            }
            finally { GUI.color = previousColor; }
        }

        private void DrawIcon(Rect rect, GUIContent content)
        {
            GUI.Label(rect, content, Style.ComponentIconStyle);
        }

        private void DrawContextMenu(Rect rect, ComponentItem item)
        {
            Event ev = Event.current;

            if (ev.type == EventType.MouseDown && ev.button == 1 && rect.Contains(ev.mousePosition))
            {
                var method = typeof(EditorUtility).GetMethod("DisplayObjectContextMenu", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(Rect), typeof(Object), typeof(int) }, null);
                method.Invoke(null, new object[] { rect, item.Component, 0 });
                ev.Use();
            }
        }

        // Helpers

        private bool CanShow(ComponentItem item, Settings settings)
        {
            if (item.IsNullComponent || item.Type.Excluded)
            {
                return false;
            }

            bool shown = item.Type.Shown;

            if (item.IsBuiltIn)
            {
                return settings.Components.DisplayBuiltIn || shown;
            }

            return settings.Components.DisplayMonoScripts || shown;
        }

        private Rect GetIconPosition(Rect rect)
        {
            rect.x += rect.width - INDENT_SIZE;
            rect.width = INDENT_SIZE;
            return rect;
        }
    }
}
