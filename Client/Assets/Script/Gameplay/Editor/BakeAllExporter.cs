using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// 서버 임베디드 bake 산출물 <b>7종을 한 번에</b> 굽는다.
    ///
    /// <para><b>왜 필요한가</b>: 저작(SO)과 bake(JSON)가 갈라지는 사고는 "Export 를 돌렸나?"를 사람이
    /// 기억하는 구조에서 나온다. 실제로 <c>abilities.json</c> 이 그렇게 어긋났고(CA-5 에서 SO 만 바꾸고
    /// Export 미실행 → 서버가 옛 판정 창으로 검증), <c>items.json</c> 은 아예 Exporter 가 없어 클라
    /// 카탈로그와 갈라졌다(A4). 메뉴 하나로 전부 굽고 <b>결과를 한 화면에 보여주면</b> 빠뜨릴 자리가 없다.</para>
    ///
    /// <para><b>자동화</b>: <see cref="BakeAll"/> 는 다이얼로그가 없어 Unity CLI 로 호출할 수 있다 —
    /// <c>unity command --project-path . eval "Game.Gameplay.Editor.BakeAllExporter.BakeAll()"</c>.
    /// 저작 드리프트 점검을 CI/훅에서 돌리려면 이걸 부르고 <c>git diff --exit-code</c> 로 판정한다.</para>
    /// </summary>
    public static class BakeAllExporter
    {
        /// <summary>bake 대상 전체. 새 Exporter 를 만들면 <b>여기에 등록</b>한다(빠뜨리면 전체 bake 에서 누락).</summary>
        private static readonly (string Label, Func<int> Bake)[] Exporters =
        {
            ("Item", ItemCatalogExporter.BakeAll),
            ("Quest", QuestCatalogExporter.BakeAll),
            ("Ability", AbilityCatalogExporter.BakeAll),
            ("Monster", MonsterCatalogExporter.BakeAll),
            ("DropTable", DropTableExporter.BakeAll),
            ("LevelTable", LevelTableExporter.BakeAll),
            ("MapData", MapDataExporter.BakeAll),
        };

        [MenuItem("Tools/Bake/Export ALL (SO → JSON)", priority = 0)]
        public static void ExportAll()
        {
            var report = BakeAllWithReport(out var failed);
            if (failed > 0)
                EditorToolReport.ErrorLater("Export ALL", $"{failed}종이 실패했습니다. 콘솔에서 사유를 확인하세요.\n\n{report}");
            else
                EditorToolReport.Later("Export ALL", $"{report}\n서버 반영은 서버 재빌드가 필요합니다.");
        }

        /// <summary>전체 bake. 반환: 실패한 Exporter 수(0 이면 전부 성공). <b>다이얼로그 없음</b> — 자동화용.</summary>
        public static int BakeAll()
        {
            BakeAllWithReport(out var failed);
            return failed;
        }

        private static string BakeAllWithReport(out int failed)
        {
            var sb = new StringBuilder();
            var results = new List<string>();
            failed = 0;

            foreach (var (label, bake) in Exporters)
            {
                int count;
                try
                {
                    count = bake();
                }
                catch (Exception e)
                {
                    // 한 Exporter 가 던져도 나머지는 굽는다 — 부분 실패가 전체를 막으면 원인 파악이 늦어진다.
                    Debug.LogError($"[BakeAll] {label} 예외: {e}");
                    failed++;
                    results.Add($"  {label,-11} 예외");
                    continue;
                }

                if (count < 0) { failed++; results.Add($"  {label,-11} 검증 실패(콘솔 참조)"); }
                else if (count == 0) { failed++; results.Add($"  {label,-11} 저작 SO 없음"); }
                else results.Add($"  {label,-11} {count}건");
            }

            sb.AppendLine($"bake {Exporters.Length}종 — 성공 {Exporters.Length - failed} / 실패 {failed}");
            foreach (var r in results) sb.AppendLine(r);

            var text = sb.ToString();
            if (failed > 0) Debug.LogError("[BakeAll]\n" + text);
            else Debug.Log("[BakeAll]\n" + text);
            return text;
        }
    }
}
