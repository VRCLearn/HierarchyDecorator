using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Editor
{
    public class ComponentItem
    {
        // --- Properties
        public Settings HDSettings => HierarchyDecorator.Settings;

        public Component Component { get; private set; }
        public ComponentType Type => GetComponentInfo(HDSettings);
        public bool IsNullComponent { get; private set; }

        public readonly bool IsBehaviour;

        // - Visual

        public GUIContent Content => Type.Content;

        public string DisplayName => Type.DisplayName;
        public bool IsBuiltIn => Type.IsBuiltIn;
        // Use the same component eligibility as Unity's Inspector titlebar.
        public bool HasToggle => GetEnabledState() != -1;

        public bool Active { get; private set; }

        // --- Constructor 

        public ComponentItem(Component component)
        {
            Component = component;
            IsNullComponent = component == null;

            IsBehaviour = Component is Behaviour;

            if (IsNullComponent)
            {
                return;
            }

            IsNullComponent = Type == null;
            Active = GetActiveState();
        }

        // --- Methods

        public bool IsValid()
        {
            return Component == null == IsNullComponent;
        }

        private ComponentType GetComponentInfo(Settings settings)
        {
            if (IsNullComponent)
                return null;

            var type = Component.GetType();
            if (settings.Components.TryGetComponent(type, out ComponentType c))
            {
                return c;
            }

            if (!settings.Components.TryGetCustomComponent(type, out c) && settings.Components.RegisterCustomComponent(Component))
            {
                EditorUtility.SetDirty(settings);
                settings.Components.TryGetCustomComponent(type, out c);
            }

            return c;
        }

        internal int GetEnabledState() => IsNullComponent || Component == null ? -1 : EditorUtility.GetObjectEnabled(Component);

        private bool GetActiveState() => GetEnabledState() != 0;

        public void ToggleActive()
        {
            SetActive(!GetActiveState());
        }

        public void SetActive(bool active)
        {
            if (!HasToggle)
            {
                return;
            }

            Active = GetActiveState();
            if (Active == active)
            {
                return;
            }

            Undo.RecordObject(Component, active ? "Enable Component" : "Disable Component");
            EditorUtility.SetObjectEnabled(Component, active);
            if (PrefabUtility.IsPartOfPrefabInstance(Component))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(Component);
            }
            Active = GetActiveState();
        }

        public void UpdateActiveState()
        {
            Active = GetActiveState();
        }
    }
}
