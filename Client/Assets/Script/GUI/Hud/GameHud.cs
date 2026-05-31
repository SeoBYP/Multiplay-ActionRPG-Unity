using Cysharp.Threading.Tasks;
using Game.Presentation.InGame;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.OutGame
{
    /// <summary>
    /// 인게임 HUD View.
    ///
    /// MVI 규칙:
    ///   - State를 받아 UI를 렌더링한다.
    ///   - 사용자 입력을 Intent로 변환해 Model에 전달한다.
    ///   - Service / Repository 직접 호출 없음.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [Inject] private InGameModel _model;

        [SerializeField] private Button returnToLobbyButton;

        private void Start()
        {
            returnToLobbyButton.onClick.AddListener(OnClickReturnToLobby);

            _model.State
                .Subscribe(Render)
                .AddTo(destroyCancellationToken);
        }

        private void OnClickReturnToLobby()
        {
            _model.Accept(InGameIntent.ReturnToLobby.Instance);
        }

        private void Render(InGameState state)
        {
            // 복귀 처리 중에는 버튼 비활성화 (중복 클릭 방지)
            returnToLobbyButton.interactable = !state.IsReturning;
        }
    }
}