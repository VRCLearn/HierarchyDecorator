
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
        private readonly List<List<ComponentItem>> stackGroups = new List<List<ComponentItem>>();
        private readonly List<ComponentItem> items = new List<ComponentItem>();
        private readonly GUIContent iconContent = new GUIContent();
        private readonly StringBuilder tooltip = new StringBuilder();

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
            foreach (List<ComponentItem> group in stackedComponents.Values)
                group.Clear();
            stackedComponents.Clear();

            ComponentList components = item.GetComponentsForDrawing();
            for (int i = 0; i < components.Count; i++)
            {
                ComponentItem component = components[i];
                if (component.IsNullComponent && settings.Components.ShowMissingScriptWarning)
                {
                    requireWarning = true;
                    continue;
                }

                ComponentType type = component.Type;
                if (type == null || type.Excluded || !CanShow(type, settings.Components))
                {
                    continue;
                }

                Texture texture = stackIcons ? type.Content.image : null;
                if (texture != null)
                {
                    if (stackedComponents.TryGetValue(texture, out List<ComponentItem> group))
                    {
                        group.Add(component);
                        continue;
                    }

                    int groupIndex = stackedComponents.Count;
                    if (groupIndex == stackGroups.Count)
                        stackGroups.Add(new List<ComponentItem>());
                    group = stackGroups[groupIndex];
                    group.Add(component);
                    stackedComponents.Add(texture, group);
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
            EventType eventType = Event.current.type;
            if (!settings.Components.Enabled || (eventType != EventType.Repaint && eventType != EventType.MouseDown))
                return false;

            if (!settings.styleData.displayIcons && item.GetRowStyle(settings.styleData) != null)
            {
                return false;
            }

            return true;
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
                    iconContent.image = component.Content.image;
                    iconContent.text = string.Empty;
                    iconContent.tooltip = GetStackTooltip(rect, group);
                    DrawStackedComponentIcon(rect, iconContent, group, settings);
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

        private string GetStackTooltip(Rect rect, List<ComponentItem> group)
        {
            Event evt = Event.current;
            if (evt.type != EventType.Repaint || !rect.Contains(evt.mousePosition))
                return null;

            tooltip.Clear();
            tooltip.Append(group[0].DisplayName);
            for (int i = 1; i < group.Count; i++)
                tooltip.Append('\n').Append(group[i].DisplayName);
            return tooltip.ToString();
        }

        private void DrawOverflow(Rect rect, int visible, int hidden, int slots)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            string count = "+" + hidden;
            string label = slots < OverflowSlots(hidden) ? "…" : count;
            if (!rect.Contains(Event.current.mousePosition))
            {
                GUI.Label(rect, label, EditorStyles.miniLabel);
                return;
            }

            tooltip.Clear();
            tooltip.Append(count);
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

            iconContent.image = null;
            iconContent.text = label;
            iconContent.tooltip = tooltip.ToString();
            GUI.Label(rect, iconContent, EditorStyles.miniLabel);
        }

        // GUI

        private void DrawStackedComponentIcon(Rect rect, GUIContent content, List<ComponentItem> components, Settings settings)
        {
            if (IsInspectClick(rect))
            {
                Event.current.Use();
                if (components.Count == 1)
                    ComponentInspectorWindow.Open(components[0].Component, ComponentInspectorWindow.GetAnchor(rect));
                else
                    ShowInspectorMenu(rect, components);
            }

            ComponentItem firstToggleable = null;
            int enabledState = -1;
            if (settings.Components.ClickToToggleComponent)
            {
                foreach (ComponentItem component in components)
                {
                    enabledState = component.GetEnabledState();
                    if (enabledState != -1)
                    {
                        firstToggleable = component;
                        break;
                    }
                }
            }

            if (firstToggleable == null)
            {
                DrawIcon(rect, content);
                return;
            }

            Event ev = Event.current;
            bool active = enabledState != 0;
            if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
            {
                foreach (ComponentItem component in components)
                {
                    component.SetActive(!active);
                }
                active = firstToggleable.Active;
                ev.Use();
            }

            DrawActiveIcon(rect, content, active);
        }

        private void DrawComponentIcon(Rect rect, ComponentItem item, Settings settings)
        {
            if (IsInspectClick(rect))
            {
                Event.current.Use();
                ComponentInspectorWindow.Open(item.Component, ComponentInspectorWindow.GetAnchor(rect));
            }

            DrawContextMenu(rect, item);
            
            int enabledState = settings.Components.ClickToToggleComponent ? item.GetEnabledState() : -1;
            if (enabledState != -1)
            {
                DrawComponentToggle(rect, item, enabledState != 0);
            }
            else
            {
                DrawIcon(rect, item.Content);
            }
        }

        private void DrawComponentToggle(Rect rect, ComponentItem item, bool active)
        {
            Event ev = Event.current;

            if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
            {
                item.ToggleActive();
                active = item.Active;
                ev.Use();
            }

            DrawActiveIcon(rect, item.Content, active);
        }

        private void DrawActiveIcon(Rect rect, GUIContent content, bool active)
        {
            if (active)
            {
                DrawIcon(rect, content);
                return;
            }

            Color previousColor = GUI.color;
            try
            {
                GUI.color = previousColor * s_DisabledColor;
                DrawIcon(rect, content);
            }
            finally { GUI.color = previousColor; }
        }

        private void DrawIcon(Rect rect, GUIContent content)
        {
            GUI.Label(rect, content, Style.ComponentIconStyle);
        }

        private static bool IsInspectClick(Rect rect)
        {
            Event evt = Event.current;
            return evt.type == EventType.MouseDown && evt.button == 0 && evt.alt &&
                !evt.shift && !evt.control && !evt.command && rect.Contains(evt.mousePosition);
        }

        private static void ShowInspectorMenu(Rect rect, List<ComponentItem> items)
        {
            Vector2 anchor = ComponentInspectorWindow.GetAnchor(rect);
            var menu = new GenericMenu();
            for (int i = 0; i < items.Count; i++)
            {
                ComponentItem item = items[i];
                Component component = item.Component;
                string name = item.DisplayName.Replace('/', '／');
                if (items.Count(other => other.DisplayName == item.DisplayName) > 1)
                    name += $" ({i + 1})";
                menu.AddItem(new GUIContent(name), false, () => ComponentInspectorWindow.Open(component, anchor));
            }
            menu.DropDown(rect);
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

        private static bool CanShow(ComponentType type, ComponentData settings)
        {
            return type.Shown || (type.IsBuiltIn ? settings.DisplayBuiltIn : settings.DisplayMonoScripts);
        }

        private Rect GetIconPosition(Rect rect)
        {
            rect.x += rect.width - INDENT_SIZE;
            rect.width = INDENT_SIZE;
            return rect;
        }
    }
}
