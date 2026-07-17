using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Network.Socket.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// 전투 트레이스 뷰어(AC-C1b'). 설계 = <c>docs/wiki/combat-diagnostics.md</c> §2.4.
    ///
    /// <para><b>로직 없음</b> — <see cref="CombatTraceRecorder.Shared"/>(단일 소스)를 읽어 그리기만 한다.
    /// 병합·구간 계산은 <see cref="CombatTraceJoin"/>(순수 함수, EditMode 테스트 대상)에 있다.
    /// 그래서 이 창 자체엔 테스트가 없다 — 검증 가치가 있는 건 전부 Recorder/Join 쪽이다.</para>
    ///
    /// <para><b>왜 IMGUI 인가</b>: §2.4 초안은 UI Toolkit <c>MultiColumnListView</c> 를 적었지만,
    /// 이 창은 <b>플레이 중 매 초 갱신되는 진단 덤프</b>다. IMGUI 는 즉시모드라 갱신이 곧 그리기이고,
    /// 상주 UI 트리·바인딩을 유지할 이유가 없다. 선례(<c>MapEditorWindow</c>)도 IMGUI 다.</para>
    /// </summary>
    public class CombatTraceWindow : EditorWindow
    {
        private enum SummaryTab { Timeline, Judgement }

        private SummaryTab _tab = SummaryTab.Timeline;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private int _selected = -1;
        private bool _gatedOnly;

        private List<CombatTraceRecord> _records = new List<CombatTraceRecord>();
        private CombatTraceEntry[] _entries = Array.Empty<CombatTraceEntry>();

        [MenuItem("Tools/Combat/Combat Trace")]
        private static void Open() => GetWindow<CombatTraceWindow>("Combat Trace").minSize = new Vector2(620, 460);

        private void OnEnable() => EditorApplication.update += Repaint;
        private void OnDisable() => EditorApplication.update -= Repaint;

        private void OnGUI()
        {
            var rec = CombatTraceRecorder.Shared;

            DrawToolbar(rec);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode 에서만 기록됩니다. 플레이 후 Record 를 켜세요.", MessageType.Info);
                return;
            }

            Refresh(rec);

            if (_entries.Length == 0)
            {
                EditorGUILayout.HelpBox(rec.Enabled
                        ? "기록 중 — 전투를 하면 여기에 쌓입니다."
                        : "Record 가 꺼져 있습니다(기본 Off — 상시 기록 금지).",
                    MessageType.None);
                return;
            }

            DrawSummary();
            DrawEventList();
            DrawDetail();
        }

        private void DrawToolbar(CombatTraceRecorder rec)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool on = GUILayout.Toggle(rec.Enabled, rec.Enabled ? "● Recording" : "○ Record",
                    EditorStyles.toolbarButton, GUILayout.Width(90));
                if (on != rec.Enabled) rec.Enabled = on;

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    rec.Clear();
                    _selected = -1;
                }

                if (GUILayout.Button("Export CSV", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    ExportCsv();

                GUILayout.FlexibleSpace();
                _gatedOnly = GUILayout.Toggle(_gatedOnly, "거부만", EditorStyles.toolbarButton, GUILayout.Width(60));

                // Total > Count = 링이 돌아 오래된 기록이 덮였다는 신호(측정 유실 경고).
                string loss = rec.Total > rec.Count ? $"  ⚠ {rec.Total - rec.Count} 건 덮임" : "";
                GUILayout.Label($"{rec.Count}/{CombatTraceRecorder.Capacity}{loss}", EditorStyles.miniLabel);
            }
        }

        private void Refresh(CombatTraceRecorder rec)
        {
            _entries = rec.Snapshot();
            var all = CombatTraceJoin.Build(_entries);
            _records = _gatedOnly ? all.Where(r => r.LikelyGated).ToList() : all;
            if (_selected >= _records.Count) _selected = _records.Count - 1;
        }

        private void DrawSummary()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(_tab == SummaryTab.Timeline, "타임라인", EditorStyles.miniButtonLeft)) _tab = SummaryTab.Timeline;
                if (GUILayout.Toggle(_tab == SummaryTab.Judgement, "판정", EditorStyles.miniButtonRight)) _tab = SummaryTab.Judgement;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_tab == SummaryTab.Timeline) DrawTimelineSummary();
                else DrawJudgementSummary();
            }
        }

        private void DrawTimelineSummary()
        {
            var done = _records.Where(r => r.SendToHpMs >= 0).ToList();
            EditorGUILayout.LabelField($"완결 스윙 {done.Count} / 전체 {_records.Count}", EditorStyles.miniLabel);
            if (done.Count == 0)
            {
                EditorGUILayout.LabelField("HP 반영까지 이어진 스윙이 없습니다(빗나감·게이트 거부만).", EditorStyles.miniLabel);
                return;
            }

            Row("구간", "avg", "p95", "max", header: true);
            Stat("송신→발동 통지", done.Select(r => r.ActivateRoundTripMs));
            Stat("데미지→HP 반영", done.Where(r => r.DamageToHpMs >= 0).Select(r => r.DamageToHpMs));
            Stat("── 송신→HP 반영", done.Select(r => r.SendToHpMs));
        }

        private void Stat(string label, IEnumerable<long> values)
        {
            var v = values.Where(x => x >= 0).OrderBy(x => x).ToList();
            if (v.Count == 0) { Row(label, "-", "-", "-"); return; }
            long avg = (long)v.Average();
            long p95 = v[Mathf.Clamp((int)(v.Count * 0.95f), 0, v.Count - 1)];
            Row(label, $"{avg}ms", $"{p95}ms", $"{v[v.Count - 1]}ms");
        }

        private static void Row(string a, string b, string c, string d, bool header = false)
        {
            var style = header ? EditorStyles.boldLabel : EditorStyles.label;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(a, style, GUILayout.Width(160));
                EditorGUILayout.LabelField(b, style, GUILayout.Width(70));
                EditorGUILayout.LabelField(c, style, GUILayout.Width(70));
                EditorGUILayout.LabelField(d, style, GUILayout.Width(70));
            }
        }

        private void DrawJudgementSummary()
        {
            // 어빌리티별 발동수·평균 데미지·게이트 의심 비율. "왜 공격이 안 나갔나" 가 분포로 보인다.
            Row("어빌리티(netId)", "발동", "평균뎀", "게이트의심", header: true);
            foreach (var g in _records.GroupBy(r => r.NetworkId).OrderBy(g => g.Key))
            {
                var hits = g.Where(r => r.FinalDamage > 0).ToList();
                int gated = g.Count(r => r.LikelyGated);
                Row($"netId {g.Key}",
                    g.Count().ToString(),
                    hits.Count > 0 ? hits.Average(r => r.FinalDamage).ToString("F1") : "-",
                    gated > 0 ? $"{gated} ⚠" : "0");
            }

            int stale = _entries.Count(e => e.Kind == CombatTraceKind.StaleDropped);
            EditorGUILayout.LabelField(stale > 0
                    ? $"스테일 드롭 {stale} 건 — 순서 역전이 실재함(AC-C3 가 막아낸 횟수)"
                    : "스테일 드롭 0 건",
                EditorStyles.miniLabel);
        }

        private void DrawEventList()
        {
            EditorGUILayout.LabelField("이벤트 (최신순)", EditorStyles.boldLabel);
            using (var s = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.Height(140)))
            {
                _listScroll = s.scrollPosition;
                for (int i = _records.Count - 1; i >= 0; i--)
                {
                    var r = _records[i];
                    bool sel = i == _selected;
                    string dmg = r.FinalDamage > 0 ? r.FinalDamage.ToString() : "-";
                    string gate = r.LikelyGated ? "거부의심 ⚠" : "Ok";
                    string total = r.SendToHpMs >= 0 ? $"{r.SendToHpMs}ms" : "-";
                    string line = $"actor {r.ActorId,-6} netId {r.NetworkId,-3} → {r.TargetId,-6} dmg {dmg,-5} {gate,-11} {total}";

                    if (GUILayout.Toggle(sel, line, EditorStyles.miniButton) != sel)
                        _selected = sel ? -1 : i;
                }
            }
        }

        private void DrawDetail()
        {
            if (_selected < 0 || _selected >= _records.Count)
            {
                EditorGUILayout.LabelField("이벤트를 선택하면 판정·타임라인이 보입니다.", EditorStyles.miniLabel);
                return;
            }

            var r = _records[_selected];
            using (var s = new EditorGUILayout.ScrollViewScope(_detailScroll, EditorStyles.helpBox))
            {
                _detailScroll = s.scrollPosition;

                EditorGUILayout.LabelField("■ 판정 (왜 이 숫자인가)", EditorStyles.boldLabel);
                if (r.FinalDamage > 0)
                {
                    // 클라는 base(SO)를 모르는 채로 이 창을 띄울 수 있으므로 final 만 확정으로 쓰고,
                    // base 를 알면 AP-DEF 를 역산해 보여준다(§2.4 정정 — AP/DEF 분해는 서버 로그와 조인).
                    EditorGUILayout.LabelField($"  final    {r.FinalDamage}   ← 서버 권위 결과");
                    EditorGUILayout.LabelField($"  HP 반영  → {r.HpAfter}");
                    EditorGUILayout.HelpBox(
                        "산식 입력(base/AP/DEF) 분해는 서버 권위라 클라에 오지 않는다.\n" +
                        $"서버 [CombatTrace] 로그를 actor={r.ActorId} · seq={r.Seq} 로 조인하면 formula·base·AP·DEF 가 그대로 나온다.\n" +
                        "base 를 아는 경우 AP-DEF = final - base 로 역산(CombatTraceJoin.InferStatContribution).",
                        MessageType.Info);
                }
                else if (r.LikelyGated)
                {
                    EditorGUILayout.HelpBox(
                        "서버가 발동을 알리지 않았다(S_AbilityActivated 미수신) = 게이트에 막혔을 가능성.\n" +
                        "쿨다운·마나·콤보 cadence 중 무엇인지는 서버 [CombatTrace] gate 로그로 확정한다.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField("  발동은 됐으나 데미지 없음 — 빗나감(hitbox 미적중).");
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("■ 타임라인 (언제 반영됐나)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  t_send        0ms");
                EditorGUILayout.LabelField($"  발동 통지   {Fmt(r.ActivateRoundTripMs)}   상행+서버게이트+하행");
                EditorGUILayout.LabelField($"  HP 반영     {Fmt(r.SendToHpMs)}   ← 체감 지연의 본체");
                if (r.Seq > 0) EditorGUILayout.LabelField($"  seq          {r.Seq}   (서버 로그 조인 키)");
            }
        }

        private static string Fmt(long ms) => ms < 0 ? "미수신" : $"+{ms}ms";

        private void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("Export Combat Trace", "", "combat-trace.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder("actorId,networkId,targetId,finalDamage,hpAfter,seq,activateMs,sendToHpMs,likelyGated\n");
            foreach (var r in _records)
                sb.Append($"{r.ActorId},{r.NetworkId},{r.TargetId},{r.FinalDamage},{r.HpAfter},{r.Seq},{r.ActivateRoundTripMs},{r.SendToHpMs},{r.LikelyGated}\n");

            // global:: 필수 — 이 파일의 네임스페이스가 Game.Gameplay.Editor 라 `System.IO` 가 **Game.System.IO** 로 해석된다
            // (프로젝트에 Game.System 이 있어 전역 System 을 가린다).
            global::System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log($"[CombatTrace] CSV 저장: {path} ({_records.Count} 건)");
        }
    }
}
