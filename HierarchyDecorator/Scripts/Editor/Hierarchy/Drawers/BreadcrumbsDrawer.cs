using System.Collections.Generic;
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

        private static Texture2D ancestorLineMask;
        private readonly Dictionary<Transform, AncestorPath> ancestorPaths = new Dictionary<Transform, AncestorPath>();
        private readonly List<Transform> pendingAncestors = new List<Transform>();
        private readonly List<AncestorGuide> ancestorGuides = new List<AncestorGuide>();
        private float previousRowY;

        private struct AncestorPath
        {
            public int depth;
            public int guideIndex;
        }

        private struct AncestorGuide
        {
            public int depth;
            public int previousIndex;
        }

        protected override bool DrawerIsEnabled(HierarchyItem item, Settings settings)
        {
            if (!settings.globalData.showBreadcrumbs || Event.current.type != EventType.Repaint)
            {
                // Layout precedes the next repaint. Hide flags and sibling order are read afresh.
                ClearAncestorPaths();
                return false;
            }

            return true;
        }

        protected override void DrawInternal(Rect row, HierarchyItem item, Settings settings)
        {
            // A restarted or discontinuous row sequence can also follow a repaint in another window.
            if (row.y <= previousRowY || row.y > previousRowY + row.height)
                ClearAncestorPaths();
            previousRowY = row.y;

            // Search results use a flat layout and have no branch columns.
            if (row.x < MinimumRowX)
                return;

            Transform node = item.Transform;
            Transform parent = node.parent;
            if (parent == null)
                return;

            BreadcrumbSettings breadcrumbs = settings.globalData.instanceBreadcrumbs;
            float column = row.x - HierarchyIndentWidth - FoldoutCenterOffset;
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float pixel = 1f / pixelsPerPoint;
            Color color = breadcrumbs.color;
            color.a *= LineOpacity;

            Color previousColor = GUI.color;
            GUI.color = previousColor * color;
            try
            {
                DrawBranch(row, node, parent, column, breadcrumbs, pixel);

                AncestorPath path = GetAncestorPath(parent);
                if (path.guideIndex >= 0)
                    DrawAncestorLines(row, column, path, breadcrumbs.ancestorStyle, pixelsPerPoint, pixel);
            }
            finally
            {
                GUI.color = previousColor;
            }
        }

        private void DrawAncestorLines(Rect row, float column, AncestorPath path, BreadcrumbStyle style,
            float pixelsPerPoint, float pixel)
        {
            float repeats = GetMaskRepeatCount(row, style, pixelsPerPoint);
            Texture2D mask = repeats > 0f ? GetAncestorLineMask() : null;
            // Texture coordinates start at the bottom. The top of the row starts on an opaque texel.
            Rect uv = new Rect(0f, 1f - repeats, 1f, repeats);

            for (int index = path.guideIndex; index >= 0;)
            {
                AncestorGuide guide = ancestorGuides[index];
                float guideColumn = column - (path.depth - guide.depth + 1) * HierarchyIndentWidth;
                if (repeats > 0f)
                {
                    float x = Mathf.Round(guideColumn / pixel) * pixel;
                    GUI.DrawTextureWithTexCoords(new Rect(x, row.yMin, pixel, row.height), mask, uv);
                }
                else
                {
                    DrawLine(new Vector2(guideColumn, row.yMin), new Vector2(guideColumn, row.yMax), style, pixel);
                }
                index = guide.previousIndex;
            }
        }

        private static float GetMaskRepeatCount(Rect row, BreadcrumbStyle style, float pixelsPerPoint)
        {
            if (style == BreadcrumbStyle.Solid)
                return 0f;

            // The texture matches the segmented path only when every boundary lands on a device pixel.
            double firstPixel = (double)row.yMin * pixelsPerPoint;
            double lastPixel = (double)row.yMax * pixelsPerPoint;
            double segmentPixels = (double)GetSegmentLength(style) * pixelsPerPoint;
            double length = lastPixel - firstPixel;
            if (length <= segmentPixels || firstPixel % 1d != 0d || lastPixel % 1d != 0d || segmentPixels % 1d != 0d)
                return 0f;

            return (float)(length / (2d * segmentPixels));
        }

        private static Texture2D GetAncestorLineMask()
        {
            if (ancestorLineMask != null)
                return ancestorLineMask;

            ancestorLineMask = new Texture2D(1, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            ancestorLineMask.SetPixel(0, 0, Color.clear);
            ancestorLineMask.SetPixel(0, 1, Color.white);
            ancestorLineMask.Apply(false, true);
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseAncestorLineMask;
            return ancestorLineMask;
        }

        private static void ReleaseAncestorLineMask()
        {
            Object.DestroyImmediate(ancestorLineMask);
            ancestorLineMask = null;
        }

        private void ClearAncestorPaths()
        {
            if (ancestorPaths.Count == 0)
                return;

            ancestorPaths.Clear();
            ancestorGuides.Clear();
        }

        private AncestorPath GetAncestorPath(Transform node)
        {
            AncestorPath path = default;
            Transform parent = node;
            while (parent != null)
            {
                if (ancestorPaths.TryGetValue(parent, out path))
                    break;

                pendingAncestors.Add(parent);
                parent = parent.parent;
            }

            if (parent == null)
                path = new AncestorPath { depth = -1, guideIndex = -1 };

            for (int i = pendingAncestors.Count - 1; i >= 0; i--)
            {
                Transform ancestor = pendingAncestors[i];
                path.depth++;
                if (parent != null && HasVisibleChild(parent, ancestor.GetSiblingIndex() + 1, parent.childCount, 1))
                {
                    ancestorGuides.Add(new AncestorGuide { depth = path.depth, previousIndex = path.guideIndex });
                    path.guideIndex = ancestorGuides.Count - 1;
                }

                ancestorPaths.Add(ancestor, path);
                parent = ancestor;
            }

            pendingAncestors.Clear();
            return path;
        }

        private static void DrawBranch(Rect row, Transform node, Transform parent, float column, BreadcrumbSettings settings, float pixel)
        {
            column = Mathf.Round(column / pixel) * pixel;
            int siblingIndex = node.GetSiblingIndex();
            int siblingCount = parent.childCount;
            bool hasPrevious = HasVisibleChild(parent, siblingIndex - 1, siblingCount, -1);
            bool hasNext = HasVisibleChild(parent, siblingIndex + 1, siblingCount, 1);
            // The first displayed child draws the stem under its parent's arrow.
            float top = hasPrevious ? row.yMin : row.yMin - Mathf.Max(0f, row.height * 0.5f - ParentFoldoutClearance);
            float bottom = hasNext ? row.yMax : row.center.y;
            DrawLine(new Vector2(column, top), new Vector2(column, bottom), settings.directStyle, pixel);

            if (settings.displayHorizontal)
            {
                // A continuing stem already owns the junction pixel. A final branch ends just above it.
                float start = hasNext ? column + pixel : column;
                bool hasFoldout = HasVisibleChild(node, 0, node.childCount, 1);
                float end = row.x - (hasFoldout ? FoldoutCenterOffset + FoldoutHalfWidth : IconClearance);
                DrawLine(new Vector2(start, row.center.y), new Vector2(end, row.center.y), settings.directStyle, pixel);
            }
        }

        private static bool HasVisibleChild(Transform parent, int index, int count, int direction)
        {
            for (; index >= 0 && index < count; index += direction)
            {
                if ((parent.GetChild(index).gameObject.hideFlags & HideFlags.HideInHierarchy) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static void DrawLine(Vector2 start, Vector2 end, BreadcrumbStyle style, float pixel)
        {
            if (style == BreadcrumbStyle.Solid)
            {
                DrawPixelSegment(start, end, pixel);
                return;
            }

            float length = Vector2.Distance(start, end);
            float segment = GetSegmentLength(style);
            for (float offset = 0f; offset < length; offset += segment * 2f)
            {
                DrawPixelSegment(Vector2.Lerp(start, end, offset / length),
                    Vector2.Lerp(start, end, (offset + segment) / length), pixel);
            }
        }

        private static float GetSegmentLength(BreadcrumbStyle style) => style == BreadcrumbStyle.Dotted ? 1f : 2f;

        private static void DrawPixelSegment(Vector2 start, Vector2 end, float pixel)
        {
            Rect rect = GetPixelSegmentRect(start, end, pixel);
            if (rect.width > 0f && rect.height > 0f)
            {
                GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
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
