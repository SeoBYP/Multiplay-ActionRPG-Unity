namespace GameServer.Application.Domains.Progression;

/// <summary>
/// 한 플레이어의 **합산 전투 스탯**(서버 권위). 오늘은 레벨 테이블 룩업뿐 —
/// 미래에 장비(3.2)·버프가 여기 합류한다(단일 합산 권위, authority-model §4c).
///
/// SocketServer 로는 이 "계산된 결과"만 게임시작 메시지로 넘긴다(입력=Level/장비/버프 아님).
/// SocketServer 는 스탯이 어떻게 나오는지 몰라도 권위 숫자만 전투에 적용한다.
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
