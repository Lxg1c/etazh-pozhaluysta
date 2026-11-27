using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Etazh.Prototype
{
    internal sealed class MainMenuView
    {
        private const string FullscreenKey = "Etazh.Fullscreen";
        private const float DesignWidth = 1280;
        private const float DesignHeight = 720;

        private readonly VisualElement _screen;
        private readonly VisualElement _main;
        private readonly VisualElement _settings;
        private readonly Button _start;
        private readonly Button _settingsButton;
        private readonly Button _displayMode;
        private bool _fullscreen;

        public MainMenuView(VisualElement root, Texture2D background, Action start, Action quit)
        {
            _screen = new VisualElement { name = "main-menu" };
            _screen.AddToClassList("main-menu-screen");
            _screen.style.backgroundImage = background;
            _screen.styleSheets.Add(Resources.Load<StyleSheet>("MainMenu"));
            _screen.style.unityFont = Resources.Load<Font>("Fonts/PressStart2P-Regular");
            root.Add(_screen);

            var canvas = new VisualElement { pickingMode = PickingMode.Ignore };
            canvas.AddToClassList("main-menu-canvas");
            _screen.Add(canvas);
            _screen.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                Rect size = evt.newRect;
                float scale = Mathf.Min(size.width / DesignWidth, size.height / DesignHeight);
                canvas.style.scale = new Scale(new Vector3(scale, scale, 1));
                canvas.style.top = (size.height - DesignHeight * scale) / 2;
            });

            AddTitle(canvas, "ЭТАЖ,", "main-menu-title-first");
            AddTitle(canvas, "ПОЖАЛУЙСТА", "main-menu-title-second");
            _main = new VisualElement();
            _main.AddToClassList("main-menu-actions");
            canvas.Add(_main);
            _start = AddButton(_main, "НАЧАТЬ СМЕНУ", "main-menu-start", start);
            _settingsButton = AddButton(_main, "НАСТРОЙКИ", "main-menu-settings", ShowSettings);
            AddButton(_main, "ВЫХОД", "main-menu-quit", quit);

            _settings = new VisualElement { name = "main-menu-options" };
            _settings.AddToClassList("main-menu-actions");
            canvas.Add(_settings);
            var displayLabel = new Label("РЕЖИМ ЭКРАНА");
            displayLabel.AddToClassList("main-menu-setting-label");
            _settings.Add(displayLabel);
            _displayMode = AddButton(_settings, "", "main-menu-display", ToggleFullscreen);
            AddButton(_settings, "НАЗАД", "main-menu-back", () => CloseSettings(_settingsButton));
            _screen.RegisterCallback<NavigationCancelEvent>(evt =>
            {
                if (_settings.style.display != DisplayStyle.Flex)
                    return;
                CloseSettings(_settingsButton);
                evt.StopPropagation();
            });

            _fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            if (PlayerPrefs.HasKey(FullscreenKey))
                ApplyDisplayMode();
            RefreshDisplayModeLabel();
        }

        public bool IsVisible => _screen.style.display == DisplayStyle.Flex;

        public void Show()
        {
            _screen.style.display = DisplayStyle.Flex;
            CloseSettings(_start);
        }

        public void Hide() => _screen.style.display = DisplayStyle.None;

        private void ShowSettings()
        {
            _main.style.display = DisplayStyle.None;
            _settings.style.display = DisplayStyle.Flex;
            _displayMode.Focus();
        }

        private void CloseSettings(Button focus)
        {
            _settings.style.display = DisplayStyle.None;
            _main.style.display = DisplayStyle.Flex;
            focus.schedule.Execute(() =>
            {
                if (IsVisible && _main.style.display == DisplayStyle.Flex)
                    focus.Focus();
            });
        }

        private void ToggleFullscreen()
        {
            _fullscreen = !_fullscreen;
            PlayerPrefs.SetInt(FullscreenKey, _fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            ApplyDisplayMode();
            RefreshDisplayModeLabel();
        }

        private void ApplyDisplayMode()
        {
            if (!Application.isEditor)
                Screen.fullScreen = _fullscreen;
        }

        private void RefreshDisplayModeLabel()
            => _displayMode.text = _fullscreen ? "ПОЛНЫЙ ЭКРАН" : "В ОКНЕ";

        private static void AddTitle(VisualElement canvas, string text, string className)
        {
            var title = new Label(text) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("main-menu-title");
            title.AddToClassList(className);
            canvas.Add(title);
        }

        private static Button AddButton(VisualElement parent, string text, string name, Action action)
        {
            var button = new Button(action) { name = name, text = text };
            button.AddToClassList("main-menu-button");
            var cursor = new Label("▶") { pickingMode = PickingMode.Ignore };
            cursor.AddToClassList("main-menu-cursor");
            button.Add(cursor);
            button.RegisterCallback<PointerEnterEvent>(_ => button.Focus());
            parent.Add(button);
            return button;
        }
    }
}
