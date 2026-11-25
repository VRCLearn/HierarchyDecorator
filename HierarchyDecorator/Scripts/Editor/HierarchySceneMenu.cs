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
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HierarchyDecorator.Editor
{
    internal static class HierarchySceneMenu
    {
        private const string AssetsPrefix = "Assets/";

        internal static bool CanSwitch => !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            PrefabStageUtility.GetCurrentPrefabStage() == null;

        internal static void Show(Rect screenAnchor, Scene sourceScene)
        {
            if (CanSwitch && sourceScene.IsValid())
                PopupWindow.Show(GUIUtility.ScreenToGUIRect(screenAnchor), new ScenePopup(sourceScene, screenAnchor.height));
        }

        internal static void OpenScene(Scene sourceScene, string guid)
        {
            if (!CanSwitch || !sourceScene.IsValid() || !TryResolveScene(guid, out string path) ||
                string.Equals(sourceScene.path, path, StringComparison.Ordinal))
                return;

            Scene existing = SceneManager.GetSceneByPath(path);
            if (existing.IsValid() && existing.isLoaded)
            {
                SceneManager.SetActiveScene(existing);
                return;
            }

            // Only the scene being replaced needs a save prompt. Other open scenes stay untouched.
            if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { sourceScene }) ||
                !CanSwitch || !sourceScene.IsValid() || !TryResolveScene(guid, out path))
                return;

            existing = SceneManager.GetSceneByPath(path);
            if (existing.IsValid())
            {
                if (!existing.isLoaded)
                    existing = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(existing);
                return;
            }

            Scene previousActive = SceneManager.GetActiveScene();
            bool replacingActive = previousActive == sourceScene;
            // Open first so a failed load cannot discard the source scene.
            Scene replacement = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (!replacement.IsValid() || !replacement.isLoaded)
                return;

            EditorSceneManager.MoveSceneBefore(replacement, sourceScene);
            if (!EditorSceneManager.CloseScene(sourceScene, true))
            {
                EditorSceneManager.CloseScene(replacement, true);
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                return;
            }

            Scene active = replacingActive ? replacement : previousActive;
            if (active.IsValid() && active.isLoaded)
                SceneManager.SetActiveScene(active);
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

        private sealed class ScenePopup : PopupWindowContent
        {
            private readonly float rowHeight;
            private readonly Scene sourceScene;
            private readonly string favoritesKey;
            private readonly HashSet<string> favorites;
            private readonly List<SceneEntry> scenes = new List<SceneEntry>();
            private readonly List<SceneEntry> filtered = new List<SceneEntry>();
            private readonly SearchField searchField = new SearchField();
            private readonly GUIContent addFavorite = new GUIContent("☆", SettingsLocalization.Text("Bookmark Scene"));
            private readonly GUIContent removeFavorite = new GUIContent("★", SettingsLocalization.Text("Remove Scene Bookmark"));
            private string search = string.Empty;
            private Vector2 scroll;
            private int selectedIndex = -1;
            private bool focusSearch = true;
            private GUIStyle rowStyle;
            private GUIStyle currentStyle;
            private GUIStyle starStyle;

            internal ScenePopup(Scene sourceScene, float hierarchyRowHeight)
            {
                this.sourceScene = sourceScene;
                rowHeight = Mathf.Max(1f, hierarchyRowHeight);
                favoritesKey = "com.vrclearn.hierarchydecorator.sceneBookmarks." + Hash128.Compute(Application.dataPath);
                favorites = new HashSet<string>(EditorPrefs.GetString(favoritesKey, string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
                Texture icon = EditorGUIUtility.ObjectContent(null, typeof(SceneAsset)).image;
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (IsScenePath(path))
                        scenes.Add(new SceneEntry(guid, path, icon, sourceScene.path));
                }
                SortAndFilter();
            }

            public override Vector2 GetWindowSize() => new Vector2(330f, Mathf.Min(300f, 33f + Mathf.Max(1, scenes.Count) * rowHeight));

            public override void OnOpen() => editorWindow.wantsMouseMove = true;

            public override void OnGUI(Rect rect)
            {
                if (rowStyle == null)
                {
                    rowStyle = new GUIStyle(EditorStyles.label) { padding = new RectOffset(4, 4, 0, 0), alignment = TextAnchor.MiddleLeft };
                    currentStyle = new GUIStyle(rowStyle) { fontStyle = FontStyle.Bold };
                    starStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
                }
                HandleKeyboard();
                if (focusSearch)
                {
                    searchField.SetFocus();
                    focusSearch = false;
                }
                string nextSearch = searchField.OnGUI(new Rect(6f, 6f, rect.width - 12f, 20f), search);
                if (nextSearch != search)
                {
                    search = nextSearch;
                    scroll = Vector2.zero;
                    SortAndFilter();
                }

                Rect viewport = new Rect(2f, 31f, rect.width - 4f, rect.height - 33f);
                if (filtered.Count == 0)
                {
                    GUI.Label(viewport, SettingsLocalization.Text("No scenes found"), EditorStyles.centeredGreyMiniLabel);
                    return;
                }
                bool needsScroll = filtered.Count * rowHeight > viewport.height;
                Rect content = new Rect(0f, 0f, viewport.width - (needsScroll ? 15f : 0f), filtered.Count * rowHeight);
                bool mouseInViewport = viewport.Contains(Event.current.mousePosition);
                scroll = GUI.BeginScrollView(viewport, scroll, content);
                try
                {
                    int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / rowHeight));
                    int last = Mathf.Min(filtered.Count, Mathf.CeilToInt((scroll.y + viewport.height) / rowHeight));
                    for (int i = first; i < last; i++)
                        DrawScene(new Rect(0f, i * rowHeight, content.width, rowHeight), filtered[i], i, mouseInViewport);
                }
                finally { GUI.EndScrollView(); }
                if (Event.current.type == EventType.MouseMove)
                    editorWindow.Repaint();
            }

            private void DrawScene(Rect rect, SceneEntry entry, int index, bool mouseInViewport)
            {
                Event evt = Event.current;
                bool hovered = mouseInViewport && rect.Contains(evt.mousePosition);
                if (evt.type == EventType.Repaint && (hovered || index == selectedIndex))
                    EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.36f, 0.53f) : new Color(0.24f, 0.49f, 0.78f, 0.45f));

                float starWidth = rowHeight + 2f;
                Rect starRect = new Rect(rect.xMax - starWidth, rect.y, starWidth, rect.height);
                bool favorite = favorites.Contains(entry.Guid);
                GUIContent star = favorite ? removeFavorite : hovered || index == selectedIndex ? addFavorite : GUIContent.none;
                if (GUI.Button(starRect, star, starStyle))
                {
                    if (favorite) favorites.Remove(entry.Guid);
                    else favorites.Add(entry.Guid);
                    var saved = new List<string>(favorites);
                    saved.Sort(StringComparer.Ordinal);
                    EditorPrefs.SetString(favoritesKey, string.Join(",", saved.ToArray()));
                    SortAndFilter();
                    editorWindow.Repaint();
                    return;
                }

                Rect labelRect = rect;
                labelRect.xMax = starRect.x;
                if (entry.Loaded && !entry.Current)
                {
                    float statusWidth = EditorStyles.miniLabel.CalcSize(entry.Status).x + 8f;
                    Rect status = new Rect(labelRect.xMax - statusWidth, rect.y, statusWidth, rect.height);
                    GUI.Label(status, entry.Status, EditorStyles.miniLabel);
                    labelRect.xMax = status.x;
                }
                GUI.Label(labelRect, entry.Content, entry.Current ? currentStyle : rowStyle);
                if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && !evt.control && !evt.command && !evt.shift &&
                    hovered && !starRect.Contains(evt.mousePosition))
                {
                    evt.Use();
                    Select(entry);
                }
            }

            private void HandleKeyboard()
            {
                Event evt = Event.current;
                if (evt.type != EventType.KeyDown) return;
                if (evt.keyCode == KeyCode.Escape)
                {
                    evt.Use();
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }
                if (filtered.Count > 0 && (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.UpArrow))
                {
                    selectedIndex = Mathf.Clamp(selectedIndex + (evt.keyCode == KeyCode.DownArrow ? 1 : -1), 0, filtered.Count - 1);
                    float top = selectedIndex * rowHeight;
                    float height = GetWindowSize().y - 33f;
                    if (top < scroll.y) scroll.y = top;
                    if (top + rowHeight > scroll.y + height) scroll.y = top + rowHeight - height;
                    evt.Use();
                    editorWindow.Repaint();
                }
                if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && selectedIndex >= 0 && selectedIndex < filtered.Count)
                {
                    evt.Use();
                    Select(filtered[selectedIndex]);
                }
            }

            private void Select(SceneEntry entry)
            {
                string guid = entry.Guid;
                Scene source = sourceScene;
                editorWindow.Close();
                EditorApplication.delayCall += () => OpenScene(source, guid);
                GUIUtility.ExitGUI();
            }

            private void SortAndFilter()
            {
                scenes.Sort((left, right) =>
                {
                    int order = favorites.Contains(right.Guid).CompareTo(favorites.Contains(left.Guid));
                    if (order == 0) order = StringComparer.OrdinalIgnoreCase.Compare(left.Content.text, right.Content.text);
                    return order == 0 ? StringComparer.OrdinalIgnoreCase.Compare(left.Path, right.Path) : order;
                });
                filtered.Clear();
                foreach (SceneEntry entry in scenes)
                    if (search.Length == 0 || entry.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        filtered.Add(entry);
                selectedIndex = -1;
            }
        }

        private sealed class SceneEntry
        {
            internal readonly string Guid;
            internal readonly string Path;
            internal readonly GUIContent Content;
            internal readonly bool Current;
            internal readonly bool Loaded;
            internal readonly GUIContent Status;

            internal SceneEntry(string guid, string path, Texture icon, string currentPath)
            {
                Guid = guid;
                Path = path;
                Current = string.Equals(path, currentPath, StringComparison.Ordinal);
                Scene scene = SceneManager.GetSceneByPath(path);
                Loaded = scene.IsValid() && scene.isLoaded;
                Content = new GUIContent(System.IO.Path.GetFileNameWithoutExtension(path), icon, path +
                    (Current ? " (" + SettingsLocalization.Text("Current") + ")" : string.Empty));
                Status = new GUIContent(SettingsLocalization.Text("Loaded"));
            }
        }
    }
}
#endif
