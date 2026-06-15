using System;
using Cysharp.Threading.Tasks;
using Game.GUI.Common;
using Script.GUI.Inventory;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.GUI
{
    /// <summary>
    /// 슬롯 클릭 → ItemActionPanel(Addressable)을 슬롯 오른쪽에 Canvas 직속으로 띄우고, 뒤 백드롭으로 닫는 공용 컨트롤러.
    /// Inventory·Equipment 양쪽이 동일 동작을 공유한다(중복 제거). 한 번에 하나만 연다.
    ///
    /// 패널의 버튼 구성은 호출처가 <paramref name="configure"/> 콜백에서 panel.Bind(...)로 주입한다
    /// (인벤토리=use/equip, 장비창=unequip). 컨트롤러는 로드/배치/닫기만 담당한다.
    /// </summary>
    public sealed class ItemActionPanelController
    {
        private ItemActionPanel _activePanel;
        private BackDropButton _activeBackdrop;

        public async UniTask OpenAsync(Canvas canvas, RectTransform slotRect, Action<ItemActionPanel> configure)
        {
            Close(); // 기존 열린 팝업 정리(한 번에 하나)

            if (canvas == null)
            {
                Debug.LogWarning("[ItemActionPanel] Canvas 없음 — 액션 패널 생략");
                return;
            }

            // 백드롭 먼저(즉시 뒤 화면 차단) → 패널을 그 위(SetAsLastSibling). 둘 다 Canvas 직속.
            _activeBackdrop = BackDropButton.Create(canvas.transform);
            _activeBackdrop.Clicked += Close;
            var openedBackdrop = _activeBackdrop; // 로드 중 재진입 감지용

            GameObject go;
            try
            {
                go = await Addressables.InstantiateAsync(AddressKeys.UI.ItemActionPanel, canvas.transform).Task.AsUniTask();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ItemActionPanel] Addressable 로드 실패: {e.Message}");
                Close();
                return;
            }

            // 로드 중 다른 슬롯 클릭/닫힘이 끼어들었으면 이 인스턴스는 폐기(현재 백드롭이 바뀜).
            if (_activeBackdrop != openedBackdrop)
            {
                Addressables.ReleaseInstance(go);
                return;
            }

            _activePanel = go.GetComponent<ItemActionPanel>();
            if (_activePanel == null)
            {
                Debug.LogError("[ItemActionPanel] 컴포넌트 없음 (prefab 확인)");
                Addressables.ReleaseInstance(go);
                Close();
                return;
            }

            var panelRt = (RectTransform)_activePanel.transform;
            panelRt.SetAsLastSibling();

            // 슬롯 오른쪽 변 중앙에 패널 왼쪽-중앙 pivot 을 맞춘다(자식 아님 — 월드 좌표 계산).
            var corners = new Vector3[4];
            slotRect.GetWorldCorners(corners); // 0 BL, 1 TL, 2 TR, 3 BR
            panelRt.pivot = new Vector2(0f, 0.5f);
            panelRt.position = (corners[2] + corners[3]) * 0.5f;

            _activePanel.OnCloseRequested += Close;
            configure(_activePanel);
        }

        /// <summary>열린 액션 패널 + 백드롭을 파괴한다(버튼 사용·백드롭 클릭·창 닫기/파괴 시).</summary>
        public void Close()
        {
            if (_activePanel != null)
            {
                _activePanel.OnCloseRequested -= Close;
                Addressables.ReleaseInstance(_activePanel.gameObject); // InstantiateAsync 짝
                _activePanel = null;
            }
            if (_activeBackdrop != null)
            {
                _activeBackdrop.Clicked -= Close;
                UnityEngine.Object.Destroy(_activeBackdrop.gameObject);
                _activeBackdrop = null;
            }
        }
    }
}
