using System.Collections.Generic;
using UnityEngine;

namespace HierarchyDecorator.Editor
{
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

        [SerializeField, HideInInspector] private ObjectRowColors rowColors = new ObjectRowColors();
        [SerializeField, HideInInspector] private List<Color> paletteColors = new List<Color>();
        [SerializeField, HideInInspector] private Color paletteColor = new Color(0.56f, 0.52f, 0.84f);

        // Retain the serialized field name for existing installations.
        [SerializeField, HideInInspector]
        private bool needsPresetSelection;

        // Properties

        internal bool NeedsInitialSetup
        {
            get => needsPresetSelection;
            set => needsPresetSelection = value;
        }

        internal ObjectRowColors RowColors => rowColors;
        internal List<Color> PaletteColors => paletteColors;

        internal Color PaletteColor
        {
            get => paletteColor;
            set => paletteColor = value;
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

        internal void RestoreDefaults()
        {
            globalData = new GlobalData();
            styleData = new HierarchyStyleData();
            components = new ComponentData();
            components.OnInitialize();
            components.UpdateComponents();
            styleData.UpdateStyles(UnityEditor.EditorGUIUtility.isProSkin);

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
