using System;
using System.Collections.Generic;
using Game.Presentation.DungeonLobby;
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

        [Header("던전 선택")]
        [Tooltip("선택지 메타(mapId→표시이름). 미할당이면 던전 선택 없이 서버 기본 맵으로 생성.")]
        [SerializeField] private DungeonCatalog  dungeonCatalog;
        [Tooltip("던전 선택 드롭다운. 미할당이면 기본 맵 사용(하위호환).")]
        [SerializeField] private TMP_Dropdown    dungeonDropdown;

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
            PopulateDungeonDropdown();

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

            _model.Accept(new LobbyIntent.CreateRoom(roomName, _maxPlayers, SelectedMapId()));
            Close();
        }

        // ── 던전 선택 ────────────────────────────────

        /// <summary>카탈로그의 표시이름으로 드롭다운을 채운다. 카탈로그/드롭다운 미할당이면 아무것도 안 함(기본 맵).</summary>
        private void PopulateDungeonDropdown()
        {
            if (dungeonDropdown == null || dungeonCatalog == null) return;

            dungeonDropdown.ClearOptions();
            var labels = new List<string>(dungeonCatalog.Dungeons.Count);
            foreach (var d in dungeonCatalog.Dungeons)
                labels.Add(d.DisplayName);
            dungeonDropdown.AddOptions(labels);
            dungeonDropdown.value = 0;
            dungeonDropdown.RefreshShownValue();
        }

        /// <summary>선택된 던전의 mapId. 드롭다운/카탈로그 미할당 또는 선택 없음이면 ""(서버 기본 맵).</summary>
        private string SelectedMapId()
        {
            if (dungeonDropdown == null || dungeonCatalog == null) return "";
            int i = dungeonDropdown.value;
            if (i < 0 || i >= dungeonCatalog.Dungeons.Count) return "";
            return dungeonCatalog.Dungeons[i].MapId ?? "";
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
