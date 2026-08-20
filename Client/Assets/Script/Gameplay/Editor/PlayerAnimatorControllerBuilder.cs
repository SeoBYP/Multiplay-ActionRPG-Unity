using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// 플레이어 AnimatorController 를 <b>ARPGWarrior(Humanoid) 클립으로 코드에서 생성</b>한다(P2).
    ///
    /// <b>왜 코드 생성인가</b>: 상태·전이가 20개를 넘어 손으로 만들면 재현이 안 되고 리뷰도 불가능하다.
    /// 스크립트면 diff 가 남고 언제든 같은 결과를 다시 만든다(구 컨트롤러가 모델 교체 때 조용히 깨진 전례).
    ///
    /// <b>왜 새 컨트롤러인가</b>: 구 PlayerController.controller 의 클립 22개는 전부 <b>Generic</b> 인데
    /// 현재 플레이어 메시(HornedKnight)는 <b>Humanoid</b> 아바타다 → 바인딩이 하나도 안 붙고(실측 0.0도),
    /// 게다가 빈 휴머노이드 포즈가 root 본을 -1.06m 로 밀어 캐릭터가 지면에 파묻힌다(실측).
    /// ARPGWarrior 클립은 Humanoid 라 같은 아바타에 정상 리타깃된다(실측 15.5도).
    ///
    /// <b>파라미터 계약</b>: <see cref="Game.Gameplay.Character.CharacterAgentAnimations"/> 가 쓰는 이름을 그대로 만든다
    /// (Speed·MoveX·MoveY·Strafe·Grounded·ComboStep·Jump·Fall·Land·Attack·Interact·Dead·Dodge·Revive
    ///  + P5 의 DodgeX·DodgeY·Hit + P6 의 Climbing·ClimbSpeed) → 드라이버 코드는 파라미터명 문자열만 프리팹에서 채우면 된다.
    /// </summary>
    public static class PlayerAnimatorControllerBuilder
    {
        private const string ControllerPath = "Assets/GameResources/Animations/Player/PlayerController_ARPG.controller";
        private const string BaseDir = "Assets/Art/ARPGPack/ARPGWarrior/Animations/Humanoid";
        private const string AdditionalDir = "Assets/Art/ARPGPack/ARPGWarrior/Additional_Animations/Humanoid";

        private static readonly string[] PlayerPrefabs =
        {
            "Assets/Prefabs/Character/PlayerCharacter.prefab",
            "Assets/Prefabs/Character/RemotePlayerCharacter.prefab",
        };

        // ── 블렌드 좌표 = 클립의 <b>실측</b> 이동 속도(m/s). AnimationClip.averageSpeed 로 측정했다.
        //    임계값을 실측 속도에 두어야 "블렌드된 발 속도 = 실제 이동 속도"가 되어 발 슬라이딩이 사라진다.
        //    (예전엔 Walk 2.0/Sprint 5.335 로 코드 속도를 그대로 썼는데, 클립은 각각 2.26·3.44 라 발이 미끄러졌다.)
        private const float SpeedIdle = 0f;
        private const float SpeedWalk = 2.26f;   // Walk_Forward 실측
        private const float SpeedRun = 3.31f;    // Run_Forward 실측
        private const float SpeedSprint = 5.335f; // LocomotionSettings.SprintSpeed(게임 속도)
        private const float SprintClipSpeed = 3.44f; // Sprint 클립 실측 — 게임 속도와 달라 배속으로 보정한다
        private static float SprintTimeScale => SpeedSprint / SprintClipSpeed; // ≈1.55

        private static readonly List<string> Missing = new List<string>();

        [MenuItem("Tools/ARPGWarrior/플레이어 컨트롤러 생성 + 프리팹 배선")]
        public static void BuildAndWireMenu() => Debug.Log(BuildAndWireAll());

        /// <summary>모달 없는 진입점(CLI/CI 용).</summary>
        public static string BuildAndWireAll()
        {
            Missing.Clear();
            var ctrl = Build();
            string wired = Wire(ctrl);

            AssetDatabase.SaveAssets();

            string report = "[PlayerAnimatorControllerBuilder] 생성: " + ControllerPath + "\n" +
                            "  파라미터 " + ctrl.parameters.Length +
                            " · 상태 " + ctrl.layers[0].stateMachine.states.Length + "\n" +
                            "  " + wired;
            if (Missing.Count > 0)
                report += "\n  ⚠ 못 찾은 클립 " + Missing.Count + "개: " + string.Join(", ", Missing);
            return report;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 컨트롤러 생성
        // ─────────────────────────────────────────────────────────────────────
        private static AnimatorController Build()
        {
            AssetDatabase.DeleteAsset(ControllerPath); // 항상 처음부터 — 부분 갱신은 잔여 상태가 남아 재현이 깨진다
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("DodgeX", AnimatorControllerParameterType.Float); // P5: 회피 방향(로컬 좌−/우+)
            ctrl.AddParameter("DodgeY", AnimatorControllerParameterType.Float); // P5: 회피 방향(로컬 후−/전+)
            ctrl.AddParameter("ClimbSpeed", AnimatorControllerParameterType.Float); // P6: 사다리 클립 배속(음수=역재생=하강)
            ctrl.AddParameter("Strafe", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Climbing", AnimatorControllerParameterType.Bool); // P6: 사다리 부착 중
            ctrl.AddParameter("ComboStep", AnimatorControllerParameterType.Int);
            foreach (string t in new[] { "Jump", "Fall", "Land", "Attack", "Interact", "Dead", "Dodge", "Revive", "Hit" })
                ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            // ── 1) 로코모션(비락온) — Speed 1D 4점 블렌드
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out var locoTree, 0);
            locoTree.blendType = BlendTreeType.Simple1D;
            locoTree.blendParameter = "Speed";
            locoTree.useAutomaticThresholds = false;
            locoTree.AddChild(Base("Idle1"), SpeedIdle);
            locoTree.AddChild(Base("Walk_Forward_IPC"), SpeedWalk);
            locoTree.AddChild(Base("Run_Forward_IPC"), SpeedRun);
            locoTree.AddChild(Base("Sprint_IPC"), SpeedSprint);
            SetChildTimeScale(locoTree, 3, SprintTimeScale); // 스프린트 클립(3.44m/s)을 게임 속도(5.335)에 맞춰 가속

            // ── 2) 락온 스트레이프 — MoveX/MoveY 2D 8방향(+중앙 Idle)
            //     좌표 = 각 클립의 <b>실측 속도 벡터</b>(m/s). Walk 링(≈2.3) + Run 링(≈3.4) + Sprint(5.335, 배속 보정).
            //     걷는 속도로 왼쪽을 누르면 Walk_Leftward 가, 달리면 Run_Leftward 가 나온다(예전엔 Run 링 하나뿐이라 걸어도 달리는 발).
            var strafe = ctrl.CreateBlendTreeInController("StrafeLocomotion", out var strafeTree, 0);
            strafeTree.blendType = BlendTreeType.FreeformCartesian2D;
            strafeTree.blendParameter = "MoveX";
            strafeTree.blendParameterY = "MoveY";
            strafeTree.AddChild(Base("Idle1"), new Vector2(0f, 0f));

            strafeTree.AddChild(Base("Walk_Forward_IPC"), new Vector2(0f, 2.26f));
            strafeTree.AddChild(Base("Walk_Forward_Left_IPC"), new Vector2(-1.59f, 1.56f));
            strafeTree.AddChild(Base("Walk_Leftward_IPC"), new Vector2(-2.31f, 0f));
            strafeTree.AddChild(Base("Walk_Backward_Left_IPC"), new Vector2(-1.65f, -1.61f));
            strafeTree.AddChild(Base("Walk_Backward_IPC"), new Vector2(0f, -2.30f));
            strafeTree.AddChild(Base("Walk_Backward_Right_IPC"), new Vector2(1.64f, -1.62f));
            strafeTree.AddChild(Base("Walk_Rightward_IPC"), new Vector2(2.31f, 0f));
            strafeTree.AddChild(Base("Walk_Forward_Right_IPC"), new Vector2(1.66f, 1.62f));

            strafeTree.AddChild(Base("Run_Forward_IPC"), new Vector2(0f, 3.31f));
            strafeTree.AddChild(Base("Run_Forward_Left_IPC"), new Vector2(-2.43f, 2.36f));
            strafeTree.AddChild(Base("Run_Leftward_IPC"), new Vector2(-3.43f, 0f));
            strafeTree.AddChild(Base("Run_Backward_Left_IPC"), new Vector2(-2.42f, -2.40f));
            strafeTree.AddChild(Base("Run_Backward_IPC"), new Vector2(0f, -3.40f));
            strafeTree.AddChild(Base("Run_Backward_Right_IPC"), new Vector2(2.43f, -2.41f));
            strafeTree.AddChild(Base("Run_Rightward_IPC"), new Vector2(3.43f, 0f));
            strafeTree.AddChild(Base("Run_Forward_Right_IPC"), new Vector2(2.41f, 2.33f));

            strafeTree.AddChild(Base("Sprint_IPC"), new Vector2(0f, SpeedSprint));
            SetChildTimeScale(strafeTree, strafeTree.children.Length - 1, SprintTimeScale);

            // ── 3) 공중/착지 — FSM(Jump/Fall/Land State)이 트리거로 몬다
            var jump = AddState(sm, "Jump", Additional("Jump"), new Vector3(320f, -120f));
            var airborne = AddState(sm, "Airborne", Base("Airborne"), new Vector3(320f, -40f));
            var landing = AddState(sm, "Landing", Base("Landing"), new Vector3(320f, 40f));

            // ── 4) 공격 콤보 — ComboStep(0~3)이 단계를 고른다.
            //     ComboD(3)는 P4 에서 서버 skillId 확장 후 도달 가능(지금은 컨트롤러만 준비).
            var comboA = AddState(sm, "ComboA", Base("Attack_Combo1"), new Vector3(-320f, -120f));
            var comboB = AddState(sm, "ComboB", Base("Attack_Combo2"), new Vector3(-320f, -40f));
            var comboC = AddState(sm, "ComboC", Base("Attack_Combo3"), new Vector3(-320f, 40f));
            var comboD = AddState(sm, "ComboD", Base("Attack_Combo4"), new Vector3(-320f, 120f));

            // 공격만 클립의 루트 이동(0.63~1.42m 전진)을 실제로 쓴다 — RootMotionRelay 가 이 태그를 보고 적용.
            // 회피(Evade 3.6m)·사망·기상도 루트 이동이 있지만 드라이버가 이동을 전담하므로 태그를 주지 않는다(이중 이동 방지).
            foreach (var c in new[] { comboA, comboB, comboC, comboD })
                c.tag = Game.Gameplay.Character.RootMotionRelay.RootMotionTag;

            // ── 5) 기타 액션
            // 회피(P5) — DodgeX/DodgeY 8방향 블렌드. 드라이버가 트리거 전에 방향을 세팅한다.
            var dodge = ctrl.CreateBlendTreeInController("Dodge", out var dodgeTree, 0);
            dodgeTree.blendType = BlendTreeType.FreeformDirectional2D;
            dodgeTree.blendParameter = "DodgeX";
            dodgeTree.blendParameterY = "DodgeY";
            dodgeTree.AddChild(Base("Evade_Forward"), new Vector2(0f, 1f));
            dodgeTree.AddChild(Base("Evade_Forward_Left"), new Vector2(-0.7f, 0.7f));
            dodgeTree.AddChild(Base("Evade_Left"), new Vector2(-1f, 0f));
            dodgeTree.AddChild(Base("Evade_Backward_Left"), new Vector2(-0.7f, -0.7f));
            dodgeTree.AddChild(Base("Evade_Backward"), new Vector2(0f, -1f));
            dodgeTree.AddChild(Base("Evade_Backward_Right"), new Vector2(0.7f, -0.7f));
            dodgeTree.AddChild(Base("Evade_Right"), new Vector2(1f, 0f));
            dodgeTree.AddChild(Base("Evade_Forward_Right"), new Vector2(0.7f, 0.7f));

            // 사다리(P6) — 오르기 클립 하나로 양방향. ClimbSpeed 를 **배속**으로 써서 음수면 역재생(내려가기),
            // 0 이면 그 자리에 멈춘 포즈가 된다. 전용 하강 클립(Climb_Down)은 쓰지 않는다(상태 1개로 충분 — 간결성).
            var climb = AddState(sm, "Climb", Additional("Climb_Up"), new Vector3(0f, -200f));
            climb.speedParameterActive = true;
            climb.speedParameter = "ClimbSpeed";

            // 피격 리액션(P5) — 연출 전용(이동잠금 없음). 연타 피격은 다시 처음부터 재생.
            var hit = AddState(sm, "Hit", Base("Hit1"), new Vector3(320f, 200f));
            var interact = AddState(sm, "Interact", Additional("Buff"), new Vector3(-320f, 280f)); // 전용 줍기 클립 없음 → 플레이스홀더
            var dead = AddState(sm, "Dead", Base("Death"), new Vector3(0f, 280f));
            var getUp = AddState(sm, "GetUp", Additional("Getup1_Idle1"), new Vector3(0f, 360f));

            sm.defaultState = loco;

            // ── 전이 ──────────────────────────────────────────────────────────
            // 락온 진입/해제(상태값 기반 — 트리거 아님)
            Transition(loco, strafe, 0.15f, Cond("Strafe", AnimatorConditionMode.If));
            Transition(strafe, loco, 0.15f, Cond("Strafe", AnimatorConditionMode.IfNot));

            // 공중 — 트리거는 어느 상태에서든 즉시 받아야 하므로 AnyState
            AnyTransition(sm, jump, 0.05f, Cond("Jump", AnimatorConditionMode.If));
            AnyTransition(sm, airborne, 0.15f, Cond("Fall", AnimatorConditionMode.If));
            AnyTransition(sm, landing, 0.05f, Cond("Land", AnimatorConditionMode.If));
            ExitTimeTransition(jump, airborne, 0.9f, 0.1f); // 점프 정점 후 낙하 루프로
            ExitTimeTransition(landing, loco, 0.8f, 0.1f);

            // 공격 — ComboStep 이 Attack 트리거보다 먼저 세팅된다(PlayerCharacterAgent 순서 보장)
            AnyTransition(sm, comboA, 0.05f, Cond("Attack", AnimatorConditionMode.If), Cond("ComboStep", AnimatorConditionMode.Equals, 0f));
            AnyTransition(sm, comboB, 0.05f, Cond("Attack", AnimatorConditionMode.If), Cond("ComboStep", AnimatorConditionMode.Equals, 1f));
            AnyTransition(sm, comboC, 0.05f, Cond("Attack", AnimatorConditionMode.If), Cond("ComboStep", AnimatorConditionMode.Equals, 2f));
            AnyTransition(sm, comboD, 0.05f, Cond("Attack", AnimatorConditionMode.If), Cond("ComboStep", AnimatorConditionMode.Equals, 3f));
            // 앞 단계(A~C)는 다음 타로 이어지므로 85% 에서 빠져도 되지만, <b>마지막 타(D)는 끝까지 보여준다</b>
            // — 마무리 모션이 잘리면 콤보가 흐지부지 끝난 느낌이 난다(사용자 피드백).
            foreach (var c in new[] { comboA, comboB, comboC })
                ExitTimeTransition(c, loco, 0.85f, 0.15f);
            ExitTimeTransition(comboD, loco, 0.97f, 0.1f);

            AnyTransition(sm, dodge, 0.05f, Cond("Dodge", AnimatorConditionMode.If));
            ExitTimeTransition(dodge, loco, 0.85f, 0.1f);

            // 피격은 어떤 동작 중에도 끊고 들어간다(공격 중 포함). 연속 피격은 자기 전이로 재생을 리셋한다.
            AnyTransitionAllowSelf(sm, hit, 0.05f, Cond("Hit", AnimatorConditionMode.If));
            ExitTimeTransition(hit, loco, 0.85f, 0.12f);

            AnyTransition(sm, interact, 0.1f, Cond("Interact", AnimatorConditionMode.If));
            ExitTimeTransition(interact, loco, 0.9f, 0.1f);

            // 사다리(P6) — 상태값(bool) 기반. FSM(ClimbState)이 붙고 떼는 순간과 1:1.
            AnyTransition(sm, climb, 0.15f, Cond("Climbing", AnimatorConditionMode.If));
            Transition(climb, loco, 0.15f, Cond("Climbing", AnimatorConditionMode.IfNot));

            // 사망은 홀드(나가는 exit-time 전이 없음) — 복귀는 양성 Revive 트리거로만
            AnyTransition(sm, dead, 0.1f, Cond("Dead", AnimatorConditionMode.If));
            Transition(dead, getUp, 0.1f, Cond("Revive", AnimatorConditionMode.If));
            ExitTimeTransition(getUp, loco, 0.9f, 0.1f);

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 프리팹 배선 (로컬 + 원격 — 원격도 같은 Generic/Humanoid 불일치를 겪고 있었다)
        // ─────────────────────────────────────────────────────────────────────
        private static string Wire(AnimatorController ctrl)
        {
            int wired = 0;
            foreach (string path in PlayerPrefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var animator = root.GetComponentInChildren<Animator>(true);
                    if (animator == null) { Missing.Add(path + "(Animator 없음)"); continue; }
                    animator.runtimeAnimatorController = ctrl;
                    PrefabUtility.SaveAsPrefabAsset(root, path);

                    // ⚠️ 필수: Build() 가 컨트롤러를 DeleteAsset→재생성하므로, 프리팹의 **임포트된 사본**이
                    // 삭제된 옛 인스턴스를 물고 남아 런타임에 controller=null 이 된다(실측: 디스크 YAML 은 정상인데
                    // LoadAssetAtPath 결과만 NULL → 플레이 시 "Animator is not playing an AnimatorController" 경고 폭탄).
                    // 저장 직후 강제 재임포트로 캐시를 갱신한다.
                    AssetDatabase.ImportAsset(path,
                        ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    wired++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return "프리팹 배선 " + wired + "/" + PlayerPrefabs.Length;
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────
        private static AnimationClip Base(string name) => Load(BaseDir + "/ARPG_Warrior_" + name + ".anim", name);
        private static AnimationClip Additional(string name) => Load(AdditionalDir + "/ARPG_Warrior_" + name + ".anim", name);

        private static AnimationClip Load(string path, string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) Missing.Add(name);
            return clip;
        }

        /// <summary>블렌드 트리 자식의 재생 배속 설정 — 클립 속도와 게임 속도가 다를 때 보정한다(ChildMotion 은 값 타입이라 배열 재대입 필요).</summary>
        private static void SetChildTimeScale(BlendTree tree, int index, float timeScale)
        {
            var children = tree.children;
            if (index < 0 || index >= children.Length) return;
            children[index].timeScale = timeScale;
            tree.children = children;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, Vector3 pos)
        {
            var state = sm.AddState(name, pos);
            state.motion = motion;
            return state;
        }

        private struct Condition
        {
            public string Parameter;
            public AnimatorConditionMode Mode;
            public float Threshold;
        }

        private static Condition Cond(string parameter, AnimatorConditionMode mode, float threshold = 0f)
            => new Condition { Parameter = parameter, Mode = mode, Threshold = threshold };

        private static void Transition(AnimatorState from, AnimatorState to, float duration, params Condition[] conds)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.hasFixedDuration = true;
            foreach (var c in conds) t.AddCondition(c.Mode, c.Threshold, c.Parameter);
        }

        private static void AnyTransition(AnimatorStateMachine sm, AnimatorState to, float duration, params Condition[] conds)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.hasFixedDuration = true;
            t.canTransitionToSelf = false;
            foreach (var c in conds) t.AddCondition(c.Mode, c.Threshold, c.Parameter);
        }

        /// <summary>AnyState 전이 + 자기 자신으로의 재진입 허용(연속 피격 리셋용).</summary>
        private static void AnyTransitionAllowSelf(AnimatorStateMachine sm, AnimatorState to, float duration, params Condition[] conds)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.hasFixedDuration = true;
            t.canTransitionToSelf = true;
            foreach (var c in conds) t.AddCondition(c.Mode, c.Threshold, c.Parameter);
        }

        private static void ExitTimeTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
            t.hasFixedDuration = true;
        }
    }
}
