using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace HierarchyDecorator
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

        private static void UpdateComponentData()
        {
            Settings.Components.UpdateData();
            Settings.Components.UpdateComponents(true);
            EditorUtility.SetDirty(Settings);
        }

        // Factory Methods

        /// <summary>
        /// Restore local settings, migrating a copy of an existing asset when needed.
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
                Settings legacy = FindLegacySettings();
                settings = legacy != null ? UnityEngine.Object.Instantiate(legacy) : ScriptableObject.CreateInstance<Settings>();
                settings.name = "HierarchyDecorator Settings";
                settings.NeedsPresetSelection = legacy == null;
            }

            settings.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
            settings.styleData.UpdateStyles(EditorGUIUtility.isProSkin);
            SessionState.SetInt(SettingsSessionKey, settings.GetInstanceID());

            if (!hasLocalFile)
                SaveSettings(settings);

            return settings;
        }

        private static Settings FindLegacySettings()
        {
            string path = AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(Constants.Paths.PREF_GUID, string.Empty));
            Settings settings = AssetDatabase.LoadAssetAtPath<Settings>(path);
            if (settings != null)
                return settings;

            settings = AssetDatabase.LoadAssetAtPath<Settings>(Constants.Paths.DEFAULT_ASSET_PATH);
            if (settings != null)
                return settings;

            foreach (string guid in AssetDatabase.FindAssets("t:" + SETTINGS_TYPE_STRING, new[] { "Assets" }))
            {
                settings = AssetDatabase.LoadAssetAtPath<Settings>(AssetDatabase.GUIDToAssetPath(guid));
                if (settings != null)
                    return settings;
            }

            return null;
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

        private static void SaveSettings(Settings settings)
        {
            string path = Constants.Paths.SETTINGS_FILE_PATH;
            string temporaryPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { settings }, temporaryPath, true);
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);

                EditorUtility.ClearDirty(settings);
                s_SaveErrorReported = false;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                if (!s_SaveErrorReported)
                    Debug.LogError("Cannot save HierarchyDecorator settings to " + path + ": " + exception.Message);
                s_SaveErrorReported = true;
                EditorUtility.SetDirty(settings);
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
