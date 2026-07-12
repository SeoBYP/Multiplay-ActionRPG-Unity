namespace Game.Presentation.InGame
{
    /// <summary>
    /// 아이템 획득 토스트 메시지(모델 계약). <see cref="Game.Presentation.Shop.ShopToastMessage"/> 패턴을 따른다 —
    /// Model 이 표시할 메시지를 struct 로 발행하고 View(GameHud)는 표시만 담당한다.
    /// 획득은 항상 긍정 이벤트(서버 S_ItemPickedUp 성공 시에만 도착)라 성공/실패 플래그는 두지 않는다.
    /// </summary>
    public readonly struct ItemToastMessage
    {
        public readonly string Message;

        public ItemToastMessage(string message)
        {
            Message = message;
        }
    }
}
