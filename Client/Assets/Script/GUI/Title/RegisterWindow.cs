using Cysharp.Threading.Tasks;
using Game.Presentation.Title;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Title
{
    /// <summary>
    /// 회원가입 창. 입력 검증(빈 값·비밀번호 확인)과 가입 요청은 TitleModel 이 한다.
    /// 가입 성공 후 씬 전환은 이 창을 연 LoginWindow 가 State 구독으로 처리한다.
    /// </summary>
    public class RegisterWindow : MonoBehaviour
    {
        [Inject] private TitleModel _model;

        [SerializeField] private InputField emailField;
        [SerializeField] private InputField passwordField;
        [SerializeField] private InputField passwordConfirmField;
        [SerializeField] private Button     registerButton;
        [SerializeField] private Button     backButton;
        [SerializeField] private GameObject loginWindow;
        [SerializeField] private Text       errorText;

        private void Start()
        {
            registerButton.onClick.AddListener(OnRegister);
            backButton.onClick.AddListener(OnBack);

            _model.State
                .Subscribe(OnStateChanged)
                .AddTo(destroyCancellationToken);
        }

        private void OnStateChanged(TitleState state)
        {
            errorText.text = state.ErrorMessage ?? "";
            registerButton.interactable = !state.IsLoading;
        }

        private void OnRegister()
        {
            _model.Accept(new TitleIntent.Register(
                emailField.text, passwordField.text, passwordConfirmField.text));
        }

        private void OnBack()
        {
            loginWindow.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
