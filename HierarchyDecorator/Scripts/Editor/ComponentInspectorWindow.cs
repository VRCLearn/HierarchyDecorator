using System.Reflection;
using UnityEditor;
using UnityEngine;
#if UNITY_2022_1_OR_NEWER
using UnityEditor.UIElements;
using UnityEngine.UIElements;
#endif

namespace HierarchyDecorator.Editor
{
    internal sealed class ComponentInspectorWindow : EditorWindow
    {
        private const float Width = 420f;
        private const float HeaderHeight = 24f;
        private const float MinHeight = 56f;
        private const float MaxHeight = 480f;
        private const float HeaderActionSize = 20f;
        private const float HeaderActionInset = 4f;
        private const string PinArtworkGuid = "d923819116e649d19aa008678a36a26b";
#if UNITY_2022_1_OR_NEWER
        private const float FrameWidth = 1f;
        private const float BodyPadding = 5f;
        private static ComponentInspectorWindow pendingWindow;
#endif
        private static ComponentInspectorWindow transientWindow;
        private static GUIStyle closeButtonStyle;
        private static GUIContent closeButtonContent;
        private static Texture2D pinArtwork;
        private static readonly GUIContent PinContent = new GUIContent(string.Empty, "Pin");
        private static readonly GUIContent UnpinContent = new GUIContent(string.Empty, "Unpin");
        private static readonly FieldInfo WindowHost = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo HostContainer = WindowHost?.FieldType.GetProperty("window", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly PropertyInfo ContainerPosition = HostContainer?.PropertyType.GetProperty("position");
#if UNITY_EDITOR_WIN
        private static readonly MethodInfo SendCaptionEvent = HostContainer?.PropertyType.GetMethod("SendCaptionEvent", new[] { typeof(bool) });
#endif

        [SerializeField] private Component component;
        [SerializeField] private bool pinned;
        [SerializeField] private Rect popupBounds;
        private UnityEditor.Editor inspector;
        private GUIContent title;
        private Texture titleIcon;
        private Vector2 pointerStart;
        private Vector2 windowStart;
        private bool boundsQueued;
#if UNITY_2022_1_OR_NEWER
        private InspectorElement fields;
        private VisualElement measuringArea;
        private Vector2 openingAnchor;
        private bool openingQueued;
#endif
#if !UNITY_2022_1_OR_NEWER
        private Vector2 scroll;
#endif

        internal static Vector2 GetAnchor(Rect icon)
        {
            Vector2 anchor = GUIUtility.GUIToScreenPoint(new Vector2(icon.xMax, icon.yMin));
            EditorWindow hierarchy = focusedWindow;
            if (hierarchy != null && hierarchy.GetType().Name == "SceneHierarchyWindow")
                anchor.x = hierarchy.position.xMax;
            anchor.x += 6f;
            return anchor;
        }

        internal static void Open(Component component, Vector2 anchor)
        {
#if UNITY_2022_1_OR_NEWER
            EditorWindow source = focusedWindow;
#endif
            EditorApplication.delayCall += () =>
            {
                if (component == null)
                    return;

                var window = CreateInstance<ComponentInspectorWindow>();
                window.component = component;
                window.minSize = new Vector2(Width, MinHeight);
                window.maxSize = new Vector2(Width, MaxHeight);
#if UNITY_2022_1_OR_NEWER
                if (pendingWindow != null)
                    DestroyImmediate(pendingWindow);
                pendingWindow = window;
                window.MeasureBeforeShowing(source, anchor);
#else
                window.ShowAt(anchor, 300f);
#endif
            };
        }

        private void ShowAt(Vector2 anchor, float height)
        {
            if (transientWindow != null)
                transientWindow.Close();

            popupBounds = GameObjectIconWindow.FitPopupToDesktop(anchor, new Vector2(Width, height));
            position = popupBounds;
            transientWindow = this;
            ShowPopup();
        }

#if UNITY_2022_1_OR_NEWER
        private void MeasureBeforeShowing(EditorWindow source, Vector2 anchor)
        {
            openingAnchor = anchor;
            if (source == null || source is ComponentInspectorWindow || source.rootVisualElement.panel == null)
            {
                var hierarchyType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
                if (hierarchyType != null)
                {
                    foreach (EditorWindow hierarchy in Resources.FindObjectsOfTypeAll(hierarchyType))
                    {
                        if (hierarchy.rootVisualElement.panel == null)
                            continue;
                        source = hierarchy;
                        break;
                    }
                }
            }

            if (source == null || source is ComponentInspectorWindow || source.rootVisualElement.panel == null)
            {
                pendingWindow = null;
                ShowAt(anchor, 300f);
                return;
            }

            fields = CreateInspectorFields();
            measuringArea = new VisualElement { pickingMode = PickingMode.Ignore };
            measuringArea.style.position = Position.Absolute;
            measuringArea.style.left = -Width;
            measuringArea.style.width = Width - 2f * (FrameWidth + BodyPadding);
            measuringArea.style.opacity = 0f;
            measuringArea.RegisterCallback<DetachFromPanelEvent>(OnMeasuringAreaDetached);
            fields.RegisterCallback<GeometryChangedEvent>(OnOpeningLayout);
            measuringArea.Add(fields);
            source.rootVisualElement.Add(measuringArea);
            source.Repaint();
        }

        private void OnOpeningLayout(GeometryChangedEvent evt)
        {
            if (openingQueued || !IsMeasured(evt.newRect))
                return;

            openingQueued = true;
            EditorApplication.delayCall += ShowMeasured;
        }

        private void ShowMeasured()
        {
            if (pendingWindow != this || component == null)
                return;

            float height = GetPopupHeight(fields.layout.height);
            fields.UnregisterCallback<GeometryChangedEvent>(OnOpeningLayout);
            measuringArea.UnregisterCallback<DetachFromPanelEvent>(OnMeasuringAreaDetached);
            fields.RemoveFromHierarchy();
            measuringArea.RemoveFromHierarchy();
            measuringArea = null;
            pendingWindow = null;
            ShowAt(openingAnchor, height);
        }

        private void OnMeasuringAreaDetached(DetachFromPanelEvent evt)
        {
            if (evt.target == measuringArea)
                EditorApplication.delayCall += DiscardMeasurement;
        }

        private void DiscardMeasurement()
        {
            if (pendingWindow == this)
                DestroyImmediate(this);
        }

        private InspectorElement CreateInspectorFields()
        {
            UnityEditor.Editor.CreateCachedEditor(component, null, ref inspector);
            var element = new InspectorElement(inspector);
            element.style.flexShrink = 0f;
            return element;
        }

        private void CreateGUI()
        {
            if (component == null)
                return;

            var frame = rootVisualElement.style;
            bool dark = EditorGUIUtility.isProSkin;
            Color background = dark ? new Color32(56, 56, 56, 255) : new Color32(200, 200, 200, 255);
            frame.backgroundColor = background;
            frame.borderTopWidth = frame.borderBottomWidth = frame.borderLeftWidth = frame.borderRightWidth = FrameWidth;
            Color border = dark ? new Color32(34, 34, 34, 255) : new Color32(138, 138, 138, 255);
            frame.borderTopColor = frame.borderBottomColor = frame.borderLeftColor = frame.borderRightColor = border;

            var header = new IMGUIContainer(DrawHeader);
            header.style.height = HeaderHeight;
            header.style.flexShrink = 0f;
            Color headerBackground = dark ? new Color32(48, 48, 48, 255) : new Color32(214, 214, 214, 255);
            header.style.backgroundColor = headerBackground;
            rootVisualElement.Add(header);

            var body = new ScrollView();
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            body.style.marginTop = body.style.marginBottom = body.style.marginLeft = body.style.marginRight = BodyPadding;
            if (fields == null)
                fields = CreateInspectorFields();
            fields.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (!IsMeasured(evt.newRect))
                    return;

                float height = GetPopupHeight(evt.newRect.height);
                if (height == popupBounds.height)
                    return;
                Vector2 origin = pinned ? GetWindowOrigin() : popupBounds.position;
                QueueBounds(GameObjectIconWindow.FitPopupToDesktop(origin, new Vector2(Width, height)));
            });
            body.Add(fields);
            AddScrollEdge(body, background);
            rootVisualElement.Add(body);
        }

