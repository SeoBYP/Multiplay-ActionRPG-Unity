using System.Collections.Generic;
using Game.Presentation.InGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace Game.GUI.OutGame
{
    /// <summary>
    /// 던전 파티원 HP HUD(화면 좌상단). MVI View — 오직 <see cref="PartyModel"/> 하나만 주입받는다.
    ///
    /// GameHud(로컬 전용, InGameModel)와 분리한 이유: MVI "뷰=모델 1개" 규칙 위반을 피하고,
    /// 프리팹 수술 없이 코드로 자체 Canvas·행 풀을 구성해 어느 씬에서든 생성/해제 가능(생명주기 원칙 3).
    ///
    /// 데이터: PartyModel.Changed 발행 시 GetParty() 재조회 → 행 풀 재바인딩. HP 진실원은 서버 권위(GAS).
    /// </summary>
    public sealed class PartyHpView : IStartable, System.IDisposable
    {
        private readonly PartyModel _model;

        private RectTransform _panel;
        private readonly List<Row> _rows = new();

        private const float RowWidth = 220f;
        private const float RowHeight = 30f;

        public PartyHpView(PartyModel model)
        {
            _model = model;
        }

        public void Start()
        {
            BuildCanvas();
            _model.Changed += Render;
            Render();
        }

        public void Dispose()
        {
            _model.Changed -= Render;
            if (_panel != null) Object.Destroy(_panel.gameObject.transform.root.gameObject);
        }

        /// <summary>화면 좌상단에 자체 Overlay Canvas + 세로 레이아웃 컨테이너를 코드로 만든다(비상호작용).</summary>
        private void BuildCanvas()
        {
            var canvasGo = new GameObject("PartyHpCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // HUD 위, 팝업(수백) 아래
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f; // 높이 기준(세로 목록이라 세로 스케일 고정이 안정적)
            // 레이캐스트 불필요(표시 전용) — GraphicRaycaster 미부착.

            var panelGo = new GameObject("PartyHpPanel", typeof(RectTransform));
            _panel = (RectTransform)panelGo.transform;
            _panel.SetParent(canvasGo.transform, false);
            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(24f, -24f);

            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void Render()
        {
            if (_panel == null) return;
            var party = _model.GetParty();

            while (_rows.Count < party.Count)
                _rows.Add(new Row(_panel));

            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < party.Count) _rows[i].Bind(party[i]);
                else _rows[i].Hide();
            }
        }

        /// <summary>파티 HUD 한 행 — 배경 + HP 채움바(Filled) + "닉네임  hp/maxHp" 텍스트.</summary>
        private sealed class Row
        {
            private readonly GameObject _go;
            private readonly Image _fill;
            private readonly TextMeshProUGUI _label;

            private static readonly Color LocalColor = new Color(0.35f, 0.8f, 1f);     // 내 캐릭터 강조(하늘)
            private static readonly Color AllyColor = new Color(0.6f, 1f, 0.5f);       // 아군(연두)
            private static readonly Color DeadColor = new Color(0.55f, 0.55f, 0.55f);  // 사망(회색)

            public Row(RectTransform parent)
            {
                _go = new GameObject("PartyRow", typeof(RectTransform));
                var rt = (RectTransform)_go.transform;
                rt.SetParent(parent, false);

                var le = _go.AddComponent<LayoutElement>();
                le.preferredWidth = RowWidth;
                le.preferredHeight = RowHeight;

                // 배경(어두운 반투명)
                var bg = _go.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.55f);

                // HP 채움바(가로 Filled)
                var fillGo = new GameObject("Fill", typeof(RectTransform));
                var frt = (RectTransform)fillGo.transform;
                frt.SetParent(rt, false);
                Stretch(frt);
                _fill = fillGo.AddComponent<Image>();
                _fill.type = Image.Type.Filled;
                _fill.fillMethod = Image.FillMethod.Horizontal;
                _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                _fill.color = AllyColor;

                // 라벨(닉네임 + 수치)
                var labelGo = new GameObject("Label", typeof(RectTransform));
                var lrt = (RectTransform)labelGo.transform;
                lrt.SetParent(rt, false);
                Stretch(lrt);
                lrt.offsetMin = new Vector2(8f, 0f);
                lrt.offsetMax = new Vector2(-8f, 0f);
                _label = labelGo.AddComponent<TextMeshProUGUI>();
                _label.font = TMP_Settings.defaultFontAsset;
                _label.fontSize = 16f;
                _label.alignment = TextAlignmentOptions.Left;
                _label.enableWordWrapping = false;
                _label.overflowMode = TextOverflowModes.Ellipsis;
                _label.color = Color.black;
            }

            public void Bind(PartyMemberInfo info)
            {
                _go.SetActive(true);
                float ratio = info.MaxHp > 0 ? Mathf.Clamp01((float)info.Hp / info.MaxHp) : 0f;
                bool dead = info.Hp <= 0;
                _fill.fillAmount = ratio;
                _fill.color = dead ? DeadColor : (info.IsLocal ? LocalColor : AllyColor);
                string name = info.IsLocal ? $"<b>{info.Nickname}</b>" : info.Nickname;
                _label.text = $"{name}  {info.Hp}/{info.MaxHp}";
            }

            public void Hide()
            {
                if (_go.activeSelf) _go.SetActive(false);
            }

            private static void Stretch(RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }
    }
}
