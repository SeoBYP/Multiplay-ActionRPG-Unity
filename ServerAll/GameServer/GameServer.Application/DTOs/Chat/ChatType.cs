namespace GameServer.Application.DTOs.Chat;

public enum ChatType
{
    Global,   // 전체 채팅 (로비 전체)
    Room,     // 방 채팅 (특정 방만)
    Whisper   // 귓속말 (1:1)
}