        private static void AddScrollEdge(ScrollView body, Color background)
        {
            var edge = new VisualElement { pickingMode = PickingMode.Ignore, visible = false };
            edge.style.position = Position.Absolute;
            edge.style.left = edge.style.right = edge.style.bottom = 0f;
            edge.style.height = 18f;
            edge.generateVisualContent += context =>
            {
                float width = edge.contentRect.width;
                float height = edge.contentRect.height;
                Color clear = new Color(background.r, background.g, background.b, 0f);
                var mesh = context.Allocate(4, 6);
                mesh.SetNextVertex(new Vertex { position = new Vector3(0f, 0f, Vertex.nearZ), tint = clear });
                mesh.SetNextVertex(new Vertex { position = new Vector3(width, 0f, Vertex.nearZ), tint = clear });
                mesh.SetNextVertex(new Vertex { position = new Vector3(width, height, Vertex.nearZ), tint = background });
                mesh.SetNextVertex(new Vertex { position = new Vector3(0f, height, Vertex.nearZ), tint = background });
                mesh.SetNextIndex(0);
                mesh.SetNextIndex(1);
                mesh.SetNextIndex(2);
                mesh.SetNextIndex(2);
                mesh.SetNextIndex(3);
                mesh.SetNextIndex(0);
            };

            var viewport = body.contentViewport;
            viewport.Add(edge);
            void UpdateEdge()
            {
                edge.visible = body.contentContainer.layout.height - viewport.layout.height - body.scrollOffset.y > 1f;
            }

            viewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateEdge());
            body.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateEdge());
            body.verticalScroller.valueChanged += _ => UpdateEdge();
        }

        private static bool IsMeasured(Rect bounds)
        {
            return bounds.width > 0f && bounds.height >= 0f && !float.IsInfinity(bounds.height);
        }

        private static float GetPopupHeight(float contentHeight)
        {
            return Mathf.Clamp(Mathf.Ceil(HeaderHeight + contentHeight + 2f * (FrameWidth + BodyPadding)), MinHeight, MaxHeight);
        }
