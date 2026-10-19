using UnityEngine;

namespace HierarchyDecorator.Editor
{
    internal enum SettingsPreset { Detailed, Minimal }

    /// <summary>
    /// ScriptableObject containing all settings and relevant data for the hierarchy
    /// </summary>
    public class Settings : ScriptableObject, ISerializationCallbackReceiver
    {
        // Fields

        public GlobalData globalData = new GlobalData ();
        public HierarchyStyleData styleData = new HierarchyStyleData ();

        [SerializeField]
        private ComponentData components = new ComponentData ();

        // Missing in older settings files, so existing configurations skip setup.
        [SerializeField, HideInInspector]
        private bool needsPresetSelection;

        // Properties

        internal bool NeedsPresetSelection
        {
            get => needsPresetSelection;
            set => needsPresetSelection = value;
        }

        public ComponentData Components
        {
            get
            {
                return components;
            }
        }

        // Settings Creation

        private void OnEnable()
        {
            components.OnInitialize ();
        }

        /// <summary>
        /// Setup defaults for the new settings asset
        /// </summary>
        internal void SetDefaults(bool isDarkMode)
        {
            components.UpdateData ();
            styleData.UpdateStyles (isDarkMode);
        }

        internal void ApplyPreset(SettingsPreset preset)
        {
            globalData = new GlobalData();
            styleData = new HierarchyStyleData();
            components = new ComponentData();
            components.OnInitialize();
            components.UpdateComponents();
            styleData.UpdateStyles(UnityEditor.EditorGUIUtility.isProSkin);

            bool detailed = preset == SettingsPreset.Detailed;
            globalData.tagSettings.show = detailed;
            globalData.layerSettings.show = detailed;
            globalData.showBreadcrumbs = detailed;
            styleData.twoToneBackground = detailed;
            needsPresetSelection = false;
        }

        // Serialization

        public void OnBeforeSerialize()
        {
            components.UpdateData ();
        }

        public void OnAfterDeserialize()
        {
            components.UpdateData ();
        }
    }
}
