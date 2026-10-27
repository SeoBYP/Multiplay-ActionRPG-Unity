namespace GameServer.Application.Domains.Progression;

/// <summary>
/// 한 플레이어의 합산 전투 스탯(서버 권위). SocketServer 로는 이 계산 결과만 게임시작 메시지로 넘긴다
/// (입력인 Level/장비/버프가 아님).
/// </summary>
public readonly record struct PlayerStats(
    int Level,
    int MaxHealth,
    int MaxMana,
    int AttackPower,
    int Defense,
    int Strength,
    int Dexterity,
    int Intelligence);
