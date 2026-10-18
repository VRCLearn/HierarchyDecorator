using UnityEditor;
using UnityEngine;

namespace VRCLearn.HierarchyDecorator
{
    public class SerializedPropertyElement : GUIDrawer<SerializedProperty>
    {
        private GUIContent content;

        public SerializedPropertyElement(SerializedProperty target) : base (target) 
        {
            content = SettingsLocalization.Content(target.displayName, target.tooltip);
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
