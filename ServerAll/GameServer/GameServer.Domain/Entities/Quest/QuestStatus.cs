namespace GameServer.Domain.Entities.Quest;

/// <summary>
/// 영속되는 퀘스트 상태. 행이 없으면 "미수주"(NotAccepted)로 취급한다.
/// "완료"는 별도 상태가 아니라 Accepted + Progress≥Required 파생(카탈로그 RequiredCount 기준).
/// </summary>
public enum QuestStatus
{
    Accepted,  // 수주함(진행 중 또는 완료=보상 대기)
    Claimed,   // 보상 수령 완료(종료)
}
