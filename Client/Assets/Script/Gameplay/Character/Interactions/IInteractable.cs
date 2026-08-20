using UnityEngine;

namespace Game.Gameplay.Character
{
    public interface IInteractable
    {
        void Interact(GameObject interactor);

        /// <summary>
        /// 상호작용 안내에 띄울 <b>행동 이름</b>(예: "오르기"·"줍기"). HUD 가 키 라벨과 합쳐 "[E] 오르기" 로 보여준다.
        /// 기본 구현을 둔 이유: 구현체 7종을 모두 고치지 않고도 추가할 수 있고, 문구가 필요 없는 대상은 그대로 둔다.
        /// </summary>
        string InteractionPrompt => "상호작용";
    }
}
