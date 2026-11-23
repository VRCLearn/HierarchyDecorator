using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Editor
{
    public class StyleDrawer : HierarchyDrawer
    {
        private static readonly HideFlags IgnoreFoldoutFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;

        protected override void DrawInternal(Rect rect, HierarchyItem item, Settings settings)
        {
            bool hasStyle = settings.styleData.twoToneBackground;
            bool repaint = Event.current.type == EventType.Repaint;
            GameObjectIconWindow.RowPreview preview = repaint ? GameObjectIconWindow.GetRowPreview(item.GameObject) : default;
            Texture2D visibleIcon = repaint ? GameObjectIconInfo.GetIcon(item.GameObject) : null;
            if (preview.HasIcon)
            {
                if (preview.Icon != null || visibleIcon == null)
                {
                    visibleIcon = preview.Icon;
                }
                else
                {
                    visibleIcon = HierarchyGUI.GetStandardContent(item.GameObject, item.PrefabInfo == PrefabInfo.Root)
                        .image as Texture2D;
                }
            }

            // Only draw the two tone background if there's no style override

            if (hasStyle)
            {
                DrawTwoToneContent(rect, item.GameObject, settings);
                hasStyle = true;
            }

            // Draw the style if one is to be applied
            // Have to make sure selection colours are drawn on top when required too

            string displayName = item.DisplayName;
            if (settings.styleData.TryGetStyleFromPrefix(displayName, out HierarchyStyle prefix))
            {
                Rect styleRect = (item.HasParent)
                    ? rect
                    : GetActualHierarchyWidth(rect);

                Rect labelRect = rect;
                if (visibleIcon != null)
                {
                    labelRect.xMin += GameObjectIconInfo.LabelOffset;
                }

                HierarchyGUI.DrawHierarchyStyle(prefix, styleRect, labelRect, displayName);
                hasStyle = true;
            }

            Color rowTint = Color.clear;
            if (repaint)
            {
                rowTint = GetRowTint(item.GameObject, settings.RowColors, preview);
                if (rowTint.a > 0f)
                {
                    EditorGUI.DrawRect(GetActualHierarchyWidth(rect), rowTint);
                }
            }

            if (visibleIcon != null)
            {
                GameObjectIconInfo.Draw(rect, item.GameObject, visibleIcon, settings.styleData, prefix, rowTint);
            }

            if (!item.HasChildren || !hasStyle)
            {
                return;
            }

            // - Need to validate children to check if some can be visible

            bool canShow = false;
            for (int i = 0; i < item.Transform.childCount; i++)
            {
                Transform child = item.Transform.GetChild(i);

                if ((child.hideFlags & IgnoreFoldoutFlags) == 0)
                {
                    canShow = true;
                    break;
                }
            }

            if (!canShow)
            {
                return;
            }

            DrawFoldout(rect, item.Foldout);
        }

        private static Color GetRowTint(GameObject target, ObjectRowColors colors, GameObjectIconWindow.RowPreview preview)
        {
            if (TryGetColor(target, colors, preview, out Color color, out _))
            {
                color.a = EditorGUIUtility.isProSkin ? 0.28f : 0.20f;
                return color;
            }

            return GetRegionTint(target.transform.parent, colors);
        }

        private static Color GetRegionTint(Transform parent, ObjectRowColors colors)
        {
            if (colors.HasRegions || GameObjectIconWindow.HasRegionPreview)
            {
                for (; parent != null; parent = parent.parent)
                {
                    if (TryGetRegionTint(parent.gameObject, colors, out Color color))
                    {
                        return color;
                    }
                }
            }

            return Color.clear;
        }

        private static bool TryGetRegionTint(GameObject owner, ObjectRowColors colors, out Color color)
        {
            if (TryGetColor(owner, colors, GameObjectIconWindow.GetRowPreview(owner), out color, out bool region) && region)
            {
                color.a = EditorGUIUtility.isProSkin ? 0.12f : 0.08f;
                return true;
            }

            return false;
        }

        private static bool TryGetColor(GameObject target, ObjectRowColors colors, GameObjectIconWindow.RowPreview preview,
            out Color color, out bool region)
        {
            if (!preview.HasColor)
            {
                return colors.TryGetColor(target, out color, out region);
            }

            color = preview.Color.GetValueOrDefault();
            region = preview.ColorRegion;
            return preview.Color.HasValue;
        }

        protected override bool DrawerIsEnabled(HierarchyItem instance, Settings settings)
        {
            return Event.current.type == EventType.Repaint ||
                settings.styleData.Count > 0 || settings.styleData.twoToneBackground;
        }

        // Standards

        private void DrawFoldout(Rect rect, bool foldout)
        {
            GUI.Toggle (GetToggleRect (rect), foldout, GUIContent.none, EditorStyles.foldout);
        }

        // Rect GUI

        private void DrawTwoToneContent(Rect rect, GameObject instance, Settings _settings)
        {
            Rect twoToneRect = GetActualHierarchyWidth (rect);
            var color = _settings.styleData.GetColorMode(EditorGUIUtility.isProSkin);

            Handles.DrawSolidRectangleWithOutline (twoToneRect, color.GetColor(twoToneRect), Color.clear);
            HierarchyGUI.DrawStandardContent (rect, instance);
        }

        // Helpers

        private Rect GetActualHierarchyWidth(Rect rect)
        {
            rect.width += rect.x;

#if UNITY_2019_1_OR_NEWER
            rect.x = 32f;
#else
            rect.x = 0;
            rect.width += 32f;
#endif

            return rect;
        }

        private Rect GetToggleRect(Rect rect)
        {
            rect.width = 0f;
            rect.height -= 0f;

            rect.x -= 14f;

            return rect;
        }
    }
}
