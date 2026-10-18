using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
{
    internal class InitialSetupWindow : EditorWindow
    {
        private Vector2 scrollPosition;

        [SerializeField]
        private bool allowReselection;

        internal static void Open(bool allowReselection = false)
        {
            var window = GetWindow<InitialSetupWindow>(true, "Hierarchy Decorator", true);
            window.allowReselection = allowReselection;
            window.minSize = new Vector2(420f, 360f);
        }

        private void OnEnable()
        {
            SettingsLocalization.LanguageChanged += Repaint;
        }

        private void OnDisable()
        {
            SettingsLocalization.LanguageChanged -= Repaint;
        }

        private void OnGUI()
        {
            if (!allowReselection && !HierarchyDecorator.Settings.NeedsPresetSelection)
            {
                Close();
                return;
            }

            SettingsPreset? selected = null;
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12f);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(12f);
                    SettingsLocalization.DrawLanguageSelector();
                    GUILayout.Space(8f);
                    EditorGUILayout.LabelField(SettingsLocalization.Text("Choose a preset"), EditorStyles.boldLabel);
                    GUILayout.Label(SettingsLocalization.Text("Choose the Hierarchy appearance. You can adjust these options in Settings later."), EditorStyles.wordWrappedLabel);
                    GUILayout.Space(12f);

                    if (DrawPreset("Detailed", "Original upstream defaults: show tags, layers, breadcrumbs, and alternating row backgrounds.", "Use detailed preset"))
                        selected = SettingsPreset.Detailed;

                    GUILayout.Space(8f);
                    if (DrawPreset("Minimal", "Previous VRCLearn defaults: hide tags, layers, breadcrumbs, and alternating row backgrounds. Keep component icons, active toggles, and scene highlighting.", "Use minimal preset"))
                        selected = SettingsPreset.Minimal;

                    GUILayout.Space(12f);
                }
                GUILayout.Space(12f);
            }
            EditorGUILayout.EndScrollView();

            if (selected.HasValue)
            {
                if (allowReselection)
                    HierarchyDecorator.ApplyPreset(selected.Value);
                else
                    HierarchyDecorator.ApplyInitialPreset(selected.Value);
                Close();
                GUIUtility.ExitGUI();
            }
        }

        private static bool DrawPreset(string title, string description, string button)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(SettingsLocalization.Text(title), EditorStyles.boldLabel);
                GUILayout.Label(SettingsLocalization.Text(description), EditorStyles.wordWrappedLabel);
                GUILayout.Space(6f);
                return GUILayout.Button(SettingsLocalization.Text(button), GUILayout.Height(28f));
            }
        }
    }
}
