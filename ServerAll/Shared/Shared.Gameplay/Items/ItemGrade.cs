namespace Shared.Gameplay.Items
{
    /// <summary>
    /// 아이템 등급(레어도). 클라(표시 색·도감 정렬)·서버(정의 카탈로그) 공통 — Shared.Gameplay 단일 소스.
    ///
    /// 이전에는 서버 `GameServer.Domain.Entities.Inventory.ItemGrade` 와 클라
    /// `Game.Presentation.Inventory.ItemGrade` 로 **따로 선언**돼 있었다(값은 같았으나 동기화 강제 장치 없음).
    /// 순서(낮은→높은)는 색/정렬 기준이므로 바꾸지 않는다.
    /// </summary>
    public enum ItemGrade
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
    }
}
