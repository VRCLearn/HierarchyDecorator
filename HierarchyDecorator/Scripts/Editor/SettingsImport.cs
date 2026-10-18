using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VRCLearn.HierarchyDecorator
{
    internal static class SettingsImport
    {
        private static readonly string[] SettingsFields = { "globalData", "styleData", "components" };

        internal static List<string> FindCandidates()
        {
            var candidates = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddCandidate(AssetDatabase.GUIDToAssetPath(
                EditorPrefs.GetString(Application.productName + "_HD_GUID", string.Empty)));
            AddCandidate("Assets/HierarchyDecorator/Settings.asset");
            foreach (string guid in AssetDatabase.FindAssets("t:Settings", new[] { "Assets" }))
                AddCandidate(AssetDatabase.GUIDToAssetPath(guid));
            return candidates;

            void AddCandidate(string path)
            {
                if (!string.IsNullOrEmpty(path) && seen.Add(path) && IsSettings(AssetDatabase.LoadMainAssetAtPath(path)))
                    candidates.Add(path);
            }
        }

        internal static Settings Read(string path)
        {
            Object source = AssetDatabase.LoadMainAssetAtPath(path);
            if (!IsSettings(source))
                throw new InvalidDataException("The selected wooshii settings asset is unavailable: " + path);
            return CopySettings(source);
        }

        private static Settings CopySettings(Object source)
        {
            Settings copy = ScriptableObject.CreateInstance<Settings>();
            try
            {
                using (var serializedSource = new SerializedObject(source))
                using (var serializedCopy = new SerializedObject(copy))
                {
                    foreach (string field in SettingsFields)
                    {
                        SerializedProperty property = serializedSource.FindProperty(field);
                        if (property == null)
                            throw new InvalidDataException("The selected settings are missing the " + field + " field.");

                        SerializedProperty end = property.GetEndProperty();
                        bool enterChildren;
                        do
                        {
                            SerializedProperty destination = serializedCopy.FindProperty(property.propertyPath);
                            enterChildren = destination != null && property.propertyType == SerializedPropertyType.Generic;
                            if (enterChildren)
                            {
                                if (property.isArray)
                                    destination.arraySize = property.arraySize;
                            }
                            else if (destination != null)
                            {
                                // Copy leaves only: namespace changes do not affect nested value types,
                                // and object references retain their original Unity asset identity.
                                serializedCopy.CopyFromSerializedProperty(property);
                            }
                        }
                        while (property.Next(enterChildren) && !SerializedProperty.EqualContents(property, end));
                    }

                    serializedCopy.ApplyModifiedPropertiesWithoutUndo();
                }

                copy.name = "VRCLearn HierarchyDecorator Settings";
                copy.NeedsPresetSelection = false;
                copy.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
                return copy;
            }
            catch
            {
                Object.DestroyImmediate(copy);
                throw;
            }
        }

        private static bool IsSettings(Object value)
        {
            if (value == null)
                return false;
            Type type = value.GetType();
            return type.FullName == "HierarchyDecorator.Settings"
                && type.Assembly.GetName().Name == "Wooshii.HierarchyDecorator.Editor";
        }
    }
}
