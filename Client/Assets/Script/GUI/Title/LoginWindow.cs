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
    [SerializeField] private Button     registerButton;
    [SerializeField] private GameObject registerWindow;
    [SerializeField] private Text       errorText;

    private void Start()
    {
        loginButton.onClick.AddListener(OnLogin);
        registerButton.onClick.AddListener(OpenRegister);

        // 자동 로그인·로그인·회원가입 성공 시 State가 IsAuthenticated = true로 변경 → 씬 전환.
        // RegisterWindow 는 이 창에서만 열리므로(이 구독이 먼저 살아 있다) 씬 전환은 여기 한 곳만 담당한다.
        _model.State
            .Subscribe(OnStateChanged)
            .AddTo(destroyCancellationToken);
    }

    private void OnStateChanged(TitleState state)
    {
        errorText.text = state.ErrorMessage ?? "";
        loginButton.interactable = !state.IsLoading;

        if (state.IsAuthenticated)
            SceneManager.LoadScene("Main");
    }

    private void OpenRegister()
    {
        registerWindow.SetActive(true);
        gameObject.SetActive(false);
    }

    private void OnLogin()
    {
        _model.Accept(new TitleIntent.Login(emailField.text, passwordField.text));
    }
}
