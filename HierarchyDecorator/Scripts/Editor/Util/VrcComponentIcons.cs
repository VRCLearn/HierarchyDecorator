using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Editor
{
    internal static class VrcComponentIcons
    {
        private static readonly Dictionary<Texture2D, Texture2D> ConstraintIcons = new Dictionary<Texture2D, Texture2D>();
        private static readonly Color32 ConstraintPinColor = new Color32(43, 170, 193, 255);

        static VrcComponentIcons()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearConstraintIcons;
            EditorApplication.quitting += ClearConstraintIcons;
        }

        // Type identity is checked without referencing the optional VRChat SDK.
        // These icons describe attached components, never their targets or build results.
        internal static Texture2D GetIcon(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                string guid;
                switch (current.FullName + ", " + current.Assembly.GetName().Name)
                {
                    case "VRC.SDKBase.VRC_AvatarDescriptor, VRCSDKBase":
                        guid = "c192d5342fd24148a2cc53986cc7532e";
                        break;
                    case "VRC.SDKBase.VRC_MirrorReflection, VRCSDKBase":
                        guid = "4a2b4e16df2e4cfd93e5772865c6e610";
                        break;
                    case "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone, VRC.SDK3.Dynamics.PhysBone":
                        guid = "50d1897da169479dbe6fb24f9b62518c";
                        break;
                    case "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider, VRC.SDK3.Dynamics.PhysBone":
                        guid = "e7e07e06f34c4f2e96fa17cd3b47a9c8";
                        break;
                    case "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver, VRC.SDK3.Dynamics.Contact":
                        guid = "c197db74d15f42cdaaf5c9386ccdb867";
                        break;
                    case "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender, VRC.SDK3.Dynamics.Contact":
                        guid = "492c2d68f2e84548a2b12f135f896e31";
                        break;
                    case "VRC.SDK3.Dynamics.Constraint.Components.VRCAimConstraint, VRC.SDK3.Dynamics.Constraint":
                        return GetConstraintIcon("AimConstraint Icon");
                    case "VRC.SDK3.Dynamics.Constraint.Components.VRCLookAtConstraint, VRC.SDK3.Dynamics.Constraint":
                        return GetConstraintIcon("LookAtConstraint Icon");
                    case "VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint, VRC.SDK3.Dynamics.Constraint":
                        return GetConstraintIcon("ParentConstraint Icon");
                    case "VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint, VRC.SDK3.Dynamics.Constraint":
                        return GetConstraintIcon("PositionConstraint Icon");
                    case "VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint, VRC.SDK3.Dynamics.Constraint":
                        return GetConstraintIcon("RotationConstraint Icon");
                    case "VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint, VRC.SDK3.Dynamics.Constraint":
                        return GetConstraintIcon("ScaleConstraint Icon");
                    default:
                        continue;
                }

                return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            }

            return null;
        }

        private static Texture2D GetConstraintIcon(string name)
        {
            // Icon recoloring requires a graphics context; headless runs keep the existing icon path.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            var source = EditorGUIUtility.IconContent(name).image as Texture2D;
            if (source == null) return null;
            Texture2D icon;
            if (ConstraintIcons.TryGetValue(source, out icon) && icon != null) return icon;

            // Unity's built-in icons are not readable. Copy once, retaining the original pixels.
            var target = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            icon = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                icon.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                Color32[] pixels = icon.GetPixels32();
                // Brand hex values are sRGB; this copy stores the project's render-space values.
                Color32 pinColor = QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? (Color32)((Color)ConstraintPinColor).linear : ConstraintPinColor;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    if (pixel.a > 0 && pixel.r > pixel.g && pixel.b > pixel.g)
                        pixels[i] = new Color32(pinColor.r, pinColor.g, pinColor.b, pixel.a);
                }
                icon.SetPixels32(pixels);
                icon.Apply(false, true);
                icon.name = "VRC" + name.Replace(" Icon", "");
                icon.hideFlags = HideFlags.HideAndDontSave;
                icon.filterMode = source.filterMode;
                icon.wrapMode = TextureWrapMode.Clamp;
                ConstraintIcons[source] = icon;
                return icon;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(icon);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void ClearConstraintIcons()
        {
            foreach (Texture2D icon in ConstraintIcons.Values)
                if (icon != null) UnityEngine.Object.DestroyImmediate(icon);
            ConstraintIcons.Clear();
        }
    }
}
