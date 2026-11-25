using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Editor
{
    public class SerializedPropertyElement : GUIDrawer<SerializedProperty>
    {
        private GUIContent content;

        public SerializedPropertyElement(SerializedProperty target) : this(target, target.displayName)
        {
        }

        public SerializedPropertyElement(SerializedProperty target, string label) : base (target)
        {
            content = SettingsLocalization.Content(label, target.tooltip);
        }

        protected override void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            switch (Target.propertyType)
            {
                default:
                    SettingsLocalization.PropertyField(Target);
                    break;

                case SerializedPropertyType.Boolean:
                    Target.boolValue = GUILayout.Toggle (Target.boolValue, content);
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Target.serializedObject.ApplyModifiedProperties();
            }
        }

        protected override float GetHeight()
        {
            return EditorGUI.GetPropertyHeight(Target, Target.isExpanded);
        }
    }
}
