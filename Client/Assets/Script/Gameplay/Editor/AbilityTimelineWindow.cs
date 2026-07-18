using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Abilities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
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

        private VisualElement _content;   // 스크롤 내부, width = 타임라인 길이
        private VisualElement _headerColumn; // 왼쪽 고정 트랙 헤더 열(이름·＋/×)
        private VisualElement _scrub;
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
        private Transform _previewRoot;
        private readonly List<GameObject> _previewSpawned = new();

        [MenuItem("Tools/Ability/Ability Timeline")]
        private static void Open() => GetWindow<AbilityTimelineWindow>("Ability Timeline").minSize = new Vector2(760, 440);

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            LoadStyleSheet(root); // 디자인 폴리시(.uss)

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

            var detailsScroll = new ScrollView(ScrollViewMode.Vertical) { name = "atl-details-scroll" };
            detailsScroll.style.width = 300; detailsScroll.style.flexShrink = 0;
            _inspectorBody = new VisualElement { name = "atl-details" };
            _inspectorBody.AddToClassList("atl-details");
            detailsScroll.Add(_inspectorBody);
            body.Add(detailsScroll);

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
            zoom.RegisterValueChangedCallback(e => { _pxPerMs = e.newValue; RebuildAll(); });
            bar.Add(new Label("Zoom") { style = { unityTextAlign = TextAnchor.MiddleCenter } });
            bar.Add(zoom);
            // ※ Snap/FPS 격자는 제거됨 — 편집은 항상 0.1ms 단위(판정창 int 만 1ms).

            bar.Add(new ToolbarSpacer());
            _previewButton = new ToolbarButton(TogglePreview) { text = "▶ Preview" };
            bar.Add(_previewButton);
            var notify = new ToolbarToggle { text = "Notify", value = _previewNotify };
            notify.RegisterValueChangedCallback(e => _previewNotify = e.newValue);
            bar.Add(notify);

            // ※ Cue 카탈로그 필드는 상세 패널 '고급(선택)'으로 이동(직접 리소스가 기본이므로).
            bar.Add(new ToolbarSpacer());
            var actor = new ObjectField("Actor") { objectType = typeof(GameObject), value = _actorPrefab,
                tooltip = "Event 이벤트의 메서드 드롭다운 소스 — 이 프리팹의 컴포넌트 메서드를 나열." };
            actor.style.width = 190;
            actor.RegisterValueChangedCallback(e => { _actorPrefab = e.newValue as GameObject; RefreshInspector(); });
            bar.Add(actor);

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
            _selected = -1; _selection.Clear(); _hitboxSelected = false;
            _laneCount = new[] { 1, 1, 1, 1 }; // 어빌리티 바뀌면 빈 레인 초기화(사용 중 레인은 EffLanes 가 복원)
            _scrubMs = 0;
            RebuildAll();
        }

        // ─────────────────────── 좌표 ───────────────────────

        private float TotalMs => _target == null ? 400f
            : Mathf.Max(_target.startupMs + _target.activeMs + _target.recoveryMs, MaxEventMs() + 50, 400);

        private float MaxEventMs()
        {
            float m = 0;
            if (_target != null)
                foreach (var e in _target.cueEvents) if (e != null) m = Mathf.Max(m, e.timeMs);
            return m;
        }

        private float XForTime(float ms) => LeftPad + ms * _pxPerMs;
        private float TimeForX(float x) => Mathf.Max(0f, (x - LeftPad) / _pxPerMs);
        // 편집 격자 = 항상 0.1ms(Snap/FPS 제거됨). 판정창 startup/active 는 int 계약이라 최종 1ms.
        private static float Snap(float ms) => Mathf.Round(ms * 10f) / 10f;
        private float RowTop(int row) => RulerH + row * (RowH + RowGap);

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

        private void RebuildAll()
        {
            if (_content == null) return;
            _content.Clear();
            _headerColumn?.Clear();

            RefreshInspector();

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
            float contentH = RulerH + _rows.Count * (RowH + RowGap) + 4f;
            _content.style.width = contentW;
            _content.style.height = contentH;
            if (_headerColumn != null) _headerColumn.style.height = contentH;

            BuildHeaderCorner();
            BuildRuler(contentW);
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
            corner.style.left = 0; corner.style.top = 0; corner.style.width = HeaderW; corner.style.height = RulerH;
            corner.style.backgroundColor = ColRuler;
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
            head.style.backgroundColor = (r % 2 == 0) ? ColRowB : ColRowA;
            head.style.flexDirection = FlexDirection.Row; head.style.alignItems = Align.Center;
            _headerColumn.Add(head);

            var name = new Label(kind == KHitbox ? "판정창" : $"{KindName(kind)} {LaneMark(lane)}");
            name.style.flexGrow = 1; name.style.marginLeft = 5; name.style.fontSize = 10;
            name.style.color = new Color(0.78f, 0.78f, 0.78f);
            head.Add(name);

            if (kind != KHitbox)
            {
                int capKind = kind, capLane = lane;
                var add = new Button(() => AddLane(capKind)) { text = "＋", tooltip = $"{KindName(kind)} 레인 추가" };
                add.style.width = 20; add.style.height = 18; add.style.marginRight = 1; add.style.paddingLeft = 0; add.style.paddingRight = 0;
                head.Add(add);
                var del = new Button(() => RemoveLane(capKind, capLane)) { text = "×", tooltip = "이 레인 삭제(빈 레인만)" };
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
            _content.Add(ruler);

            for (int ms = 0; ms <= TotalMs; ms += 100)
            {
                float x = XForTime(ms);
                var tick = new VisualElement();
                tick.style.position = Position.Absolute;
                tick.style.left = x; tick.style.top = 0;
                tick.style.width = 1; tick.style.height = RulerH;
                tick.style.backgroundColor = new Color(1, 1, 1, 0.3f);
                ruler.Add(tick);

                var lbl = new Label($"{ms}");
                lbl.style.position = Position.Absolute;
                lbl.style.left = x + 3; lbl.style.top = 3;
                lbl.style.fontSize = 9;
                lbl.style.color = new Color(0.85f, 0.85f, 0.85f);
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

        /// <summary>클립 레인(오른쪽 스크롤 영역) — 배경 + 우클릭 이벤트 추가만. 이름·＋/× 는 왼쪽 헤더 열이 담당.</summary>
        private void BuildTrackLane(int r, float w)
        {
            var (kind, lane) = _rows[r];
            var track = new VisualElement { name = "cue-track" };
            track.style.position = Position.Absolute;
            track.style.left = 0; track.style.top = RowTop(r);
            track.style.width = w; track.style.height = RowH;
            track.style.backgroundColor = (r % 2 == 0) ? ColRowB : ColRowA;
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
            anchor.tooltip = $"주 애니(cueTrigger): {_target.cueTrigger} · t=0 발동 (편집=AbilityDefinition 인스펙터)";
            int animRow = RowIndexOf(2, 0); // Anim 첫 레인
            if (animRow < 0) return;
            _rowTracks[animRow].Add(anchor);

            var lbl = new Label($"▶ {_target.cueTrigger}");
            lbl.style.position = Position.Absolute;
            lbl.style.left = 14; lbl.style.top = 8; lbl.style.fontSize = 9;
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
            bar.style.backgroundColor = new Color(ColHitbox.r, ColHitbox.g, ColHitbox.b, 0.85f);
            track.Add(bar);

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
                float bw = _hitboxSelected ? 2 : 0; // 선택 하이라이트
                bar.style.borderTopWidth = bar.style.borderBottomWidth = bar.style.borderLeftWidth = bar.style.borderRightWidth = bw;
                bar.style.borderTopColor = bar.style.borderBottomColor = bar.style.borderLeftColor = bar.style.borderRightColor = ColSel;
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
            bar.RegisterCallback<PointerUpEvent>(e => { if (bar.HasPointerCapture(e.pointerId)) { bar.ReleasePointer(e.pointerId); RebuildAll(); } });

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
            grip.RegisterCallback<PointerUpEvent>(e => { if (grip.HasPointerCapture(e.pointerId)) { grip.ReleasePointer(e.pointerId); RebuildAll(); } });
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
                    float w = Mathf.Max(20, e2.durationMs * _pxPerMs); // 최소 폭(즉발도 본체 클릭·선택되게 — 그립 사이 여유)
                    clip.style.left = x0; clip.style.width = w;
                    gripL.style.left = x0 - 3; gripR.style.left = x0 + w - 3;
                    lbl.style.left = x0 + 10;
                    lbl.text = string.IsNullOrEmpty(e2.id) ? "(id 미정)" : e2.id;
                    clip.tooltip = $"{e2.kind} · {(string.IsNullOrEmpty(e2.id) ? "(id 미정)" : e2.id)} · {Mathf.RoundToInt(e2.timeMs)}~{Mathf.RoundToInt(e2.timeMs + e2.durationMs)}ms"
                                 + (e2.kind == ECueKind.Vfx && !string.IsNullOrEmpty(e2.socket) ? $" @ {e2.socket}" : "");
                    float bw = _selection.Contains(index) ? 2 : 0; // P8: 다중 선택 전부 하이라이트
                    clip.style.borderTopWidth = clip.style.borderBottomWidth = clip.style.borderLeftWidth = clip.style.borderRightWidth = bw;
                    clip.style.borderTopColor = clip.style.borderBottomColor = clip.style.borderLeftColor = clip.style.borderRightColor = ColSel;
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
                clip.RegisterCallback<PointerUpEvent>(e => { if (clip.HasPointerCapture(e.pointerId)) { clip.ReleasePointer(e.pointerId); RebuildAll(); } });

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
            grip.RegisterCallback<PointerUpEvent>(e => { if (grip.HasPointerCapture(e.pointerId)) { grip.ReleasePointer(e.pointerId); RebuildAll(); } });
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
            _scrub.style.top = 0; _scrub.style.width = 1; _scrub.style.height = h;
            _scrub.style.backgroundColor = ColScrub;
            _content.Add(_scrub);

            var head = new VisualElement();
            head.style.position = Position.Absolute;
            head.style.left = -5; head.style.top = 0; head.style.width = 11; head.style.height = 11;
            head.style.backgroundColor = ColScrub;
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
                sec.Add(BoundField(new EnumField("종류", ECueKind.Sfx), el.FindPropertyRelative("kind"), RebuildAll));
                sec.Add(BoundField(new FloatField("시각(ms)"), el.FindPropertyRelative("timeMs"), RebuildAll));
                sec.Add(BoundField(new FloatField("길이(ms)"), el.FindPropertyRelative("durationMs"), RebuildAll));
                sec.Add(BoundInt2(new IntegerField("레인") { tooltip = "같은 종류 안의 행(0=첫 레인). 트랙 헤더 ＋/× 로도 레인 관리." }, el.FindPropertyRelative("lane")));

                if (kind == ECueKind.Sfx)
                    sec.Add(BoundObject(new ObjectField("SFX 클립") { objectType = typeof(AudioClip) }, el.FindPropertyRelative("sfxClip")));
                else if (kind == ECueKind.Vfx)
                {
                    sec.Add(BoundObject(new ObjectField("VFX 프리팹") { objectType = typeof(GameObject) }, el.FindPropertyRelative("vfxPrefab")));
                    sec.Add(BoundText(new TextField("소켓"), el.FindPropertyRelative("socket")));
                }
                else if (kind == ECueKind.Event)
                    BuildEventInspector(el, sec); // 메서드 드롭다운 + 타입 인자
                else
                    sec.Add(Hint("Anim = 지연 애니 트리거(현재 재생 없음). 시각·길이만 유효."));

                int selCount = _selection.Count;
                var actions = RowBtns();
                actions.Add(new Button(DuplicateSelected) { text = selCount > 1 ? $"복제 ({selCount})" : "복제", style = { flexGrow = 1 } });
                actions.Add(new Button(DeleteSelectedEvents) { text = selCount > 1 ? $"삭제 ({selCount})" : "삭제", style = { flexGrow = 1 } });
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
            gp.Add(BoundInt2(new IntegerField("startup(ms)"), _so.FindProperty("startupMs")));
            gp.Add(BoundInt2(new IntegerField("active(ms)"), _so.FindProperty("activeMs")));
            var gpBtns = RowBtns();
            gpBtns.Add(new Button(AbilityCatalogExporter.Export) { text = "Export", tooltip = "판정창 변경을 서버 abilities.json 에 재bake.", style = { flexGrow = 1 } });
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

        // 세로 상세 패널용 — 패널 폭을 채우고 .atl-field 클래스로 라벨 폭·간격을 .uss 가 통제(W-A 폴리시).
        private VisualElement BoundField(EnumField f, SerializedProperty p, global::System.Action onChanged)
        {
            f.AddToClassList("atl-field");
            f.Init((ECueKind)p.enumValueIndex);
            f.RegisterValueChangedCallback(e =>
            {
                _so.Update(); p.enumValueIndex = (int)(ECueKind)e.newValue; _so.ApplyModifiedProperties(); onChanged?.Invoke();
            });
            return f;
        }

        private VisualElement BoundField(FloatField f, SerializedProperty p, global::System.Action onChanged)
        {
            f.AddToClassList("atl-field"); f.value = p.floatValue;
            f.isDelayed = true; // ★ Enter/blur 에만 커밋 — 매 키 입력마다 RebuildAll 로 필드가 파괴돼 편집 불가하던 것 방지
            f.RegisterValueChangedCallback(e =>
            {
                _so.Update(); p.floatValue = Mathf.Max(0f, e.newValue); _so.ApplyModifiedProperties(); onChanged?.Invoke();
            });
            return f;
        }

        private VisualElement BoundText(TextField f, SerializedProperty p)
        {
            f.AddToClassList("atl-field"); f.value = p.stringValue;
            f.RegisterValueChangedCallback(e => { _so.Update(); p.stringValue = e.newValue; _so.ApplyModifiedProperties(); });
            return f;
        }

        private VisualElement BoundObject(ObjectField f, SerializedProperty p)
        {
            f.AddToClassList("atl-field"); f.value = p.objectReferenceValue;
            f.RegisterValueChangedCallback(e => { _so.Update(); p.objectReferenceValue = e.newValue; _so.ApplyModifiedProperties(); RebuildAll(); });
            return f;
        }

        private VisualElement BoundInt2(IntegerField f, SerializedProperty p)
        {
            f.AddToClassList("atl-field"); f.value = p.intValue; f.isDelayed = true;
            f.RegisterValueChangedCallback(e => { _so.Update(); p.intValue = e.newValue; _so.ApplyModifiedProperties(); RebuildAll(); });
            return f;
        }

        private VisualElement BoundToggle(Toggle f, SerializedProperty p)
        {
            f.AddToClassList("atl-field"); f.value = p.boolValue;
            f.RegisterValueChangedCallback(e => { _so.Update(); p.boolValue = e.newValue; _so.ApplyModifiedProperties(); });
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
            methodField.AddToClassList("atl-field"); methodField.style.flexGrow = 1; methodField.value = imProp.stringValue;
            methodField.isDelayed = true; // ★ 커밋 시에만 RebuildAll — 타이핑 중 필드 파괴 방지
            methodField.RegisterValueChangedCallback(e => { _so.Update(); imProp.stringValue = e.newValue; _so.ApplyModifiedProperties(); RebuildAll(); });
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
                        RefreshInspector(); RebuildAll();
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
            string[] fields = { "timeMs", "durationMs", "kind", "lane", "sfxClip", "vfxPrefab", "id", "socket", "invokeMethod", "argType", "argFloat", "argInt", "argBool", "argString" };
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

        private void OnDisable() => StopPreview(); // 창 닫힘/도메인 리로드 시 update 해제 + 스폰 정리

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
            CleanupPreview();
        }

        /// <summary>실시간으로 스크럽을 굴리며, 이 프레임에 지나간 이벤트를 발화한다(참조 R7 = TriggerInEditMode).</summary>
        private void PreviewTick()
        {
            if (!_previewing || _target == null) { StopPreview(); return; }

            double now = EditorApplication.timeSinceStartup;
            _scrubMs += (float)((now - _previewLastTime) * 1000.0);
            _previewLastTime = now;

            foreach (var ev in _target.cueEvents)
                if (ev != null && ev.timeMs > _previewPrevMs && ev.timeMs <= _scrubMs)
                    PreviewFire(ev);
            _previewPrevMs = _scrubMs;

            PositionScrub();
            Repaint();

            if (_scrubMs >= TotalMs) StopPreview();
        }

        private void PreviewFire(AbilityCueEvent ev)
        {
            if (_previewNotify)
                Debug.Log($"[TimelinePreview] {Mathf.RoundToInt(ev.timeMs)}ms · {ev.kind} · '{(string.IsNullOrEmpty(ev.id) ? "(id 미정)" : ev.id)}'");

            switch (ev.kind)
            {
                case ECueKind.Sfx:
                {
                    var clip = ev.sfxClip != null ? ev.sfxClip
                             : (_catalog != null && _catalog.TryGetSfx(ev.id, out var s) ? s.clip : null);
                    if (clip != null) PlayPreviewClip(clip);
                    break;
                }
                case ECueKind.Vfx:
                {
                    var prefab = ev.vfxPrefab != null ? ev.vfxPrefab
                               : (_catalog != null && _catalog.TryGetVfx(ev.id, out var v) ? v.prefab : null);
                    if (prefab != null) SpawnPreviewVfx(prefab);
                    break;
                }
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

        private void SpawnPreviewVfx(GameObject prefab)
        {
            if (prefab == null) return;
            if (_previewRoot == null)
                _previewRoot = new GameObject("[TimelinePreview]") { hideFlags = HideFlags.HideAndDontSave }.transform;
            var go = UnityEngine.Object.Instantiate(prefab, _previewRoot);
            go.hideFlags = HideFlags.HideAndDontSave;
            _previewSpawned.Add(go);
            // 에디트모드에선 파티클이 자동 시뮬 안 될 수 있다(스폰 가시화까지가 MVP) — 수명 관리는 Stop 에서 일괄 정리.
        }

        private void CleanupPreview()
        {
            foreach (var go in _previewSpawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _previewSpawned.Clear();
            if (_previewRoot != null) { UnityEngine.Object.DestroyImmediate(_previewRoot.gameObject); _previewRoot = null; }
        }

        private static Color ColorFor(ECueKind k) => k switch { ECueKind.Vfx => ColVfx, ECueKind.Sfx => ColSfx, ECueKind.Event => ColEvent, _ => ColAnim };

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
