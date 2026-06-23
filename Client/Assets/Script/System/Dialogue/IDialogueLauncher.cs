using UnityEngine;

namespace Game.System.Dialogue
{
    /// <summary>
    /// 대화 시작 다리(레이어 경계). NPC(Game.Gameplay)는 이 인터페이스로 npcId(문자열)만 넘긴다 —
    /// Presentation/GUI 타입을 Gameplay 에 노출하지 않기 위함. 구현은 DialogueModel(Presentation).
    /// </summary>
    public interface IDialogueLauncher
    {
        /// <summary>해당 npcId 의 대화를 연다(콘텐츠/창 로드·시작은 구현체 책임).</summary>
        void Open(string npcId);
    }

    /// <summary>
    /// 대화 카메라 다리(A3). 대화 중 전용 Cinemachine vcam 으로 전환(Priority 승격, Brain 블렌딩).
    /// Enter=NPC(Gameplay)가 대상 Transform 과 함께 호출 / SetShot=DialogueModel 이 노드 진입마다 구도 적용 / Exit=대화 종료.
    /// 구현체 없으면(씬에 컨트롤러 미배치) 카메라 미동작 — 대화 자체는 정상(주입 optional).
    /// </summary>
    public interface IDialogueCamera
    {
        /// <summary>대화 진입 — 카메라가 바라볼 NPC/플레이어 Transform 등록.</summary>
        void Enter(Transform npc, Transform player);

        /// <summary>현재 노드 구도 적용(vcam 타깃/Priority).</summary>
        void SetShot(DialogueShot shot);

        /// <summary>대화 종료 — 게임 카메라로 복귀.</summary>
        void Exit();
    }
}
