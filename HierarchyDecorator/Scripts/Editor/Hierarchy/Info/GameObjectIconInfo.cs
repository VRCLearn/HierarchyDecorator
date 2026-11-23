using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HierarchyDecorator.Editor
{
    internal static class GameObjectIconInfo
    {
        private const float IconWidth = 16f;
        internal const float LabelOffset = 18f;

#if !UNITY_2021_2_OR_NEWER
        // Unity 2018.4 exposes the same icon getter internally.
        private static readonly Func<Object, Texture2D> GetLegacyIcon =
            (Func<Object, Texture2D>)Delegate.CreateDelegate(
                typeof(Func<Object, Texture2D>), typeof(EditorGUIUtility).GetMethod(
                    "GetIconForObject", BindingFlags.Static | BindingFlags.NonPublic));
#endif

        internal static Texture2D GetIcon(GameObject target)
        {
#if UNITY_2021_2_OR_NEWER
            return EditorGUIUtility.GetIconForObject(target);
#else
            return GetLegacyIcon(target);
#endif
        }

        internal static Rect GetSlot(Rect row)
        {
            return new Rect(row.x, row.y, Mathf.Min(row.width, IconWidth), row.height);
        }

        internal static void Draw(Rect row, GameObject target, Texture2D icon,
            HierarchyStyleData styles, HierarchyStyle style, Color rowTint)
        {
            if (row.width < IconWidth)
            {
                return;
            }

            Rect slot = GetSlot(row);
            bool dark = EditorGUIUtility.isProSkin;
            if (styles.twoToneBackground)
            {
                EditorGUI.DrawRect(slot, styles.GetColorMode(dark).GetColor(row));
            }
            else
            {
                DrawNativeBackground(row, slot, target, dark);
            }

            if (style != null)
            {
                EditorGUI.DrawRect(slot, style.GetCurrentMode(dark).backgroundColour);
            }

            if (rowTint.a > 0f)
            {
                EditorGUI.DrawRect(slot, rowTint);
            }

            Color tint = GUI.color;
            if (!target.activeInHierarchy && style == null && !styles.twoToneBackground)
            {
                tint.a *= 0.5f;
            }

            GUI.DrawTexture(slot, icon, ScaleMode.ScaleToFit, true, 0f, tint, 0f, 0f);
            if (PrefabUtility.IsAddedGameObjectOverride(target))
            {
                GUI.DrawTexture(slot, EditorGUIUtility.IconContent("PrefabOverlayAdded Icon").image,
                    ScaleMode.ScaleToFit, true, 0f, tint, 0f, 0f);
            }
        }

        private static void DrawNativeBackground(Rect row, Rect slot, GameObject target, bool dark)
        {
            Rect fullRow = row;
            fullRow.xMin = 0f;
            bool hovered = fullRow.Contains(Event.current.mousePosition);
            Color background = Constants.DefaultBackgroundColor;
            if (hovered)
            {
                background = dark ? new Color32(65, 65, 65, 255) : new Color32(190, 190, 190, 255);
            }

            EditorGUI.DrawRect(slot, background);
            GUIStyle selectionStyle = GUI.skin.FindStyle("TV Selection");
            if (selectionStyle != null && Selection.Contains(target))
            {
                EditorWindow focusedWindow = EditorWindow.focusedWindow;
                bool focused = focusedWindow != null && focusedWindow.GetType().Name == "SceneHierarchyWindow";
                GUI.BeginClip(slot, -slot.position, Vector2.zero, false);
                try
                {
                    selectionStyle.Draw(fullRow, false, false, true, focused);
                }
                finally
                {
                    GUI.EndClip();
                }
            }
        }
    }
}
