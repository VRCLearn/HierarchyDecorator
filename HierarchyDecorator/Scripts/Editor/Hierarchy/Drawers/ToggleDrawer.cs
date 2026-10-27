using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif

namespace HierarchyDecorator.Editor
{
    public class ToggleDrawer : HierarchyDrawer
    {
        // --- State 

        private bool isHoldingMouse = false;
        private bool isPressingShift = false;

        // --- Toggle

        private GUIStyle currentToggleOnStyle, currentToggleOffStyle;

        // Target data for settings based off the first instance selected
        
        private GameObject targetInstance = null;

        private bool targetActiveState = false;
        private int targetDepth = 0;

        // --- Selection

        private Event CurrentEvent => Event.current;
        private List<Object> swipedInstances = new List<Object> ();

        // Properties

        private GameObject[] SelectedInstances => Selection.gameObjects;

        // Methods

        protected override bool DrawerIsEnabled(HierarchyItem item, Settings settings)
        {
            return settings.globalData.showActiveToggles && PrefabStageUtility.GetCurrentPrefabStage () == null;
        }

        protected override void DrawInternal(Rect rect, HierarchyItem item, Settings settings)
        {
            rect.width = 16f;

#if UNITY_2019_1_OR_NEWER
            rect.x = 32;
#else
            rect.x = 1f;
            rect.y--;
#endif

            // Handle events if they're valid

            GameObject instance = item.GameObject;
            HandleEvent (rect, instance, settings);

            if (isPressingShift)
            {
                HandleMultiToggle (rect, instance, settings);
            }
            else
            if (isHoldingMouse && settings.globalData.activeSwiping)
            {
                HandleSwiping (rect, instance, settings);
            }

            // Draw toggles

            DrawToggles (rect, instance, settings.globalData.activeToggleType, !settings.globalData.activeSwiping);
        }

        private void DrawToggles(Rect rect, GameObject instance, GlobalData.ToggleType toggleType, bool canUpdate = true)
        {
            bool isActive = instance.activeSelf;

            // Draw toggle

            EditorGUI.BeginChangeCheck ();
            {
#if UNITY_2019_1_OR_NEWER
                switch (toggleType)
                {
                    case GlobalData.ToggleType.Checkbox:
                        _RecreateStyleIfNeeded(Style.Toggle, Style.ToggleMixed);
                        isActive = EditorGUI.Toggle(rect, isActive, isActive ? currentToggleOnStyle : currentToggleOffStyle);
                        break;
                    case GlobalData.ToggleType.Dot:
                        _RecreateStyleIfNeeded(Style.ToggleDot, Style.ToggleDot);
                        var color = GUI.color;
                        if (!isActive)
                            GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.15f);
                        isActive = EditorGUI.Toggle(rect, isActive, isActive ? currentToggleOnStyle : currentToggleOffStyle);
                        GUI.color = color;
                        break;
                }
                void _RecreateStyleIfNeeded(GUIStyle on, GUIStyle off)
                {
                    if (currentToggleOnStyle == null || currentToggleOnStyle.name != on.name)
                        currentToggleOnStyle = new GUIStyle(on);
                    if (currentToggleOffStyle == null || currentToggleOffStyle.name != off.name)
                        currentToggleOffStyle = new GUIStyle(off);
                }
#else
                isActive = EditorGUI.Toggle (rect, isActive);
#endif
            }
            if (EditorGUI.EndChangeCheck ())
            {
                if (canUpdate)
                {
                    Undo.RecordObject(instance, isActive ? "Enable GameObject" : "Disable GameObject");
                    instance.SetActive (isActive);
                    if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                    }
                }
            }
        }

        // --- Behaviour 

        private void HandleSwiping(Rect rect, GameObject instance, Settings settings)
        {
            // Ignore instance if the mouse is not interacting with it

            if (!rect.Contains (CurrentEvent.mousePosition))
            {
                return;
            }

            // Do not reselect the instance

            if (swipedInstances.Contains (instance))
            {
                return;
            }

            // If swiping is allowed on the current instance, toggle and register the toggle

            if (IsSwipingValid (settings, instance))
            {
                Undo.RecordObject (instance, string.Format ("Changed the active state of an {0}", instance.name));

                swipedInstances.Add (instance);
                instance.SetActive (!instance.activeSelf);
                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                }
            }
        }

        private void HandleMultiToggle(Rect rect, GameObject instance, Settings settings)
        {
            if (CurrentEvent.type != EventType.MouseDown || CurrentEvent.button != 0
                || !rect.Contains(CurrentEvent.mousePosition))
            {
                return;
            }

            GameObject[] instances = Selection.Contains(instance) ? SelectedInstances : new[] { instance };
            instances = System.Array.FindAll(instances, selected => selected != null && selected.scene.IsValid());
            bool active = !targetActiveState;
            Undo.RecordObjects(instances, active ? "Enable Selected GameObjects" : "Disable Selected GameObjects");
            foreach (GameObject selected in instances)
            {
                if (selected == null || selected.activeSelf == active)
                {
                    continue;
                }

                selected.SetActive(active);
                if (PrefabUtility.IsPartOfPrefabInstance(selected))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(selected);
                }
            }
            CurrentEvent.Use();
        }

        private void HandleEvent(Rect rect, GameObject instance, Settings settings)
        {
            EventType eventType = CurrentEvent.type;

            if (eventType == EventType.MouseDown && CurrentEvent.button == 0
                && rect.Contains(CurrentEvent.mousePosition))
            {
                swipedInstances.Clear();
                isPressingShift = CurrentEvent.shift;
                isHoldingMouse = true;
                targetInstance = instance;
                targetActiveState = targetInstance.activeSelf;
                targetDepth = GetInstanceDepth(targetInstance.transform);
            }

            if (isHoldingMouse && ((eventType == EventType.MouseUp && CurrentEvent.button == 0)
                || eventType == EventType.Ignore))
            {
                isPressingShift = false;
                isHoldingMouse = false;

                targetInstance = null;
                swipedInstances.Clear ();
            }
        }

        // 

        private bool IsSwipingValid(Settings settings, GameObject instance)
        {
            // --- Active Swiping

            if (settings.globalData.activeSwiping)
            {
                // Cancel out early if we've not prepared and to allow normal functionality

                if (targetInstance == null)
                {
                    return true;
                }

                // If selectionOnly is enabled, check if the instance has been selected

                if (settings.globalData.swipeSelectionOnly)
                {
                    if (SelectedInstances.Length > 1 && !Selection.Contains (instance))
                    {
                        return false;
                    }
                }

                // If states have to be the same, check for the same state as the first

                if (settings.globalData.swipeSameState && targetActiveState != instance.activeSelf)
                {                
                    return false;
                }

                // If depth has to be valid, calculate depth and check

                if (settings.globalData.depthMode != DepthMode.All)
                {
                    int depth = GetInstanceDepth (instance.transform);

                    switch (settings.globalData.depthMode)
                    {
                        case DepthMode.SameDepth:
                            return depth == targetDepth;

                        case DepthMode.SameDepthOrLower:
                            return depth >= targetDepth;

                        case DepthMode.SameDepthOrHigher:
                            return depth <= targetDepth;
                    }
                }
            }

            return true;
        }

        private int GetInstanceDepth(Transform transform)
        {
            int depth = 0;

            while (transform.parent != null)
            {
                transform = transform.parent;
                depth++;
            }

            return depth;
        }
    }
}
