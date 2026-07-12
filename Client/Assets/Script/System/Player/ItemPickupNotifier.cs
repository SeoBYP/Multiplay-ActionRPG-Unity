using System;

namespace Game.System.Player
{
    /// <summary>
    /// 아이템 획득 알림 허브(소스 무관). 던전 줍기(소켓 S_ItemPickedUp)와 Main 로컬 줍기(LocalGroundItem→ClaimKill)는
    /// 경로가 다르지만 획득 토스트는 하나여야 한다. Main 은 비네트워크라 소켓 상태(ISocketPacketState)로 위장하지 않고
    /// 이 허브로 통지한다.
    ///
    /// 생산: LocalGroundItem(Main). 소비: InGameModel(→ GameHud 토스트). (던전 줍기는 ISocketPacketState 경로 유지.)
    /// Gameplay↔Presentation 형제라 공통 하위 Game.System.Player 에 둔다([[PartyAscRegistry]] 와 동일 위치·패턴).
    /// </summary>
    public sealed class ItemPickupNotifier
    {
        /// <summary>(itemId, qty). InGameModel 이 구독해 이름 조회 후 획득 토스트로 재발행.</summary>
        public event Action<string, int> OnPickup;

        public void Notify(string itemId, int qty)
        {
            if (string.IsNullOrEmpty(itemId) || qty <= 0) return;
            OnPickup?.Invoke(itemId, qty);
        }
    }
}
