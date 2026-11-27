using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Etazh.Prototype.Editor
{
    [InitializeOnLoad]
    internal static class SceneHudPreview
    {
        private const string MenuPath = "Tools/Этаж, пожалуйста/HUD в Scene";
        private const string FrameMenuPath = "Tools/Этаж, пожалуйста/Кадрировать дом и HUD";
        private const string EnabledKey = "Etazh.SceneHudPreview";
        private const float ArtworkHeight = 600f;
        private const float HeaderOffset = 61f;
        private const float RailGap = 8f;

        private static readonly ConditionalWeakTable<SceneView, Preview> Previews = new();
        private static PrototypeController _source;
        private static Scene _scene;
        private static bool _refreshSource = true;

        static SceneHudPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.hierarchyChanged += () => _refreshSource = true;
            EditorApplication.playModeStateChanged += _ => _refreshSource = true;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            SessionState.SetBool(EnabledKey, !Enabled);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        [MenuItem(FrameMenuPath)]
        private static void Frame()
        {
            if (!ValidateFrame())
                return;

            SceneView view = SceneView.lastActiveSceneView;
            Bounds bounds = _source.SceneHudBounds;
            Vector3 max = bounds.max;
            Vector3 min = bounds.min;
            bounds.Encapsulate(max + new Vector3(bounds.size.x / 3f + 0.4f, bounds.size.y * 0.14f, 0));
            bounds.Encapsulate(min - new Vector3(0.2f, bounds.size.y * 0.09f, 0));
            bounds.Expand(0.35f);
            Rect viewport = view.cameraViewport;
            if (view.in2DMode && viewport.width > 0 && viewport.height > 0)
            {
                view.pivot = bounds.center;
                view.size = Mathf.Max(bounds.extents.y, bounds.extents.x * viewport.height / viewport.width);
                view.Repaint();
            }
            else
            {
                view.Frame(bounds, true);
            }
            view.Focus();
        }

        [MenuItem(FrameMenuPath, true)]
        private static bool ValidateFrame()
            => Enabled && _source != null && _source.SceneHudBounds.size.y > 0
                && !EditorApplication.isPlayingOrWillChangePlaymode && SceneView.lastActiveSceneView != null;

        private static bool Enabled => SessionState.GetBool(EnabledKey, true);

        private static void Update()
        {
            bool show = Enabled && !EditorApplication.isPlayingOrWillChangePlaymode
                && PrefabStageUtility.GetCurrentPrefabStage() == null;
            if (show)
            {
                Scene scene = SceneManager.GetActiveScene();
                if (_refreshSource || _scene != scene)
                {
                    _scene = scene;
                    _source = FindController(scene);
                    _refreshSource = false;
                }
            }

            Bounds bounds = show && _source != null ? _source.SceneHudBounds : default;
            foreach (SceneView view in SceneView.sceneViews)
            {
                if (!view.in2DMode || view.camera == null || bounds.size.y <= 0)
                {
                    if (Previews.TryGetValue(view, out Preview hidden))
                        hidden.Hide();
                    continue;
                }

                Previews.GetValue(view, static _ => new Preview()).Show(view, _source, bounds);
            }
        }

        private static PrototypeController FindController(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out PrototypeController controller))
                    return controller;
            return null;
        }

        private sealed class Preview
        {
            private PrototypeController _source;
            private VisualElement _clip;
            private VisualElement _root;
            private VisualElement _stage;
            private VisualElement _header;
            private VisualElement _satisfaction;
            private VisualElement _board;
            private VisualElement _picture;
            private VisualElement _rail;
            private Rect _lastRect;
            private Rect _lastViewport;

            public void Hide()
            {
                if (_clip != null)
                    _clip.style.display = DisplayStyle.None;
            }

            public void Show(SceneView view, PrototypeController source, Bounds bounds)
            {
                Rect viewport = view.cameraViewport;
                Vector3 topLeft = view.camera.WorldToViewportPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z));
                Vector3 bottomRight = view.camera.WorldToViewportPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z));
                var picture = Rect.MinMaxRect(
                    topLeft.x * viewport.width, (1 - topLeft.y) * viewport.height,
                    bottomRight.x * viewport.width, (1 - bottomRight.y) * viewport.height);
                if (picture.width <= 0 || picture.height <= 0)
                {
                    Hide();
                    return;
                }

                if (_clip == null || source != _source || _clip.parent != view.rootVisualElement)
                    Attach(view, source);

                _clip.style.display = DisplayStyle.Flex;
                if (_lastViewport != viewport)
                {
                    _clip.style.left = viewport.x;
                    _clip.style.top = viewport.y;
                    _clip.style.width = viewport.width;
                    _clip.style.height = viewport.height;
                    _lastViewport = viewport;
                }
                if (_lastRect == picture)
                    return;
                _lastRect = picture;

                float scale = picture.height / ArtworkHeight;
                float pictureWidth = picture.width / scale;
                float railWidth = pictureWidth / 3f;
                float width = pictureWidth + RailGap + railWidth;

                _root.style.left = picture.x;
                _root.style.top = picture.y - HeaderOffset * scale;
                _root.style.width = width;
                _root.style.scale = new Scale(new Vector3(scale, scale, 1));
                _stage.style.height = ArtworkHeight;
                _board.style.height = ArtworkHeight;
                _board.style.width = width;
                _picture.style.width = pictureWidth;
                _rail.style.width = railWidth;
                _header.style.width = width;
                _satisfaction.style.width = railWidth;
            }

            private void Attach(SceneView view, PrototypeController source)
            {
                _clip?.RemoveFromHierarchy();
                _source = source;
                _lastRect = default;
                _lastViewport = default;
                _clip = new VisualElement { pickingMode = PickingMode.Ignore };
                _clip.style.position = Position.Absolute;
                _clip.style.overflow = Overflow.Hidden;
                _root = source.CreateSceneHudPreview();
                _root.name = "etazh-scene-hud";
                _root.style.position = Position.Absolute;
                _root.style.transformOrigin = new TransformOrigin(0, 0);
                _root.style.backgroundColor = Color.clear;
                _root.style.paddingLeft = 0;
                _root.style.paddingRight = 0;
                _root.style.paddingTop = 0;
                _root.style.paddingBottom = 0;
                _stage = _root.Q(className: "stage");
                _stage.style.flexGrow = 0;
                _header = _root.Q(className: "header");
                _satisfaction = _root.Q(className: "satisfaction");
                _board = _root.Q("art-board");
                _picture = _root.Q("art-viewport");
                _rail = _root.Q(className: "art-controls");
                AllowScenePicking(_root);
                _clip.Add(_root);
                view.rootVisualElement.Add(_clip);
            }

            private static void AllowScenePicking(VisualElement element)
            {
                element.pickingMode = PickingMode.Ignore;
                element.focusable = false;
                foreach (VisualElement child in element.Children())
                    AllowScenePicking(child);
            }
        }
    }
}
