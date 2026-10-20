using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.UI
{
    public sealed class MainMenuSettings : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private GameObject home;
        [SerializeField] private GameObject settings;
        [SerializeField] private Button openButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Slider volume;
        [SerializeField] private Slider hudScale;
        [SerializeField] private Toggle motion;
        [SerializeField] private TMP_Text volumeText;
        [SerializeField] private TMP_Text scaleText;

        public void Configure(GameObject menu, GameObject panel, Button open, Button back,
            Slider audio, Slider scale, Toggle animation, TMP_Text audioValue, TMP_Text scaleValue)
        {
            home = menu;
            settings = panel;
            openButton = open;
            backButton = back;
            volume = audio;
            hudScale = scale;
            motion = animation;
            volumeText = audioValue;
            scaleText = scaleValue;
        }

        private void Awake()
        {
            openButton.onClick.AddListener(Open);
            backButton.onClick.AddListener(Close);
            volume.onValueChanged.AddListener(ChangeVolume);
            hudScale.onValueChanged.AddListener(ChangeScale);
            motion.onValueChanged.AddListener(ChangeMotion);
            foreach (Selectable control in settings.GetComponentsInChildren<Selectable>(true))
                control.gameObject.AddComponent<PaperMenuCancel>();
        }

        public void Open()
        {
            volume.SetValueWithoutNotify(PaperUISettings.Volume);
            hudScale.SetValueWithoutNotify(PlayerPrefs.GetFloat(HudScaler.PrefsKey, 1f));
            motion.SetIsOnWithoutNotify(PaperUISettings.MotionEnabled);
            RefreshValues();
            home.SetActive(false);
            settings.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(backButton.gameObject);
        }

        public void Close()
        {
            settings.SetActive(false);
            home.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(openButton.gameObject);
        }

        public void OnCancel(BaseEventData data)
        {
            if (settings.activeSelf) { Close(); data.Use(); }
        }

        private void ChangeVolume(float value)
        {
            PaperUISettings.SetVolume(value);
            RefreshValues();
        }

        private void ChangeScale(float value)
        {
            PlayerPrefs.SetFloat(HudScaler.PrefsKey, value);
            PlayerPrefs.Save();
            RefreshValues();
        }

        private void ChangeMotion(bool value)
        {
            PaperUISettings.SetMotion(value);
            RefreshValues();
        }

        private void RefreshValues()
        {
            volumeText.text = Mathf.RoundToInt(volume.value * 100f) + "%";
            scaleText.text = Mathf.RoundToInt(hudScale.value * 100f) + "%";
            motion.GetComponentInChildren<TMP_Text>(true).text = motion.isOn ? "ON" : "OFF";
        }

        private void OnDestroy()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (backButton != null) backButton.onClick.RemoveListener(Close);
            if (volume != null) volume.onValueChanged.RemoveListener(ChangeVolume);
            if (hudScale != null) hudScale.onValueChanged.RemoveListener(ChangeScale);
            if (motion != null) motion.onValueChanged.RemoveListener(ChangeMotion);
        }
    }
}
