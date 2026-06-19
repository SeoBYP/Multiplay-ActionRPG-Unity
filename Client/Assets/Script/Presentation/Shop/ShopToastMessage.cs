namespace Game.Presentation.Shop
{
    /// <summary>구매 결과 토스트(메시지 + 성공여부). View 가 성공=초록/실패=빨강 등으로 구분 표시.</summary>
    public readonly struct ShopToastMessage
    {
        public readonly string Message;
        public readonly bool Success;

        public ShopToastMessage(string message, bool success)
        {
            Message = message;
            Success = success;
        }
    }
}
