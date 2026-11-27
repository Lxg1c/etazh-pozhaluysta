using UnityEngine;
using UnityEngine.UIElements;

namespace Etazh.Prototype
{
    internal sealed class VolumeControl : VisualElement
    {
        private const string Preference = "Etazh.Volume";
        private readonly Label _label;
        private readonly SliderInt _slider;

        public VolumeControl(string sliderName)
        {
            AddToClassList("volume-control");
            styleSheets.Add(Resources.Load<StyleSheet>("VolumeControl"));
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList("volume-label");
            Add(_label);
            _slider = new SliderInt(0, 100) { name = sliderName, tooltip = "Громкость игры" };
            Add(_slider);
            var save = _slider.schedule.Execute(PlayerPrefs.Save);
            save.Pause();
            _slider.RegisterValueChangedCallback(evt =>
            {
                Apply(evt.newValue);
                PlayerPrefs.SetInt(Preference, evt.newValue);
                _label.text = Caption(evt.newValue);
                save.ExecuteLater(250);
            });
            Refresh();
        }

        private static int Saved => Mathf.Clamp(PlayerPrefs.GetInt(Preference, 100), 0, 100);

        public static void Restore() => Apply(Saved);

        public void Refresh()
        {
            int percent = Saved;
            _slider.SetValueWithoutNotify(percent);
            _label.text = Caption(percent);
        }

        private static string Caption(int percent) => $"ГРОМКОСТЬ {percent}%";

        private static void Apply(int percent)
        {
            float gain = percent / 100f;
            AudioListener.volume = gain * gain;
        }
    }
}
