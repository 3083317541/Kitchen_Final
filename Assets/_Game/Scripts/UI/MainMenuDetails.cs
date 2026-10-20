using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitchen.UI
{
    public sealed class MainMenuDetails : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private GameObject home;
        [SerializeField] private GameObject tutorial;
        [SerializeField] private GameObject classic;
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Button classicButton;
        [SerializeField] private Button tutorialBack;
        [SerializeField] private Button classicBack;
        private Button source;

        public void Configure(GameObject menu, GameObject tutorialPage, GameObject classicPage,
            Button openTutorial, Button openClassic, Button backTutorial, Button backClassic)
        {
            home = menu;
            tutorial = tutorialPage;
            classic = classicPage;
            tutorialButton = openTutorial;
            classicButton = openClassic;
            tutorialBack = backTutorial;
            classicBack = backClassic;
        }

        private void Awake()
        {
            tutorialButton.onClick.AddListener(OpenTutorial);
            classicButton.onClick.AddListener(OpenClassic);
            tutorialBack.onClick.AddListener(Back);
            classicBack.onClick.AddListener(Back);
            foreach (Selectable control in tutorial.GetComponentsInChildren<Selectable>(true))
                control.gameObject.AddComponent<PaperMenuCancel>();
            foreach (Selectable control in classic.GetComponentsInChildren<Selectable>(true))
                control.gameObject.AddComponent<PaperMenuCancel>();
        }

        public void OpenTutorial() { Open(tutorial, tutorialButton, tutorialBack); }
        public void OpenClassic() { Open(classic, classicButton, classicBack); }

        private void Open(GameObject page, Button entry, Button back)
        {
            source = entry;
            home.SetActive(false);
            tutorial.SetActive(page == tutorial);
            classic.SetActive(page == classic);
            EventSystem.current?.SetSelectedGameObject(back.gameObject);
        }

        public void Back()
        {
            tutorial.SetActive(false);
            classic.SetActive(false);
            home.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(source != null ? source.gameObject : tutorialButton.gameObject);
        }

        public void OnCancel(BaseEventData data)
        {
            if (tutorial.activeSelf || classic.activeSelf) { Back(); data.Use(); }
        }

        private void OnDestroy()
        {
            if (tutorialButton != null) tutorialButton.onClick.RemoveListener(OpenTutorial);
            if (classicButton != null) classicButton.onClick.RemoveListener(OpenClassic);
            if (tutorialBack != null) tutorialBack.onClick.RemoveListener(Back);
            if (classicBack != null) classicBack.onClick.RemoveListener(Back);
        }
    }
}
