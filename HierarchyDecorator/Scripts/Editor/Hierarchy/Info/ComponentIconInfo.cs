
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
        private readonly List<RowIcon> items = new List<RowIcon>();
        private readonly Dictionary<int, string> overflowLabels = new Dictionary<int, string>();
        private readonly GUIContent iconContent = new GUIContent();
        private readonly StringBuilder tooltip = new StringBuilder();

        private bool requireWarning;
        private int iconLimit;

        private readonly struct RowIcon
        {
            internal readonly ComponentItem Item;
            internal readonly GUIContent Content;
            internal readonly List<ComponentItem> Stack;

            internal RowIcon(ComponentItem item, GUIContent content, List<ComponentItem> stack)
            {
                Item = item;
                Content = content;
                Stack = stack;
            }
        }

#if UNITY_2019_1_OR_NEWER
        private readonly GUIContent warningGUI = EditorGUIUtility.IconContent("warning");
#else
        private readonly GUIContent warningGUI = EditorGUIUtility.IconContent("console.warnicon");
#endif
        protected override void OnDrawInit(HierarchyItem item, Settings settings)
        {
            requireWarning = false;
            ComponentData componentSettings = settings.Components;
            iconLimit = componentSettings.ComponentIconLimit;
            bool stackIcons = componentSettings.StackScripts;
            bool showMissingScriptWarning = componentSettings.ShowMissingScriptWarning;
            items.Clear();
            foreach (List<ComponentItem> group in stackedComponents.Values)
                group.Clear();
            stackedComponents.Clear();

            ComponentList components = item.GetComponentsForDrawing();
            for (int i = 0; i < components.Count; i++)
            {
                ComponentItem component = components[i];
                if (component.IsNullComponent && showMissingScriptWarning)
                {
                    requireWarning = true;
                    continue;
                }

                ComponentType type = component.Type;
                if (type == null || type.Excluded || !CanShow(type, componentSettings))
                {
                    continue;
                }

                GUIContent content = type.Content;
                Texture texture = stackIcons ? content.image : null;
                List<ComponentItem> group = null;
                if (texture != null)
                {
                    if (stackedComponents.TryGetValue(texture, out group))
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
                items.Add(new RowIcon(component, content, group));
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
            Event evt = Event.current;
            bool repaint = evt.type == EventType.Repaint;
            Vector2 mousePosition = evt.mousePosition;

            for (int i = 0; i < visible; i++)
            {
                if (repaint || rect.Contains(mousePosition))
                {
                    RowIcon icon = items[i];
                    if (icon.Stack != null)
                    {
                        iconContent.image = icon.Content.image;
                        iconContent.text = string.Empty;
                        iconContent.tooltip = GetStackTooltip(rect, icon.Stack);
                        DrawStackedComponentIcon(rect, iconContent, icon.Stack, settings);
                    }
                    else
                    {
                        DrawComponentIcon(rect, icon.Item, icon.Content, settings);
                    }
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

            if (!overflowLabels.TryGetValue(hidden, out string count))
            {
                count = "+" + hidden;
                overflowLabels.Add(hidden, count);
            }
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
                RowIcon icon = items[i];
                if (icon.Stack == null)
                {
                    tooltip.Append('\n').Append(icon.Item.DisplayName);
                    continue;
                }

                foreach (ComponentItem member in icon.Stack)
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

        private void DrawComponentIcon(Rect rect, ComponentItem item, GUIContent content, Settings settings)
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
                DrawComponentToggle(rect, item, content, enabledState != 0);
            }
            else
            {
                DrawIcon(rect, content);
            }
        }

        private void DrawComponentToggle(Rect rect, ComponentItem item, GUIContent content, bool active)
        {
            Event ev = Event.current;

            if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
            {
                item.ToggleActive();
                active = item.Active;
                ev.Use();
            }

            DrawActiveIcon(rect, content, active);
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