#else
        private void OnGUI()
        {
            if (component == null)
                return;

            DrawHeader();
            GUILayout.Space(HeaderHeight);
            UnityEditor.Editor.CreateCachedEditor(component, null, ref inspector);
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                inspector.OnInspectorGUI();
            }
        }
#endif

        private void DrawHeader()
        {
            if (component == null)
                return;

            if (title == null)
            {
                title = new GUIContent(ObjectNames.GetInspectorTitle(component));
                var item = new ComponentItem(component);
                if (!item.IsNullComponent)
                    titleIcon = item.Content.image;
                if (titleIcon == null)
                    titleIcon = EditorGUIUtility.ObjectContent(component, component.GetType()).image;
            }

            var header = new Rect(0f, 0f, popupBounds.width, HeaderHeight);
#if UNITY_2022_1_OR_NEWER
            header.width -= 2f * FrameWidth;
#else
            GUI.Box(header, GUIContent.none, EditorStyles.toolbar);
#endif
            var closeArea = new Rect(header.xMax - HeaderActionInset - HeaderActionSize,
                header.center.y - HeaderActionSize * 0.5f, HeaderActionSize, HeaderActionSize);
            if (DrawCloseButton(closeArea))
            {
                Close();
                GUIUtility.ExitGUI();
            }

            var pinArea = new Rect(closeArea.xMin - HeaderActionSize, closeArea.y, HeaderActionSize, HeaderActionSize);
            DrawHeaderActionHover(pinArea);
            if (GUI.Button(pinArea, pinned ? UnpinContent : PinContent, GUIStyle.none))
                SetPinned(!pinned);
            if (Event.current.type == EventType.Repaint)
                DrawPin(pinArea.center, pinned);

            int enabled = EditorUtility.GetObjectEnabled(component);
            float titleX = 7f;
            if (enabled != -1)
            {
                bool active = EditorGUI.Toggle(new Rect(7f, 4f, 16f, 16f), enabled != 0);
                if (active != (enabled != 0))
                    new ComponentItem(component).SetActive(active);
                titleX = 26f;
            }

            var dragArea = new Rect(titleX, 0f, pinArea.xMin - titleX, HeaderHeight);
            var titleArea = dragArea;
            if (titleIcon != null)
            {
                GUI.DrawTexture(new Rect(titleX, (HeaderHeight - 16f) * 0.5f, 16f, 16f), titleIcon, ScaleMode.ScaleToFit);
                titleArea.xMin += 20f;
            }
            EditorGUI.LabelField(titleArea, title, EditorStyles.boldLabel);
            HandleDrag(dragArea);
        }

        private static bool DrawCloseButton(Rect rect)
        {
            if (closeButtonContent == null)
            {
                var icon = EditorGUIUtility.FindTexture(EditorGUIUtility.isProSkin ? "d_winbtn_win_close" : "winbtn_win_close");
                closeButtonContent = icon != null ? new GUIContent(icon, "Close") : new GUIContent("×", "Close");
                closeButtonStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(),
                };
            }

            DrawHeaderActionHover(rect);
            return GUI.Button(rect, closeButtonContent, closeButtonStyle);
        }

        private static void DrawHeaderActionHover(Rect rect)
        {
            if (Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition))
            {
                Color hover = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.09f) : new Color(0f, 0f, 0f, 0.09f);
                EditorGUI.DrawRect(rect, hover);
            }
        }

        private static void DrawPin(Vector2 center, bool pinned)
        {
            if (pinArtwork == null)
                pinArtwork = AssetUtility.LoadAssetFromGUID<Texture2D>(PinArtworkGuid);
            if (pinArtwork == null)
                return;

            Color color = EditorStyles.label.normal.textColor;
            if (!pinned)
                color.a *= 0.55f;
            var rect = new Rect(center.x - 8f, center.y - 8f, 16f, 16f);
            GUI.DrawTexture(rect, pinArtwork, ScaleMode.ScaleToFit, true, 0f, color, 0f, 0f);
        }

        private void SetPinned(bool value)
        {
            if (pinned == value)
                return;

            pinned = value;
            if (pinned)
            {
                if (transientWindow == this)
                    transientWindow = null;
            }
            else
            {
                popupBounds.position = GetWindowOrigin();
                if (transientWindow != null && transientWindow != this)
                    transientWindow.Close();
                transientWindow = this;
            }
            Repaint();
        }

        private void HandleDrag(Rect rect)
        {
            int control = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event evt = Event.current;
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);
            switch (evt.GetTypeForControl(control))
            {
                case EventType.MouseDown when evt.button == 0 && rect.Contains(evt.mousePosition):
                    pointerStart = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                    windowStart = GetWindowOrigin();
                    GUIUtility.hotControl = control;
                    evt.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == control:
                    Vector2 displacement = GUIUtility.GUIToScreenPoint(evt.mousePosition) - pointerStart;
                    if (pinned || displacement.sqrMagnitude >= 9f)
                    {
                        SetPinned(true);
                        evt.Use();
#if UNITY_EDITOR_WIN
                        if (!Application.isBatchMode && SendCaptionEvent != null && GetContainer() is object container)
                        {
                            GUIUtility.hotControl = 0;
                            SendCaptionEvent.Invoke(container, new object[] { true });
                            return;
                        }
#endif
                        QueueBounds(GameObjectIconWindow.FitPopupToDesktop(windowStart + displacement, popupBounds.size));
                        break;
                    }
                    evt.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == control && evt.button == 0:
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    break;
            }
        }

        private object GetContainer()
        {
            object host = WindowHost?.GetValue(this);
            return host != null ? HostContainer?.GetValue(host) : null;
        }

        private Vector2 GetWindowOrigin()
        {
            object container = GetContainer();
            return container != null && ContainerPosition?.GetValue(container) is Rect bounds
                ? bounds.position : popupBounds.position;
        }

        private void QueueBounds(Rect bounds)
        {
            if (bounds == popupBounds)
                return;

            popupBounds = bounds;
            if (boundsQueued)
                return;

            boundsQueued = true;
            EditorApplication.delayCall += ApplyBounds;
        }

        private void ApplyBounds()
        {
            boundsQueued = false;
            if (this != null)
                position = popupBounds;
        }

        private void OnLostFocus()
        {
            if (transientWindow == this)
            {
                EditorApplication.update -= CheckFocus;
                EditorApplication.update += CheckFocus;
            }
        }

        private void CheckFocus()
        {
            EditorWindow focused = focusedWindow;
            // Unity's own pickers temporarily take focus from the component inspector.
            if (!pinned && focused != null && IsInspectorPicker(focused))
                return;

            EditorApplication.update -= CheckFocus;
            if (transientWindow == this && focused != this)
                Close();
        }

        private static bool IsInspectorPicker(EditorWindow window)
        {
            switch (window.GetType().Name)
            {
                case "ObjectSelector":
                case "ColorPicker":
                case "GradientPicker":
                case "CurveEditorWindow":
                case "AdvancedDropdownWindow":
                case "SearchPickerWindow":
                    return true;
                default:
                    return false;
            }
        }

        private void Update()
        {
            if (component == null)
                Close();
        }

        private void OnDisable()
        {
#if UNITY_2022_1_OR_NEWER
            EditorApplication.delayCall -= ShowMeasured;
            EditorApplication.delayCall -= DiscardMeasurement;
            if (measuringArea != null)
            {
                measuringArea.UnregisterCallback<DetachFromPanelEvent>(OnMeasuringAreaDetached);
                measuringArea.RemoveFromHierarchy();
            }
            if (pendingWindow == this)
                pendingWindow = null;
#endif
            EditorApplication.delayCall -= ApplyBounds;
            EditorApplication.update -= CheckFocus;
            if (transientWindow == this)
                transientWindow = null;
            if (inspector != null)
                DestroyImmediate(inspector);
        }
    }
}
