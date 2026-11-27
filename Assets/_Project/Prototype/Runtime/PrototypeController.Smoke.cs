#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace Etazh.Prototype
{
    public sealed partial class PrototypeController
    {
        private bool _smokeRunning;

        private void TryStartSmoke()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-prototype-smoke");
            if (index < 0 || index + 1 >= arguments.Length)
                return;

            _smokeRunning = true;
            StartCoroutine(RunSmoke(arguments[index + 1]));
        }

        private IEnumerator RunSmoke(string evidenceDirectory)
        {
            Directory.CreateDirectory(evidenceDirectory);
            var results = new List<string>();
            int baselineScore = 0;
            yield return new WaitForEndOfFrame();

            for (int attempt = 0; attempt < 3; attempt++)
            {
                SubmitStart();
                if (!_active || _model.Tick != 0 || _model.CurrentFloor != 1 ||
                    _model.Score != 0 || _model.WaitingAt(1) != 0 || _model.ApproachProgressAt(1) != 0)
                {
                    ReportFailure(evidenceDirectory, "Shift or restart did not reset the HUD and model.");
                    yield break;
                }

                if (attempt == 0)
                {
                    for (int tick = 0; tick < ShiftRules.ApproachTicks + ShiftRules.StopTicks; tick++)
                        AdvanceTick();

                    PressFifthFloorWithTestKeyboard();
                    if (_model.State != LiftState.Moving || _model.TargetFloor != 5 || _model.RidingTo(5) != 1)
                    {
                        ReportFailure(evidenceDirectory, "The first passenger did not auto-board or digit 5 did not select the destination.");
                        yield break;
                    }

                    results.Add("Standalone Input System: the first passenger auto-boarded and digit 5 selected their destination.");
                }

                while (_active)
                {
                    if (_model.State == LiftState.Idle)
                        Dispatch(attempt);

                    AdvanceTick();
                    if (_model.Tick % 50 == 0)
                        yield return null;
                }

                if (_model.Tick != ShiftRules.ShiftTicks || _model.Score != _model.DeliveredPoints - ShiftRules.StairPenalty * _model.LeftByStairs ||
                    _document.rootVisualElement.Q<Button>("start-shift").text != "Повторить смену")
                {
                    ReportFailure(evidenceDirectory, "Final score, timer, or repeat action was incorrect.");
                    yield break;
                }

                if (attempt == 0)
                    baselineScore = _model.Score;

                if (attempt == 2 && (_model.LeftByStairs == 0 || _model.Score >= baselineScore || _best < baselineScore))
                {
                    ReportFailure(evidenceDirectory, "Missing calls had no consequence, or replay lost the higher score.");
                    yield break;
                }

                results.Add($"Shift {attempt + 1}: tick={_model.Tick}, delivered={_model.Delivered}, stairs={_model.LeftByStairs}, score={_model.Score}, best={_best}");
            }

            File.WriteAllLines(Path.Combine(evidenceDirectory, "player-smoke.txt"), results);
            Application.Quit(0);
        }

        private void SubmitStart()
        {
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            string action = _mainMenu?.IsVisible == true ? "main-menu-start" : "start-shift";
            Button button = _document.rootVisualElement.Q<Button>(action);
            submit.target = button;
            button.SendEvent(submit);
        }

        private static void PressFifthFloorWithTestKeyboard()
        {
            InputSettings settings = InputSystem.settings;
            InputSettings.BackgroundBehavior originalFocus = settings.backgroundBehavior;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit5));
                InputSystem.Update();
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = originalFocus;
            }
        }

        private void Dispatch(int attempt)
        {
            if (attempt == 2 && _model.Tick < 32 * ShiftRules.TicksPerSecond)
                return;

            int target = 0;
            int bestPriority = int.MinValue;
            for (int floor = 1; floor <= ShiftRules.FloorCount; floor++)
            {
                int waiting = _model.WaitingAt(floor);
                int riding = _model.RidingTo(floor);
                if (floor == _model.CurrentFloor || (waiting == 0 && riding == 0))
                    continue;

                int priority = attempt == 1
                    ? 20 * waiting + 10 * riding
                    : 10 * waiting + 40 * riding;
                priority -= 5 * Math.Abs(floor - _model.CurrentFloor);
                if (priority <= bestPriority)
                    continue;

                bestPriority = priority;
                target = floor;
            }

            if (target != 0)
                SelectFloor(target);
        }

        private static void ReportFailure(string evidenceDirectory, string message)
        {
            File.WriteAllText(Path.Combine(evidenceDirectory, "player-smoke.txt"), "FAIL: " + message);
            Debug.LogError(message);
            Application.Quit(1);
        }
    }
}
#endif
