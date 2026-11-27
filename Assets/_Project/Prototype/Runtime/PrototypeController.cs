using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Etazh.Prototype
{
    public sealed partial class PrototypeController : MonoBehaviour
    {
        private const double TickDuration = 1d / ShiftRules.TicksPerSecond;
        [SerializeField] private UIDocument _document;
        [SerializeField] private ElevatorArtwork _artwork;

        private InputActionMap _keyboard;
        private PrototypeView _view;
        private VisualElement _hudRoot;
        private MainMenuView _mainMenu;
        private ShiftModel _model;
        private bool _active;
        private bool _paused;
        private int? _best;
        private double _elapsed;

        private void Awake() => VolumeControl.Restore();

        private void Start()
        {
            VisualElement root = _document.rootVisualElement;
            _hudRoot = new VisualElement();
            root.Add(_hudRoot);
            Texture2D menuBackground = _artwork != null ? Resources.Load<Texture2D>("Menu/Lobby") : null;
            _view = new PrototypeView(_hudRoot, SelectFloor, StartShift,
                _artwork != null ? _artwork.Initialize() : null,
                _artwork != null ? _artwork.PortraitFor : null,
                returnToMenu: menuBackground != null ? ShowMainMenu : null, togglePause: TogglePause);

            _model = new ShiftModel(Array.Empty<PassengerCall>());
            Refresh();
            PositionPresentation();
            if (menuBackground != null)
            {
                _mainMenu = new MainMenuView(root, menuBackground, StartShift, QuitGame);
                ShowMainMenu();
            }
            else
            {
                _view.ShowIntro();
            }
            BindKeyboard();
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            TryStartSmoke();
#endif
        }

        private void Update()
        {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (_smokeRunning)
                return;
#endif
            if (!_active || _paused)
                return;

            _elapsed += Time.unscaledDeltaTime;
            while (_elapsed >= TickDuration && _active)
            {
                _elapsed -= TickDuration;
                AdvanceTick();
            }

            PositionPresentation();
        }

        private void OnEnable() => _keyboard?.Enable();

        private void OnDisable() => _keyboard?.Disable();

        private void OnDestroy() => _keyboard?.Dispose();

        private void BindKeyboard()
        {
            _keyboard = new InputActionMap("Elevator");
            for (int floor = 1; floor <= ShiftRules.FloorCount; floor++)
            {
                int destination = floor;
                InputAction select = _keyboard.AddAction($"Floor{floor}", InputActionType.Button);
                select.AddBinding($"<Keyboard>/{floor}");
                select.AddBinding($"<Keyboard>/numpad{floor}");
                select.performed += _ => SelectFloor(destination);
            }

            InputAction start = _keyboard.AddAction("StartShift", InputActionType.Button);
            start.AddBinding("<Keyboard>/enter");
            start.AddBinding("<Keyboard>/numpadEnter");
            start.performed += _ =>
            {
                if (_mainMenu?.IsVisible != true)
                    StartShift();
            };
            InputAction pause = _keyboard.AddAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.performed += _ => TogglePause();
            _keyboard.Enable();
        }

        private void StartShift()
        {
            if (_active)
                return;

            var schedule = ShiftRules.CreateSchedule();
            _model = new ShiftModel(schedule);
            _view.PrepareShiftHints(schedule);
            _active = true;
            _elapsed = 0;
            _mainMenu?.Hide();
            _hudRoot.style.display = DisplayStyle.Flex;
            _view.HideOverlay();
            Refresh();
            PositionPresentation();
        }

        private void ShowMainMenu()
        {
            _hudRoot.style.display = DisplayStyle.None;
            _mainMenu.Show();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SelectFloor(int floor)
        {
            if (_active && !_paused && _model.TrySelectFloor(floor))
            {
                Refresh();
                PositionPresentation();
            }
        }

        private void TogglePause()
        {
            if (!_active)
                return;

            _paused = !_paused;
            _view.SetPaused(_paused, _model.State == LiftState.Idle);
        }

        private void AdvanceTick()
        {
            _model.Advance();
            if (_model.Finished)
            {
                _active = false;
                _best = _best.HasValue ? Math.Max(_best.Value, _model.Score) : _model.Score;
                _view.ShowResults(_model, _best.Value);
            }

            Refresh();
        }

        private void Refresh() => _view.Refresh(_model, _active, _best);

        private void PositionPresentation()
        {
            double partialTick = _active ? _elapsed / TickDuration : 0;
            bool moving = _model.State == LiftState.Moving && !_model.Finished;
            _view.PositionCabin(_model.LiftPosition(partialTick), moving,
                moving && _model.TargetFloor > _model.CurrentFloor);
            if (_artwork != null)
                _artwork.Draw(_model, partialTick);
        }

#if UNITY_EDITOR
        public Bounds SceneHudBounds => _artwork != null ? _artwork.PreviewBounds : default;

        public VisualElement CreateSceneHudPreview()
        {
            var root = new VisualElement();
            var view = new PrototypeView(root, _ => { }, () => { }, scenePreview: true);
            view.Refresh(new ShiftModel(Array.Empty<PassengerCall>()), active: false);
            view.HideOverlay();
            return root;
        }
#endif
    }
}
