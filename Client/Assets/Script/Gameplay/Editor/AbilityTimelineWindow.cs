using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Abilities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Playables;   // W2 프리뷰: PlayableExtensions(SetTime/SetSourcePlayable) 확장 메서드
using UnityEngine.UIElements;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// 어빌리티 연출 타임라인 편집 창(CA-5 Phase 2, <b>UI Toolkit</b>). 한 <see cref="AbilityDefinition"/> 의 시간축을 보며
    /// 언제 SFX/VFX 를 낼지, 판정창(startup/active)이 언제 열리는지를 드래그로 편집한다.
    ///
    /// 두 갈래를 한 화면에서 편집하되 데이터는 갈라진다(gas-architecture §2.5):
    ///   · <b>판정창(게임플레이)</b> startup/active — 서버가 읽는다 → 편집 후 <b>Export(재bake)·서버 재빌드</b> 필요(창이 경고).
    ///   · <b>연출(SFX/VFX/Anim)</b> cueEvents — 클라 로컬, bake 안 됨 → 편집 즉시 유효.
    ///
    /// <b>이벤트 추가 = 트랙 빈 곳 우클릭</b>("뭘 쓸지 아직 모름" → id 는 미정으로 생성, 인스펙터에서 나중에 채운다).
    /// 편집은 SerializedObject 경유 → Undo·dirty 자동. 라이브 3D 프리뷰는 Phase 2.5 확장점.
    /// </summary>
    public sealed class AbilityTimelineWindow : EditorWindow
    {
        private const float LeftPad = 8f;
        private const float RulerH = 22f;
        private const float SectionsH = 16f; // W3: 룰러 아래 이름 구간 밴드 높이
        private const float RowH = 30f;
        private const float RowGap = 2f;
        private const float HeaderW = 108f; // 왼쪽 트랙 헤더 열 폭(이름 + ＋/×)

        private static readonly Color ColRuler = new(0.18f, 0.18f, 0.18f);
        private static readonly Color ColRowA = new(0.24f, 0.24f, 0.24f);
        private static readonly Color ColRowB = new(0.27f, 0.27f, 0.27f);
        private static readonly Color ColHitbox = new(0.90f, 0.55f, 0.15f);
        private static readonly Color ColVfx = new(0.25f, 0.70f, 0.90f);
        private static readonly Color ColSfx = new(0.35f, 0.80f, 0.45f);
        private static readonly Color ColAnim = new(0.70f, 0.50f, 0.90f);
        private static readonly Color ColScrub = new(0.95f, 0.30f, 0.30f);
        private static readonly Color ColSel = new(1f, 1f, 1f);

        private static readonly Color ColEvent = new(0.85f, 0.75f, 0.30f); // Event(메서드 호출) = 노란
        // W-B: 행은 (kind, lane) 조합으로 동적. kind 인덱스 = ECueKind 값(Sfx0/Vfx1/Anim2/Event3), 판정창=-1.
        private const int KHitbox = -1;


        [SerializeField] private AbilityDefinition _target;
        [SerializeField] private CueCatalog _catalog;      // (선택) id 폴백 소스
        [SerializeField] private GameObject _actorPrefab;  // P7: Event 메서드 드롭다운 소스(액터 프리팹)
        private SerializedObject _so;
        private float _pxPerMs = 0.6f;
        private float _scrubMs;
        private int _selected = -1;                     // primary(인스펙터·드래그·리사이즈 대상)
        private readonly HashSet<int> _selection = new(); // P8 다중 선택(그룹 delete/nudge/duplicate). primary 포함.
        private bool _hitboxSelected;                     // 판정창 바가 선택됨(이벤트 선택과 배타)
        private readonly global::System.Collections.Generic.HashSet<(int kind, int lane)> _mutedLanes = new(); // W6: 프리뷰 음소거 레인

        private VisualElement _content;   // 스크롤 내부, width = 타임라인 길이
        private VisualElement _headerColumn; // 왼쪽 고정 트랙 헤더 열(이름·＋/×)
        private VisualElement _scrub;
        private int _renamingSection = -1; // W3: 인라인 이름변경 중인 구간(-1=없음)
        [SerializeField] private int[] _laneCount = { 1, 1, 1, 1 };        // kind별 저작 레인 수(Sfx/Vfx/Anim/Event)
        private readonly global::System.Collections.Generic.List<(int kind, int lane)> _rows = new();
        private VisualElement[] _rowTracks = global::System.Array.Empty<VisualElement>();
        private VisualElement _inspectorBody;
        private ToolbarButton _previewButton;

        // P5 에디트모드 프리뷰
        private bool _previewing;
        private bool _previewNotify = true;
        private double _previewLastTime;
        private float _previewPrevMs;
        private readonly global::System.Collections.Generic.Dictionary<int, GameObject> _vfxInstances = new(); // W2b: cue 인덱스 → 프리뷰 씬 VFX 인스턴스

        // W2 라이브 메시 프리뷰 — 격리 PreviewScene 렌더 + previewClip 을 PlayableGraph 로 스크럽 샘플(휴머노이드/제네릭 공용, 전역 AnimationMode 부작용 없음)
        private VisualElement _viewport;
        private IMGUIContainer _viewportGui;
        private Button _viewFoldButton;
        private ObjectField _actorField;                // 오른쪽 프리뷰 패널의 Actor 필드(툴바에서 이동)
        private ObjectField _previewClipField;          // 오른쪽 프리뷰 패널의 Clip 필드(타겟별 재바인딩)
        [SerializeField] private bool _viewportOpen = true;
        private const string ActorPrefKey = "AbilityTimeline.ActorGuid"; // Actor 프리팹 GUID 영속(에디터 재시작에도 유지)
        private UnityEditor.PreviewRenderUtility _pru;
        private GameObject _actorInstance;              // 프리뷰 씬 인스턴스(HideAndDontSave, 게임 스크립트 제거)
        private GameObject _sampledActorPrefab;         // 현재 인스턴스의 원본(툴바 Actor 변경 감지)
        private float _lastSampledMs = float.NaN;
        private Vector2 _viewOrbit = new(130f, 12f);    // yaw, pitch(도)
        private float _viewDist;                        // 카메라 거리(오토프레임 후 휠로 조절)
        private Vector3 _viewPivot;                      // 액터 바운즈 중심
        private bool _viewFramed;                       // 오토프레임 1회 완료
        private UnityEngine.Playables.PlayableGraph _graph;
        private UnityEngine.Animations.AnimationClipPlayable _clipPlayable;
        private bool _graphValid;
        private AnimationClip _graphClip;
        private AnimationClip _autoClip;                                  // W2c: cueTrigger 자동 해석 결과 캐시
        private GameObject _autoClipActor;                                // 어느 액터로 해석했나(캐시 키)
        private Game.Gameplay.Character.AnimationTriggerType _autoClipTrigger; // 어느 트리거로(캐시 키)
        private RenderTexture _rt;                       // URP 렌더 타겟(빌트인 BeginPreview 경로는 URP 셰이더가 마젠타)
        private Light _previewLight;                     // 프리뷰 씬 직접 조명(PRU 기본 조명은 BeginPreview 경로 전용)

        [MenuItem("Tools/Ability/Ability Timeline")]
        private static void Open() => GetWindow<AbilityTimelineWindow>("Ability Timeline").minSize = new Vector2(760, 440);

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            LoadStyleSheet(root); // 디자인 폴리시(.uss)
            LoadActorFromPrefs(); // 저장된 Actor 프리팹 복원(요청 ②)

            BuildToolbar(root);

            // 본문 = [트랙 헤더(왼쪽 고정) | 클립 영역(가로 스크롤) | 상세 패널(오른쪽)]
            var body = new VisualElement { name = "atl-body" };
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;
            root.Add(body);

            // 트랙 헤더 열 = Unity Timeline/언리얼처럼 이름·＋/× 를 왼쪽 고정 열에 두어 스크롤·마커와 겹치지 않게.
            _headerColumn = new VisualElement { name = "atl-track-heads" };
            _headerColumn.style.width = HeaderW; _headerColumn.style.flexShrink = 0;
            _headerColumn.style.position = Position.Relative;
            _headerColumn.style.borderRightWidth = 1; _headerColumn.style.borderRightColor = new Color(0, 0, 0, 0.35f);
            body.Add(_headerColumn);

            var scroll = new ScrollView(ScrollViewMode.Horizontal) { name = "timeline-scroll" };
            scroll.style.flexGrow = 1f;
            _content = new VisualElement { name = "timeline-content" };
            _content.style.position = Position.Relative;
            scroll.Add(_content);
            body.Add(scroll);

            // 오른쪽 열 = [프리뷰 패널(위) | 인스펙터(아래)]. 타임라인과 배경색으로 구분(요청 ③).
            var rightColumn = new VisualElement { name = "atl-rightcol" };
            rightColumn.style.width = 340; rightColumn.style.flexShrink = 0;
            rightColumn.style.flexDirection = FlexDirection.Column;
            rightColumn.style.backgroundColor = new Color(0.135f, 0.15f, 0.18f); // 청록빛 다크 = 타임라인과 구분
            rightColumn.style.borderLeftWidth = 1; rightColumn.style.borderLeftColor = new Color(0, 0, 0, 0.4f);
            body.Add(rightColumn);

            BuildPreviewPanel(rightColumn); // W2: Actor+Clip 필드 + 라이브 프리뷰 뷰포트(요청 ①)

            var detailsScroll = new ScrollView(ScrollViewMode.Vertical) { name = "atl-details-scroll" };
            detailsScroll.style.flexGrow = 1;
            _inspectorBody = new VisualElement { name = "atl-details" };
            _inspectorBody.AddToClassList("atl-details");
            detailsScroll.Add(_inspectorBody);
            rightColumn.Add(detailsScroll);

            // P8 단축키: Delete=삭제 · ←/→=넛지 · Ctrl+D=복제 (선택 집합 전체)
            root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (_selection.Count == 0) return;
                float step = 0.1f; // 0.1ms 미세 넛지 (Snap 격자 제거)
                switch (e.keyCode)
                {
                    case KeyCode.Delete:      DeleteSelectedEvents(); e.StopPropagation(); break;
                    case KeyCode.LeftArrow:   NudgeSelected(-step);   e.StopPropagation(); break;
                    case KeyCode.RightArrow:  NudgeSelected(step);    e.StopPropagation(); break;
                    case KeyCode.D when e.ctrlKey || e.commandKey: DuplicateSelected(); e.StopPropagation(); break;
                }
            });

            if (_catalog == null) _catalog = FindSingleCatalog(); // 프로젝트에 CueCatalog 1개면 자동
            RebuildAll();
            RebindPreviewClip(); // 초기 타겟의 previewClip 을 프리뷰 패널 Clip 필드에 바인딩
        }

        /// <summary>프로젝트의 CueCatalog 가 정확히 1개면 반환(자동 지정). 여러 개/없음이면 null(수동 지정).</summary>
        private static CueCatalog FindSingleCatalog()
        {
            var guids = AssetDatabase.FindAssets("t:CueCatalog");
            return guids.Length == 1 ? AssetDatabase.LoadAssetAtPath<CueCatalog>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        /// <summary>디자인 폴리시 스타일시트(.uss) 로드. 없으면 인라인 스타일로 폴백(치명적 아님).</summary>
        private static void LoadStyleSheet(VisualElement root)
        {
            const string ussPath = "Assets/Script/Gameplay/Editor/AbilityTimelineWindow.uss";
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
        }

        // ── 상세 패널(W-A) 폴리시 헬퍼 ──
        private VisualElement Section(string title)
        {
            var sec = new VisualElement();
            sec.AddToClassList("atl-section");
            var t = new Label(title);
            t.AddToClassList("atl-section-title");
            sec.Add(t);
            _inspectorBody.Add(sec);
            return sec;
        }

        private static Label Hint(string text)
        {
            var l = new Label(text);
            l.AddToClassList("atl-hint");
            return l;
        }

        private static VisualElement RowBtns()
        {
            var v = new VisualElement();
            v.AddToClassList("atl-btn-row");
            return v;
        }

        // ─────────────────────── 툴바 ───────────────────────

        private void BuildToolbar(VisualElement root)
        {
            var bar = new Toolbar();

            var field = new ObjectField { objectType = typeof(AbilityDefinition), value = _target };
            field.style.width = 210;
            field.RegisterValueChangedCallback(e => SetTarget(e.newValue as AbilityDefinition));
            bar.Add(field);

            var menu = new ToolbarMenu { text = "Ability ▾" };
            menu.RegisterCallback<PointerDownEvent>(_ =>
            {
                menu.menu.MenuItems().Clear();
                foreach (var a in AssetDatabase.FindAssets("t:AbilityDefinition")
                             .Select(AssetDatabase.GUIDToAssetPath)
                             .Select(AssetDatabase.LoadAssetAtPath<AbilityDefinition>)
                             .Where(x => x != null).OrderBy(x => x.id))
                {
                    var captured = a;
                    menu.menu.AppendAction(a.id, _ => { field.value = captured; }, _ => DropdownMenuAction.Status.Normal);
                }
            });
            bar.Add(menu);

            bar.Add(new ToolbarSpacer());
            var zoom = new Slider(0.15f, 3f) { value = _pxPerMs };
            zoom.style.width = 110;
            zoom.RegisterValueChangedCallback(e => { _pxPerMs = e.newValue; RebuildTimeline(); });
            bar.Add(new Label("Zoom") { style = { unityTextAlign = TextAnchor.MiddleCenter } });
            bar.Add(zoom);
            // ※ Snap/FPS 격자는 제거됨 — 편집은 항상 0.1ms 단위(판정창 int 만 1ms).

            bar.Add(new ToolbarSpacer());
            _previewButton = new ToolbarButton(TogglePreview) { text = "▶ Preview" };
            bar.Add(_previewButton);
            var notify = new ToolbarToggle { text = "Notify", value = _previewNotify };
            notify.RegisterValueChangedCallback(e => _previewNotify = e.newValue);
            bar.Add(notify);

            // ※ Cue 카탈로그·Actor 필드는 오른쪽 프리뷰 패널로 이동(직접 리소스·프리뷰가 기본이므로).
            bar.Add(new ToolbarSpacer());
            var hint = new Label("트랙 빈 곳 우클릭=추가 · Ctrl+클릭=다중선택 · ←/→ 넛지 · Ctrl+D 복제 · Del 삭제");
            hint.style.marginLeft = 10;
            hint.style.unityTextAlign = TextAnchor.MiddleLeft;
            hint.style.color = new Color(0.7f, 0.7f, 0.7f);
            bar.Add(hint);

            root.Add(bar);
        }

        private void SetTarget(AbilityDefinition a)
        {
            if (_previewing) StopPreview();
            _target = a;
            _so = a != null ? new SerializedObject(a) : null;
            _selected = -1; _selection.Clear(); _hitboxSelected = false; _mutedLanes.Clear();
            _laneCount = new[] { 1, 1, 1, 1 }; // 어빌리티 바뀌면 빈 레인 초기화(사용 중 레인은 EffLanes 가 복원)
            _scrubMs = 0;
            RebuildAll();
            RebindPreviewClip();       // 프리뷰 패널 Clip 필드를 새 어빌리티에 재바인딩
            _viewportGui?.MarkDirtyRepaint();
        }

        // ─────────────────────── 좌표 ───────────────────────

        private float TotalMs => _target == null ? 400f
            : Mathf.Max(_target.startupMs + _target.activeMs + _target.recoveryMs, MaxEventMs() + 50, MaxSectionMs() + 50, 400);

        private float MaxEventMs()
        {
            float m = 0;
            if (_target != null)
                foreach (var e in _target.cueEvents) if (e != null) m = Mathf.Max(m, e.timeMs);
            return m;
        }

        private float MaxSectionMs() // W3: 구간이 타임라인 끝을 넘어도 룰러/밴드가 덮도록
        {
            float m = 0;
            if (_target != null)
                foreach (var s in _target.sections) if (s != null) m = Mathf.Max(m, s.endMs);
            return m;
        }

        private float XForTime(float ms) => LeftPad + ms * _pxPerMs;
        private float TimeForX(float x) => Mathf.Max(0f, (x - LeftPad) / _pxPerMs);
        // 편집 격자 = 항상 0.1ms(Snap/FPS 제거됨). 판정창 startup/active 는 int 계약이라 최종 1ms.
        private static float Snap(float ms) => Mathf.Round(ms * 10f) / 10f;
        private float RowTop(int row) => RulerH + SectionsH + row * (RowH + RowGap); // W3: 구간 밴드만큼 하향

        // ── W-B 레인 행 레이아웃 ──
        private int EffLanes(int kind)
        {
            int n = Mathf.Max(1, _laneCount[kind]);
            foreach (var e in _target.cueEvents)
                if (e != null && (int)e.kind == kind) n = Mathf.Max(n, Mathf.Max(0, e.lane) + 1);
            return n;
        }

        /// <summary>행 순서 = Anim 레인 · 판정창 · VFX 레인 · SFX 레인 · Event 레인.</summary>
        private void BuildRowLayout()
        {
            _rows.Clear();
            for (int l = 0; l < EffLanes(2); l++) _rows.Add((2, l)); // Anim
            _rows.Add((KHitbox, 0));                                 // 판정창(단일)
            for (int l = 0; l < EffLanes(1); l++) _rows.Add((1, l)); // VFX
            for (int l = 0; l < EffLanes(0); l++) _rows.Add((0, l)); // SFX
            for (int l = 0; l < EffLanes(3); l++) _rows.Add((3, l)); // Event
        }

        private int RowIndexOf(int kind, int lane)
        {
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].kind == kind && _rows[i].lane == lane) return i;
            return -1;
        }

        private static string KindName(int kind) => kind switch { 0 => "SFX", 1 => "VFX", 2 => "Anim", 3 => "Event", _ => "판정창" };
        private static string LaneMark(int lane) => lane < 9 ? ((char)('①' + lane)).ToString() : $"#{lane + 1}";

        // ─────────────────────── 재구성 ───────────────────────

        /// <summary>타임라인 + 인스펙터 전체 재구성. 구조 변경(추가·삭제·복제·레인·타겟 교체)에만 쓴다.
        /// 인스펙터 필드 <b>값</b> 편집에는 쓰지 않는다 — 편집 중 필드가 파괴돼 포커스를 잃기 때문(W1). 그 경우 <see cref="RebuildTimeline"/> 만.</summary>
        private void RebuildAll() { RebuildTimeline(); RefreshInspector(); }

        /// <summary>드래그(클립·그립·판정창 리사이즈/이동) 중엔 타임라인 재구성을 건너뛴다 — 재구성이 드래그 중인 그립을 파괴해 포인터 캡처를 끊기 때문(W1 회귀 수정).
        /// 선택된 이벤트의 인스펙터 bound 필드가 <see cref="RebuildTimeline"/> 대신 이걸 호출: TrackPropertyValue 가 드래그의 SO 쓰기에 반응해도 무시.
        /// 드래그 중엔 각 드래그 핸들러의 <c>layout()</c> 가 라이브 갱신을 담당하고, PointerUp 이 최종 재구성한다.</summary>
        private void RebuildTimelineUnlessDragging()
        {
            var panel = _content?.panel;
            if (panel != null && panel.GetCapturingElement(PointerId.mousePointerId) != null) return; // 포인터 캡처됨 = 드래그 중
            RebuildTimeline();
        }

        /// <summary>왼쪽 헤더 열 + 타임라인 캔버스(룰러·클립·판정창)만 재구성. 오른쪽 인스펙터(_inspectorBody)는 건드리지 않는다.
        /// 지오메트리 편집(시각·길이·레인·startup·active)·드래그·줌은 이것만 호출 → 바인딩된 인스펙터 필드가 살아남아 포커스 유지(W1).</summary>
        private void RebuildTimeline()
        {
            if (_content == null) return;
            _content.Clear();
            _headerColumn?.Clear();

            if (_target == null)
            {
                var msg = new Label("편집할 AbilityDefinition 을 지정하세요 (툴바의 필드 또는 Ability ▾).");
                msg.style.marginTop = 12; msg.style.marginLeft = 10;
                _content.Add(msg);
                _content.style.width = 400;
                _content.style.height = 60;
                return;
            }
            if (_so == null || _so.targetObject != _target) _so = new SerializedObject(_target);
            _so.Update();

            BuildRowLayout();
            _rowTracks = new VisualElement[_rows.Count];
            float contentW = XForTime(TotalMs) + 40f;
            float contentH = RulerH + SectionsH + _rows.Count * (RowH + RowGap) + 4f;
            _content.style.width = contentW;
            _content.style.height = contentH;
            if (_headerColumn != null) _headerColumn.style.height = contentH;

            BuildHeaderCorner();
            BuildRuler(contentW);
            BuildSectionsBand(contentW); // W3 이름 구간 밴드
            for (int r = 0; r < _rows.Count; r++) { BuildTrackHeader(r); BuildTrackLane(r, contentW); }

            BuildAnimAnchor();
            BuildHitboxBar();
            BuildEventClips();
            BuildScrub(contentH);
        }

        /// <summary>헤더 열 상단 코너(룰러 높이 맞춤).</summary>
        private void BuildHeaderCorner()
        {
            if (_headerColumn == null) return;
            var corner = new VisualElement();
            corner.style.position = Position.Absolute;
            corner.style.left = 0; corner.style.top = 0; corner.style.width = HeaderW; corner.style.height = RulerH + SectionsH; // W3: 룰러+구간밴드 커버
            corner.style.backgroundColor = ColRuler;
            var cornerLbl = new Label("구간") { style = { position = Position.Absolute, left = 5, top = RulerH, fontSize = 9, color = new Color(0.6f, 0.6f, 0.6f) } };
            corner.Add(cornerLbl);
            _headerColumn.Add(corner);
        }

        /// <summary>왼쪽 고정 트랙 헤더 행 — 이름 + (연출 트랙만) ＋레인추가·×빈레인삭제.</summary>
        private void BuildTrackHeader(int r)
        {
            if (_headerColumn == null) return;
            var (kind, lane) = _rows[r];
            var head = new VisualElement();
            head.style.position = Position.Absolute;
            head.style.left = 0; head.style.top = RowTop(r); head.style.width = HeaderW; head.style.height = RowH;
            head.style.backgroundColor = RowTint(kind, lane);
            head.style.flexDirection = FlexDirection.Row; head.style.alignItems = Align.Center;
            head.style.borderBottomWidth = 1; head.style.borderBottomColor = new Color(0, 0, 0, 0.28f); // 행 구분선
            _headerColumn.Add(head);

            // 왼쪽 컬러 액센트 바(종류 색) — 한눈에 트랙 종류 구분(둥근 알약)
            var accent = new VisualElement();
            accent.style.width = 4; accent.style.height = RowH - 10; accent.style.flexShrink = 0; accent.style.marginLeft = 3;
            accent.style.backgroundColor = KindColor(kind);
            accent.style.borderTopLeftRadius = accent.style.borderTopRightRadius = accent.style.borderBottomLeftRadius = accent.style.borderBottomRightRadius = 2;
            head.Add(accent);

            var name = new Label(kind == KHitbox ? "판정창" : $"{KindName(kind)} {LaneMark(lane)}");
            name.style.flexGrow = 1; name.style.marginLeft = 6; name.style.fontSize = 10;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = Lighten(KindColor(kind), 0.55f); // 종류색을 밝게 → 헤더에서 톤 통일
            head.Add(name);

            if (kind != KHitbox)
            {
                int capKind = kind, capLane = lane;
                bool muted = _mutedLanes.Contains((kind, lane));
                if (muted) name.style.color = new Color(0.5f, 0.5f, 0.5f); // W6: 음소거 레인 흐리게

                var mute = new Button(() =>
                {
                    if (!_mutedLanes.Remove((capKind, capLane))) _mutedLanes.Add((capKind, capLane));
                    RebuildAll();
                }) { text = "M", tooltip = "이 레인 음소거(▶Preview 에서 제외)" };
                mute.AddToClassList("atl-hdr-btn");
                mute.style.width = 20; mute.style.height = 18; mute.style.marginRight = 1; mute.style.paddingLeft = 0; mute.style.paddingRight = 0;
                if (muted) mute.style.color = new Color(0.95f, 0.5f, 0.5f);
                head.Add(mute);

                var add = new Button(() => AddLane(capKind)) { text = "＋", tooltip = $"{KindName(kind)} 레인 추가" };
                add.AddToClassList("atl-hdr-btn");
                add.style.width = 20; add.style.height = 18; add.style.marginRight = 1; add.style.paddingLeft = 0; add.style.paddingRight = 0;
                head.Add(add);
                var del = new Button(() => RemoveLane(capKind, capLane)) { text = "×", tooltip = "이 레인 삭제(빈 레인만)" };
                del.AddToClassList("atl-hdr-btn");
                del.style.width = 20; del.style.height = 18; del.style.marginRight = 4; del.style.paddingLeft = 0; del.style.paddingRight = 0;
                head.Add(del);
            }
        }

        private void BuildRuler(float w)
        {
            var ruler = new VisualElement();
            ruler.style.position = Position.Absolute;
            ruler.style.left = 0; ruler.style.top = 0;
            ruler.style.width = w; ruler.style.height = RulerH;
            ruler.style.backgroundColor = ColRuler;
            ruler.style.borderBottomWidth = 1; ruler.style.borderBottomColor = new Color(0, 0, 0, 0.35f);
            _content.Add(ruler);

            for (int ms = 0; ms <= TotalMs; ms += 50)
            {
                bool major = ms % 100 == 0;
                float x = XForTime(ms);
                var tick = new VisualElement();
                tick.style.position = Position.Absolute;
                tick.style.left = x; tick.style.top = major ? 0 : RulerH * 0.5f;
                tick.style.width = 1; tick.style.height = major ? RulerH : RulerH * 0.5f;
                tick.style.backgroundColor = new Color(1, 1, 1, major ? 0.32f : 0.13f);
                ruler.Add(tick);

                if (!major) continue;
                var lbl = new Label($"{ms}");
                lbl.style.position = Position.Absolute;
                lbl.style.left = x + 3; lbl.style.top = 3;
                lbl.style.fontSize = 9;
                lbl.style.color = new Color(0.9f, 0.9f, 0.93f);
                ruler.Add(lbl);
            }

            // 룰러 클릭 = 스크럽 이동
            ruler.RegisterCallback<PointerDownEvent>(e =>
            {
                _scrubMs = Snap(TimeForX(ruler.WorldToLocal(e.position).x));
                PositionScrub();
                e.StopPropagation();
            });
        }

        // ─────────────────────── W3 이름 구간(Sections) 밴드 ───────────────────────

        private static readonly Color[] SectionColors =
        {
            new(0.30f, 0.45f, 0.65f), new(0.55f, 0.40f, 0.62f), new(0.34f, 0.56f, 0.46f),
            new(0.66f, 0.50f, 0.30f), new(0.58f, 0.36f, 0.42f),
        };

        /// <summary>룰러 아래 이름 구간 밴드(Unreal Montage Sections). 빈 곳 우클릭=추가 · 구간 클릭=플레이헤드 점프 · 좌/우 그립=리사이즈 · 우클릭=이름/루프/삭제.</summary>
        private void BuildSectionsBand(float w)
        {
            var band = new VisualElement { name = "atl-sections" };
            band.style.position = Position.Absolute;
            band.style.left = 0; band.style.top = RulerH; band.style.width = w; band.style.height = SectionsH;
            band.style.backgroundColor = new Color(0.14f, 0.14f, 0.16f);
            _content.Add(band);
            band.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                float t = Snap(TimeForX(evt.localMousePosition.x));
                evt.menu.AppendAction($"구간 추가 ({Mathf.RoundToInt(t)}ms)", _ => AddSection(t));
            }));

            var sections = _target.sections;
            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                if (s == null) continue;
                int index = i;

                var segColor = SectionColors[i % SectionColors.Length];
                var seg = new VisualElement();
                seg.style.position = Position.Absolute; seg.style.top = 1; seg.style.height = SectionsH - 2;
                seg.style.backgroundColor = segColor;
                seg.style.borderTopLeftRadius = seg.style.borderTopRightRadius = seg.style.borderBottomLeftRadius = seg.style.borderBottomRightRadius = 3;
                seg.style.borderTopWidth = seg.style.borderBottomWidth = seg.style.borderLeftWidth = seg.style.borderRightWidth = 1;
                seg.style.borderTopColor = seg.style.borderBottomColor = seg.style.borderLeftColor = seg.style.borderRightColor = Lighten(segColor, 0.25f);
                seg.style.overflow = Overflow.Hidden;
                band.Add(seg);

                var lbl = new Label(); lbl.style.fontSize = 9; lbl.style.marginLeft = 4; lbl.style.color = Color.white; lbl.style.unityFontStyleAndWeight = FontStyle.Bold; lbl.pickingMode = PickingMode.Ignore;
                var gripL = MakeSectionGrip(band); var gripR = MakeSectionGrip(band);

                void Layout()
                {
                    var s2 = index < _target.sections.Count ? _target.sections[index] : null;
                    if (s2 == null) return;
                    float x0 = XForTime(s2.startMs), x1 = XForTime(Mathf.Max(s2.startMs, s2.endMs));
                    seg.style.left = x0; seg.style.width = Mathf.Max(12, x1 - x0);
                    gripL.style.left = x0 - 2; gripR.style.left = x1 - 3;
                    lbl.text = (s2.loop ? "⟲ " : "") + s2.name;
                    seg.tooltip = $"{s2.name} · {Mathf.RoundToInt(s2.startMs)}~{Mathf.RoundToInt(s2.endMs)}ms · 클릭=점프" + (s2.loop ? " · ⟲루프" : "");
                }
                Layout();

                // 인라인 이름변경 중이면 TextField, 아니면 라벨
                if (_renamingSection == index)
                {
                    var tf = new TextField { value = s.name }; tf.style.flexGrow = 1; tf.style.marginLeft = 2; tf.style.marginTop = -1;
                    tf.RegisterCallback<FocusOutEvent>(_ =>
                    {
                        _so.Update(); _so.FindProperty("sections").GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue = tf.value; _so.ApplyModifiedProperties();
                        _renamingSection = -1; RebuildTimeline();
                    });
                    tf.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Escape) tf.Blur(); });
                    seg.Add(tf);
                    tf.schedule.Execute(() => { tf.Focus(); tf.SelectAll(); }).ExecuteLater(1);
                }
                else seg.Add(lbl);

                // 본체 클릭 = 플레이헤드 그 구간 start 로 점프
                seg.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0 || _renamingSection == index) return;
                    var s2 = index < _target.sections.Count ? _target.sections[index] : null;
                    if (s2 != null) { _scrubMs = Snap(s2.startMs); PositionScrub(); }
                    e.StopPropagation();
                });

                WireSectionGrip(gripL, band, index, isStart: true, Layout);
                WireSectionGrip(gripR, band, index, isStart: false, Layout);

                seg.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("이름 변경", _ => { _renamingSection = index; RebuildTimeline(); });
                    evt.menu.AppendAction(s.loop ? "루프 해제" : "루프 설정", _ => ToggleSectionLoop(index));
                    evt.menu.AppendAction("삭제", _ => DeleteSection(index));
                }));
            }
        }

        private VisualElement MakeSectionGrip(VisualElement band)
        {
            var g = new VisualElement();
            g.style.position = Position.Absolute; g.style.top = 1; g.style.width = 5; g.style.height = SectionsH - 2;
            g.style.backgroundColor = new Color(0, 0, 0, 0.35f);
            band.Add(g);
            return g;
        }

        private void WireSectionGrip(VisualElement grip, VisualElement band, int index, bool isStart, global::System.Action layout)
        {
            grip.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) { grip.CapturePointer(e.pointerId); e.StopPropagation(); } });
            grip.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!grip.HasPointerCapture(e.pointerId)) return;
                float t = Snap(TimeForX(band.WorldToLocal(e.position).x));
                _so.Update();
                var el = _so.FindProperty("sections").GetArrayElementAtIndex(index);
                var sp = el.FindPropertyRelative("startMs"); var ep = el.FindPropertyRelative("endMs");
                if (isStart) sp.floatValue = Mathf.Clamp(t, 0f, ep.floatValue);
                else ep.floatValue = Mathf.Max(t, sp.floatValue);
                _so.ApplyModifiedProperties();
                layout(); // 리빌드 없이 즉시 재배치(캡처 유지)
            });
            grip.RegisterCallback<PointerUpEvent>(e => { if (grip.HasPointerCapture(e.pointerId)) { grip.ReleasePointer(e.pointerId); RebuildTimeline(); } });
        }

        private void AddSection(float startMs)
        {
            if (_so == null) return;
            var arr = _so.FindProperty("sections");
            int i = arr.arraySize; arr.arraySize++;
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("name").stringValue = $"Section {i + 1}";
            el.FindPropertyRelative("startMs").floatValue = startMs;
            el.FindPropertyRelative("endMs").floatValue = startMs + 200f;
            el.FindPropertyRelative("loop").boolValue = false;
            _so.ApplyModifiedProperties();
            RebuildTimeline();
        }

        private void DeleteSection(int index)
        {
            if (_so == null) return;
            var arr = _so.FindProperty("sections");
            if (index < 0 || index >= arr.arraySize) return;
            DeleteArrayItem(arr, index); // sections 는 List<class> — 1차 삭제가 null 로만 → 헬퍼가 실제 제거
            _so.ApplyModifiedProperties();
            if (_renamingSection == index) _renamingSection = -1;
            RebuildTimeline();
        }

        private void ToggleSectionLoop(int index)
        {
            if (_so == null) return;
            _so.Update();
            var lp = _so.FindProperty("sections").GetArrayElementAtIndex(index).FindPropertyRelative("loop");
            lp.boolValue = !lp.boolValue;
            _so.ApplyModifiedProperties();
            RebuildTimeline();
        }

        /// <summary>첫 유효 루프 구간(있으면 ▶Preview 가 그 [start,end) 를 반복). W3.</summary>
        private AbilitySection FindLoopSection()
        {
            if (_target == null) return null;
            foreach (var s in _target.sections) if (s != null && s.loop && s.endMs > s.startMs) return s;
            return null;
        }

        /// <summary>클립 레인(오른쪽 스크롤 영역) — 배경 + 우클릭 이벤트 추가만. 이름·＋/× 는 왼쪽 헤더 열이 담당.</summary>
        private void BuildTrackLane(int r, float w)
        {
            var (kind, lane) = _rows[r];
            var track = new VisualElement { name = "cue-track" };
            track.style.position = Position.Absolute;
            track.style.left = 0; track.style.top = RowTop(r);
            track.style.width = w; track.style.height = RowH;
            track.style.backgroundColor = RowTint(kind, lane); // 종류별 색조(헤더와 동일)
            if (kind != KHitbox && _mutedLanes.Contains((kind, lane))) track.style.opacity = 0.45f; // W6: 음소거 레인 흐리게(클립 포함)
            _content.Add(track);
            _rowTracks[r] = track;

            if (kind != KHitbox)
            {
                int capKind = kind, capLane = lane;
                track.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    float t = Snap(TimeForX(evt.localMousePosition.x));
                    evt.menu.AppendAction($"{KindName(capKind)} 이벤트 추가 ({Mathf.RoundToInt(t)}ms)", _ => AddEvent((ECueKind)capKind, t, capLane));
                }));
            }
        }

        private void BuildAnimAnchor()
        {
            // 주 애니(cueTrigger)는 t=0 앵커(편집은 없음 — AbilityDefinition 인스펙터에서). 지연 Anim 은 마커.
            var anchor = new VisualElement();
            anchor.style.position = Position.Absolute;
            anchor.style.left = XForTime(0); anchor.style.top = 6;
            anchor.style.width = 10; anchor.style.height = RowH - 12;
            anchor.style.backgroundColor = ColAnim;
            anchor.style.borderTopLeftRadius = anchor.style.borderTopRightRadius = anchor.style.borderBottomLeftRadius = anchor.style.borderBottomRightRadius = 3;
            anchor.style.borderTopWidth = anchor.style.borderBottomWidth = anchor.style.borderLeftWidth = anchor.style.borderRightWidth = 1;
            anchor.style.borderTopColor = anchor.style.borderBottomColor = anchor.style.borderLeftColor = anchor.style.borderRightColor = Darken(ColAnim, 0.42f);
            anchor.tooltip = $"주 애니(cueTrigger): {_target.cueTrigger} · t=0 발동 (편집=AbilityDefinition 인스펙터)";
            int animRow = RowIndexOf(2, 0); // Anim 첫 레인
            if (animRow < 0) return;
            _rowTracks[animRow].Add(anchor);
            AddGloss(anchor, ColAnim);

            var lbl = new Label($"▶ {_target.cueTrigger}");
            lbl.style.position = Position.Absolute;
            lbl.style.left = 16; lbl.style.top = 8; lbl.style.fontSize = 9;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = Lighten(ColAnim, 0.45f);
            _rowTracks[animRow].Add(lbl);
        }

        private void BuildHitboxBar()
        {
            int hbRow = RowIndexOf(KHitbox, 0);
            if (hbRow < 0) return;
            var track = _rowTracks[hbRow];

            var bar = new VisualElement();
            bar.style.position = Position.Absolute;
            bar.style.top = 6; bar.style.height = RowH - 12;
            bar.style.backgroundColor = new Color(ColHitbox.r, ColHitbox.g, ColHitbox.b, 0.9f);
            bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius = bar.style.borderBottomLeftRadius = bar.style.borderBottomRightRadius = 4;
            track.Add(bar);
            AddGloss(bar, ColHitbox);

            var lbl = new Label();
            lbl.style.position = Position.Absolute;
            lbl.style.left = 8; lbl.style.top = 2; lbl.style.fontSize = 9;
            lbl.pickingMode = PickingMode.Ignore; // 라벨이 바 드래그를 가로채지 않게
            bar.Add(lbl);

            var gripL = MakeGrip(track);
            var gripR = MakeGrip(track);

            // 리빌드 없이 즉시 재배치 — 드래그 중 포인터 캡처가 끊기지 않게(RebuildAll 은 놓을 때만).
            void Layout()
            {
                int s = _target.startupMs, a = _target.activeMs;
                float x0 = XForTime(s), x1 = XForTime(s + a);
                bar.style.left = x0; bar.style.width = Mathf.Max(8, x1 - x0);
                gripL.style.left = x0 - 3; gripR.style.left = x1 - 3;
                lbl.text = $"{s}~{s + a}ms (서버 bake)";
                string tip = $"판정창 {s}~{s + a}ms · 클릭=선택(오른쪽 편집) · 드래그=이동 · 그립=크기";
                bar.tooltip = gripL.tooltip = gripR.tooltip = tip;
                float bw = _hitboxSelected ? 2 : 1; // 선택 하이라이트(평상시 어두운 테두리)
                var bc = _hitboxSelected ? ColSel : Darken(ColHitbox, 0.42f);
                bar.style.borderTopWidth = bar.style.borderBottomWidth = bar.style.borderLeftWidth = bar.style.borderRightWidth = bw;
                bar.style.borderTopColor = bar.style.borderBottomColor = bar.style.borderLeftColor = bar.style.borderRightColor = bc;
            }
            Layout();

            // 본체: 클릭=선택(오른쪽 패널 편집) + 드래그=이동(startup 이동, active 길이 유지)
            float grab = 0;
            bar.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                SelectHitbox(); Layout(); // 즉시 하이라이트 + 오른쪽 패널에 판정창 편집 표시
                grab = TimeForX(track.WorldToLocal(e.position).x) - _target.startupMs;
                bar.CapturePointer(e.pointerId); e.StopPropagation();
            });
            bar.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!bar.HasPointerCapture(e.pointerId)) return;
                int ns = Mathf.Max(0, Mathf.RoundToInt(Snap(TimeForX(track.WorldToLocal(e.position).x) - grab)));
                _so.Update(); _so.FindProperty("startupMs").intValue = ns; _so.ApplyModifiedProperties();
                Layout();
            });
            bar.RegisterCallback<PointerUpEvent>(e => { if (bar.HasPointerCapture(e.pointerId)) { bar.ReleasePointer(e.pointerId); RebuildTimeline(); } }); // 드래그로 SO 갱신됨 → 타임라인만 재배치(바인딩된 인스펙터는 자동 반영)

            WireGrip(gripL, track, isStart: true, Layout);   // 좌 그립 = startup(끝 고정)
            WireGrip(gripR, track, isStart: false, Layout);  // 우 그립 = active(끝)
        }

        private VisualElement MakeGrip(VisualElement track)
        {
            var g = new VisualElement();
            g.style.position = Position.Absolute;
            g.style.top = 5; g.style.width = 6; g.style.height = RowH - 10;
            g.style.backgroundColor = new Color(0.5f, 0.28f, 0.05f); // 진한 주황 = "여기 잡고 크기 조절"
            track.Add(g);
            return g;
        }

        private void WireGrip(VisualElement grip, VisualElement track, bool isStart, global::System.Action layout)
        {
            grip.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) { SelectHitbox(); grip.CapturePointer(e.pointerId); e.StopPropagation(); } });
            grip.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!grip.HasPointerCapture(e.pointerId)) return;
                float t = Snap(TimeForX(track.WorldToLocal(e.position).x));
                _so.Update();
                if (isStart)
                {
                    int end = _target.startupMs + _target.activeMs;
                    int ns = Mathf.Clamp(Mathf.RoundToInt(t), 0, end - 1);
                    _so.FindProperty("startupMs").intValue = ns;
                    _so.FindProperty("activeMs").intValue = end - ns; // 끝 고정
                }
                else
                {
                    _so.FindProperty("activeMs").intValue = Mathf.Max(1, Mathf.RoundToInt(t) - _target.startupMs);
                }
                _so.ApplyModifiedProperties();
                layout(); // 리빌드 없이 즉시 재배치 → 부드러운 크기 조절
            });
            grip.RegisterCallback<PointerUpEvent>(e => { if (grip.HasPointerCapture(e.pointerId)) { grip.ReleasePointer(e.pointerId); RebuildTimeline(); } });
        }

        // 모든 이벤트(VFX/SFX/Anim)를 판정창처럼 **리사이즈 가능한 클립**으로 그린다(P6).
        // 본체 드래그=이동 · 우 그립=길이(durationMs) 늘리기 · 좌 그립=시작(끝 고정). duration 0 = 즉발(점처럼 얇게).
        private void BuildEventClips()
        {
            var events = _target.cueEvents;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev == null) continue;
                int row = RowIndexOf((int)ev.kind, Mathf.Max(0, ev.lane));
                if (row < 0) continue; // 레이아웃 밖(방어)
                var track = _rowTracks[row];
                int index = i;
                Color col = ColorFor(ev.kind);

                var clip = new VisualElement { name = "cue-clip" };
                clip.AddToClassList("atl-marker");
                clip.style.position = Position.Absolute; clip.style.top = 6; clip.style.height = RowH - 12;
                clip.style.backgroundColor = col;
                track.Add(clip);
                AddGloss(clip, col); // 상단 광택으로 입체감

                var lbl = new Label();
                lbl.style.position = Position.Absolute; lbl.style.left = 10; lbl.style.top = 1; lbl.style.fontSize = 9;
                lbl.pickingMode = PickingMode.Ignore; // 라벨이 클립 드래그를 가로채지 않게
                track.Add(lbl);

                var gripL = MakeGrip(track); var gripR = MakeGrip(track);
                gripL.style.backgroundColor = gripR.style.backgroundColor = new Color(0, 0, 0, 0.4f);

                void Layout()
                {
                    var e2 = index < _target.cueEvents.Count ? _target.cueEvents[index] : null;
                    if (e2 == null) return;
                    float x0 = XForTime(e2.timeMs);
                    // W5: 즉발(길이 0)=둥근 점 · 지속(길이>0)=바 로 시각 구분.
                    bool isPoint = e2.durationMs <= 0.5f;
                    float w = isPoint ? 14f : Mathf.Max(20, e2.durationMs * _pxPerMs);
                    clip.style.left = x0; clip.style.width = w;
                    float rad = isPoint ? (RowH - 12) / 2f : 3f;
                    clip.style.borderTopLeftRadius = clip.style.borderTopRightRadius = rad;
                    clip.style.borderBottomLeftRadius = clip.style.borderBottomRightRadius = rad;
                    gripL.style.left = x0 - 3; gripR.style.left = x0 + w - 3;
                    lbl.style.left = x0 + w + 3;
                    lbl.text = string.IsNullOrEmpty(e2.id) ? "(id 미정)" : e2.id;
                    clip.tooltip = $"{e2.kind} · {(string.IsNullOrEmpty(e2.id) ? "(id 미정)" : e2.id)} · {Mathf.RoundToInt(e2.timeMs)}~{Mathf.RoundToInt(e2.timeMs + e2.durationMs)}ms"
                                 + (e2.kind == ECueKind.Vfx && !string.IsNullOrEmpty(e2.socket) ? $" @ {e2.socket}" : "");
                    bool selc = _selection.Contains(index); // P8: 다중 선택 전부 하이라이트
                    float bw = selc ? 2 : 1;
                    var bc = selc ? ColSel : Darken(col, 0.42f); // 평상시엔 어두운 테두리로 입체, 선택 시 흰색
                    clip.style.borderTopWidth = clip.style.borderBottomWidth = clip.style.borderLeftWidth = clip.style.borderRightWidth = bw;
                    clip.style.borderTopColor = clip.style.borderBottomColor = clip.style.borderLeftColor = clip.style.borderRightColor = bc;
                }
                Layout();

                // 본체 = 선택 + 이동(시각 이동, 길이 유지)
                float grab = 0;
                clip.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    if (e.ctrlKey || e.commandKey) ToggleSelect(index); else SelectSingle(index); // P8: Ctrl+클릭=다중
                    Layout(); RefreshInspector(); // Layout()=이 클립 즉시 하이라이트(나머지는 PointerUp RebuildAll)
                    grab = TimeForX(track.WorldToLocal(e.position).x) - _target.cueEvents[index].timeMs;
                    clip.CapturePointer(e.pointerId); e.StopPropagation();
                });
                clip.RegisterCallback<PointerMoveEvent>(e =>
                {
                    if (!clip.HasPointerCapture(e.pointerId)) return;
                    float nt = Mathf.Max(0, Snap(TimeForX(track.WorldToLocal(e.position).x) - grab));
                    SetEventFloat(index, "timeMs", nt);
                    Layout(); // 리빌드 없이 즉시 재배치(캡처 유지)
                });
                clip.RegisterCallback<PointerUpEvent>(e => { if (clip.HasPointerCapture(e.pointerId)) { clip.ReleasePointer(e.pointerId); RebuildTimeline(); } });

                WireEventGrip(gripR, track, index, isEnd: true, Layout);   // 우 = 길이(끝)
                WireEventGrip(gripL, track, index, isEnd: false, Layout);  // 좌 = 시작(끝 고정)

                clip.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("복제", _ => { if (!_selection.Contains(index)) SelectSingle(index); DuplicateSelected(); });
                    evt.menu.AppendAction("삭제", _ => { if (!_selection.Contains(index)) SelectSingle(index); DeleteSelectedEvents(); });
                }));
            }
        }

        private void WireEventGrip(VisualElement grip, VisualElement track, int index, bool isEnd, global::System.Action layout)
        {
            grip.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                SelectSingle(index); layout(); RefreshInspector(); // 리사이즈는 단일 선택
                grip.CapturePointer(e.pointerId); e.StopPropagation();
            });
            grip.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!grip.HasPointerCapture(e.pointerId)) return;
                var ev = index < _target.cueEvents.Count ? _target.cueEvents[index] : null;
                if (ev == null) return;
                float t = Snap(TimeForX(track.WorldToLocal(e.position).x));
                if (isEnd)
                {
                    SetEventFloat(index, "durationMs", Mathf.Max(0, t - ev.timeMs));
                }
                else
                {
                    float end = ev.timeMs + ev.durationMs;
                    float ns = Mathf.Clamp(t, 0, end);
                    _so.Update();
                    var el = _so.FindProperty("cueEvents").GetArrayElementAtIndex(index);
                    el.FindPropertyRelative("timeMs").floatValue = ns;
                    el.FindPropertyRelative("durationMs").floatValue = end - ns;
                    _so.ApplyModifiedProperties();
                }
                layout();
            });
            grip.RegisterCallback<PointerUpEvent>(e => { if (grip.HasPointerCapture(e.pointerId)) { grip.ReleasePointer(e.pointerId); RebuildTimeline(); } });
        }

        private void SetEventFloat(int index, string field, float value)
        {
            _so.Update();
            _so.FindProperty("cueEvents").GetArrayElementAtIndex(index).FindPropertyRelative(field).floatValue = value;
            _so.ApplyModifiedProperties();
        }

        private void BuildScrub(float h)
        {
            _scrub = new VisualElement();
            _scrub.style.position = Position.Absolute;
            _scrub.style.top = 0; _scrub.style.width = 2; _scrub.style.height = h;
            _scrub.style.backgroundColor = ColScrub;
            _content.Add(_scrub);

            var head = new VisualElement();
            head.style.position = Position.Absolute;
            head.style.left = -5; head.style.top = 0; head.style.width = 12; head.style.height = 12;
            head.style.backgroundColor = ColScrub;
            head.style.borderTopLeftRadius = head.style.borderTopRightRadius = head.style.borderBottomLeftRadius = head.style.borderBottomRightRadius = 3;
            head.style.borderTopWidth = head.style.borderBottomWidth = head.style.borderLeftWidth = head.style.borderRightWidth = 1;
            head.style.borderTopColor = head.style.borderBottomColor = head.style.borderLeftColor = head.style.borderRightColor = Darken(ColScrub, 0.3f);
            _scrub.Add(head);

            head.RegisterCallback<PointerDownEvent>(e => { head.CapturePointer(e.pointerId); e.StopPropagation(); });
            head.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!head.HasPointerCapture(e.pointerId)) return;
                _scrubMs = Snap(TimeForX(_content.WorldToLocal(e.position).x));
                PositionScrub();
            });
            head.RegisterCallback<PointerUpEvent>(e => { if (head.HasPointerCapture(e.pointerId)) head.ReleasePointer(e.pointerId); });

            PositionScrub();
        }

        private void PositionScrub()
        {
            if (_scrub != null) _scrub.style.left = XForTime(_scrubMs);
            _viewportGui?.MarkDirtyRepaint(); // W2: 스크럽 이동 → 뷰포트 재샘플·재렌더
        }

        // ─────────────────────── 인스펙터 ───────────────────────

        private void RefreshInspector()
        {
            if (_inspectorBody == null) return;
            _inspectorBody.Clear();

            if (_target == null)
            {
                _inspectorBody.Add(new Label("타겟 없음."));
                return;
            }
            if (_so == null || _so.targetObject != _target) _so = new SerializedObject(_target);
            _so.Update(); // 독립 호출(argType/actor/catalog 변경) 시 커밋된 최신값으로 레이아웃 결정

            // ── 판정창 선택 시: 그 편집을 맨 위로(클릭 피드백) ──
            if (_hitboxSelected)
            {
                BuildHitboxSection(true);
                _inspectorBody.Add(Hint("이벤트를 클릭하면 그 이벤트를 여기서 편집합니다."));
                return;
            }

            // ── 선택 이벤트 (W-A: 어느 kind 든 오른쪽에서 전 필드 편집) ──
            if (ValidSel())
            {
                var el = _so.FindProperty("cueEvents").GetArrayElementAtIndex(_selected);
                var kind = (ECueKind)el.FindPropertyRelative("kind").enumValueIndex;

                var sec = Section($"{kind} 이벤트 · [{_selected}]");

                // 종류 = 레이아웃 교체(Sfx클립↔Vfx프리팹) → 바인딩 대신 수동(변경 시 인스펙터·타임라인 전부 재구성).
                var kindField = new EnumField("종류", kind); kindField.AddToClassList("atl-field");
                var kindProp = el.FindPropertyRelative("kind");
                kindField.RegisterValueChangedCallback(e => { _so.Update(); kindProp.enumValueIndex = (int)(ECueKind)e.newValue; _so.ApplyModifiedProperties(); RebuildAll(); });
                sec.Add(kindField);

                sec.Add(BoundField(new FloatField("시각(ms)"), el.FindPropertyRelative("timeMs"), RebuildTimelineUnlessDragging));
                sec.Add(BoundField(new FloatField("길이(ms)"), el.FindPropertyRelative("durationMs"), RebuildTimelineUnlessDragging));
                sec.Add(BoundInt2(new IntegerField("레인") { tooltip = "같은 종류 안의 행(0=첫 레인). 트랙 헤더 ＋/× 로도 레인 관리." }, el.FindPropertyRelative("lane"), RebuildTimelineUnlessDragging));

                if (kind == ECueKind.Sfx)
                    sec.Add(BoundObject(new ObjectField("SFX 클립") { objectType = typeof(AudioClip) }, el.FindPropertyRelative("sfxClip")));
                else if (kind == ECueKind.Vfx)
                {
                    sec.Add(BoundObject(new ObjectField("VFX 프리팹") { objectType = typeof(GameObject) }, el.FindPropertyRelative("vfxPrefab")));
                    sec.Add(BoundText(new TextField("소켓"), el.FindPropertyRelative("socket")));
                }
                else if (kind == ECueKind.Event)
                    BuildEventInspector(el, sec); // 메서드 드롭다운 + 타입 인자
                else // Anim
                {
                    var tp = el.FindPropertyRelative("animTrigger");
                    sec.Add(BoundField(new EnumField("애니 트리거", (Game.Gameplay.Character.AnimationTriggerType)tp.enumValueIndex), tp, null));
                    sec.Add(Hint("지연 애니 트리거(주 트리거 cueTrigger 는 t=0). 액터 Animator 로 발화(W7)."));
                }

                int selCount = _selection.Count;
                var actions = RowBtns();
                actions.Add(new Button(DuplicateSelected) { text = selCount > 1 ? $"복제 ({selCount})" : "복제", style = { flexGrow = 1 } });
                var delBtn = new Button(DeleteSelectedEvents) { text = selCount > 1 ? $"삭제 ({selCount})" : "삭제", style = { flexGrow = 1 } };
                delBtn.AddToClassList("atl-danger");
                actions.Add(delBtn);
                sec.Add(actions);
                if (selCount > 1) sec.Add(Hint($"다중 {selCount}개 — Ctrl+클릭 토글 · ←/→ 넛지 · Ctrl+D 복제 · Del 삭제"));

                // 고급(선택) — Cue 카탈로그(툴바에서 이동) + 폴백 id
                if (kind == ECueKind.Sfx || kind == ECueKind.Vfx)
                {
                    var adv = Section("고급 (선택) — 직접 리소스 없을 때만");
                    adv.Add(CueCatalogField());
                    adv.Add(BoundText(new TextField("카탈로그 id") { tooltip = "직접 리소스가 없을 때만 폴백 조회. 보통 비워둔다." }, el.FindPropertyRelative("id")));
                }
            }
            else
            {
                _inspectorBody.Add(Hint("이벤트를 클릭해 선택 · 트랙 빈 곳 우클릭=추가."));
            }

            // ── 판정창 (어빌리티 레벨 · 서버 bake · 이벤트 미선택 시에도 참조용으로 항상 표시) ──
            BuildHitboxSection(false);
        }

        /// <summary>판정창(startup/active·Export·→Event) 섹션. selected=true 면 "선택됨" 강조.</summary>
        private void BuildHitboxSection(bool selected)
        {
            var gp = Section(selected ? "판정창 (선택됨) · 서버 bake" : "판정창 (서버 bake)");
            gp.Add(BoundInt2(new IntegerField("startup(ms)"), _so.FindProperty("startupMs"), RebuildTimelineUnlessDragging));
            gp.Add(BoundInt2(new IntegerField("active(ms)"), _so.FindProperty("activeMs"), RebuildTimelineUnlessDragging));
            var gpBtns = RowBtns();
            var exportBtn = new Button(AbilityCatalogExporter.Export) { text = "Export", tooltip = "판정창 변경을 서버 abilities.json 에 재bake.", style = { flexGrow = 1 } };
            exportBtn.AddToClassList("atl-accent");
            gpBtns.Add(exportBtn);
            gpBtns.Add(new Button(GenerateHitWindowEvents) { text = "→ Event", tooltip = "판정창을 Event 2개(ActivateWindow@시작·DeactivateWindow@끝)로 생성 — Main WeaponHitbox 개폐(옛 Phase 3).", style = { flexGrow = 1 } });
            gp.Add(gpBtns);
            gp.Add(Hint("startup/active 는 서버가 읽는 값 → Export 후 서버 재빌드. SFX/VFX 는 bake 없이 즉시."));
        }

        private static VisualElement Horizontal()
        {
            var v = new VisualElement();
            v.style.flexDirection = FlexDirection.Row;
            return v;
        }

        // 세로 상세 패널용 — .atl-field 로 라벨 폭·간격을 .uss 가 통제(W-A). BindProperty 로 SerializedProperty 양방향 바인딩(자동 동기화·Undo, W1).
        // onChanged 는 지오메트리(시각·길이·레인·startup·active)에만 넘긴다 → TrackPropertyValue 가 값이 실제로 바뀐 뒤(=SO 갱신 후) 호출 → 타임라인만 리빌드(인스펙터 필드는 살려 포커스 유지).
        // 종류(kind)·인자타입(argType)처럼 인스펙터 레이아웃 자체를 바꾸는 enum 은 여기서 바인딩하지 않는다(리빌드로 즉시 파괴되므로 수동 콜백이 맞다).
        private VisualElement BoundField(EnumField f, SerializedProperty p, global::System.Action onChanged)
        {
            f.AddToClassList("atl-field");
            f.BindProperty(p);
            if (onChanged != null) f.TrackPropertyValue(p, _ => onChanged());
            return f;
        }

        private VisualElement BoundField(FloatField f, SerializedProperty p, global::System.Action onChanged)
        {
            f.AddToClassList("atl-field");
            f.isDelayed = true; // Enter/blur 커밋 — 타이핑 중 리빌드로 커서가 튀지 않게
            f.BindProperty(p);
            if (onChanged != null) f.TrackPropertyValue(p, _ => onChanged());
            return f;
        }

        private VisualElement BoundText(TextField f, SerializedProperty p)
        {
            f.AddToClassList("atl-field");
            f.BindProperty(p);
            return f;
        }

        private VisualElement BoundObject(ObjectField f, SerializedProperty p)
        {
            f.AddToClassList("atl-field");
            f.BindProperty(p); // sfxClip/vfxPrefab 은 클립 위치·라벨에 영향 없음 → 리빌드 불필요
            return f;
        }

        private VisualElement BoundInt2(IntegerField f, SerializedProperty p, global::System.Action onChanged = null)
        {
            f.AddToClassList("atl-field");
            f.isDelayed = true;
            f.BindProperty(p);
            if (onChanged != null) f.TrackPropertyValue(p, _ => onChanged());
            return f;
        }

        private VisualElement BoundToggle(Toggle f, SerializedProperty p)
        {
            f.AddToClassList("atl-field");
            f.BindProperty(p);
            return f;
        }

        /// <summary>window-level CueCatalog 폴백 필드(고급 섹션). 변경 시 인스펙터 갱신.</summary>
        private VisualElement CueCatalogField()
        {
            var f = new ObjectField("Cue 카탈로그") { objectType = typeof(CueCatalog), value = _catalog,
                tooltip = "직접 리소스 대신 id 로 조회할 때만. 보통 비워둔다." };
            f.AddToClassList("atl-field");
            f.RegisterValueChangedCallback(e => { _catalog = e.newValue as CueCatalog; RefreshInspector(); });
            return f;
        }

        // ─────────────────────── P7 Event 인스펙터(메서드 + 타입 인자) ───────────────────────

        private void BuildEventInspector(SerializedProperty el, VisualElement sec)
        {
            var imProp = el.FindPropertyRelative("invokeMethod");
            var mrow = new VisualElement(); mrow.AddToClassList("atl-method-row");
            var methodField = new TextField("메서드")
            { tooltip = "액터 컴포넌트의 public 메서드(예: WeaponHitbox.ActivateWindow → ActivateWindow). ▾ 로 목록 선택." };
            methodField.AddToClassList("atl-field"); methodField.style.flexGrow = 1;
            methodField.isDelayed = true;
            methodField.BindProperty(imProp); // 메서드명은 타임라인 라벨에 안 쓰임 → 리빌드 없이 바인딩만(W1)
            mrow.Add(methodField);

            var mpick = new Button(() => ShowMethodMenu(el)) { text = "▾" };
            mpick.style.width = 22;
            mpick.SetEnabled(_actorPrefab != null);
            mpick.tooltip = _actorPrefab == null ? "툴바 Actor 에 프리팹을 지정하면 호출 가능 메서드 목록이 뜹니다." : "Actor 의 호출 가능 메서드(void, 0/1 인자)";
            mrow.Add(mpick);
            sec.Add(mrow);

            // 타입 인자(참조 R5): None/Float/Int/Bool/String.
            var atProp = el.FindPropertyRelative("argType");
            var argType = (EInvokeArgType)atProp.enumValueIndex;
            var at = new EnumField("인자", argType); at.AddToClassList("atl-field");
            at.RegisterValueChangedCallback(e => { _so.Update(); atProp.enumValueIndex = (int)(EInvokeArgType)e.newValue; _so.ApplyModifiedProperties(); RefreshInspector(); });
            sec.Add(at);
            switch (argType)
            {
                case EInvokeArgType.Float:  sec.Add(BoundField(new FloatField("값"), el.FindPropertyRelative("argFloat"), null)); break;
                case EInvokeArgType.Int:    sec.Add(BoundInt2(new IntegerField("값"), el.FindPropertyRelative("argInt"))); break;
                case EInvokeArgType.Bool:   sec.Add(BoundToggle(new Toggle("값"), el.FindPropertyRelative("argBool"))); break;
                case EInvokeArgType.String: sec.Add(BoundText(new TextField("값"), el.FindPropertyRelative("argString"))); break;
            }
        }

        /// <summary>Actor 프리팹의 컴포넌트에서 호출 가능(void · 0/1 지원-타입 인자) public 메서드를 나열(참조 R4). 선택 시 메서드+인자타입 세팅.</summary>
        private void ShowMethodMenu(SerializedProperty el)
        {
            if (_actorPrefab == null) return;
            var menu = new GenericMenu();
            var seen = new HashSet<string>();
            foreach (var c in _actorPrefab.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                foreach (var m in c.GetType().GetMethods(global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public))
                {
                    if (m.ReturnType != typeof(void) || m.IsSpecialName || m.IsGenericMethod) continue;
                    var ps = m.GetParameters();
                    if (ps.Length > 1) continue;

                    var at = EInvokeArgType.None;
                    if (ps.Length == 1)
                    {
                        var pt = ps[0].ParameterType;
                        if (pt == typeof(float)) at = EInvokeArgType.Float;
                        else if (pt == typeof(int)) at = EInvokeArgType.Int;
                        else if (pt == typeof(bool)) at = EInvokeArgType.Bool;
                        else if (pt == typeof(string)) at = EInvokeArgType.String;
                        else continue; // 지원 안 하는 인자 타입 제외
                    }
                    string label = $"{c.GetType().Name}/{m.Name}({(at == EInvokeArgType.None ? "" : at.ToString())})";
                    if (!seen.Add(label)) continue;
                    string mName = m.Name; var mArg = at;
                    menu.AddItem(new GUIContent(label), false, () =>
                    {
                        _so.Update();
                        el.FindPropertyRelative("invokeMethod").stringValue = mName;
                        el.FindPropertyRelative("argType").enumValueIndex = (int)mArg;
                        _so.ApplyModifiedProperties();
                        RebuildAll(); // = RebuildTimeline + RefreshInspector(새 인자타입 필드 반영)
                    });
                }
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("호출 가능한 void 0/1-인자 메서드가 없습니다"));
            menu.ShowAsContext();
        }


        // ─────────────────────── 편집 ───────────────────────

        private bool ValidSel() => _target != null && _selected >= 0 && _selected < _target.cueEvents.Count;

        private void AddEvent(ECueKind kind, float timeMs, int lane = 0)
        {
            if (_so == null) return;
            var arr = _so.FindProperty("cueEvents");
            int i = arr.arraySize;
            arr.arraySize++;
            var e = arr.GetArrayElementAtIndex(i);
            ResetEvent(e, kind, timeMs); // 새 요소는 이전 값을 상속 → 전 필드 초기화
            e.FindPropertyRelative("lane").intValue = lane; // W-B: 추가한 레인
            _so.ApplyModifiedProperties();
            SelectSingle(i);
            RebuildAll();
        }

        private void AddInvokeEvent(float timeMs, string method)
        {
            var arr = _so.FindProperty("cueEvents");
            int i = arr.arraySize;
            arr.arraySize++;
            var e = arr.GetArrayElementAtIndex(i);
            ResetEvent(e, ECueKind.Event, timeMs);
            e.FindPropertyRelative("invokeMethod").stringValue = method;
            _so.ApplyModifiedProperties();
        }

        /// <summary>새 cueEvents 요소의 모든 필드를 초기화 — Unity 배열 증가가 이전 요소 값을 복사하는 함정 차단.</summary>
        private static void ResetEvent(SerializedProperty e, ECueKind kind, float timeMs)
        {
            e.FindPropertyRelative("timeMs").floatValue = timeMs;
            e.FindPropertyRelative("durationMs").floatValue = 0f;
            e.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            e.FindPropertyRelative("lane").intValue = 0;
            e.FindPropertyRelative("sfxClip").objectReferenceValue = null;
            e.FindPropertyRelative("vfxPrefab").objectReferenceValue = null;
            e.FindPropertyRelative("id").stringValue = "";
            e.FindPropertyRelative("socket").stringValue = "";
            e.FindPropertyRelative("animTrigger").enumValueIndex = 0; // None
            e.FindPropertyRelative("invokeMethod").stringValue = "";
            e.FindPropertyRelative("argType").enumValueIndex = (int)EInvokeArgType.None;
            e.FindPropertyRelative("argString").stringValue = "";
        }

        /// <summary>판정창(startup/active)을 Event 2개로 굽는다 — Main WeaponHitbox 를 타임라인이 개폐(옛 Phase 3).
        /// 이후 애니이벤트 제거·Actor 프리팹의 WeaponHitbox 존재는 사용자 배선.</summary>
        private void GenerateHitWindowEvents()
        {
            if (_so == null) return;
            int s = _target.startupMs, end = _target.startupMs + _target.activeMs;
            AddInvokeEvent(s, "ActivateWindow");
            AddInvokeEvent(end, "DeactivateWindow");
            RebuildAll();
        }

        private void DeleteEvent(int index)
        {
            if (_so == null || index < 0 || index >= _target.cueEvents.Count) return;
            DeleteArrayItem(_so.FindProperty("cueEvents"), index);
            _so.ApplyModifiedProperties();
            _selected = -1; _selection.Clear();
            RebuildAll();
        }

        /// <summary>관리 참조 배열(List&lt;class&gt;)은 1차 삭제가 요소를 null 로만 만들고 크기가 안 준다 → 한 번 더 지워 실제 제거.</summary>
        private static void DeleteArrayItem(SerializedProperty arr, int index)
        {
            int before = arr.arraySize;
            arr.DeleteArrayElementAtIndex(index);
            if (arr.arraySize == before) arr.DeleteArrayElementAtIndex(index);
        }

        // ─────────────────────── P8 선택 집합 + 그룹 연산 ───────────────────────

        private void SelectSingle(int i) { _hitboxSelected = false; _selection.Clear(); _selection.Add(i); _selected = i; }

        private void ToggleSelect(int i)
        {
            _hitboxSelected = false;
            if (!_selection.Remove(i)) { _selection.Add(i); _selected = i; }
            else if (_selected == i) _selected = _selection.Count > 0 ? _selection.First() : -1;
        }

        /// <summary>판정창 바 선택(이벤트 선택과 배타) → 오른쪽 패널이 판정창 편집을 위로 올린다.</summary>
        private void SelectHitbox() { _hitboxSelected = true; _selected = -1; _selection.Clear(); RefreshInspector(); }

        /// <summary>선택 집합 전체 삭제(내림차순 — 인덱스 밀림 방지).</summary>
        private void DeleteSelectedEvents()
        {
            if (_so == null || _selection.Count == 0) return;
            var arr = _so.FindProperty("cueEvents");
            foreach (var idx in _selection.OrderByDescending(x => x))
                if (idx >= 0 && idx < arr.arraySize) DeleteArrayItem(arr, idx);
            _so.ApplyModifiedProperties();
            _selected = -1; _selection.Clear();
            RebuildAll();
        }

        /// <summary>선택 집합 전체 시각 이동(화살표 넛지). 0 미만으로는 안 내려간다.</summary>
        private void NudgeSelected(float deltaMs)
        {
            if (_so == null || _selection.Count == 0) return;
            var arr = _so.FindProperty("cueEvents");
            foreach (var idx in _selection)
            {
                if (idx < 0 || idx >= arr.arraySize) continue;
                var p = arr.GetArrayElementAtIndex(idx).FindPropertyRelative("timeMs");
                p.floatValue = Mathf.Max(0f, p.floatValue + deltaMs);
            }
            _so.ApplyModifiedProperties();
            RebuildAll();
        }

        /// <summary>선택 집합 전체 복제(끝에 추가, 살짝 오프셋으로 겹침 방지). 복제본들이 새 선택이 된다.</summary>
        private void DuplicateSelected()
        {
            if (_so == null || _selection.Count == 0) return;
            var arr = _so.FindProperty("cueEvents");
            var sources = _selection.Where(i => i >= 0 && i < arr.arraySize).OrderBy(i => i).ToList();
            _selection.Clear();
            float offset = 20f; // 복제본 겹침 방지 오프셋
            foreach (var src in sources)
            {
                int n = arr.arraySize;
                arr.arraySize++;
                CopyEvent(arr.GetArrayElementAtIndex(src), arr.GetArrayElementAtIndex(n));
                var tp = arr.GetArrayElementAtIndex(n).FindPropertyRelative("timeMs");
                tp.floatValue += offset;
                _selection.Add(n);
            }
            _so.ApplyModifiedProperties();
            _selected = _selection.Count > 0 ? _selection.Last() : -1;
            RebuildAll();
        }

        private static void CopyEvent(SerializedProperty s, SerializedProperty d)
        {
            string[] fields = { "timeMs", "durationMs", "kind", "lane", "sfxClip", "vfxPrefab", "id", "socket", "animTrigger", "invokeMethod", "argType", "argFloat", "argInt", "argBool", "argString" };
            foreach (var fn in fields)
            {
                var sp = s.FindPropertyRelative(fn);
                var dp = d.FindPropertyRelative(fn);
                switch (sp.propertyType)
                {
                    case SerializedPropertyType.Float: dp.floatValue = sp.floatValue; break;
                    case SerializedPropertyType.Integer: dp.intValue = sp.intValue; break;
                    case SerializedPropertyType.Boolean: dp.boolValue = sp.boolValue; break;
                    case SerializedPropertyType.String: dp.stringValue = sp.stringValue; break;
                    case SerializedPropertyType.Enum: dp.enumValueIndex = sp.enumValueIndex; break;
                    case SerializedPropertyType.ObjectReference: dp.objectReferenceValue = sp.objectReferenceValue; break;
                }
            }
        }

        // ─────────────────────── P5 프리뷰(에디트모드) ───────────────────────

        private void OnDisable() { StopPreview(); CleanupViewport(); } // 창 닫힘/도메인 리로드 시 update 해제 + 스폰/뷰포트 정리

        private void TogglePreview()
        {
            if (_previewing) StopPreview();
            else StartPreview();
        }

        private void StartPreview()
        {
            if (_target == null) return;
            _previewing = true;
            _scrubMs = 0f; _previewPrevMs = -1f;
            _previewLastTime = EditorApplication.timeSinceStartup;
            if (_previewButton != null) _previewButton.text = "■ Stop";
            EditorApplication.update += PreviewTick;
        }

        private void StopPreview()
        {
            if (_previewing) EditorApplication.update -= PreviewTick;
            _previewing = false;
            if (_previewButton != null) _previewButton.text = "▶ Preview";
            // VFX 는 SampleVfx 가 현재 스크럽 시각 기준으로 관리 → 정지해도 그 프레임 상태 유지(뷰포트가 계속 그 시각을 보여줌). 정리는 CleanupViewport.
        }

        /// <summary>실시간으로 스크럽을 굴리며, 이 프레임에 지나간 이벤트를 발화한다(참조 R7 = TriggerInEditMode).</summary>
        private void PreviewTick()
        {
            if (!_previewing || _target == null) { StopPreview(); return; }

            double now = EditorApplication.timeSinceStartup;
            _scrubMs += (float)((now - _previewLastTime) * 1000.0);
            _previewLastTime = now;

            // W3: 루프 구간이 있으면 그 [start,end) 안에서 반복(scrubMs 랩 + 이벤트 재발화)
            var loopSec = FindLoopSection();
            if (loopSec != null && _scrubMs >= loopSec.endMs)
            {
                _scrubMs = loopSec.startMs;
                _previewPrevMs = loopSec.startMs - 1f;
            }

            foreach (var ev in _target.cueEvents)
                if (ev != null && ev.timeMs > _previewPrevMs && ev.timeMs <= _scrubMs)
                    PreviewFire(ev);
            _previewPrevMs = _scrubMs;

            PositionScrub();
            Repaint();

            if (loopSec == null && _scrubMs >= TotalMs) StopPreview(); // 루프 없을 때만 끝에서 정지
        }

        private void PreviewFire(AbilityCueEvent ev)
        {
            if (_mutedLanes.Contains(((int)ev.kind, Mathf.Max(0, ev.lane)))) return; // W6: 음소거 레인은 프리뷰 제외

            if (_previewNotify)
                Debug.Log($"[TimelinePreview] {Mathf.RoundToInt(ev.timeMs)}ms · {ev.kind} · '{(string.IsNullOrEmpty(ev.id) ? "(id 미정)" : ev.id)}'");

            // SFX 만 실시간 발화(스크럽마다 소리 스팸 방지). VFX 는 SampleVfx(스크럽·재생 공용, 뷰포트 스폰)가 담당 — W2b.
            if (ev.kind == ECueKind.Sfx)
            {
                var clip = ev.sfxClip != null ? ev.sfxClip
                         : (_catalog != null && _catalog.TryGetSfx(ev.id, out var s) ? s.clip : null);
                if (clip != null) PlayPreviewClip(clip);
            }
        }

        /// <summary>에디터에서 오디오 클립 프리뷰 재생(내부 UnityEditor.AudioUtil, 버전차 대비 리플렉션). 실패는 무해.</summary>
        private static void PlayPreviewClip(AudioClip clip)
        {
            if (clip == null) return;
            try
            {
                var util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
                var m = util?.GetMethod("PlayPreviewClip", new[] { typeof(AudioClip), typeof(int), typeof(bool) });
                m?.Invoke(null, new object[] { clip, 0, false });
            }
            catch { /* 프리뷰 오디오 실패는 무해 — Notify 로그로 충분 */ }
        }

        // ─────────────────────── W2b VFX 뷰포트 스폰(스크럽 동조) ───────────────────────

        private const float DefaultVfxLifeMs = 1500f; // 길이 0(즉발) VFX 의 기본 프리뷰 노출 시간

        /// <summary>스크럽/재생 시각 ms 에 "살아있어야 할" Vfx 큐를 프리뷰 씬 소켓에 확보하고 ParticleSystem 을 그 시각으로 시뮬(앞뒤 동조). 창 밖 인스턴스는 제거.
        /// 애니(SampleActor)와 나란히 <see cref="DrawViewport"/> 에서 호출 → 실시간 재생·수동 스크럽 공용.</summary>
        private void SampleVfx(float ms)
        {
            if (_actorInstance == null || _target == null) { ClearVfx(); return; }
            var events = _target.cueEvents;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                GameObject prefab = null; float autoLifeMs = 0f;
                if (ev != null && ev.kind == ECueKind.Vfx && !_mutedLanes.Contains(((int)ev.kind, Mathf.Max(0, ev.lane))))
                {
                    prefab = ev.vfxPrefab;
                    if (prefab == null && _catalog != null && _catalog.TryGetVfx(ev.id, out var v)) { prefab = v.prefab; autoLifeMs = v.autoDestroySec * 1000f; }
                }
                float life = ev != null && ev.durationMs > 0f ? ev.durationMs : (autoLifeMs > 0f ? autoLifeMs : DefaultVfxLifeMs);
                bool show = prefab != null && ms >= ev.timeMs && ms < ev.timeMs + life;

                _vfxInstances.TryGetValue(i, out var inst);
                if (show)
                {
                    if (inst == null) { inst = InstantiateVfxPreview(prefab, ev.socket); _vfxInstances[i] = inst; }
                    SimulateParticles(inst, (ms - ev.timeMs) / 1000f);
                }
                else if (inst != null) { UnityEngine.Object.DestroyImmediate(inst); _vfxInstances[i] = null; }
            }
        }

        private GameObject InstantiateVfxPreview(GameObject prefab, string socket)
        {
            var at = ResolveActorSocket(socket);
            var go = UnityEngine.Object.Instantiate(prefab, at.position, at.rotation, at);
            go.hideFlags = HideFlags.HideAndDontSave;
            return go;
        }

        /// <summary>소켓 이름을 액터 인스턴스에서 조회(런타임 AbilityCuePlayer 와 동일 규칙). 빈 이름/미발견이면 루트.</summary>
        private Transform ResolveActorSocket(string socket)
        {
            if (_actorInstance == null) return null;
            if (!string.IsNullOrEmpty(socket))
                foreach (var t in _actorInstance.GetComponentsInChildren<Transform>(true))
                    if (t.name == socket) return t;
            return _actorInstance.transform;
        }

        /// <summary>에디트모드는 파티클을 자동 재생 안 함 → 각 ParticleSystem 을 t 초로 명시 시뮬(restart). 스크럽 앞뒤 이동에 대응.</summary>
        private static void SimulateParticles(GameObject inst, float t)
        {
            if (inst == null) return;
            t = Mathf.Max(0f, t);
            foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>())
                ps.Simulate(t, false, true);
        }

        private void ClearVfx()
        {
            foreach (var kv in _vfxInstances) if (kv.Value != null) UnityEngine.Object.DestroyImmediate(kv.Value);
            _vfxInstances.Clear();
        }

        // ─────────────────────── W2 라이브 메시 프리뷰 뷰포트 ───────────────────────

        /// <summary>오른쪽 열 상단 프리뷰 패널 — Actor(영속) + Clip(타겟별) 필드 + 라이브 뷰포트(IMGUIContainer→URP 렌더). 접힘 토글.</summary>
        private void BuildPreviewPanel(VisualElement parent)
        {
            _viewport = new VisualElement { name = "atl-viewport" };
            _viewport.style.flexShrink = 0;
            _viewport.style.backgroundColor = new Color(0.11f, 0.12f, 0.15f);
            _viewport.style.borderBottomWidth = 1; _viewport.style.borderBottomColor = new Color(0, 0, 0, 0.45f);
            _viewport.style.paddingTop = 3; _viewport.style.paddingLeft = 4; _viewport.style.paddingRight = 4; _viewport.style.paddingBottom = 4;

            var head = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _viewFoldButton = new Button(ToggleViewport) { text = _viewportOpen ? "▾" : "▸", tooltip = "프리뷰 뷰포트 접기/펼치기" };
            _viewFoldButton.style.width = 20; _viewFoldButton.style.height = 18;
            head.Add(_viewFoldButton);
            head.Add(new Label("라이브 프리뷰") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 4, unityFontStyleAndWeight = FontStyle.Bold } });
            _viewport.Add(head);

            _actorField = new ObjectField("Actor") { objectType = typeof(GameObject), value = _actorPrefab,
                tooltip = "프리뷰·Event 메서드 소스. EditorPrefs 에 저장돼 다음에도 유지(요청 ②)." };
            _actorField.AddToClassList("atl-field");
            _actorField.RegisterValueChangedCallback(e =>
            {
                _actorPrefab = e.newValue as GameObject; SaveActorToPrefs();
                RefreshInspector(); _viewportGui?.MarkDirtyRepaint(); // Actor 변경 → 인스턴스 재생성(DrawViewport)
            });
            _viewport.Add(_actorField);

            _previewClipField = new ObjectField("클립") { objectType = typeof(AnimationClip),
                tooltip = "cueTrigger 가 재생하는 클립. 어빌리티 에셋에 저장돼 어빌리티별 유지." };
            _previewClipField.AddToClassList("atl-field");
            _previewClipField.RegisterValueChangedCallback(_ => { _graphValid = false; _lastSampledMs = float.NaN; _viewportGui?.MarkDirtyRepaint(); });
            _viewport.Add(_previewClipField);

            _viewportGui = new IMGUIContainer(DrawViewport);
            _viewportGui.style.height = 240;
            _viewportGui.style.marginTop = 3;
            _viewportGui.style.display = _viewportOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _viewport.Add(_viewportGui);

            var tip = new Label("드래그=회전 · 휠=줌 · ▶Preview/스크럽에 애니 동조") { style = { unityTextAlign = TextAnchor.MiddleLeft, color = new Color(0.55f, 0.55f, 0.55f), fontSize = 10 } };
            _viewport.Add(tip);

            parent.Add(_viewport);
        }

        private void ToggleViewport()
        {
            _viewportOpen = !_viewportOpen;
            if (_viewportGui != null) _viewportGui.style.display = _viewportOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_viewFoldButton != null) _viewFoldButton.text = _viewportOpen ? "▾" : "▸";
            Repaint();
        }

        /// <summary>프리뷰 패널 Clip 필드를 현재 어빌리티의 previewClip 에 바인딩(타겟 교체 시 재호출). 타겟 없으면 비활성.</summary>
        private void RebindPreviewClip()
        {
            if (_previewClipField == null) return;
            if (_so != null) _previewClipField.BindProperty(_so.FindProperty("previewClip"));
            else _previewClipField.SetValueWithoutNotify(null);
            _previewClipField.SetEnabled(_so != null);
        }

        /// <summary>저장된 Actor 프리팹 GUID → 로드(요청 ②, 에디터 재시작에도 유지). 이미 있으면(도메인 리로드 생존) 스킵.</summary>
        private void LoadActorFromPrefs()
        {
            if (_actorPrefab != null) return;
            var guid = EditorPrefs.GetString(ActorPrefKey, "");
            if (string.IsNullOrEmpty(guid)) return;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path)) _actorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private void SaveActorToPrefs()
        {
            if (_actorPrefab == null) { EditorPrefs.DeleteKey(ActorPrefKey); return; }
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_actorPrefab));
            if (!string.IsNullOrEmpty(guid)) EditorPrefs.SetString(ActorPrefKey, guid);
        }

        private void DrawViewport()
        {
            var r = _viewportGui.contentRect;
            if (r.width < 8 || r.height < 8) return;

            if (_actorPrefab == null)
            {
                GUI.Label(r, "툴바 Actor 에 프리팹을 지정하면 메시가 표시됩니다.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            HandleViewportInput(r);
            EnsurePreviewScene();
            if (_actorInstance == null) return;

            if (_lastSampledMs != _scrubMs) { SampleActor(_scrubMs); SampleVfx(_scrubMs); _lastSampledMs = _scrubMs; }

            var tex = RenderActor(r);
            if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, false);

            var lbl = $"{Mathf.RoundToInt(_scrubMs)} ms";
            if (_graphClip != null) lbl += "  · " + _graphClip.name + (_target != null && _target.previewClip == null ? " (자동)" : "");
            else lbl += "  (클립 없음 — 바인드 포즈)";
            GUI.Label(new Rect(r.x + 6, r.y + 3, r.width - 10, 16), lbl, EditorStyles.whiteMiniLabel);
        }

        private void HandleViewportInput(Rect r)
        {
            var e = Event.current;
            if (e == null) return;
            if (e.type == EventType.MouseDrag && e.button == 0 && r.Contains(e.mousePosition))
            {
                _viewOrbit.x += e.delta.x * 0.5f;
                _viewOrbit.y = Mathf.Clamp(_viewOrbit.y + e.delta.y * 0.5f, -85f, 85f);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && r.Contains(e.mousePosition))
            {
                _viewDist = Mathf.Clamp(_viewDist * (1f + e.delta.y * 0.05f), 0.4f, 100f);
                e.Use();
            }
        }

        /// <summary>PreviewRenderUtility 준비 + 툴바 Actor 가 바뀌었으면 인스턴스 재생성.</summary>
        private void EnsurePreviewScene()
        {
            if (_pru == null) _pru = new UnityEditor.PreviewRenderUtility();
            if (_actorInstance == null || _sampledActorPrefab != _actorPrefab) RecreateActor();
        }

        private void RecreateActor()
        {
            ClearVfx(); // 옛 액터에 붙은 VFX 인스턴스 정리(액터와 함께 파괴되나 맵을 비워 재조정)
            DestroyGraph();
            if (_actorInstance != null) { UnityEngine.Object.DestroyImmediate(_actorInstance); _actorInstance = null; }
            _sampledActorPrefab = _actorPrefab;
            _viewFramed = false;
            _lastSampledMs = float.NaN;
            if (_actorPrefab == null) return;

            // 비활성 홀더 아래로 Instantiate → 게임 스크립트 Awake 가 돌기 전에 전부 제거(네트워크·DI 없이 순수 렌더/애니만).
            var holder = new GameObject("ATL-holder") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);
            _actorInstance = UnityEngine.Object.Instantiate(_actorPrefab, holder.transform);
            StripRuntimeBehaviours(_actorInstance);
            _actorInstance.transform.SetParent(null, false);
            _actorInstance.hideFlags = HideFlags.HideAndDontSave;
            _actorInstance.SetActive(true);
            UnityEngine.Object.DestroyImmediate(holder);

            var animator = _actorInstance.GetComponentInChildren<Animator>();
            if (animator != null) { animator.enabled = true; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }

            _pru.AddSingleGO(_actorInstance);

            // URP SubmitRenderRequest 경로는 PRU 기본 조명을 안 쓴다 → 프리뷰 씬에 직접 디렉셔널 라이트 추가.
            if (_previewLight == null)
            {
                var lgo = new GameObject("ATL-preview-light") { hideFlags = HideFlags.HideAndDontSave };
                _previewLight = lgo.AddComponent<Light>();
                _previewLight.type = LightType.Directional;
                _previewLight.intensity = 1.2f;
                _pru.AddSingleGO(lgo);
            }
        }

        /// <summary>프리뷰 인스턴스의 게임 스크립트(MonoBehaviour) 전부 제거 — 순수 렌더/애니만 남긴다.
        /// <b>[RequireComponent] 의존을 존중해</b> "다른 살아있는 컴포넌트가 요구하지 않는 것"부터 제거(요구하는 쪽 먼저) → "제거 불가" 에러 방지.</summary>
        private static void StripRuntimeBehaviours(GameObject root)
        {
            var list = new global::System.Collections.Generic.List<MonoBehaviour>(root.GetComponentsInChildren<MonoBehaviour>(true));
            int guard = list.Count * list.Count + 8;
            while (list.Count > 0 && guard-- > 0)
            {
                bool removed = false;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var c = list[i];
                    if (c == null) { list.RemoveAt(i); continue; }
                    if (RequiredBySibling(c, list)) continue; // 아직 형제가 요구 → 나중 패스에서
                    UnityEngine.Object.DestroyImmediate(c);
                    list.RemoveAt(i); removed = true;
                }
                if (!removed) break; // 순환/불가(비정상) — 남은 건 그대로 둔다
            }
        }

        /// <summary>같은 GameObject 의 다른 살아있는 MonoBehaviour 가 <c>[RequireComponent]</c> 로 target 타입을 요구하는가.</summary>
        private static bool RequiredBySibling(MonoBehaviour target, global::System.Collections.Generic.List<MonoBehaviour> remaining)
        {
            var t = target.GetType();
            var go = target.gameObject;
            foreach (var other in remaining)
            {
                if (other == null || ReferenceEquals(other, target) || other.gameObject != go) continue;
                foreach (var a in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    var rc = (RequireComponent)a;
                    if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(t)) ||
                        (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(t)) ||
                        (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(t))) return true;
                }
            }
            return false;
        }

        /// <summary>previewClip 을 PlayableGraph 로 액터 Animator 에 스크럽 샘플(휴머노이드/제네릭 공용). 클립/인스턴스 바뀌면 그래프 재생성.</summary>
        private void SampleActor(float ms)
        {
            EnsureGraph();
            if (!_graphValid) return;
            float len = _graphClip.length;
            float t = len > 0f ? Mathf.Clamp(ms / 1000f, 0f, len) : 0f;
            _clipPlayable.SetTime(t);
            _clipPlayable.SetTime(t); // 2회 = 프레임 보간 잔상 제거(Timeline 관용)
            _graph.Evaluate();
        }

        private void EnsureGraph()
        {
            var clip = ResolvePreviewClip();
            var animator = _actorInstance != null ? _actorInstance.GetComponentInChildren<Animator>() : null;
            if (clip == null || animator == null) { DestroyGraph(); return; }
            if (_graphValid && _graphClip == clip) return;

            DestroyGraph();
            _graph = UnityEngine.Playables.PlayableGraph.Create("ATL-Preview");
            _graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
            var output = UnityEngine.Animations.AnimationPlayableOutput.Create(_graph, "out", animator);
            _clipPlayable = UnityEngine.Animations.AnimationClipPlayable.Create(_graph, clip);
            _clipPlayable.SetApplyFootIK(false);
            output.SetSourcePlayable(_clipPlayable);
            _graphClip = clip; _graphValid = true;
        }

        private void DestroyGraph()
        {
            if (_graphValid && _graph.IsValid()) _graph.Destroy();
            _graphValid = false; _graphClip = null;
        }

        // ─────────────────────── W2c cueTrigger 자동 클립 해석 ───────────────────────

        /// <summary>샘플할 클립 = previewClip(수동 지정 우선) → 없으면 cueTrigger 로 액터 AnimatorController 에서 자동 해석(캐시). AC-5 W2c.</summary>
        private AnimationClip ResolvePreviewClip()
        {
            if (_target == null) return null;
            if (_target.previewClip != null) return _target.previewClip; // 수동 지정 우선
            if (_autoClipActor == _actorPrefab && _autoClipTrigger == _target.cueTrigger) return _autoClip; // 캐시(액터·트리거 동일)
            _autoClip = AutoResolveClipFromTrigger(_actorPrefab, _target.cueTrigger);
            _autoClipActor = _actorPrefab; _autoClipTrigger = _target.cueTrigger;
            return _autoClip;
        }

        /// <summary>cueTrigger(enum) → CharacterAgentAnimations 의 파라미터 이름 → AnimatorController 에서 그 트리거를 조건으로 쓰는 전이의 목적 State 의 모션(Clip/BlendTree 첫 클립). 못 찾으면 null(바인드 포즈).</summary>
        private static AnimationClip AutoResolveClipFromTrigger(GameObject actorPrefab, Game.Gameplay.Character.AnimationTriggerType trigger)
        {
            if (actorPrefab == null || trigger == Game.Gameplay.Character.AnimationTriggerType.None) return null;
            var ca = actorPrefab.GetComponentInChildren<Game.Gameplay.Character.CharacterAgentAnimations>(true);
            var animator = actorPrefab.GetComponentInChildren<Animator>(true);
            if (ca == null || animator == null) return null;

            string param = TriggerParamName(ca, trigger);
            if (string.IsNullOrEmpty(param)) return null;

            var rac = animator.runtimeAnimatorController;
            var ovr = rac as AnimatorOverrideController;
            var ac = rac as UnityEditor.Animations.AnimatorController
                     ?? (ovr != null ? ovr.runtimeAnimatorController as UnityEditor.Animations.AnimatorController : null);
            if (ac == null) return null;

            var state = FindStateByTriggerParam(ac, param);
            if (state == null) return null;
            var clip = state.motion as AnimationClip;
            if (clip == null && state.motion is UnityEditor.Animations.BlendTree bt) clip = FirstClipInBlendTree(bt);
            if (clip != null && ovr != null) { var o = ovr[clip]; if (o != null) clip = o; } // 오버라이드 컨트롤러 매핑
            return clip;
        }

        /// <summary>enum → CharacterAgentAnimations 직렬화 필드(Animator 파라미터 이름). 컨트롤러마다 파라미터명이 달라도 이 매핑이 흡수(codemap §2.64).</summary>
        private static string TriggerParamName(Game.Gameplay.Character.CharacterAgentAnimations ca, Game.Gameplay.Character.AnimationTriggerType trigger)
        {
            string field = trigger == Game.Gameplay.Character.AnimationTriggerType.Attack ? "m_animationAttackTrigger"
                         : trigger == Game.Gameplay.Character.AnimationTriggerType.Jump ? "m_animationJumpTrigger"
                         : trigger == Game.Gameplay.Character.AnimationTriggerType.Fall ? "m_animationFallTrigger"
                         : trigger == Game.Gameplay.Character.AnimationTriggerType.Land ? "m_animationLandTrigger"
                         : trigger == Game.Gameplay.Character.AnimationTriggerType.Interact ? "m_animationInteractTrigger"
                         : trigger == Game.Gameplay.Character.AnimationTriggerType.Dead ? "m_animationDeathTrigger"
                         : trigger == Game.Gameplay.Character.AnimationTriggerType.Dodge ? "m_animationDodgeTrigger"
                         : trigger == Game.Gameplay.Character.AnimationTriggerType.Revive ? "m_animationReviveTrigger"
                         : null;
            if (field == null) return null;
            var sp = new SerializedObject(ca).FindProperty(field);
            return sp != null ? sp.stringValue : null;
        }

        private static UnityEditor.Animations.AnimatorState FindStateByTriggerParam(UnityEditor.Animations.AnimatorController ac, string param)
        {
            foreach (var layer in ac.layers)
            {
                var s = SearchStateMachine(layer.stateMachine, param);
                if (s != null) return s;
            }
            return null;
        }

        private static UnityEditor.Animations.AnimatorState SearchStateMachine(UnityEditor.Animations.AnimatorStateMachine sm, string param)
        {
            foreach (var t in sm.anyStateTransitions) if (UsesParam(t, param) && t.destinationState != null) return t.destinationState;
            foreach (var cs in sm.states)
                foreach (var t in cs.state.transitions) if (UsesParam(t, param) && t.destinationState != null) return t.destinationState;
            foreach (var t in sm.entryTransitions) if (UsesParam(t, param) && t.destinationState != null) return t.destinationState;
            foreach (var ssm in sm.stateMachines)
            {
                var s = SearchStateMachine(ssm.stateMachine, param);
                if (s != null) return s;
            }
            return null;
        }

        private static bool UsesParam(UnityEditor.Animations.AnimatorTransitionBase t, string param)
        {
            foreach (var c in t.conditions) if (c.parameter == param) return true;
            return false;
        }

        private static AnimationClip FirstClipInBlendTree(UnityEditor.Animations.BlendTree bt)
        {
            foreach (var child in bt.children)
            {
                if (child.motion is AnimationClip c) return c;
                if (child.motion is UnityEditor.Animations.BlendTree sub) { var r = FirstClipInBlendTree(sub); if (r != null) return r; }
            }
            return null;
        }

        private void PositionCamera()
        {
            if (!_viewFramed) FrameActor();
            var cam = _pru.camera;
            var rot = Quaternion.Euler(_viewOrbit.y, _viewOrbit.x, 0f);
            cam.transform.position = _viewPivot - (rot * Vector3.forward) * _viewDist;
            cam.transform.rotation = rot;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = Mathf.Max(50f, _viewDist * 6f);
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.13f, 0.14f);
            if (_previewLight != null) _previewLight.transform.rotation = Quaternion.Euler(40f, 40f, 0f);
        }

        /// <summary>프리뷰 카메라를 <b>URP 파이프라인</b>으로 RT 에 렌더한다. 빌트인 경로(BeginPreview/Render)는 URP 셰이더가 마젠타로 나오므로
        /// <c>RenderPipeline.SubmitRenderRequest</c> 를 쓴다. 미지원(빌트인 프로젝트)이면 예전 경로로 폴백.</summary>
        private Texture RenderActor(Rect r)
        {
            int w = Mathf.Max(8, (int)r.width), h = Mathf.Max(8, (int)r.height);
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) _rt.Release();
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };
            }
            var cam = _pru.camera;
            cam.aspect = (float)w / h;
            PositionCamera();

            var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = _rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req))
            {
                cam.targetTexture = _rt;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
                cam.targetTexture = null;
                return _rt;
            }
            // 빌트인 폴백(URP 아닌 프로젝트)
            _pru.BeginPreview(r, GUIStyle.none);
            _pru.Render();
            return _pru.EndPreview();
        }

        private void FrameActor()
        {
            var b = ComputeBounds(_actorInstance);
            _viewPivot = b.center;
            _viewDist = Mathf.Max(1.5f, b.size.magnitude * 1.1f);
            _viewFramed = true;
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position + Vector3.up, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private void CleanupViewport()
        {
            ClearVfx();
            DestroyGraph();
            if (_actorInstance != null) { UnityEngine.Object.DestroyImmediate(_actorInstance); _actorInstance = null; }
            _previewLight = null; // PRU.Cleanup 이 프리뷰 씬과 함께 파괴 → 참조만 해제
            _sampledActorPrefab = null; _viewFramed = false; _lastSampledMs = float.NaN;
            if (_rt != null) { _rt.Release(); _rt = null; }
            if (_pru != null) { _pru.Cleanup(); _pru = null; }
        }

        private static Color ColorFor(ECueKind k) => k switch { ECueKind.Vfx => ColVfx, ECueKind.Sfx => ColSfx, ECueKind.Event => ColEvent, _ => ColAnim };

        private static Color Lighten(Color c, float t) => Color.Lerp(c, Color.white, t); // 광택 하이라이트용
        private static Color Darken(Color c, float t) => Color.Lerp(c, Color.black, t);  // 테두리·그림자용

        /// <summary>클립·바에 얹는 상단 광택 스트립(위쪽 밝은 하이라이트). 밋밋한 단색을 입체감 있게.</summary>
        private static void AddGloss(VisualElement el, Color baseColor)
        {
            var gloss = new VisualElement { pickingMode = PickingMode.Ignore };
            gloss.style.position = Position.Absolute;
            gloss.style.left = 0; gloss.style.right = 0; gloss.style.top = 0; gloss.style.height = Length.Percent(45);
            gloss.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
            gloss.style.borderTopLeftRadius = gloss.style.borderTopRightRadius = 3;
            el.Add(gloss);
        }

        /// <summary>kind 별 밝은 대표색(마커·헤더 액센트). 판정창=주황.</summary>
        private static Color KindColor(int kind) => kind switch { 0 => ColSfx, 1 => ColVfx, 2 => ColAnim, 3 => ColEvent, _ => ColHitbox };

        /// <summary>트랙 행 배경 = kind 색조를 입힌 어두운 톤(종류 구분). 같은 kind 라도 레인 짝/홀로 살짝 명암 차.</summary>
        private static Color RowTint(int kind, int lane)
        {
            var c = Color.Lerp(new Color(0.19f, 0.19f, 0.19f), KindColor(kind), 0.22f);
            if (lane % 2 == 1) c = new Color(Mathf.Min(1f, c.r * 1.15f), Mathf.Min(1f, c.g * 1.15f), Mathf.Min(1f, c.b * 1.15f));
            return c;
        }

        // ── W-B 레인 추가/삭제 ──
        private void AddLane(int kind)
        {
            _laneCount[kind] = EffLanes(kind) + 1; // 맨 아래 빈 레인 하나 추가
            RebuildAll();
        }

        private void RemoveLane(int kind, int lane)
        {
            // 이 레인에 이벤트가 있으면 삭제 불가
            foreach (var e in _target.cueEvents)
                if (e != null && (int)e.kind == kind && Mathf.Max(0, e.lane) == lane)
                {
                    Debug.LogWarning($"[Timeline] {KindName(kind)} 레인 {LaneMark(lane)} 에 이벤트가 있어 삭제할 수 없습니다.");
                    return;
                }
            if (EffLanes(kind) <= 1) return; // 마지막 하나는 유지
            // 상위 레인 이벤트를 한 칸 내림
            _so.Update();
            var arr = _so.FindProperty("cueEvents");
            for (int i = 0; i < arr.arraySize; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("kind").enumValueIndex == kind)
                {
                    var lp = e.FindPropertyRelative("lane");
                    if (lp.intValue > lane) lp.intValue--;
                }
            }
            _so.ApplyModifiedProperties();
            _laneCount[kind] = Mathf.Max(1, EffLanes(kind) - 1);
            RebuildAll();
        }
    }
}
