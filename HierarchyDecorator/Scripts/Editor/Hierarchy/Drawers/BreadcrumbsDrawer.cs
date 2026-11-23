using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Editor
{
    public class BreadcrumbsDrawer : HierarchyDrawer
    {
        private const float MinimumRowX = 60f;
        private const float FoldoutCenterOffset = 8f;
        private const float FoldoutHalfWidth = 6f;
        private const float ParentFoldoutClearance = 4f;
        private const float IconClearance = 3f;
        private const float LineOpacity = 0.6f;

        protected override bool DrawerIsEnabled(HierarchyItem item, Settings settings)
        {
            return settings.globalData.showBreadcrumbs && Event.current.type == EventType.Repaint;
        }

        protected override void DrawInternal(Rect row, HierarchyItem item, Settings settings)
        {
            Transform node = item.Transform;
            Transform parent = node.parent;
            // Search results use a flat layout and have no branch columns.
            if (parent == null || row.x < MinimumRowX)
            {
                return;
            }

            BreadcrumbSettings breadcrumbs = settings.globalData.instanceBreadcrumbs;
            float column = row.x - HierarchyIndentWidth - FoldoutCenterOffset;
            float pixel = 1f / EditorGUIUtility.pixelsPerPoint;
            Color color = breadcrumbs.color;
            color.a *= LineOpacity;
            DrawBranch(row, node, column, breadcrumbs, color, pixel);

            for (Transform ancestor = parent; ancestor.parent != null; ancestor = ancestor.parent)
            {
                column -= HierarchyIndentWidth;
                if (HasVisibleSibling(ancestor, 1))
                {
                    DrawLine(new Vector2(column, row.yMin), new Vector2(column, row.yMax), breadcrumbs.style, color, pixel);
                }
            }
        }

        private static void DrawBranch(Rect row, Transform node, float column, BreadcrumbSettings settings, Color color, float pixel)
        {
            column = Mathf.Round(column / pixel) * pixel;
            bool hasPrevious = HasVisibleSibling(node, -1);
            bool hasNext = HasVisibleSibling(node, 1);
            Rect branch = GetBranchBounds(row, column, hasPrevious, hasNext, HasVisibleChild(node));
            DrawLine(branch.min, new Vector2(column, branch.yMax), settings.style, color, pixel);

            if (settings.displayHorizontal)
            {
                // A continuing stem already owns the junction pixel. A final branch ends just above it.
                float start = hasNext ? column + pixel : column;
                DrawLine(new Vector2(start, row.center.y), new Vector2(branch.xMax, row.center.y), settings.style, color, pixel);
            }
        }

        private static Rect GetBranchBounds(Rect row, float column, bool hasPrevious, bool hasNext, bool hasFoldout)
        {
            // The first displayed child draws the stem under its parent's arrow.
            float top = hasPrevious ? row.yMin : row.yMin - Mathf.Max(0f, row.height * 0.5f - ParentFoldoutClearance);
            float bottom = hasNext ? row.yMax : row.center.y;
            float end = row.x - (hasFoldout ? FoldoutCenterOffset + FoldoutHalfWidth : IconClearance);
            return Rect.MinMaxRect(column, top, end, bottom);
        }

        private static bool HasVisibleSibling(Transform node, int direction)
        {
            Transform parent = node.parent;
            for (int index = node.GetSiblingIndex() + direction; index >= 0 && index < parent.childCount; index += direction)
            {
                if (IsVisibleInHierarchy(parent.GetChild(index)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasVisibleChild(Transform node)
        {
            for (int index = 0; index < node.childCount; index++)
            {
                if (IsVisibleInHierarchy(node.GetChild(index)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsVisibleInHierarchy(Transform node)
        {
            return (node.gameObject.hideFlags & HideFlags.HideInHierarchy) == 0;
        }

        private static void DrawLine(Vector2 start, Vector2 end, BreadcrumbStyle style, Color color, float pixel)
        {
            if (style == BreadcrumbStyle.Solid)
            {
                DrawPixelSegment(start, end, color, pixel);
                return;
            }

            float length = Vector2.Distance(start, end);
            float segment = style == BreadcrumbStyle.Dotted ? 1f : 2f;
            for (float offset = 0f; offset < length; offset += segment * 2f)
            {
                DrawPixelSegment(Vector2.Lerp(start, end, offset / length),
                    Vector2.Lerp(start, end, (offset + segment) / length), color, pixel);
            }
        }

        private static void DrawPixelSegment(Vector2 start, Vector2 end, Color color, float pixel)
        {
            Rect rect = GetPixelSegmentRect(start, end, pixel);
            if (rect.width > 0f && rect.height > 0f)
            {
                EditorGUI.DrawRect(rect, color);
            }
        }

        private static Rect GetPixelSegmentRect(Vector2 start, Vector2 end, float pixel)
        {
            Vector2 first = new Vector2(Mathf.Round(start.x / pixel), Mathf.Round(start.y / pixel));
            Vector2 last = new Vector2(Mathf.Round(end.x / pixel), Mathf.Round(end.y / pixel));
            Vector2 size = start.x == end.x ? new Vector2(1f, last.y - first.y) : new Vector2(last.x - first.x, 1f);
            return new Rect(first * pixel, size * pixel);
        }
    }
}
