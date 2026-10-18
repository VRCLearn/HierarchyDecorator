using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
{
    internal class InitialSetupWindow : EditorWindow
    {
        private const string DetailedPreviewGuid = "8c2c86f2ed464384ab7693ff820cd9a6";
        private const string MinimalPreviewGuid = "0938738cc3ab43e5b342977dbf236c63";

        [SerializeField]
        private bool allowReselection;
        private Texture2D detailedPreview;
        private Texture2D minimalPreview;

        internal static void Open(bool allowReselection = false)
        {
            var window = GetWindow<InitialSetupWindow>(true, "Hierarchy Decorator", true);
            window.allowReselection = allowReselection;
            window.minSize = new Vector2(760f, 340f);
        }

        private void OnEnable()
        {
            detailedPreview = LoadPreview(DetailedPreviewGuid);
            minimalPreview = LoadPreview(MinimalPreviewGuid);
            SettingsLocalization.LanguageChanged += Repaint;
        }

        private void OnDisable()
        {
            SettingsLocalization.LanguageChanged -= Repaint;
        }

        private static Texture2D LoadPreview(string guid)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private void OnGUI()
        {
            if (!allowReselection && !HierarchyDecorator.Settings.NeedsPresetSelection)
            {
                Close();
                return;
            }

            SettingsPreset? selected = null;
            float previewHeight = Mathf.Min((position.width - 60f) * 0.5f * 270f / 420f, position.height - 106f - EditorGUIUtility.singleLineHeight);
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Space(12f);
                SettingsLocalization.DrawLanguageSelector();
                GUILayout.Space(12f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12f);
                    EditorGUILayout.LabelField(SettingsLocalization.Text("Choose a preset"), EditorStyles.boldLabel);
                }
                GUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12f);
                    if (DrawPreset(detailedPreview, "Detailed", previewHeight))
                        selected = SettingsPreset.Detailed;
                    GUILayout.Space(12f);
                    if (DrawPreset(minimalPreview, "Minimal", previewHeight))
                        selected = SettingsPreset.Minimal;
                    GUILayout.Space(12f);
                }
                GUILayout.Space(12f);
            }

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

        private static bool DrawPreset(Texture2D preview, string name, float height)
        {
            bool selected;
            string label = SettingsLocalization.Text(name);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Rect imageRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(height), GUILayout.ExpandWidth(true));
                if (preview != null)
                    GUI.DrawTexture(imageRect, preview, ScaleMode.ScaleToFit);
                selected = GUI.Button(imageRect, new GUIContent(string.Empty, label), GUIStyle.none);
                GUILayout.Space(6f);
                selected |= GUILayout.Button(label, GUILayout.Height(30f));
            }
            return selected;
        }
    }
}
