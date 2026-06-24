using System;
using System.Collections.Generic;
using Game.Presentation.Progression;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.Stat
{
    /// <summary>
    /// 캐릭터 정보/스탯창 (MVI View). ProgressionModel 만 주입받는다(IProgressionService·proto 비노출).
    /// State.Lines(라벨:값 목록) → 행 템플릿 복제로 렌더. 열기/재오픈 때 Refresh(서버 권위 최신값).
    /// 색상은 프리팹의 라벨/값 TMP 에 지정(라벨=흐림, 값=밝게). 닫기 버튼은 자기 SetActive(false).
    /// 창 활성 동안 UiInputCaptureBehaviour 로 게임플레이 입력 점유(인벤/퀘스트 동일 — 이동 차단).
    /// </summary>
    public class StatWindow : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform rowContainer;   // 행 부모(VerticalLayoutGroup)
        [SerializeField] private GameObject rowTemplate;    // 행 템플릿(라벨 TMP + 값 TMP 두 자식, 비활성)

        [Inject] private ProgressionModel _model;

        private IDisposable _sub;
        private readonly List<GameObject> _rows = new();

        private void Start()
        {
            if (rowTemplate != null) rowTemplate.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            if (_model != null)
            {
                gameObject.AddComponent<Game.GUI.UiInputCaptureBehaviour>()
                          .Bind(_model.BeginUiCapture, _model.EndUiCapture);

                _sub = _model.State.Subscribe(Render);
                _model.Refresh();
            }
        }

        private void OnEnable()
        {
            // 재오픈 시 최신화(최초 활성화 때는 주입 전이라 null → Start가 담당).
            if (_model != null)
                _model.Refresh();
        }

        private void OnDestroy() => _sub?.Dispose();

        private void Render(ProgressionViewState state)
        {
            var lines = state.Lines;
            EnsureRows(lines.Count);

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row == null) continue;
                if (i < lines.Count)
                {
                    row.SetActive(true);
                    var tmps = row.GetComponentsInChildren<TextMeshProUGUI>(true);
                    if (tmps.Length >= 2)
                    {
                        tmps[0].text = lines[i].Label; // 라벨(흐림 색은 프리팹)
                        tmps[1].text = lines[i].Value; // 값(밝은 색은 프리팹)
                    }
                    else if (tmps.Length == 1)
                    {
                        tmps[0].text = $"{lines[i].Label}  {lines[i].Value}";
                    }
                }
                else
                {
                    row.SetActive(false);
                }
            }
        }

        private void EnsureRows(int needed)
        {
            if (rowTemplate == null || rowContainer == null) return;
            while (_rows.Count < needed)
                _rows.Add(Instantiate(rowTemplate, rowContainer));
        }
    }
}
