using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// ARPGWarrior 팩 클립 규격화(P1) — <b>루프 플래그 일괄 설정</b>.
    ///
    /// <b>왜 필요한가</b>: 팩이 FBX 에서 추출해 넣어 준 `.anim` 자산은 임포터 루프 설정을 승계하지 못해
    /// <b>전부 <c>m_LoopTime = 0</c></b> 이다(실측). 그대로 쓰면 Idle/Walk/Run 이 한 번 재생 후 멈칫한다.
    /// 275개를 손으로 켜는 건 비재현·실수 위험이라 대상 목록을 코드에 두고 일괄 적용한다.
    ///
    /// <b>대상 선정 기준</b>: 순환 재생되는 것만(정지/이동/공중 유지/사다리 오르내림).
    /// 원샷(Jump·Landing·Attack·Dodge·Death·Getup·Climb_*_Start/To_Idle)은 <b>끈 채로 둔다</b>.
    /// </summary>
    public static class ARPGWarriorClipSetup
    {
        private const string BaseDir = "Assets/Art/ARPGPack/ARPGWarrior/Animations/Humanoid";
        private const string AdditionalDir = "Assets/Art/ARPGPack/ARPGWarrior/Additional_Animations/Humanoid";

        /// <summary>루프로 재생돼야 하는 클립(파일명은 ARPG_Warrior_ 접두사 제외).</summary>
        private static readonly string[] LoopClipsBase =
        {
            "Idle1", "Idle2",

            // 8방향 걷기/달리기(락온 스트레이프 블렌드) + 전진 기본
            "Walk_Forward_IPC", "Walk_Forward_Left_IPC", "Walk_Forward_Right_IPC",
            "Walk_Leftward_IPC", "Walk_Rightward_IPC",
            "Walk_Backward_IPC", "Walk_Backward_Left_IPC", "Walk_Backward_Right_IPC",

            "Run_Forward_IPC", "Run_Forward_Left_IPC", "Run_Forward_Right_IPC",
            "Run_Leftward_IPC", "Run_Rightward_IPC",
            "Run_Backward_IPC", "Run_Backward_Left_IPC", "Run_Backward_Right_IPC",

            "Sprint_IPC",

            "Airborne", // 공중 유지(낙하) — 착지까지 계속 돈다
        };

        private static readonly string[] LoopClipsAdditional =
        {
            // 사다리(P6 예정) — 오르내리는 동안 순환
            "Climb_Up", "Climb_Down", "Climb_Down_Fast", "Climb_L", "Climb_R",
        };

        [MenuItem("Tools/ARPGWarrior/클립 규격화 (루프 플래그)")]
        public static void SetupAllMenu() => Debug.Log(SetupAll());

        /// <summary>모달 없는 진입점 — CI/CLI 에서 직접 호출한다(A1 교훈: 메뉴는 모달로 메인스레드를 잡는다).</summary>
        public static string SetupAll()
        {
            int changed = 0, already = 0;
            var missing = new List<string>();

            changed += Apply(BaseDir, LoopClipsBase, ref already, missing);
            changed += Apply(AdditionalDir, LoopClipsAdditional, ref already, missing);

            AssetDatabase.SaveAssets();

            string report = $"[ARPGWarriorClipSetup] 루프 ON 적용 {changed}개 / 이미 ON {already}개 / 대상 " +
                            $"{LoopClipsBase.Length + LoopClipsAdditional.Length}개";
            if (missing.Count > 0)
                report += $"\n  ⚠ 못 찾은 클립 {missing.Count}개: {string.Join(", ", missing)}";
            return report;
        }

        private static int Apply(string dir, IEnumerable<string> names, ref int already, List<string> missing)
        {
            int changed = 0;
            foreach (string name in names)
            {
                string path = $"{dir}/ARPG_Warrior_{name}.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { missing.Add(name); continue; }

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (settings.loopTime) { already++; continue; }

                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                changed++;
            }
            return changed;
        }
    }
}
