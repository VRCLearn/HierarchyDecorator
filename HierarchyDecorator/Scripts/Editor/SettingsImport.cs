using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HierarchyDecorator.Editor
{
    internal static class SettingsImport
    {
        private static readonly string[] SettingsFields = { "globalData", "styleData", "components" };

        internal static string FindSource()
        {
            string guid = EditorPrefs.GetString(Application.productName + "_HD_GUID", string.Empty);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return null;
            Object source = AssetDatabase.LoadMainAssetAtPath(path);
            if (source == null)
            {
                RebindScript(File.ReadAllBytes(SourceFilePath(path)), null);
                return path;
            }
            return IsSettings(source) ? path : null;
        }

        internal static Settings Read(string path)
        {
            Object source = AssetDatabase.LoadMainAssetAtPath(path);
            if (source != null)
            {
                if (!IsSettings(source))
                    throw new InvalidDataException("The current wooshii settings asset is unavailable: " + path);
                return CopySettings(source);
            }

            byte[] bytes = RebindScript(File.ReadAllBytes(SourceFilePath(path)), CurrentScriptGuid());
            string temporaryPath = Path.Combine(Path.GetTempPath(), "HierarchyDecorator-" + Guid.NewGuid().ToString("N") + ".asset");
            Object[] loaded = null;
            try
            {
                File.WriteAllBytes(temporaryPath, bytes);
                string loadError = null;
                Application.LogCallback captureError = (message, stackTrace, type) =>
                {
                    if (loadError == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                        loadError = message;
                };
                Application.logMessageReceived += captureError;
                try
                {
                    loaded = InternalEditorUtility.LoadSerializedFileAndForget(temporaryPath);
                }
                finally
                {
                    Application.logMessageReceived -= captureError;
                }
                // Unity can return a default-valued object after logging a parse failure.
                if (loadError != null || loaded == null || loaded.Length != 1 || !(loaded[0] is Settings))
                    throw new InvalidDataException("Cannot read the current wooshii settings: " + path +
                        (loadError == null ? string.Empty : ": " + loadError));
                return CopySettings(loaded[0]);
            }
            finally
            {
                try
                {
                    if (loaded != null)
                    {
                        foreach (Object value in loaded)
                        {
                            if (value != null && !EditorUtility.IsPersistent(value))
                                Object.DestroyImmediate(value);
                        }
                    }
                }
                finally
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static string SourceFilePath(string assetPath)
        {
            if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                if (package != null)
                    return Path.Combine(package.resolvedPath, assetPath.Substring(package.assetPath.Length + 1));
            }
            return assetPath;
        }

        private static string CurrentScriptGuid()
        {
            Settings settings = ScriptableObject.CreateInstance<Settings>();
            try
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(settings)));
                if (string.IsNullOrEmpty(guid))
                    throw new InvalidDataException("Cannot locate the current Settings script.");
                return guid;
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        // Only rebind the script reference in a copy; Unity reads all settings and asset references.
        // A null replacement validates discovery without creating a temporary file.
        private static byte[] RebindScript(byte[] bytes, string replacement)
        {
            if (bytes.Length > 0 && (bytes[0] == '%' || bytes[0] == 0xef))
            {
                var encoding = new UTF8Encoding(false, true);
                string text = encoding.GetString(bytes);
                const RegexOptions options = RegexOptions.Multiline | RegexOptions.CultureInvariant;
                if (!Regex.IsMatch(text, @"\A\uFEFF?%YAML 1\.1\r?\n") ||
                    Regex.Matches(text, @"^--- !u!114 &-?\d+[ \t]*\r?$", options).Count != 1)
                    throw new InvalidDataException("Unsupported wooshii settings text format.");
                string pattern = @"^  m_Script:[ \t]*\{fileID:[ \t]*11500000,[ \t]*guid:[ \t]*(?<guid>" +
                    UpstreamAssets.SettingsScriptGuid + @"),[ \t]*type:[ \t]*3\}[ \t]*\r?$";
                MatchCollection bindings = Regex.Matches(text, pattern, options | RegexOptions.IgnoreCase);
                if (bindings.Count != 1)
                    throw new InvalidDataException("Cannot identify the original wooshii Settings script binding.");
                foreach (string field in SettingsFields)
                {
                    if (!Regex.IsMatch(text, @"^  " + field + @":[ \t]*\r?$", options))
                        throw new InvalidDataException("The current wooshii settings are missing the " + field + " field.");
                }
                if (replacement == null)
                    return bytes;
                Group guid = bindings[0].Groups["guid"];
                return encoding.GetBytes(text.Substring(0, guid.Index) + replacement + text.Substring(guid.Index + guid.Length));
            }

            if (bytes.Length < 20)
                throw new InvalidDataException("Invalid wooshii settings binary header.");
            ulong version = ReadUnsigned(bytes, 8, 4, true);
            if (version < 17 || version > 22 || bytes[16] > 1)
                throw new InvalidDataException("Unsupported wooshii settings binary version.");
            int headerSize = version >= 22 ? 48 : 20;
            if (bytes.Length < headerSize)
                throw new InvalidDataException("Invalid wooshii settings binary header.");
            ulong metadataSize = ReadUnsigned(bytes, version >= 22 ? 20 : 0, 4, true);
            ulong fileSize = ReadUnsigned(bytes, version >= 22 ? 24 : 4, version >= 22 ? 8 : 4, true);
            ulong dataOffset = ReadUnsigned(bytes, version >= 22 ? 32 : 12, version >= 22 ? 8 : 4, true);
            if (fileSize != (ulong)bytes.Length || metadataSize == 0 ||
                dataOffset < (ulong)headerSize || dataOffset > fileSize || metadataSize > dataOffset - (ulong)headerSize)
                throw new InvalidDataException("Invalid wooshii settings binary bounds.");
            int metadataEnd = headerSize + (int)metadataSize;
            foreach (string field in SettingsFields)
            {
                byte[] name = Encoding.UTF8.GetBytes(field + "\0");
                bool found = false;
                for (int i = headerSize; i <= metadataEnd - name.Length; i++)
                {
                    if ((i == headerSize || bytes[i - 1] == 0) && Matches(bytes, i, name))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new InvalidDataException("The current wooshii settings are missing the " + field + " field.");
            }
            byte[] oldGuid = BinaryGuid(UpstreamAssets.SettingsScriptGuid);
            int binding = -1;
            for (int i = headerSize; i <= metadataEnd - 21; i++)
            {
                if (!Matches(bytes, i, oldGuid) || ReadUnsigned(bytes, i + 16, 4, bytes[16] != 0) != 3 || bytes[i + 20] != 0)
                    continue;
                if (binding != -1)
                    throw new InvalidDataException("Multiple original wooshii Settings script bindings were found.");
                binding = i;
            }
            if (binding == -1)
                throw new InvalidDataException("Cannot identify the original wooshii Settings script binding.");
            if (replacement != null)
                Array.Copy(BinaryGuid(replacement), 0, bytes, binding, 16);
            return bytes;
        }

        private static ulong ReadUnsigned(byte[] bytes, int offset, int count, bool bigEndian)
        {
            ulong value = 0;
            for (int i = 0; i < count; i++)
                value = (value << 8) | bytes[offset + (bigEndian ? i : count - i - 1)];
            return value;
        }

        private static byte[] BinaryGuid(string guid)
        {
            var bytes = new byte[16];
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = Convert.ToByte(guid.Substring(i * 2, 2), 16);
                bytes[i] = (byte)((value << 4) | (value >> 4));
            }
            return bytes;
        }

        private static bool Matches(byte[] bytes, int offset, byte[] expected)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                if (bytes[offset + i] != expected[i])
                    return false;
            }
            return true;
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
                            throw new InvalidDataException("The current wooshii settings are missing the " + field + " field.");

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

                copy.name = "Settings";
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
