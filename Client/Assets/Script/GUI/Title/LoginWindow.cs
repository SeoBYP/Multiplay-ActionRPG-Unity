using Cysharp.Threading.Tasks;
using Script.System.Auth;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

public class LoginWindow : MonoBehaviour
{
    [Inject] private readonly IAuthService _authService;
    
    [SerializeField] private InputField emailField;
    [SerializeField] private InputField passwordField;
    [SerializeField] private Button loginButton;

    private void Start()
    {
        loginButton.onClick.AddListener(OnLogin);
    }

    private void OnLogin()
    {
        _authService.LoginOrRegisterAsync(emailField.text, passwordField.text, this.destroyCancellationToken)
            .ContinueWith(result =>
            {
                switch (result)
                {
                    case AuthResult.Success:
                        SceneManager.LoadScene("Main");
                        break;
                    case AuthResult.NeedLogin:
                        Debug.Log("Need Login");
                        break;
                    case AuthResult.Failed:
                        Debug.Log("Failed");
                        break;
                    case AuthResult.TokenExpired:
                        Debug.Log("Token Expired");
                        break;
                    default:
                        Debug.Log("Unknown result");
                        break;
                }
            }).Forget(Debug.LogException);
    }
}
