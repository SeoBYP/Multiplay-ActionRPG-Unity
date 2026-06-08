namespace Server.Player;

public class PlayerState
{
    public long UserId { get; set; }
    
    public string Nickname { get; set; } = "";
    
    public float PosX { get; set; }
    
    public float PosY { get; set; }
    
    public float PosZ { get; set; }
    public float RotY { get; set; }

    /// <summary>게임 시작 시 배정된 스폰 슬롯 인덱스. 클라 결정론 스폰 입력으로 전달된다.</summary>
    public int SpawnIndex { get; set; }

    public long LastMovedAt { get; set; }

    /// <summary>
    /// 크래시/네트워크 끊김으로 세션이 사라진 시각(Unix ms). null = 접속 중.
    /// 재접속 유예 창(<see cref="Server.Room.Room.ReconnectGraceMs"/>) 판정에 사용:
    /// 끊김 시 상태를 즉시 지우지 않고 이 값을 찍어 보존(재접속하면 보존 상태로 즉시 복귀),
    /// 유예 만료 시 RoomTickService 스윕이 정리한다. 끊긴 동안 몬스터 AI 타깃에선 제외된다.
    /// </summary>
    public long? DisconnectedAtMs { get; set; }
}