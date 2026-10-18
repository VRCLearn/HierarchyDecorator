using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRCLearn.HierarchyDecorator
{
    internal class InitialSetupWindow : EditorWindow
    {
        private const string DetailedPreviewGuid = "8c2c86f2ed464384ab7693ff820cd9a6";
        private const string MinimalPreviewGuid = "0938738cc3ab43e5b342977dbf236c63";

        [SerializeField]
        private bool allowReselection;
        private Texture2D detailedPreview;
        private Texture2D minimalPreview;
        private List<string> importCandidates;
        private int selectedImport;
        private Vector2 scrollPosition;
        private string importError;

        internal static void Open(bool allowReselection = false)
        {
            var window = GetWindow<InitialSetupWindow>(true, "Hierarchy Decorator (VRCLearn)", true);
            window.allowReselection = allowReselection;
            window.minSize = new Vector2(760f, 340f);
        }

        private void OnEnable()
        {
            detailedPreview = LoadPreview(DetailedPreviewGuid);
            minimalPreview = LoadPreview(MinimalPreviewGuid);
            RefreshImports();
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

        private void RefreshImports()
        {
            importCandidates = SettingsImport.FindCandidates();
            selectedImport = 0;
            importError = null;
        }

        private void OnGUI()
        {
            if (!allowReselection && !HierarchyDecorator.Settings.NeedsPresetSelection)
            {
                Close();
                return;
            }

            SettingsPreset? selected = null;
            string migration = null;
            float previewHeight = Mathf.Min((position.width - 60f) * 0.5f * 270f / 420f, 270f);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Space(12f);
                SettingsLocalization.DrawLanguageSelector();
                migration = DrawImport();
                GUILayout.Space(12f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12f);
                    EditorGUILayout.LabelField(SettingsLocalization.Text("Choose a preset style"), EditorStyles.boldLabel);
                }
                GUILayout.Space(8f);
                using (new EditorGUI.DisabledScope(UpstreamMigration.IsRunning))
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
            EditorGUILayout.EndScrollView();

            if (migration != null)
            {
                allowReselection = true;
                try
                {
                    UpstreamMigration.Start(migration, error =>
                    {
                        if (this == null)
                            return;
                        if (error == null)
                            Close();
                        else
                        {
                            importError = error.Message;
                            Repaint();
                        }
                    });
                }
                catch (Exception exception)
                {
                    importError = exception.Message;
                }
                return;
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

        private string DrawImport()
        {
            if (importCandidates == null || importCandidates.Count == 0)
                return null;

            GUILayout.Space(12f);
            EditorGUILayout.LabelField(SettingsLocalization.Text("Migrate from wooshii"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(SettingsLocalization.Text("Choosing a preset keeps wooshii installed. Migration imports the selected settings, then removes the wooshii installation and the selected old settings asset."), MessageType.Info);
            selectedImport = EditorGUILayout.Popup(SettingsLocalization.Text("Settings source"), selectedImport, importCandidates.ToArray());
            if (!string.IsNullOrEmpty(importError))
                EditorGUILayout.HelpBox(importError, MessageType.Error);
            string migration = null;
            using (new EditorGUI.DisabledScope(UpstreamMigration.IsRunning || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(SettingsLocalization.Text(UpstreamMigration.IsRunning ? "Migrating..." : "Migrate settings and remove wooshii"), GUILayout.Height(30f)))
                {
                    importError = null;
                    migration = importCandidates[selectedImport];
                }
            }
            return migration;
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
