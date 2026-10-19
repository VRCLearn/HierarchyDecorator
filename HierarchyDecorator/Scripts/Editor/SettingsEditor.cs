using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Editor
{
    //Seeing this makes me realize I should have made a smaller name for this.
    [CustomEditor (typeof (Settings))]
    internal class SettingsEditor : UnityEditor.Editor
    {
        private static Type[] s_TabTypes;
        private static GUIContent[] s_TabLabels;

        private Settings settings;

        private SettingsTab selectedTab;

        private List<SettingsTab> tabs = new List<SettingsTab> ();

        private bool hasInitialized;
        private int selectedTabIndex = 0;

        private void OnEnable()
        {
            hasInitialized = false;
            settings = target as Settings;

            Undo.undoRedoPerformed += Refresh;
            HierarchyDecorator.OnSettings += OnSettingsChanged;
            SettingsLocalization.LanguageChanged += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Refresh;
            HierarchyDecorator.OnSettings -= OnSettingsChanged;
            SettingsLocalization.LanguageChanged -= Repaint;
        }

        public override bool UseDefaultMargins()
        {
            return false;
        }

        public override void OnInspectorGUI()
        {
            if (!hasInitialized)
            {
                if (Event.current.type != EventType.Layout)
                {
                    Repaint();
                    return;
                }

                Initialize();
            }

            EditorGUILayout.BeginVertical(Style.InspectorPadding);
            SettingsLocalization.DrawLanguageSelector();
            DrawTitle();
            EditorGUI.BeginChangeCheck();
            DrawContent();
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(settings);
            EditorGUILayout.EndVertical();
        }

        private void Initialize()
        {
            SetupValues();
            RegisterTabs();

            hasInitialized = true;
        }

        private void DrawContent()
        {
            selectedTab?.OnGUI();
 
            if (selectedTab.IsDirty)
            {
                Refresh();
            }
        }

        private void DrawTitle()
        {
            // Draw Header
            
#if UNITY_2019_1_OR_NEWER
            EditorGUILayout.BeginVertical (Style.TabBackground, GUILayout.MinHeight (32f));
#else
            EditorGUILayout.BeginVertical (Style.TabBackground, GUILayout.MinHeight (16f));
#endif
            EditorGUILayout.BeginHorizontal ();
            {
                EditorGUILayout.LabelField (SettingsLocalization.Text("Hierarchy Settings"), Style.Title);

                GUILayout.FlexibleSpace ();

                // Link to repo for convenience

                if (GUILayout.Button (SettingsLocalization.Text("GitHub Repository"), EditorStyles.miniButtonMid))
                {
                    Application.OpenURL ("https://github.com/VRCLearn/HierarchyDecorator/");
                }
            }
            EditorGUILayout.EndHorizontal ();

            HierarchyGUI.Space ();

            // --- Selection

            EditorGUI.BeginChangeCheck ();
            {
                selectedTabIndex = GUILayout.SelectionGrid (selectedTabIndex, s_TabLabels, Mathf.Min (3, tabs.Count), Style.LargeButtonSmallTextStyle);
            }
            if (EditorGUI.EndChangeCheck ())
            {
                Refresh();
                selectedTab = tabs[selectedTabIndex];
            }

            EditorGUILayout.EndVertical ();
        }
        
        private void OnSettingsChanged()
        {
            hasInitialized = false;
            Refresh();
        }

        private void Refresh()
        {
            serializedObject.Update();
            serializedObject.ApplyModifiedProperties();
            EditorApplication.RepaintHierarchyWindow();
            Repaint();
        }

        private void RegisterTabs()
        {
            if (s_TabTypes == null)
            {
                s_TabTypes = GetTabs();
                s_TabLabels = new GUIContent[s_TabTypes.Length];
            }

            for (int i = 0; i < s_TabTypes.Length; i++)
            {
                Type tabType = s_TabTypes[i];
                SettingsTab tab = Activator.CreateInstance(tabType, settings, serializedObject) as SettingsTab;


                tabs.Add(tab);
                s_TabLabels[i] = tab.Content;
            }

            selectedTab = tabs[0];
        }

        private Type[] GetTabs()
        {
            SortedList<int, Type> validTabs = new SortedList<int, Type>();

            foreach (Type tabType in ReflectionUtility.GetTypesFromAssemblies(t => t.IsSubclassOf(typeof(SettingsTab))))
            {
                if (tabType.IsAbstract)
                {
                    continue;
                }

                RegisterTabAttribute attr = tabType.GetCustomAttribute<RegisterTabAttribute>();

                if (attr == null)
                {
                    continue;
                }

                validTabs.Add(attr.priority, tabType);
            }

            return validTabs.Values.ToArray();
        }

        private void SetupValues()
        {
            tabs.Clear ();
            selectedTabIndex = 0;
        }
    }
}
