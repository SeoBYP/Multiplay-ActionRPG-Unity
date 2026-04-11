namespace Server.Player;

public class PlayerState
{
    public long UserId { get; set; }
    
    public string Nickname { get; set; } = "";
    
    public float PosX { get; set; }
    
    public float PosY { get; set; }
    
    public float PosZ { get; set; }
    public float RotY { get; set; }
    
    public long LastMovedAt { get; set; }
    
    
}