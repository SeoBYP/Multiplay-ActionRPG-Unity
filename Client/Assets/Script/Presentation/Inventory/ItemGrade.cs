namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 아이템 등급(레어도) — 클라 표시용 미러. 서버 ItemDef.Grade(GameServer.Domain) 와 itemId 로 정렬되는 미러.
    /// 표시 색(GradeColors)·도감 정렬에만 쓴다(현재 게임플레이 무효과 — 드랍 가중치 등은 서버 후속).
    /// 순서(낮은→높은)는 색/정렬 기준이므로 바꾸지 않는다.
    /// </summary>
    public enum ItemGrade
    {
        Common,
        Rare,
        Epic,
        Legendary,
    }
}
