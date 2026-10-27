using System;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Editor
{
    internal static class VrcComponentIcons
    {
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
                    case "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider, VRC.SDK3.Dynamics.PhysBone":
                        guid = "50d1897da169479dbe6fb24f9b62518c";
                        break;
                    default:
                        continue;
                }

                return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            }

            return null;
        }
    }
}
