using System.Collections.Generic;
using Game.System.Dialogue;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.Camera
{
    /// <summary>
    /// 대화 카메라(A3). 전용 Cinemachine vcam 의 Priority 를 승격해 대화 중 카메라로 전환(Brain 블렌딩, 게임 vcam 무수정).
    /// 노드별 구도(DialogueShot)에 따라 vcam 의 LookAt/Follow 타깃을 바꾼다. 씬에 1개 배치 + DI 로 IDialogueCamera 노출.
    ///   Closeup      : LookAt=NPC, Follow=NPC
    ///   OverShoulder : LookAt=NPC, Follow=Player
    ///   TwoShot      : LookAt/Follow=TargetGroup(NPC+Player)  (twoShotGroup 미할당이면 Closeup 폴백)
    /// </summary>
    public sealed class DialogueCameraController : MonoBehaviour, IDialogueCamera
    {
        [Tooltip("대화 전용 Cinemachine 카메라. 평소 Priority 낮게, 대화 시 승격.")]
        [SerializeField] private CinemachineCamera dialogueCam;
        [Tooltip("TwoShot 구도용 타깃 그룹(선택). 없으면 TwoShot 은 Closeup 으로 폴백.")]
        [SerializeField] private CinemachineTargetGroup twoShotGroup;
        [SerializeField] private int activePriority = 100;
        [SerializeField] private int idlePriority = -10;

        private Transform _npc;
        private Transform _player;

        private void Awake()
        {
            if (dialogueCam != null) dialogueCam.Priority = idlePriority; // 시작은 비활성 우선순위
        }

        public void Enter(Transform npc, Transform player)
        {
            _npc = npc;
            _player = player;
            // 실제 활성화/타깃 세팅은 SetShot 에서(노드 진입 직후 호출됨).
        }

        public void SetShot(DialogueShot shot)
        {
            if (dialogueCam == null || _npc == null) return;

            switch (shot)
            {
                case DialogueShot.OverShoulder:
                    dialogueCam.LookAt = _npc;
                    dialogueCam.Follow = _player != null ? _player : _npc;
                    break;

                case DialogueShot.TwoShot when twoShotGroup != null:
                    twoShotGroup.Targets = new List<CinemachineTargetGroup.Target>
                    {
                        new() { Object = _npc, Weight = 1f, Radius = 1f },
                        new() { Object = _player != null ? _player : _npc, Weight = 1f, Radius = 1f },
                    };
                    var groupT = twoShotGroup.transform;
                    dialogueCam.LookAt = groupT;
                    dialogueCam.Follow = groupT;
                    break;

                case DialogueShot.Closeup:
                case DialogueShot.TwoShot: // 그룹 미할당 폴백
                default:
                    dialogueCam.LookAt = _npc;
                    dialogueCam.Follow = _npc;
                    break;
            }

            dialogueCam.Priority = activePriority; // 승격 → Brain 이 대화 카메라로 블렌드
        }

        public void Exit()
        {
            if (dialogueCam != null) dialogueCam.Priority = idlePriority; // 게임 카메라로 복귀
            _npc = null;
            _player = null;
        }
    }
}
