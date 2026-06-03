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
}