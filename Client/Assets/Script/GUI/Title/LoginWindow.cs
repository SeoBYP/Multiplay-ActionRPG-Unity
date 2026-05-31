using Cysharp.Threading.Tasks;
using Game.Presentation.Title;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

public class LoginWindow : MonoBehaviour
{
    [Inject] private TitleModel _model;

    [SerializeField] private InputField emailField;
    [SerializeField] private InputField passwordField;
    [SerializeField] private Button     loginButton;

    private void Start()
    {
        loginButton.onClick.AddListener(OnLogin);

        // 자동 로그인 성공 시 State가 IsAuthenticated = true로 변경 → 씬 전환
        _model.State
            .Subscribe(OnStateChanged)
            .AddTo(destroyCancellationToken);
    }

    private void OnStateChanged(TitleState state)
    {
        if (state.IsAuthenticated)
            SceneManager.LoadScene("Main");
    }

    private void OnLogin()
    {
        _model.Accept(new TitleIntent.Login(emailField.text, passwordField.text));
    }
}
