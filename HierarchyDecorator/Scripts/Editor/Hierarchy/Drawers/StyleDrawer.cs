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
            Texture2D visibleIcon = repaint ? GameObjectIconInfo.GetVisibleIcon(item, preview) : null;

            // Only draw the two tone background if there's no style override

            if (hasStyle)
            {
                DrawTwoToneContent(rect, item.GameObject, settings);
                hasStyle = true;
            }

            // Draw the style if one is to be applied
            // Have to make sure selection colours are drawn on top when required too

            HierarchyStyle prefix = item.GetRowStyle(settings.styleData);
            if (prefix != null)
            {
                Rect styleRect = (item.HasParent)
                    ? rect
                    : GetActualHierarchyWidth(rect);

                Rect labelRect = prefix.GetLabelRect(rect, visibleIcon != null);
                HierarchyGUI.DrawHierarchyStyle(prefix, styleRect, labelRect, item.RowName);
                hasStyle = true;
            }

            Color rowTint = Color.clear;
            if (repaint)
            {
                DrawParentRegions(rect, item.Transform.parent, settings);
                rowTint = GetRowTint(item.GameObject, settings, preview);
                if (rowTint.a > 0f)
                {
                    EditorGUI.DrawRect(rect, rowTint);
                }
            }

            if (visibleIcon != null)
            {
                GameObjectIconInfo.Draw(rect, item.GameObject, visibleIcon, settings.styleData, prefix, rowTint);
            }

            if (!hasStyle || !item.HasChildren)
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

        private void DrawParentRegions(Rect row, Transform parent, Settings settings)
        {
            if (!settings.RowColors.HasRegions && !GameObjectIconWindow.HasRegionPreview)
            {
                return;
            }

            float minX = GetActualHierarchyWidth(row).xMin;
            float right = row.xMin;
            float ancestorX = right;
            for (; parent != null && right > minX; parent = parent.parent)
            {
                ancestorX -= HierarchyIndentWidth;
                if (!TryGetRegionTint(parent.gameObject, settings, out Color tint))
                {
                    continue;
                }

                float left = Mathf.Max(minX, ancestorX);
                EditorGUI.DrawRect(Rect.MinMaxRect(left, row.yMin, right, row.yMax), tint);
                right = left;
            }
        }

        private static Color GetRowTint(GameObject target, Settings settings, GameObjectIconWindow.RowPreview preview)
        {
            if (TryGetColor(target, settings, preview, out Color color, out _))
            {
                color.a = EditorGUIUtility.isProSkin ? 0.56f : 0.40f;
                return color;
            }

            return GetRegionTint(target.transform.parent, settings);
        }

        private static Color GetRegionTint(Transform parent, Settings settings)
        {
            if (settings.RowColors.HasRegions || GameObjectIconWindow.HasRegionPreview)
            {
                for (; parent != null; parent = parent.parent)
                {
                    if (TryGetRegionTint(parent.gameObject, settings, out Color color))
                    {
                        return color;
                    }
                }
            }

            return Color.clear;
        }

        private static bool TryGetRegionTint(GameObject owner, Settings settings, out Color color)
        {
            if (TryGetColor(owner, settings, GameObjectIconWindow.GetRowPreview(owner), out color, out bool region) && region)
            {
                color.a = EditorGUIUtility.isProSkin ? 0.24f : 0.16f;
                return true;
            }

            return false;
        }

        private static bool TryGetColor(GameObject target, Settings settings, GameObjectIconWindow.RowPreview preview,
            out Color color, out bool region)
        {
            if (!preview.HasColor)
            {
                return settings.RowColors.TryGetColor(target, out color, out region) &&
                    GameObjectIconWindow.IsPaletteColor(settings, color);
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

            rect.x -= HierarchyIndentWidth;

            return rect;
        }
    }
}
