using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HierarchyDecorator.Editor
{
    internal sealed class GameObjectIconWindow : EditorWindow
    {
        private const string Title = "Decorate Object";
        private const string ResetArtworkGuid = "a459cc77dd434689b3edab153ff1f081";
        private const string AddArtworkGuid = "62583739412147f39a8bc2a2380786d6";
        private const string StarArtworkGuid = "940482a15c0244f393faf9b56f57718e";
        private const int PaletteColumns = 9;
        private const float PaletteWidth = 264f;
        private const float ContentWidth = 248f;
        private const float MinPaletteHeight = 259f;
        private const float MaxPaletteHeight = 340f;
        private const float Left = 14f;
        private const float TileSize = 24f;
        private const float TileStep = 26f;
        private const float ArtworkSize = 14f;
        private const float ActionArtworkSize = 10f;
        private const float ColorTilesY = 47f;
        private const float PopupGap = 4f;
        private static readonly Rect FullTexture = new Rect(0f, 0f, 1f, 1f);
        private static readonly Dictionary<Texture2D, ArtworkMetrics> ArtworkCache = new Dictionary<Texture2D, ArtworkMetrics>();

        private static GameObjectIconWindow activePalette;
        private static GameObject pressedPaletteTarget;

        private static readonly Color[] RowColors =
        {
            new Color32(137, 149, 166, 255), new Color32(222, 104, 111, 255),
            new Color32(230, 154, 82, 255), new Color32(221, 196, 102, 255),
            new Color32(130, 181, 107, 255), new Color32(79, 184, 167, 255),
            new Color32(103, 151, 233, 255), new Color32(173, 125, 232, 255)
        };

        private static readonly string[][] IconGroups =
        {
            new[] { "Folder Icon", "Canvas Icon" },
            new[] { "Camera Icon", "Light Icon", "ParticleSystem Icon", "Terrain Icon", "MeshRenderer Icon",
                "SpriteRenderer Icon", "SkinnedMeshRenderer Icon", "ReflectionProbe Icon" },
            new[] { "BoxCollider Icon", "SphereCollider Icon", "CapsuleCollider Icon", "Rigidbody Icon",
                "CharacterController Icon", "Cloth Icon", "WheelCollider Icon" },
            new[] { "AudioSource Icon", "AudioClip Icon", "Animator Icon", "AnimationClip Icon", "Material Icon",
                "Texture2D Icon", "ScriptableObject Icon", "LODGroup Icon" }
        };

        private struct Symbol
        {
            public Texture2D icon;
            public GUIContent tooltip;
            public Rect tile;
        }

        private struct ArtworkMetrics
        {
            public Rect bounds;
            public Vector2 opticalCenter;
        }

        private struct PaletteLayout
        {
            public float iconHeaderY;
            public float textureHeaderY;
            public float hintY;
            public float Height => hintY + 18f;

            public PaletteLayout(int colorCount, int textureCount)
            {
                iconHeaderY = ColorTilesY + RowCount(1 + RowColors.Length + colorCount) * TileStep;
                textureHeaderY = iconHeaderY + 18f + IconGroups.Length * TileStep;
                hintY = textureHeaderY + (textureCount > 0 ? 20f + RowCount(textureCount) * TileStep : 2f);
            }
        }

        [SerializeField] private GameObject targetObject;
        private readonly List<Symbol> symbols = new List<Symbol>();
        private Texture2D resetArtwork;
        private Texture2D addArtwork;
        private Texture2D starArtwork;
        private Vector2 hierarchyRightEdge;
        private Vector2 paletteScroll;
        private Vector2 pointerPosition;
        private bool pointerInPalette;
        private bool dismissOnFocusLoss;
        private bool choosingPaletteItem;

        internal static void HandleRowInput(Rect row, GameObject target)
        {
            if (!IsEditable(target))
            {
                return;
            }

            Event evt = Event.current;
            Rect icon = GameObjectIconInfo.GetSlot(row);
            Rect clickArea = evt.alt ? row : icon;
            bool selectionModifier = evt.shift || evt.control || evt.command;
            if (!selectionModifier)
            {
                EditorGUIUtility.AddCursorRect(clickArea, MouseCursor.Link);
            }

            bool insideClickArea = clickArea.Contains(evt.mousePosition);
            if (evt.type == EventType.MouseDown && evt.button == 0 && !selectionModifier && insideClickArea)
            {
                pressedPaletteTarget = target;
                evt.Use();
                return;
            }

            if (evt.rawType != EventType.MouseUp || evt.button != 0 || pressedPaletteTarget != target)
            {
                return;
            }

            pressedPaletteTarget = null;
            if (evt.type != EventType.MouseUp || selectionModifier || !insideClickArea)
            {
                return;
            }

            Vector2 anchor = GUIUtility.GUIToScreenPoint(new Vector2(row.xMax, row.yMin));
            EditorWindow hierarchy = focusedWindow;
            if (hierarchy != null && hierarchy.GetType().Name == "SceneHierarchyWindow")
            {
                anchor.x = hierarchy.position.xMax;
            }
            anchor.x += PopupGap;
            evt.Use();
            RunOnNextUpdate(() =>
            {
                if (IsEditable(target))
                {
                    ShowPalette(target, anchor);
                }
            });
        }

        private static void RunOnNextUpdate(Action action)
        {
            EditorApplication.update += Run;

            void Run()
            {
                EditorApplication.update -= Run;
                action();
            }
        }

        private static void ShowPalette(GameObject target, Vector2 anchor)
        {
            if (activePalette != null)
            {
                activePalette.Close();
            }

            GameObjectIconWindow window = CreatePalette(target);
            Vector2 size = GetPaletteSize();
            window.hierarchyRightEdge = anchor;
            window.minSize = window.maxSize = size;
            window.position = FitPopupToDesktop(anchor, size);
            activePalette = window;
            // ShowAsDropDown can be dismissed by Unity's auxiliary window manager before the next frame.
            window.ShowPopup();
            window.Focus();
            window.dismissOnFocusLoss = true;
        }

        private static Rect FitPopupToDesktop(Vector2 anchor, Vector2 size)
        {
            Rect desktop = InternalEditorUtility.GetBoundsOfDesktopAtPoint(anchor);
            float x = Mathf.Clamp(anchor.x, desktop.xMin, Mathf.Max(desktop.xMin, desktop.xMax - size.x));
            float y = Mathf.Clamp(anchor.y, desktop.yMin, Mathf.Max(desktop.yMin, desktop.yMax - size.y));
            return new Rect(new Vector2(x, y), size);
        }

        private void PositionPicker(EditorWindow picker)
        {
            Vector2 size = picker.position.size;
            var besidePalette = new Vector2(position.xMax + PopupGap, position.y);
            Rect beside = FitPopupToDesktop(besidePalette, size);
            picker.position = beside.x >= besidePalette.x
                ? beside
                : FitPopupToDesktop(new Vector2(hierarchyRightEdge.x, position.y), size);
        }

        private static Vector2 GetPaletteSize()
        {
            Settings settings = HierarchyDecorator.Settings;
            var layout = new PaletteLayout(settings.PaletteColors.Count,
                settings.PaletteTextures.FindAll(IsProjectTexture).Count);
            return new Vector2(PaletteWidth, Mathf.Clamp(layout.Height, MinPaletteHeight, MaxPaletteHeight));
        }

        private static int RowCount(int count)
        {
            return (count + PaletteColumns - 1) / PaletteColumns;
        }

        private static GameObjectIconWindow CreatePalette(GameObject target)
        {
            var window = CreateInstance<GameObjectIconWindow>();
            window.targetObject = target;
            window.resetArtwork = AssetUtility.LoadAssetFromGUID<Texture2D>(ResetArtworkGuid);
            window.addArtwork = AssetUtility.LoadAssetFromGUID<Texture2D>(AddArtworkGuid);
            window.starArtwork = AssetUtility.LoadAssetFromGUID<Texture2D>(StarArtworkGuid);
            window.titleContent = SettingsLocalization.Content(Title);
            return window;
        }

        private static bool IsEditable(GameObject target)
        {
            return target != null && target.scene.IsValid() && !EditorUtility.IsPersistent(target) &&
                (target.hideFlags & HideFlags.NotEditable) == 0 &&
                !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private void OnEnable()
        {
            hideFlags = HideFlags.DontSave;
            wantsMouseMove = true;
            wantsMouseEnterLeaveWindow = true;
        }

        private void OnDestroy()
        {
            if (activePalette == this)
            {
                activePalette = null;
            }
        }

        private void OnLostFocus()
        {
            pointerInPalette = false;
            if (!dismissOnFocusLoss || choosingPaletteItem)
            {
                return;
            }

            RunOnNextUpdate(() =>
            {
                if (this != null && focusedWindow != this && !choosingPaletteItem)
                {
                    Close();
                }
            });
        }

        private void RequestClose()
        {
            RunOnNextUpdate(() =>
            {
                if (this != null)
                {
                    Close();
                }
            });
        }

        private void OnGUI()
        {
            Event evt = Event.current;
            if (!IsEditable(targetObject) || (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape))
            {
                if (evt.type == EventType.KeyDown)
                {
                    evt.Use();
                }
                RequestClose();
                return;
            }

            if (evt.type == EventType.MouseMove || evt.type == EventType.MouseEnterWindow || evt.type == EventType.ScrollWheel)
            {
                pointerPosition = evt.mousePosition;
                pointerInPalette = true;
                Repaint();
            }
            else if (evt.type == EventType.MouseLeaveWindow)
            {
                pointerInPalette = false;
                Repaint();
            }
            else if (evt.type == EventType.KeyDown || evt.type == EventType.KeyUp)
            {
                Repaint();
            }

            Texture2D icon = GameObjectIconInfo.GetIcon(targetObject);

            Settings settings = HierarchyDecorator.Settings;
            bool children = evt.alt;
            bool hasColor = settings.RowColors.TryGetColor(targetObject, out Color color);
            List<Texture2D> projectTextures = settings.PaletteTextures.FindAll(IsProjectTexture);
            var layout = new PaletteLayout(settings.PaletteColors.Count, projectTextures.Count);

            paletteScroll = GUI.BeginScrollView(new Rect(0f, 0f, position.width, position.height), paletteScroll,
                new Rect(0f, 0f, ContentWidth, layout.Height));
            try
            {
                EditorGUI.LabelField(new Rect(Left, 5f, 219f, 18f),
                    new GUIContent(targetObject.name, targetObject.name), EditorStyles.boldLabel);

                DrawColors(settings, hasColor, color, children);
                DrawIcons(projectTextures, icon, children, layout);
                EditorGUI.LabelField(new Rect(Left, layout.hintY, 232f, 15f),
                    SettingsLocalization.Content(children ? "Applying to this object and its children"
                        : "Hold Alt to include children"), children ? EditorStyles.boldLabel : EditorStyles.miniLabel);
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        private void DrawColors(Settings settings, bool hasColor, Color current, bool children)
        {
            EditorGUI.LabelField(new Rect(Left, 24f, 176f, 15f),
                SettingsLocalization.Content("Row Color"), EditorStyles.miniBoldLabel);
            if (DrawActionTile(IconTile(224f, 22f), addArtwork, "Add Color…"))
            {
                OpenColorPicker(settings.PaletteColor, children);
            }

            Rect clearTile = IconTile(Left, ColorTilesY);
            if (DrawActionTile(clearTile, resetArtwork, "Clear Color", !hasColor))
            {
                ApplyColor(targetObject, null, children);
                Repaint();
            }

            int colorCount = RowColors.Length + settings.PaletteColors.Count;
            for (int i = 0; i < colorCount; i++)
            {
                Color color = i < RowColors.Length ? RowColors[i] : settings.PaletteColors[i - RowColors.Length];
                Rect tile = PaletteTile(i + 1, ColorTilesY);
                if (DrawSwatch(tile, color, hasColor && current.Equals(color)))
                {
                    ApplyColor(targetObject, color, children);
                    Repaint();
                }
            }
        }

        private void OpenColorPicker(Color initial, bool children)
        {
            // Unity exposes ColorField publicly, but opening its picker from a button uses this overload.
            Type pickerType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ColorPicker", true);
            Type[] parameters = { typeof(Action<Color>), typeof(Color), typeof(bool), typeof(bool) };
            MethodInfo show = pickerType.GetMethod("Show", parameters);
            GameObject target = targetObject;
            Color chosen = initial;
            Action<Color> onColorChanged = color => chosen = color;
            choosingPaletteItem = true;
            if (show != null)
            {
                show.Invoke(null, new object[] { onColorChanged, initial, false, false });
            }
            else
            {
                // Newer Unity versions add an option for a transparent starting color.
                show = pickerType.GetMethod("Show", new[] { parameters[0], parameters[1], parameters[2],
                    parameters[3], typeof(bool) });
                show.Invoke(null, new object[] { onColorChanged, initial, false, false, false });
            }

            var picker = (EditorWindow)pickerType.GetProperty("instance").GetValue(null);
            PositionPicker(picker);
            EditorApplication.update += FinishColorSelection;
            GUIUtility.ExitGUI();

            void FinishColorSelection()
            {
                if (picker != null)
                {
                    return;
                }

                EditorApplication.update -= FinishColorSelection;
                ResumeAfterPicker();
                // Escape restores the original color through the same callback before closing.
                Color32 selected = chosen;
                if (((Color32)initial).Equals(selected) || !IsEditable(target))
                {
                    return;
                }

                AddPaletteColor(target, selected, children);
            }
        }

        private void ResumeAfterPicker()
        {
            if (this != null)
            {
                choosingPaletteItem = false;
                Focus();
                Repaint();
            }
        }

        private static void AddPaletteColor(GameObject target, Color32 selected, bool children)
        {
            Settings settings = HierarchyDecorator.Settings;
            Color color;
            int preset = Array.FindIndex(RowColors, entry => ((Color32)entry).Equals(selected));
            int saved = settings.PaletteColors.FindIndex(entry => ((Color32)entry).Equals(selected));
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(settings, SettingsLocalization.Text("Row Color"));
            if (preset >= 0)
            {
                color = RowColors[preset];
            }
            else if (saved >= 0)
            {
                color = settings.PaletteColors[saved];
            }
            else
            {
                color = selected;
                settings.PaletteColors.Add(color);
            }

            settings.PaletteColor = color;
            ApplyColor(target, color, children, false);
            EditorUtility.SetDirty(settings);
        }

        private void DrawIcons(List<Texture2D> textures, Texture2D current, bool children, PaletteLayout layout)
        {
            EditorGUI.LabelField(new Rect(Left, layout.iconHeaderY, 176f, 16f),
                SettingsLocalization.Content("Object Icon"), EditorStyles.miniBoldLabel);
            if (DrawActionTile(IconTile(224f, layout.iconHeaderY - 4f), addArtwork, "Add Icon…"))
            {
                GameObject target = targetObject;
                // Open the picker outside this dropdown's IMGUI pass so it has no GUI delegate.
                RunOnNextUpdate(() => OpenTexturePicker(target, children));
            }

            float iconTilesY = layout.iconHeaderY + 17f;
            if (DrawActionTile(IconTile(Left, iconTilesY), resetArtwork, "Default Icon", current == null))
            {
                ApplyIcons(targetObject, null, children);
            }

            if (symbols.Count == 0)
            {
                for (int group = 0; group < IconGroups.Length; group++)
                {
                    int slot = group == 0 ? 1 : 0;
                    foreach (string name in IconGroups[group])
                    {
                        var texture = EditorGUIUtility.IconContent(name).image as Texture2D;
                        if (texture != null)
                        {
                            symbols.Add(new Symbol
                            {
                                icon = texture,
                                tooltip = new GUIContent(string.Empty, ObjectNames.NicifyVariableName(name.Replace(" Icon", ""))),
                                tile = IconTile(Left + slot++ * TileStep, group * TileStep)
                            });
                        }
                    }

                    if (group == 0)
                    {
                        symbols.Add(new Symbol
                        {
                            icon = starArtwork,
                            tooltip = SettingsLocalization.Content(string.Empty, "Star"),
                            tile = IconTile(Left + slot * TileStep, 0f)
                        });
                    }
                }
            }

            foreach (Symbol symbol in symbols)
            {
                Rect tile = symbol.tile;
                tile.y += iconTilesY;
                if (DrawIconTile(tile, symbol.icon, symbol.icon == current, symbol.tooltip))
                {
                    ApplyIcons(targetObject, symbol.icon, children);
                }
            }

            if (textures.Count == 0)
            {
                return;
            }

            EditorGUI.LabelField(new Rect(Left, layout.textureHeaderY, 232f, 17f),
                SettingsLocalization.Content("Project Textures"), EditorStyles.miniBoldLabel);
            for (int index = 0; index < textures.Count; index++)
            {
                Texture2D texture = textures[index];
                Rect tile = PaletteTile(index, layout.textureHeaderY + 18f);
                if (DrawIconTile(tile, texture, texture == current, new GUIContent(string.Empty, texture.name)))
                {
                    ApplyIcons(targetObject, texture, children);
                }
            }
        }

        private static Rect PaletteTile(int slot, float startY)
        {
            return IconTile(Left + (slot % PaletteColumns) * TileStep, startY + (slot / PaletteColumns) * TileStep);
        }

        private bool DrawSwatch(Rect tile, Color color, bool selected)
        {
            bool clicked = DrawTile(tile, selected, GUIContent.none);
            if (Event.current.type == EventType.Repaint)
            {
                Rect swatch = ArtworkRect(tile);
                Color edge = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.18f) : new Color(0f, 0f, 0f, 0.16f);
                DrawRoundedRect(swatch, edge, 4f);
                DrawRoundedRect(InsetTile(swatch, 1f), color, 3f);
            }
            return clicked;
        }

        private bool DrawIconTile(Rect tile, Texture2D texture, bool selected, GUIContent tooltip,
            float artworkSize = ArtworkSize, Color? tint = null)
        {
            bool clicked = DrawTile(tile, selected, tooltip);
            if (texture != null && Event.current.type == EventType.Repaint)
            {
                ArtworkMetrics metrics = GetArtworkMetrics(texture);
                Rect artwork = FitArtwork(tile, metrics, texture.width, texture.height, artworkSize);
                Color previous = GUI.color;
                try
                {
                    GUI.color *= tint ?? Color.white;
                    GUI.DrawTextureWithTexCoords(artwork, texture, metrics.bounds, true);
                }
                finally
                {
                    GUI.color = previous;
                }
            }
            return clicked;
        }

        private static Rect IconTile(float x, float y)
        {
            return new Rect(x, y, TileSize, TileSize);
        }

        private static Rect ArtworkRect(Rect tile)
        {
            float inset = (TileSize - ArtworkSize) / 2f;
            return new Rect(tile.x + inset, tile.y + inset, ArtworkSize, ArtworkSize);
        }

        private static Rect FitArtwork(Rect tile, ArtworkMetrics metrics, int width, int height, float artworkSize)
        {
            Rect uv = metrics.bounds;
            var size = new Vector2(uv.width * width, uv.height * height);
            float scale = artworkSize / Mathf.Max(size.x, size.y);
            size *= scale;
            Vector2 offset = metrics.opticalCenter - uv.center;
            var correction = new Vector2(offset.x * width, -offset.y * height) * scale;
            return new Rect(tile.center - size / 2f - correction, size);
        }

        private static ArtworkMetrics GetArtworkMetrics(Texture2D texture)
        {
            if (ArtworkCache.TryGetValue(texture, out ArtworkMetrics metrics))
            {
                return metrics;
            }

            metrics = new ArtworkMetrics { bounds = FullTexture, opticalCenter = FullTexture.center };
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                int width = Mathf.Min(texture.width, 40);
                int height = Mathf.Min(texture.height, 40);
                var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                RenderTexture previous = RenderTexture.active;
                var sample = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    Graphics.Blit(texture, target);
                    RenderTexture.active = target;
                    sample.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                    metrics = MeasureArtwork(sample.GetPixels32(), width, height);
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                    DestroyImmediate(sample);
                }
            }

            ArtworkCache[texture] = metrics;
            return metrics;
        }

        private static ArtworkMetrics MeasureArtwork(Color32[] pixels, int width, int height)
        {
            int xMin = width;
            int yMin = height;
            int xMax = -1;
            int yMax = -1;
            float weight = 0f;
            Vector2 weightedPosition = Vector2.zero;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int alpha = pixels[y * width + x].a;
                    if (alpha <= 8)
                    {
                        continue;
                    }

                    xMin = Mathf.Min(xMin, x);
                    yMin = Mathf.Min(yMin, y);
                    xMax = Mathf.Max(xMax, x);
                    yMax = Mathf.Max(yMax, y);

                    // Favor the solid glyph over faint antialiasing and drop shadows.
                    float sampleWeight = alpha * alpha;
                    weight += sampleWeight;
                    weightedPosition += new Vector2(x + 0.5f, y + 0.5f) * sampleWeight;
                }
            }

            if (weight == 0f)
            {
                return new ArtworkMetrics { bounds = FullTexture, opticalCenter = FullTexture.center };
            }

            return new ArtworkMetrics
            {
                bounds = Rect.MinMaxRect((float)xMin / width, (float)yMin / height,
                    (float)(xMax + 1) / width, (float)(yMax + 1) / height),
                opticalCenter = new Vector2(weightedPosition.x / (weight * width), weightedPosition.y / (weight * height))
            };
        }

        private bool DrawActionTile(Rect tile, Texture2D icon, string tooltip, bool selected = false)
        {
            return DrawIconTile(tile, icon, selected, SettingsLocalization.Content(string.Empty, tooltip),
                ActionArtworkSize, EditorStyles.label.normal.textColor);
        }

        private bool DrawTile(Rect tile, bool selected, GUIContent tooltip)
        {
            if (Event.current.type == EventType.Repaint)
            {
                bool hovered = pointerInPalette && tile.Contains(pointerPosition + paletteScroll);
                Rect stateRect = InsetTile(tile, 1f);
                if (selected || hovered)
                {
                    // The same selection style Unity uses in its GameObject icon selector.
                    GUIStyle selection = GUI.skin.FindStyle("sv_iconselector_selection");
                    if (selection != null)
                    {
                        Color previous = GUI.color;
                        if (!selected)
                        {
                            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * 0.65f);
                        }

                        try
                        {
                            selection.Draw(stateRect, GUIContent.none, false, false, false, false);
                        }
                        finally
                        {
                            GUI.color = previous;
                        }
                    }
                    else
                    {
                        EditorStyles.miniButton.Draw(stateRect, GUIContent.none, hovered, false, selected, false);
                    }
                }
            }
            return GUI.Button(tile, tooltip, GUIStyle.none);
        }

        private static Rect InsetTile(Rect rect, float inset)
        {
            return new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);
        }

        private static void DrawRoundedRect(Rect rect, Color color, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        private void OpenTexturePicker(GameObject target, bool children)
        {
            if (this == null || !IsEditable(target))
            {
                return;
            }

            // The callback overload does not require the palette to survive until the picker closes.
            Type selectorType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ObjectSelector", true);
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            Type[] parameters =
            {
                typeof(Object), typeof(Type), typeof(Object), typeof(bool), typeof(List<int>),
                typeof(Action<Object>), typeof(Action<Object>), typeof(bool)
            };
            MethodInfo show = selectorType.GetMethod("Show", instance, null, parameters, null);
            Action<Object> onClosed = selection => RunOnNextUpdate(() =>
            {
                ResumeAfterPicker();
                if (selection is Texture2D texture)
                {
                    AddProjectIcon(target, texture, children);
                }
            });
            object[] arguments = { null, typeof(Texture2D), target, false, null, onClosed, null, false };

            if (show == null)
            {
                // Older editors omit showNoneItem, and Unity 2018.4 takes a SerializedProperty as the context.
                Array.Resize(ref parameters, parameters.Length - 1);
                Array.Resize(ref arguments, arguments.Length - 1);
                show = selectorType.GetMethod("Show", instance, null, parameters, null);
                if (show == null)
                {
                    parameters[2] = typeof(SerializedProperty);
                    show = selectorType.GetMethod("Show", instance, null, parameters, null);
                    arguments[2] = null;
                }
            }

            var selector = (EditorWindow)selectorType.GetProperty("get").GetValue(null);
            choosingPaletteItem = true;
            try
            {
                show.Invoke(selector, arguments);
                PositionPicker(selector);
            }
            catch
            {
                choosingPaletteItem = false;
                throw;
            }
        }

        private static void AddProjectIcon(GameObject target, Texture2D texture, bool children)
        {
            if (!IsEditable(target) || !IsProjectTexture(texture))
            {
                return;
            }

            Settings settings = HierarchyDecorator.Settings;
            if (!settings.PaletteTextures.Contains(texture))
            {
                settings.PaletteTextures.Add(texture);
                EditorUtility.SetDirty(settings);
            }

            ApplyIcons(target, texture, children);
        }

        private static bool IsProjectTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            return path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.StartsWith("Packages/", StringComparison.Ordinal);
        }

        private static void ApplyColor(GameObject root, Color? color, bool children, bool recordUndo = true)
        {
            Settings settings = HierarchyDecorator.Settings;
            ObjectRowColors data = settings.RowColors;
            bool changed = false;
            foreach (GameObject target in Targets(root, children))
            {
                bool hasColor = data.TryGetColor(target, out Color existing);
                if (hasColor == color.HasValue && (!hasColor || existing.Equals(color.Value)))
                {
                    continue;
                }

                if (!changed && recordUndo)
                {
                    Undo.RegisterCompleteObjectUndo(settings, SettingsLocalization.Text("Row Color"));
                }
                changed = true;
                data.SetColor(target, color);
            }

            if (changed)
            {
                EditorUtility.SetDirty(settings);
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        private static IEnumerable<GameObject> Targets(GameObject root, bool children)
        {
            if (!children)
            {
                yield return root;
                yield break;
            }

            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (IsEditable(transform.gameObject))
                {
                    yield return transform.gameObject;
                }
            }
        }

        private static void ApplyIcons(GameObject root, Texture2D texture, bool children)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(SettingsLocalization.Text("Object Icon"));
            bool changed = false;
            foreach (GameObject target in Targets(root, children))
            {
                if (GameObjectIconInfo.GetIcon(target) == texture)
                {
                    continue;
                }

                // Unity's Inspector uses this same serialized property.
                using (var serialized = new SerializedObject(target))
                {
                    serialized.FindProperty("m_Icon").objectReferenceValue = texture;
                    changed |= serialized.ApplyModifiedProperties();
                }
            }

            Undo.CollapseUndoOperations(group);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(root.scene);
                EditorApplication.RepaintHierarchyWindow();
                SceneView.RepaintAll();
            }
        }
    }
}
