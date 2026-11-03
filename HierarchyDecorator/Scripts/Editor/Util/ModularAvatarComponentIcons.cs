using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Editor
{
    internal static class ModularAvatarComponentIcons
    {
        private const int Size = 64;
        private static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();

        static ModularAvatarComponentIcons()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
        }

        internal static Texture2D GetIcon(Type type, MonoScript script)
        {
            if (type == null || script == null || type.Assembly.GetName().Name != "nadena.dev.modular-avatar.core")
                return null;
            string assetType;
            switch (type.FullName)
            {
                case "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller":
                case "nadena.dev.modular_avatar.core.ModularAvatarMenuItem":
                case "nadena.dev.modular_avatar.core.ModularAvatarMenuGroup":
                    assetType = "VRCExpressionsMenu";
                    break;
                case "nadena.dev.modular_avatar.core.ModularAvatarParameters":
                    assetType = "VRCExpressionParameters";
                    break;
                case "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator":
                    assetType = "AnimatorController";
                    break;
                case "nadena.dev.modular_avatar.core.ModularAvatarMergeBlendTree":
                    assetType = "AnimationClip";
                    break;
                case "nadena.dev.modular_avatar.core.ModularAvatarMaterialSetter":
                case "nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap":
                    assetType = "Material";
                    break;
                case "nadena.dev.modular_avatar.core.ModularAvatarMeshSettings":
                    assetType = "Mesh";
                    break;
                case "nadena.dev.modular_avatar.core.ModularAvatarObjectToggle":
                    assetType = "GameObject";
                    break;
                case "nadena.dev.modular_avatar.core.ModularAvatarBoneProxy":
                    assetType = "BoneProxy";
                    break;
                case "nadena.dev.modular_avatar.core.ModularAvatarMergeArmature":
                    assetType = "MergeArmature";
                    break;
                case "nadena.dev.modular_avatar.core.ModularAvatarBlendshapeSync":
                    assetType = "BlendshapeSync";
                    break;
                case "nadena.dev.modular_avatar.core.ModularAvatarShapeChanger":
                    assetType = "ShapeChanger";
                    break;
                default:
                    return null;
            }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            var original = AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(script)) as Texture2D;
            if (original == null) return null;
            string key = original.GetInstanceID() + ":" + assetType;
            Texture2D icon;
            if (Icons.TryGetValue(key, out icon) && icon != null) return icon;

            Texture2D badge = GetBadge(assetType);
            Color32[] badgePixels = badge != null ? ReadPixels(badge) : SymbolPixels(assetType);
            if (badgePixels == null) return null;
            Color32[] mainPixels = ReadPixels(original);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    // Retain the MA mark above-left and place the function badge below-right.
                    Color main = Sample(mainPixels, new Rect(0, 16, 48, 48), x, y);
                    Color overlay = Sample(badgePixels, new Rect(28, 0, 36, 36), x, y);
                    float alpha = overlay.a + main.a * (1f - overlay.a);
                    if (alpha <= 0f) continue;
                    Color color = (overlay * overlay.a + main * (main.a * (1f - overlay.a))) / alpha;
                    color.a = alpha;
                    pixels[y * Size + x] = color;
                }
            icon = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            {
                name = "MA_" + assetType,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                icon.SetPixels(pixels);
                icon.Apply(false, true);
                Icons[key] = icon;
                return icon;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(icon);
                throw;
            }
        }

        private static Texture2D GetBadge(string name)
        {
            Type type;
            switch (name)
            {
                // These are the icons assigned to the SDK's Expressions Menu and Parameters types.
                case "VRCExpressionsMenu":
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("576142c89b11bc84e974b3740e480a63"));
                case "VRCExpressionParameters":
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("b54460437079b5a4fb73ecebc0aef4dc"));
                case "AnimatorController": type = typeof(UnityEditor.Animations.AnimatorController); break;
                case "AnimationClip": type = typeof(AnimationClip); break;
                case "Material": type = typeof(Material); break;
                case "Mesh": type = typeof(Mesh); break;
                case "GameObject": type = typeof(GameObject); break;
                default: return null;
            }
            return EditorGUIUtility.ObjectContent(null, type).image as Texture2D;
        }

        private static Color32[] SymbolPixels(string name)
        {
            var pixels = new Color32[Size * Size];
            switch (name)
            {
                case "BoneProxy":
                    Circle(pixels, 18, 46, 12, false);
                    Circle(pixels, 46, 18, 12, false);
                    Stroke(pixels, 22, 42, 42, 22);
                    break;
                case "MergeArmature":
                    Stroke(pixels, 10, 52, 32, 28);
                    Stroke(pixels, 54, 52, 32, 28);
                    Stroke(pixels, 32, 28, 32, 10);
                    Circle(pixels, 10, 52, 6, true);
                    Circle(pixels, 54, 52, 6, true);
                    Circle(pixels, 32, 28, 6, true);
                    Circle(pixels, 32, 10, 6, true);
                    break;
                case "BlendshapeSync":
                    Stroke(pixels, 8, 44, 54, 44);
                    Stroke(pixels, 42, 56, 54, 44);
                    Stroke(pixels, 42, 32, 54, 44);
                    Stroke(pixels, 10, 20, 56, 20);
                    Stroke(pixels, 10, 20, 22, 32);
                    Stroke(pixels, 10, 20, 22, 8);
                    break;
                case "ShapeChanger":
                    for (int row = 0; row < 3; row++)
                    {
                        int y = 13 + row * 19;
                        int x = row == 1 ? 22 : 42;
                        Stroke(pixels, 7, y, 57, y);
                        Stroke(pixels, x, y - 7, x, y + 7, 8);
                    }
                    break;
                default: return null;
            }
            return pixels;
        }

        private static void Stroke(Color32[] pixels, float x1, float y1, float x2, float y2, float width = 5)
        {
            var start = new Vector2(x1, y1);
            var direction = new Vector2(x2 - x1, y2 - y1);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(point - start, direction) / direction.sqrMagnitude);
                    Paint(pixels, x, y, width * 0.5f + 0.5f - Vector2.Distance(point, start + direction * t));
                }
        }

        private static void Circle(Color32[] pixels, float cx, float cy, float radius, bool filled)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    Paint(pixels, x, y, filled ? radius + 0.5f - distance : 3f - Mathf.Abs(distance - radius));
                }
        }

        private static void Paint(Color32[] pixels, int x, int y, float coverage)
        {
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255);
            if (alpha <= pixels[y * Size + x].a) return;
            Color color = new Color32(196, 196, 196, 255);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;
            color.a = alpha / 255f;
            pixels[y * Size + x] = color;
        }

        private static Color Sample(Color32[] pixels, Rect rect, int x, int y)
        {
            if (!rect.Contains(new Vector2(x + 0.5f, y + 0.5f))) return Color.clear;
            float u = Mathf.Clamp((x + 0.5f - rect.x) * Size / rect.width - 0.5f, 0, Size - 1);
            float v = Mathf.Clamp((y + 0.5f - rect.y) * Size / rect.height - 0.5f, 0, Size - 1);
            int left = Mathf.FloorToInt(u), bottom = Mathf.FloorToInt(v);
            int right = Mathf.Min(left + 1, Size - 1), top = Mathf.Min(bottom + 1, Size - 1);
            Color a = Premultiply(pixels[bottom * Size + left]);
            Color b = Premultiply(pixels[bottom * Size + right]);
            Color c = Premultiply(pixels[top * Size + left]);
            Color d = Premultiply(pixels[top * Size + right]);
            Color sample = Color.Lerp(Color.Lerp(a, b, u - left), Color.Lerp(c, d, u - left), v - bottom);
            if (sample.a <= 0f) return Color.clear;
            float alpha = sample.a;
            sample /= alpha;
            sample.a = alpha;
            return sample;
        }

        private static Color Premultiply(Color color)
        {
            return new Color(color.r * color.a, color.g * color.a, color.b * color.a, color.a);
        }

        private static Color32[] ReadPixels(Texture source)
        {
            var target = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var copy = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        private static void Clear()
        {
            foreach (Texture2D icon in Icons.Values)
                if (icon != null) UnityEngine.Object.DestroyImmediate(icon);
            Icons.Clear();
        }
    }
}
