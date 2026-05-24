using System;
using Game.OutGame.DungeonLobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.OutGame.Lobby
{
    /// <summary>
    /// 방 생성 팝업 View.
    /// LobbyView가 Addressables로 로드 후 Setup()을 호출한다.
    /// </summary>
    public sealed class CreateDungeonRoomPopupView : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] private InputField roomNameInput;
        [SerializeField] private Button     plusPlayerButton;
        [SerializeField] private Button     minusPlayerButton;
        [SerializeField] private InputField maxPlayersInput;

        [Header("버튼")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private const int MinPlayers = 1;
        private const int MaxPlayers = 8;

        private LobbyModel _model;
        private Action     _onClose;
        private int        _maxPlayers = 1;

        /// <summary>LobbyView가 Addressable 인스턴스 생성 직후 호출.</summary>
        public void Setup(LobbyModel model, Action onClose)
        {
            _model   = model;
            _onClose = onClose;

            UpdateMaxPlayersDisplay();

            plusPlayerButton.onClick.AddListener(OnPlusClicked);
            minusPlayerButton.onClick.AddListener(OnMinusClicked);
            confirmButton.onClick.AddListener(OnConfirmClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);

            RefreshButtons();
        }

        // ── Intent 발행 ──────────────────────────────

        private void OnConfirmClicked()
        {
            var roomName = roomNameInput.text.Trim();
            if (string.IsNullOrEmpty(roomName)) return;

            _model.Accept(new LobbyIntent.CreateRoom(roomName, _maxPlayers));
            Close();
        }

        private void OnCancelClicked() => Close();

        private void Close() => _onClose?.Invoke();

        // ── 인원 수 +/- ────────────────────────────

        private void OnPlusClicked()
        {
            if (_maxPlayers >= MaxPlayers) return;
            _maxPlayers++;
            UpdateMaxPlayersDisplay();
            RefreshButtons();
        }

        private void OnMinusClicked()
        {
            if (_maxPlayers <= MinPlayers) return;
            _maxPlayers--;
            UpdateMaxPlayersDisplay();
            RefreshButtons();
        }

        private void UpdateMaxPlayersDisplay()
        {
            maxPlayersInput.text = _maxPlayers.ToString();
        }

        /// <summary>경계값에서 버튼 비활성화.</summary>
        private void RefreshButtons()
        {
            plusPlayerButton.interactable  = _maxPlayers < MaxPlayers;
            minusPlayerButton.interactable = _maxPlayers > MinPlayers;
        }
    }
}
