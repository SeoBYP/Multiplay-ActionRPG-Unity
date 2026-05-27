using Game.OutGame.Title;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace Game.Title
{
    public class MainMenuWindow : MonoBehaviour
    {
        [Inject] private TitleModel _model;

        [SerializeField] private Button gameStartButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button historyButton;
        [SerializeField] private Button creatorsButton;
        [SerializeField] private Button exitButton;

        [SerializeField] private GameObject LoginWindow;

        void Start()
        {
            gameStartButton.onClick.AddListener(OnGameStart);
            settingsButton.onClick.AddListener(OnSettings);
            historyButton.onClick.AddListener(OnHistory);
            creatorsButton.onClick.AddListener(OnCreators);
            exitButton.onClick.AddListener(OnExit);
        }

        private void OnGameStart()
        {
            if (!_model.State.CurrentValue.IsAuthenticated)
            {
                LoginWindow.SetActive(true);
                return;
            }
            SceneManager.LoadScene("Main");
        }

        private void OnSettings()  => Debug.Log("Settings");
        private void OnHistory()   => Debug.Log("History");
        private void OnCreators()  => Debug.Log("Creators");
        private void OnExit()      => Application.Quit();
    }
}
