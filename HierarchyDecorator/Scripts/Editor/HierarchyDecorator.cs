using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace HierarchyDecorator.Editor
{
    [InitializeOnLoad]
    internal static class HierarchyDecorator
    {
        public static event Action OnSettings;

        public const string SETTINGS_TYPE_STRING = "Settings";
        private const string SettingsSessionKey = "com.vrclearn.hierarchydecorator.settings.instance";
        private static double s_NextSaveTime;
        private static bool s_SaveErrorReported;

        private static Settings s_Settings;
        public static Settings Settings
        {
            get
            {
                if (s_Settings == null)
                {
                    s_Settings = GetOrCreateSettings();
                    OnSettings?.Invoke();
                }

                return s_Settings;
            }

            private set
            {
                s_Settings = value;
            }
        }


        static HierarchyDecorator()
        {
            OnSettings -= UpdateComponentData;
            OnSettings += UpdateComponentData;

            EditorApplication.delayCall -= HierarchyManager.Initialize;
            EditorApplication.delayCall += HierarchyManager.Initialize;
            EditorApplication.delayCall += ShowInitialSetup;

            EditorApplication.update += SaveDirtySettings;
            AssemblyReloadEvents.beforeAssemblyReload += FlushSettings;
            EditorApplication.quitting += FlushSettings;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        // Setup 

        private static void ShowInitialSetup()
        {
            if (Application.isBatchMode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += ShowInitialSetup;
                return;
            }

            if (Settings.NeedsPresetSelection)
                InitialSetupWindow.Open();
        }

        internal static void ApplyInitialPreset(SettingsPreset preset)
        {
            if (!Settings.NeedsPresetSelection)
                return;

            ApplyPreset(preset);
        }

        internal static void ApplyPreset(SettingsPreset preset)
        {
            Settings settings = Settings;
            settings.ApplyPreset(preset);
            OnSettings?.Invoke();
            EditorUtility.SetDirty(settings);
            SaveSettings(settings);
            InternalEditorUtility.RepaintAllViews();
        }

        internal static void ImportSettings(string path)
        {
            Settings imported = SettingsImport.Read(path);
            Settings current = Settings;
            try
            {
                EditorUtility.CopySerialized(imported, current);
                current.name = "Settings";
                current.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
                current.NeedsPresetSelection = false;
                current.styleData.UpdateStyles(EditorGUIUtility.isProSkin);
                OnSettings?.Invoke();
                if (!SaveSettings(current))
                    throw new IOException("Cannot save imported settings.");
                InternalEditorUtility.RepaintAllViews();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(imported);
            }
        }

        private static void UpdateComponentData()
        {
            Settings.Components.UpdateData();
            Settings.Components.UpdateComponents(true);
            EditorUtility.SetDirty(Settings);
        }

        // Factory Methods

        /// <summary>
        /// Restore only this package's own local settings.
        /// </summary>
        /// <returns>The loaded settings</returns>
        private static Settings GetOrCreateSettings()
        {
            Settings settings = EditorUtility.InstanceIDToObject(SessionState.GetInt(SettingsSessionKey, 0)) as Settings;
            if (settings != null && !EditorUtility.IsPersistent(settings))
            {
                settings.styleData.UpdateStyles(EditorGUIUtility.isProSkin);
                return settings;
            }

            bool hasLocalFile = File.Exists(Constants.Paths.SETTINGS_FILE_PATH);
            if (hasLocalFile)
            {
                settings = null;
                UnityEngine.Object[] loadedObjects = InternalEditorUtility.LoadSerializedFileAndForget(Constants.Paths.SETTINGS_FILE_PATH);
                if (loadedObjects != null)
                {
                    foreach (UnityEngine.Object loaded in loadedObjects)
                    {
                        if (loaded is Settings loadedSettings)
                        {
                            settings = loadedSettings;
                            break;
                        }
                    }
                }

                if (settings == null)
                    throw new InvalidDataException("Cannot load HierarchyDecorator settings from " + Constants.Paths.SETTINGS_FILE_PATH);
            }
            else
            {
                settings = ScriptableObject.CreateInstance<Settings>();
                settings.name = "Settings";
                settings.NeedsPresetSelection = true;
            }

            settings.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
            settings.styleData.UpdateStyles(EditorGUIUtility.isProSkin);
            SessionState.SetInt(SettingsSessionKey, settings.GetInstanceID());

            if (!hasLocalFile)
                SaveSettings(settings);

            return settings;
        }

        private static void SaveDirtySettings()
        {
            if (EditorApplication.timeSinceStartup < s_NextSaveTime)
                return;

            s_NextSaveTime = EditorApplication.timeSinceStartup + 0.5;
            FlushSettings();
        }

        private static void FlushSettings()
        {
            if (s_Settings != null && EditorUtility.IsDirty(s_Settings))
                SaveSettings(s_Settings);
        }

        private static bool SaveSettings(Settings settings)
        {
            string path = Constants.Paths.SETTINGS_FILE_PATH;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (Directory.Exists(path))
                    throw new IOException("The settings path is a directory.");
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                    throw new UnauthorizedAccessException("The settings file is read-only.");

                // Unity already stages and moves the serialized file. An additional
                // File.Replace can fail on Windows after the native save succeeds.
                string saveError = null;
                Application.LogCallback captureError = (message, stackTrace, type) =>
                {
                    if (saveError == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                        saveError = message;
                };
                Application.logMessageReceived += captureError;
                try
                {
                    InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { settings }, path, true);
                }
                finally
                {
                    Application.logMessageReceived -= captureError;
                }
                // The native void API can log an error without throwing.
                if (saveError != null || !File.Exists(path))
                    throw new IOException(saveError ?? "Unity did not create the settings file.");

                EditorUtility.ClearDirty(settings);
                s_SaveErrorReported = false;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                if (!s_SaveErrorReported)
                    Debug.LogError("Cannot save HierarchyDecorator settings to " + path + ": " + exception.Message);
                s_SaveErrorReported = true;
                EditorUtility.SetDirty(settings);
                return false;
            }
        }

        private static void OnUndoRedo()
        {
            if (s_Settings == null)
                return;

            s_Settings.styleData.UpdateStyles(EditorGUIUtility.isProSkin);
            s_Settings.Components.UpdateComponents(true);
            EditorUtility.SetDirty(s_Settings);
            EditorApplication.RepaintHierarchyWindow();
        }

        /// <summary>
        /// Convert into serialized object for handling GUI
        /// </summary>
        /// <returns>Serialized version of the settings</returns>
        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject (Settings);
        }
    
        /// <summary>
        /// Handles any import updates required i.e. <see cref="ComponentType"/> cache.
        /// </summary>
        public class HierarchyDecoratorProcessor : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
            {
                if (importedAssets.Length > 0)
                {
                    UpdateComponentData();
                }
            }
        }
    }
}
