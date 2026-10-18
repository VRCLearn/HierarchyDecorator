using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VRCLearn.HierarchyDecorator
{
    /// <summary>
    /// Reads independent copies of previous settings without changing their source.
    /// </summary>
    internal static class SettingsImport
    {
        private const string PreviousLocalPath = "UserSettings/HierarchyDecoratorSettings.asset";
        private const string CurrentLocalPath = "UserSettings/VRCLearnHierarchyDecoratorSettings.asset";
        private const string PreviousAssetPath = "Assets/HierarchyDecorator/Settings.asset";
        private const string PreviousScriptGuid = "00668fd727de9bb4081a8a202ce24c3b";
        private const string CurrentScriptGuid = "b0a4f62e4eb755debbb0f8c645b68a87";
        private const string CurrentClassIdentifier = "VRCLearn.HierarchyDecorator.Editor::VRCLearn.HierarchyDecorator.Settings";
        private static readonly string[] SettingsFields = { "globalData", "styleData", "components" };
        private static readonly Regex MonoBehaviourDocument = new Regex(
            @"^--- !u!114 &[^\r\n]+\r?\nMonoBehaviour:\r?\n.*?(?=^--- |\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        private static readonly Regex ScriptReference = new Regex(
            @"^  m_Script: \{fileID: 11500000, guid: (?<guid>[0-9a-fA-F]{32}), type: 3\}[ \t]*\r?$",
            RegexOptions.Multiline);

        internal sealed class Candidate
        {
            internal string Path;
            internal string Label;
        }

        internal static List<Candidate> FindCandidates()
        {
            var candidates = new List<Candidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddCandidate(PreviousLocalPath, "Previous local settings", true);
            string preferred = AssetDatabase.GUIDToAssetPath(
                EditorPrefs.GetString(Application.productName + "_HD_GUID", string.Empty));
            AddCandidate(preferred, "Previously selected settings");
            AddCandidate(PreviousAssetPath, "Previous default settings");

            foreach (string guid in AssetDatabase.FindAssets("t:Settings", new[] { "Assets" }))
                AddCandidate(AssetDatabase.GUIDToAssetPath(guid), "Settings asset");

            return candidates;

            void AddCandidate(string path, string label, bool knownLocal = false)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path) || IsCurrentLocalPath(path))
                    return;

                if (!seen.Add(System.IO.Path.GetFullPath(path)))
                    return;

                Object source = knownLocal ? null : AssetDatabase.LoadMainAssetAtPath(path);
                if (knownLocal || IsSettings(source) || HasPreviousYamlSettings(path))
                    candidates.Add(new Candidate { Path = path, Label = label });
            }
        }

        internal static Settings Read(Candidate candidate)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.Path))
                throw new ArgumentException("Select a settings file to import.", nameof(candidate));

            string path = candidate.Path;
            if (IsCurrentLocalPath(path))
                throw new InvalidOperationException("The current VRCLearn settings file cannot be imported into itself.");
            if (!File.Exists(path))
                throw new FileNotFoundException("The settings file no longer exists.", path);

            Object asset = path.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal)
                || path.Replace('\\', '/').StartsWith("Packages/", StringComparison.Ordinal)
                ? AssetDatabase.LoadMainAssetAtPath(path)
                : null;
            if (IsSettings(asset))
                return CopySettings(asset);

            if (!TryReadYaml(path, out string yaml))
                throw new InvalidDataException("Cannot import " + path + ": the file is not supported Unity YAML settings. " +
                    "Open it with the original plugin and save it as a text asset first.");

            int matched = 0;
            string mapped = MonoBehaviourDocument.Replace(yaml, match =>
            {
                string document = match.Value;
                Match script = ScriptReference.Match(document);
                if (!script.Success || !string.Equals(script.Groups["guid"].Value, PreviousScriptGuid, StringComparison.OrdinalIgnoreCase))
                    return document;

                foreach (string field in SettingsFields)
                {
                    if (!Regex.IsMatch(document, "^  " + field + ":", RegexOptions.Multiline))
                        throw new InvalidDataException("Cannot import " + path + ": missing settings field " + field + ".");
                }

                matched++;
                document = ScriptReference.Replace(document,
                    reference => reference.Value.Replace(reference.Groups["guid"].Value, CurrentScriptGuid), 1);
                var identifier = new Regex(@"^  m_EditorClassIdentifier:[^\r\n]*\r?$", RegexOptions.Multiline);
                if (identifier.IsMatch(document))
                    return identifier.Replace(document, "  m_EditorClassIdentifier: " + CurrentClassIdentifier, 1);

                Match mappedScript = ScriptReference.Match(document);
                return document.Insert(mappedScript.Index + mappedScript.Length,
                    "\n  m_EditorClassIdentifier: " + CurrentClassIdentifier);
            });

            if (matched != 1)
                throw new InvalidDataException("Cannot import " + path + ": expected one previous HierarchyDecorator settings object, found " + matched + ".");

            string temporaryPath = "UserSettings/VRCLearnHierarchyDecorator-import-" + Guid.NewGuid().ToString("N") + ".tmp";
            Object[] loadedObjects = null;
            try
            {
                Directory.CreateDirectory("UserSettings");
                File.WriteAllText(temporaryPath, mapped);
                loadedObjects = InternalEditorUtility.LoadSerializedFileAndForget(temporaryPath);
                if (loadedObjects != null)
                {
                    foreach (Object loaded in loadedObjects)
                    {
                        if (loaded is Settings)
                            return CopySettings(loaded);
                    }
                }

                throw new InvalidDataException("Unity could not deserialize the previous settings from " + path + ".");
            }
            finally
            {
                if (loadedObjects != null)
                {
                    foreach (Object loaded in loadedObjects)
                    {
                        if (loaded != null && !EditorUtility.IsPersistent(loaded))
                            Object.DestroyImmediate(loaded);
                    }
                }

                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
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
            if (value is Settings)
                return true;

            Type type = value.GetType();
            return type.FullName == "HierarchyDecorator.Settings"
                && type.Assembly.GetName().Name == "Wooshii.HierarchyDecorator.Editor";
        }

        private static bool IsCurrentLocalPath(string path)
        {
            return string.Equals(System.IO.Path.GetFullPath(path), System.IO.Path.GetFullPath(CurrentLocalPath), StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasPreviousYamlSettings(string path)
        {
            if (!TryReadYaml(path, out string yaml))
                return false;

            foreach (Match document in MonoBehaviourDocument.Matches(yaml))
            {
                Match script = ScriptReference.Match(document.Value);
                if (script.Success && string.Equals(script.Groups["guid"].Value, PreviousScriptGuid, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool TryReadYaml(string path, out string yaml)
        {
            yaml = null;
            using (var reader = new StreamReader(path))
            {
                var header = new char[5];
                if (reader.Read(header, 0, header.Length) != header.Length || new string(header) != "%YAML")
                    return false;

                yaml = new string(header) + reader.ReadToEnd();
                return true;
            }
        }
    }
}
