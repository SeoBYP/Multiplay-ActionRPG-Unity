using UnityEngine;

namespace Game.Input
{
    /// <summary>
    /// 플레이어가 F키로 상호작용할 수 있는 월드 오브젝트 계약.
    ///
    /// 구현 예:
    ///   DoorInteractable  → "F: 문 열기"
    ///   ItemInteractable  → "F: 아이템 줍기"
    ///   NpcInteractable   → "F: 대화하기"
    /// </summary>
    public interface IInteractable
    {
        /// <summary>힌트 UI에 표시할 텍스트. 예: "F: 문 열기"</summary>
        string InteractionHint { get; }

        /// <summary>현재 상호작용 가능한 상태인지.</summary>
        bool CanInteract { get; }

        /// <summary>
        /// Distance + Angle 점수가 동일할 때 타이브레이커.
        /// 높을수록 우선 선택. NPC(10) > 아이템(5) > 문(3) 등으로 설정.
        /// </summary>
        int InteractionPriority { get; }

        /// <summary>Score 계산에 필요한 월드 위치.</summary>
        Transform Transform { get; }

        void Interact();
    }
}
