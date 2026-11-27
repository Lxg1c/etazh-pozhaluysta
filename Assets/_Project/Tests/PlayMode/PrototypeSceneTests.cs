using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Etazh.Prototype.Tests
{
    public sealed class PrototypeSceneTests
    {
        [UnityTest]
        public IEnumerator ApproachShowsBeforeAutoboardingAndUpdatesBothCapacityIndicators()
        {
            yield return OpenScene();
            VisualElement root = UnityEngine.Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

            for (int floor = 1; floor <= ShiftRules.FloorCount; floor++)
                Assert.That(root.Q<Button>($"select-{floor}"), Is.Not.Null);

            Submit(root.Q<Button>("start-shift"));
            Label clock = root.Q<Label>("clock");
            Assert.That(clock.text, Is.EqualTo("05:00"));
            var clockLabels = clock.parent.Query<Label>().ToList();
            Assert.That(clockLabels.FindAll(label => label.text.Contains(":")).Count, Is.EqualTo(1));

            Assert.That(root.Q<Label>("waiting-1").text, Is.EqualTo("Подходит"));
            Assert.That(root.Q<VisualElement>("approach-1").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Button>("select-5").ClassListContains("destination"), Is.False);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!root.Q<Button>("select-5").ClassListContains("destination") && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(root.Q<Button>("select-5").ClassListContains("destination"), Is.True);
            Assert.That(clock.text, Does.StartWith("04:"));
            Assert.That(root.Q<Label>("cabin-routes").text, Is.EqualTo("→ 5"));
            Assert.That(root.Q<Label>("capacity-weight").text, Is.EqualTo("Вес 50 / 400 кг"));
            Assert.That(root.Q<Label>("capacity-space").text, Is.EqualTo("Место 1 / 4"));
            Assert.That(root.Q<Label>("satisfaction").text, Is.EqualTo("50%"));
            Assert.That(root.Q<VisualElement>("satisfaction-pointer").style.left.value.value, Is.EqualTo(50));

            deadline = Time.realtimeSinceStartup + 3f;
            Button destination = root.Q<Button>("select-5");
            while (!destination.enabledInHierarchy && Time.realtimeSinceStartup < deadline)
                yield return null;

            Submit(destination);
            deadline = Time.realtimeSinceStartup + 8f;
            while (root.Q<Label>("satisfaction").text == "50%" && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(root.Q<Label>("satisfaction").text, Is.EqualTo("53%"));
            Assert.That(root.Q<VisualElement>("satisfaction-pointer").style.left.value.value, Is.EqualTo(53));
        }

        [UnityTest]
        public IEnumerator KeyboardSelectsSameStopAsItsPanelButton()
        {
            InputSettings settings = InputSystem.settings;
            InputSettings.EditorInputBehaviorInPlayMode oldRouting = settings.editorInputBehaviorInPlayMode;
            InputSettings.BackgroundBehavior oldFocus = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return OpenScene();
                VisualElement root = UnityEngine.Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
                Submit(root.Q<Button>("start-shift"));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
                InputSystem.Update();
                float deadline = Time.realtimeSinceStartup + 2f;
                while (!root.Q<Button>("select-2").ClassListContains("selected") && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(root.Q<Button>("select-2").ClassListContains("selected"), Is.True);
                Assert.That(root.Q<Label>("lift-status").text, Is.EqualTo("Едет на 2-й этаж"));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.editorInputBehaviorInPlayMode = oldRouting;
                settings.backgroundBehavior = oldFocus;
            }
        }

        private static IEnumerator OpenScene()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("ElevatorPrototype");
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;
        }

        private static void Submit(Button button)
        {
            Assert.That(button.enabledInHierarchy, Is.True);
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }
    }
}
