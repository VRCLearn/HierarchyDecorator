#if UNITY_2019_1_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
#if !UNITY_2021_2_OR_NEWER
using PrefabStageUtility = UnityEditor.Experimental.SceneManagement.PrefabStageUtility;
#endif
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HierarchyDecorator.Editor
{
    internal static class HierarchySceneMenu
    {
        internal const string ButtonName = "hierarchy-decorator-open-scene";
        internal const float ButtonWidth = 28f;
        private const string MenuTitle = "Switch Scene";
        private const string AssetsPrefix = "Assets/";

        internal static bool CanSwitch => !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        internal static ToolbarButton CreateButton()
        {
            var sceneIcon = EditorGUIUtility.ObjectContent(null, typeof(SceneAsset)).image as Texture2D;
            ToolbarButton button = null;
            button = new ToolbarButton(() =>
            {
                if (CanSwitch && button.panel != null)
                    new SceneDropdown(sceneIcon).Show(button.worldBound);
            })
            {
                name = ButtonName,
                focusable = false,
                tooltip = SettingsLocalization.Text(MenuTitle)
            };
            button.style.position = Position.Absolute;
            button.style.width = ButtonWidth;
            button.style.height = 18f;
            button.style.flexDirection = FlexDirection.Row;
            button.style.marginLeft = button.style.marginRight = 0f;
            button.style.paddingLeft = button.style.paddingRight = 0f;
            button.style.borderLeftWidth = button.style.borderRightWidth = 0f;
            button.style.borderTopWidth = button.style.borderBottomWidth = 0f;
            button.style.backgroundColor = Color.clear;
            button.style.justifyContent = Justify.Center;
            button.style.alignItems = Align.Center;

            var icon = new Image
            {
                image = sceneIcon,
                pickingMode = PickingMode.Ignore
            };
            icon.style.width = icon.style.height = 16f;
            button.Add(icon);

            var arrow = new Label("▾") { pickingMode = PickingMode.Ignore };
            arrow.style.width = 8f;
            arrow.style.fontSize = 10f;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrow.style.marginLeft = arrow.style.marginRight = 0f;
            arrow.style.paddingLeft = arrow.style.paddingRight = 0f;
            button.Add(arrow);

            button.RegisterCallback<MouseEnterEvent>(_ => button.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.16f));
            button.RegisterCallback<MouseLeaveEvent>(_ => button.style.backgroundColor = Color.clear);
            return button;
        }

        internal static void OpenScene(string guid)
        {
            if (!CanSwitch || !TryResolveScene(guid, out string path))
                return;

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                StageUtility.GoToMainStage();
                if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                    return;

                if (!CanSwitch || !TryResolveScene(guid, out path))
                    return;
            }

            if (IsOnlyActiveScene(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!CanSwitch || PrefabStageUtility.GetCurrentPrefabStage() != null ||
                !TryResolveScene(guid, out path) || IsOnlyActiveScene(path))
                return;

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static bool IsOnlyActiveScene(string path)
        {
            Scene active = SceneManager.GetActiveScene();
            return SceneManager.sceneCount == 1 && active.isLoaded && string.Equals(active.path, path, StringComparison.Ordinal);
        }

        private static bool IsScenePath(string path) => !string.IsNullOrEmpty(path) &&
            path.StartsWith(AssetsPrefix, StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

        private static bool TryResolveScene(string guid, out string path)
        {
            path = string.IsNullOrEmpty(guid) ? string.Empty : AssetDatabase.GUIDToAssetPath(guid);
            return IsScenePath(path) &&
                File.Exists(Path.Combine(Application.dataPath, path.Substring(AssetsPrefix.Length))) &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
        }

        private sealed class SceneDropdown : AdvancedDropdown
        {
            private readonly Texture2D icon;

            public SceneDropdown(Texture2D icon) : base(new AdvancedDropdownState())
            {
                this.icon = icon;
                minimumSize = new Vector2(300f, 235f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(SettingsLocalization.Text(MenuTitle)) { id = 0 };
                var scenes = new List<SceneItem>();
                string currentPath = SceneManager.GetActiveScene().path;
                var loadedPaths = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && !string.IsNullOrEmpty(scene.path))
                        loadedPaths.Add(scene.path);
                }

                string current = SettingsLocalization.Text("Current");
                string loaded = SettingsLocalization.Text("Loaded");
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (IsScenePath(path))
                    {
                        string folder = path.Substring(0, path.LastIndexOf('/'));
                        string label = Path.GetFileNameWithoutExtension(path) + " — " + folder;
                        if (string.Equals(currentPath, path, StringComparison.Ordinal))
                            label += " (" + current + ")";
                        else if (loadedPaths.Contains(path))
                            label += " (" + loaded + ")";

                        scenes.Add(new SceneItem(guid, label, icon));
                    }
                }

                scenes.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.name, right.name));
                for (int i = 0; i < scenes.Count; i++)
                {
                    SceneItem scene = scenes[i];
                    scene.id = i + 1;
                    root.AddChild(scene);
                }
                if (scenes.Count == 0)
                    root.AddChild(new AdvancedDropdownItem(SettingsLocalization.Text("No scenes found")) { enabled = false });

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is SceneItem scene)
                    EditorApplication.delayCall += () => OpenScene(scene.Guid);
            }
        }

        private sealed class SceneItem : AdvancedDropdownItem
        {
            public string Guid { get; }

            public SceneItem(string guid, string label, Texture2D icon) : base(label)
            {
                Guid = guid;
                this.icon = icon;
            }
        }
    }
}
#endif
